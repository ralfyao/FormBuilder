namespace FormBuilder;

/// <summary>設計畫面上的按鈕：可移動、拖右邊 / 下邊 / 右下角調整大小，屬性面板設定文字與動作</summary>
public class ButtonBox : DesignBox
{
    private const int MinWidth = 40;
    private const int MinHeight = 20;

    public ButtonDefinition ButtonDef { get; }

    public ButtonBox(ButtonDefinition button)
    {
        ButtonDef = button;
        DoubleBuffered = true;
        AttachMouse(this);
        RefreshFromModel();
    }

    public override object Model => ButtonDef;
    public override Point ModelLocation => new(ButtonDef.X, ButtonDef.Y);

    public override void SetModelLocation(int x, int y)
    {
        ButtonDef.X = x;
        ButtonDef.Y = y;
        Location = new Point(x, y);
    }

    public override void RefreshFromModel()
    {
        SetBounds(ButtonDef.X, ButtonDef.Y, Math.Max(ButtonDef.Width, MinWidth), Math.Max(ButtonDef.Height, MinHeight));
        Invalidate();
    }

    protected override Size ResizableSize
    {
        get => new(ButtonDef.Width, ButtonDef.Height);
        set
        {
            ButtonDef.Width = Math.Max(MinWidth, value.Width);
            ButtonDef.Height = Math.Max(MinHeight, value.Height);
            RefreshFromModel();
        }
    }

    protected override (bool Right, bool Bottom) HitResize(Point local) =>
        (local.X >= Width - ResizeEdge, local.Y >= Height - ResizeEdge);

    protected override void OnPaint(PaintEventArgs e)
    {
        RoundButton.Draw(e.Graphics, ClientRectangle, ButtonDef.Text, ButtonDef.FontFor(Font), true, false, false, false, ForeColor, ButtonLook.From(ButtonDef));   // 跟執行時一樣的圓角按鈕
        if (IsSelected)
            using (var pen = new Pen(Color.DodgerBlue, 2))
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
    }
}
