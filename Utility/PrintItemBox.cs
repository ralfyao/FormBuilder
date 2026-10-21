namespace FormBuilder;

/// <summary>列印樣板設計器上的一個元件（文字、欄位、圖片、線條、方框）：可移動、拖右邊 / 下邊 / 右下角調整大小</summary>
public class PrintItemBox : DesignBox
{
    private const int MinSize = 4;

    public PrintItem Item { get; }

    /// <summary>欄位找不到資料來源（設計器用紅框標出來）</summary>
    public bool Missing { get; set; }

    public PrintItemBox(PrintItem item)
    {
        Item = item;
        DoubleBuffered = true;
        BackColor = Color.White;
        AttachMouse(this);
        RefreshFromModel();
    }

    public override object Model => Item;
    public override Point ModelLocation => new(Item.X, Item.Y);

    public override void SetModelLocation(int x, int y)
    {
        Item.X = x;
        Item.Y = y;
        Location = new Point(x, y);
    }

    public override void RefreshFromModel()
    {
        SetBounds(Item.X, Item.Y, Math.Max(Item.Width, MinSize), Math.Max(Item.Height, MinSize));
        Invalidate();
    }

    protected override Size ResizableSize
    {
        get => new(Item.Width, Item.Height);
        set
        {
            Item.Width = Math.Max(MinSize, value.Width);
            Item.Height = Math.Max(MinSize, value.Height);
            RefreshFromModel();
        }
    }

    protected override (bool Right, bool Bottom) HitResize(Point local) =>
        (local.X >= Width - ResizeEdge, local.Y >= Height - ResizeEdge);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        PrintRenderer.DrawItem(g, Item, new RectangleF(0, 0, Width - 1, Height - 1), null);
        // 範圍：選取時藍色粗框，平常淡灰色虛線（列印時不會印出來）
        if (Missing)
        {
            using var red = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawRectangle(red, 1, 1, Width - 3, Height - 3);
        }
        if (IsSelected)
        {
            using var pen = new Pen(Color.DodgerBlue, 2);
            g.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
        }
        else if (!(Item.Kind == PrintItemKind.方框 || Item.Border))
        {
            using var pen = new Pen(Color.Gainsboro) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
