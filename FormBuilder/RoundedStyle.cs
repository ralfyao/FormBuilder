using System.Drawing.Drawing2D;

namespace FormBuilder;

/// <summary>
/// 圓角外觀（輸入框、下拉選單、日期、按鈕）。
/// 原生的 TextBox / ComboBox 不能畫圓角，所以把它們的邊框藏起來（TextBox 無框、ComboBox / 日期用 Region 裁掉邊框），
/// 改由父控制項在它們外圍畫一個圓角框；按鈕則整個自己畫（RoundButton）
/// </summary>
public static class RoundedStyle
{
    public const int Radius = 10;

    public static readonly Color BorderColor = Color.FromArgb(160, 160, 160);
    public static readonly Color FocusColor = Color.FromArgb(0, 120, 215);

    /// <summary>每個父控制項上要畫外框的輸入控制項；Inset = 控制項相對於外框的位置與外框大小</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, List<FramedInput>> framed = new();

    private sealed record FramedInput(Control Input, Point Inset, Size FrameSize)
    {
        /// <summary>外框位置跟著控制項目前的位置算（捲動時控制項會移動）</summary>
        public Rectangle Frame => new(Input.Left - Inset.X, Input.Top - Inset.Y, FrameSize.Width, FrameSize.Height);
    }

    /// <summary>控制項實際看到的底色（透明時往上找父控制項）</summary>
    public static Color BackgroundOf(Control c)
    {
        for (; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return SystemColors.Control;
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>畫圓角框（抗鋸齒；框線落在 rect 內）</summary>
    public static void DrawBox(Graphics g, Rectangle rect, Color fill, Color border)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundRect(new RectangleF(rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1f, rect.Height - 1f), Radius);
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        using (var pen = new Pen(border)) g.DrawPath(pen, path);
        g.SmoothingMode = old;
    }

    /// <summary>
    /// 把輸入控制項放進 parent 的 frame 位置，外觀改成圓角：
    /// TextBox、ComboBox、DateTimePicker 會縮在框內、藏掉原本的邊框；勾選框照原樣放
    /// </summary>
    public static void Place(Control parent, Control input, Rectangle frame)
    {
        switch (input)
        {
            case TextBox tb:
                tb.BorderStyle = BorderStyle.None;
                tb.SetBounds(frame.X + 8, 0, frame.Width - 16, 0, BoundsSpecified.X | BoundsSpecified.Width);
                tb.Top = frame.Y + (frame.Height - tb.Height) / 2;
                break;
            case ComboBox cb:
                cb.FlatStyle = FlatStyle.Flat;
                cb.SetBounds(frame.X + 5, 0, frame.Width - 10, 0, BoundsSpecified.X | BoundsSpecified.Width);
                cb.Top = frame.Y + (frame.Height - cb.Height) / 2;
                ClipBorder(cb, 1);
                break;
            case DateTimePicker dtp:
                dtp.SetBounds(frame.X + 5, 0, frame.Width - 10, 0, BoundsSpecified.X | BoundsSpecified.Width);
                dtp.Top = frame.Y + (frame.Height - dtp.Height) / 2;
                ClipBorder(dtp, 2);
                break;
            default:
                input.Bounds = frame;
                parent.Controls.Add(input);
                return;
        }
        parent.Controls.Add(input);

        if (!framed.TryGetValue(parent, out var list))
        {
            list = new List<FramedInput>();
            framed.Add(parent, list);
            parent.Paint += (s, e) => PaintFrames(parent, e.Graphics);
        }
        list.Add(new FramedInput(input, new Point(input.Left - frame.X, input.Top - frame.Y), frame.Size));

        // 狀態改變（取得焦點、唯讀、停用、移動）時重畫外框
        void Redraw(object s, EventArgs e) => parent.Invalidate();
        input.GotFocus += Redraw;
        input.LostFocus += Redraw;
        input.EnabledChanged += Redraw;
        input.BackColorChanged += Redraw;
        input.VisibleChanged += Redraw;
        input.Move += Redraw;
        if (input is TextBox t) t.ReadOnlyChanged += Redraw;
    }

    /// <summary>用 Region 裁掉控制項自己的邊框（大小改變時重新裁）</summary>
    private static void ClipBorder(Control c, int border)
    {
        void Clip() => c.Region = new Region(new Rectangle(border, border, Math.Max(1, c.Width - border * 2), Math.Max(1, c.Height - border * 2)));
        Clip();
        c.SizeChanged += (s, e) => Clip();
    }

    private static void PaintFrames(Control parent, Graphics g)
    {
        if (!framed.TryGetValue(parent, out var list)) return;
        foreach (var item in list)
        {
            var input = item.Input;
            if (!input.Visible || input.IsDisposed) continue;
            // 停用 / 唯讀時用灰底，跟控制項本身的底色一致
            Color fill = !input.Enabled ? SystemColors.Control
                       : input is TextBox { ReadOnly: true } tb ? tb.BackColor
                       : input is TextBox ? input.BackColor
                       : SystemColors.Window;
            Color border = input.ContainsFocus ? FocusColor : BorderColor;
            DrawBox(g, item.Frame, fill, border);
        }
    }
}

/// <summary>圓角按鈕：整個自己畫（滑鼠移過、按下、停用、取得焦點有不同外觀）</summary>
public class RoundButton : Button
{
    private bool hover, pressed;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(RoundedStyle.BackgroundOf(Parent));   // 圓角外面補上父控制項的底色
        Draw(e.Graphics, ClientRectangle, Text, Font, Enabled, hover, pressed, Focused, ForeColor);
    }

    /// <summary>畫圓角按鈕（設計畫面的按鈕示意也用這個，外觀才會一致）</summary>
    public static void Draw(Graphics g, Rectangle rect, string text, Font font, bool enabled, bool hover, bool pressed, bool focused, Color foreColor)
    {
        Color fill = !enabled ? Color.FromArgb(244, 244, 244)
                   : pressed ? Color.FromArgb(204, 228, 247)
                   : hover ? Color.FromArgb(229, 241, 251)
                   : Color.FromArgb(253, 253, 253);
        Color border = !enabled ? Color.FromArgb(204, 204, 204)
                     : hover || pressed || focused ? RoundedStyle.FocusColor
                     : Color.FromArgb(173, 173, 173);
        RoundedStyle.DrawBox(g, rect, fill, border);
        TextRenderer.DrawText(g, text, font, rect, enabled ? foreColor : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>設計畫面上的輸入框示意：圓角框 + 灰字（外面露出父控制項的底色）</summary>
public class RoundedInputPreview : Label
{
    public RoundedInputPreview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = SystemColors.GrayText;
        AutoEllipsis = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        RoundedStyle.DrawBox(e.Graphics, ClientRectangle, SystemColors.Window, RoundedStyle.BorderColor);
        var textRect = Rectangle.Inflate(ClientRectangle, -8, 0);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
