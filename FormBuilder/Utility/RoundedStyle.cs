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

    /// <summary>輸入框外框高度超過這個值，文字框改成多行輸入</summary>
    public const int TallThreshold = 32;

    public static readonly Color BorderColor = Color.FromArgb(160, 160, 160);
    public static readonly Color FocusColor = Color.FromArgb(0, 120, 215);

    /// <summary>每個父控制項上要畫外框的輸入控制項；Inset = 控制項相對於外框的位置與外框大小</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, List<FramedInput>> framed = new();

    /// <summary>後台設定了底色的輸入控制項（日期選擇的 BackColor 沒有作用，外框底色要另外記）</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> fills = new();

    public static void SetFill(Control input, Color color)
    {
        fills.Remove(input);
        if (!color.IsEmpty) fills.Add(input, color);
    }

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
            case TextBox tb when IsTall(frame.Height, tb.Font):
                // 拉高的文字框：改成多行輸入，填滿整個圓角框（Enter 換行）
                tb.BorderStyle = BorderStyle.None;
                tb.Multiline = true;
                tb.AcceptsReturn = true;
                tb.WordWrap = true;
                tb.ScrollBars = ScrollBars.Vertical;
                tb.SetBounds(frame.X + 8, frame.Y + 6, frame.Width - 16, frame.Height - 12);
                break;
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

    /// <summary>外框比單行文字需要的高度還高（至少超過 32）才改成多行輸入；字放大時單行的框也會跟著變高</summary>
    public static bool IsTall(int frameHeight, Font font) => frameHeight > Math.Max(TallThreshold, font.Height + 14);

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
                       : fills.TryGetValue(input, out var custom) ? (Color)custom
                       : input is TextBox { ReadOnly: true } tb ? tb.BackColor
                       : input is TextBox ? input.BackColor
                       : SystemColors.Window;
            Color border = input.ContainsFocus ? FocusColor : BorderColor;
            DrawBox(g, item.Frame, fill, border);
        }
    }
}

/// <summary>按鈕的自訂外觀：背景色、文字顏色、圖示（內建圖示字型或圖片檔）；都沒設定就是預設外觀</summary>
public sealed class ButtonLook
{
    public Color BackColor { get; init; }
    public Color ForeColor { get; init; }
    public ButtonIcon Icon { get; init; }
    public string IconFile { get; init; }

    public static ButtonLook From(ButtonDefinition b) => new()
    {
        BackColor = b.BackColor,
        ForeColor = b.ForeColor,
        Icon = b.Icon,
        IconFile = b.IconFile,
    };

    // Segoe MDL2 Assets / Segoe Fluent Icons 的字元（Windows 10、11 內建）
    private static readonly Dictionary<ButtonIcon, string> Glyphs = new()
    {
        [ButtonIcon.儲存] = "", [ButtonIcon.搜尋] = "", [ButtonIcon.新增] = "", [ButtonIcon.刪除] = "",
        [ButtonIcon.編輯] = "", [ButtonIcon.列印] = "", [ButtonIcon.重新整理] = "", [ButtonIcon.匯出] = "",
        [ButtonIcon.確定] = "", [ButtonIcon.取消] = "", [ButtonIcon.首筆] = "", [ButtonIcon.上筆] = "",
        [ButtonIcon.下筆] = "", [ButtonIcon.末筆] = "", [ButtonIcon.設定] = "", [ButtonIcon.資訊] = "",
    };

    private static string iconFontName;
    private static readonly Dictionary<string, Image> images = new();

    /// <summary>系統有裝的圖示字型名稱；兩種都沒有（舊系統）就是 null，圖示不顯示</summary>
    private static string IconFontName
    {
        get
        {
            if (iconFontName == null)
            {
                var names = FontFamily.Families.Select(f => f.Name).ToHashSet();
                iconFontName = names.Contains("Segoe MDL2 Assets") ? "Segoe MDL2 Assets"
                             : names.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "";
            }
            return iconFontName == "" ? null : iconFontName;
        }
    }

    public string Glyph => IconFile == null && Glyphs.TryGetValue(Icon, out var g) && IconFontName != null ? g : null;

    /// <summary>圖片檔（讀過的快取起來；檔案不存在或讀不了就沒有圖示）</summary>
    public Image Image
    {
        get
        {
            if (string.IsNullOrWhiteSpace(IconFile)) return null;
            if (images.TryGetValue(IconFile, out var cached)) return cached;
            Image img = null;
            try
            {
                if (File.Exists(IconFile))
                {
                    if (Path.GetExtension(IconFile).Equals(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using var icon = new Icon(IconFile);
                        img = icon.ToBitmap();
                    }
                    else
                    {
                        using var ms = new MemoryStream(File.ReadAllBytes(IconFile));     // 不鎖住檔案
                        using var raw = Image.FromStream(ms);
                        img = new Bitmap(raw);
                    }
                }
            }
            catch (Exception) { }
            images[IconFile] = img;
            return img;
        }
    }

    public bool HasIcon => Glyph != null || Image != null;

    public static Font CreateIconFont(int pixelSize) => new(IconFontName ?? "Segoe UI", pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);

    /// <summary>底色太深用白字，太淺用黑字</summary>
    public static Color Contrast(Color back) =>
        (back.R * 299 + back.G * 587 + back.B * 114) / 1000 < 140 ? Color.White : Color.Black;
}

/// <summary>圓角按鈕：整個自己畫（滑鼠移過、按下、停用、取得焦點有不同外觀）</summary>
public class RoundButton : Button
{
    private bool hover, pressed;
    private ButtonLook look;

    /// <summary>後台設計的自訂外觀（背景色、文字顏色、圖示）；null = 預設</summary>
    public ButtonLook Look
    {
        get => look;
        set { look = value; Invalidate(); }
    }

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
        Draw(e.Graphics, ClientRectangle, Text, Font, Enabled, hover, pressed, Focused, ForeColor, look);
    }

    /// <summary>畫圓角按鈕（設計畫面的按鈕示意也用這個，外觀才會一致）</summary>
    public static void Draw(Graphics g, Rectangle rect, string text, Font font, bool enabled, bool hover, bool pressed, bool focused, Color foreColor,
                            ButtonLook look = null)
    {
        Color back = look?.BackColor ?? Color.Empty;
        bool custom = !back.IsEmpty;
        Color fill = !enabled ? Color.FromArgb(244, 244, 244)
                   : custom ? (pressed ? ControlPaint.Dark(back, 0.1f) : hover ? ControlPaint.Light(back, 0.2f) : back)
                   : pressed ? Color.FromArgb(204, 228, 247)
                   : hover ? Color.FromArgb(229, 241, 251)
                   : Color.FromArgb(253, 253, 253);
        Color border = !enabled ? Color.FromArgb(204, 204, 204)
                     : custom ? (focused ? RoundedStyle.FocusColor : ControlPaint.Dark(back, 0.15f))
                     : hover || pressed || focused ? RoundedStyle.FocusColor
                     : Color.FromArgb(173, 173, 173);
        RoundedStyle.DrawBox(g, rect, fill, border);

        Color textColor = !enabled ? SystemColors.GrayText
                        : look != null && !look.ForeColor.IsEmpty ? look.ForeColor
                        : custom ? ButtonLook.Contrast(back)
                        : foreColor;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (look == null || !look.HasIcon)
        {
            TextRenderer.DrawText(g, text, font, rect, textColor, flags | TextFormatFlags.HorizontalCenter);
            return;
        }

        // 圖示 + 文字：整組置中；沒有文字就只畫圖示
        const int pad = 6, gap = 6;
        int iconSize = Math.Clamp(rect.Height - 12, 12, 28);
        bool hasText = !string.IsNullOrEmpty(text);
        int textWidth = hasText ? TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, rect.Height), TextFormatFlags.NoPadding).Width : 0;
        int total = iconSize + (hasText ? gap + textWidth : 0);
        int x = Math.Max(rect.X + pad, rect.X + (rect.Width - total) / 2);
        var iconRect = new Rectangle(x, rect.Y + (rect.Height - iconSize) / 2, iconSize, iconSize);
        if (look.Image is { } image)
        {
            g.DrawImage(image, iconRect);
        }
        else if (look.Glyph is { } glyph)
        {
            using var iconFont = ButtonLook.CreateIconFont(iconSize - 2);
            TextRenderer.DrawText(g, glyph, iconFont, iconRect, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        if (hasText)
        {
            int textX = iconRect.Right + gap;
            TextRenderer.DrawText(g, text, font, new Rectangle(textX, rect.Y, Math.Max(0, rect.Right - pad - textX), rect.Height), textColor, flags | TextFormatFlags.Left);
        }
    }
}

/// <summary>設計畫面上的輸入框示意：圓角框 + 灰字（外面露出父控制項的底色）</summary>
public class RoundedInputPreview : Label
{
    /// <summary>輸入框底色；Empty = 預設的白底</summary>
    public Color Fill { get; set; }

    /// <summary>true = 只畫文字、不畫圓角框（文字標籤）</summary>
    public bool Plain { get; set; }

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
        if (Plain)
        {
            if (!Fill.IsEmpty) using (var brush = new SolidBrush(Fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
            using var pen = new Pen(Color.Silver) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };   // 設計時看得到範圍
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -2, 0), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            return;
        }
        RoundedStyle.DrawBox(e.Graphics, ClientRectangle, Fill.IsEmpty ? SystemColors.Window : Fill, RoundedStyle.BorderColor);
        // 拉高的輸入框跟執行時一樣是多行輸入：文字從左上角開始，可以換行
        bool tall = RoundedStyle.IsTall(Height, Font);
        var textRect = tall ? Rectangle.Inflate(ClientRectangle, -8, -6) : Rectangle.Inflate(ClientRectangle, -8, 0);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor,
            tall ? TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak
                 : TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
