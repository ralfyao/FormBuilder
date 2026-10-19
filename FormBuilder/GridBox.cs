namespace FormBuilder;

/// <summary>
/// 設計畫面上的單身 Grid 示意：標題列 + 欄位標題 + 空白列，全部自己畫（沒有子控制項，滑鼠事件才不會被吃掉）。
/// 拖曳右邊、下邊、右下角調整大小；點一下（沒有拖動）開啟欄位設定
/// </summary>
public class GridBox : DesignBox
{
    private const int TitleHeight = 24;
    private const int HeaderHeight = 24;
    private const int RowHeight = 22;
    private const int MinWidth = 200;
    private const int MinHeight = 100;

    public GridDefinition GridDef { get; }

    public GridBox(GridDefinition grid)
    {
        GridDef = grid;
        DoubleBuffered = true;
        AttachMouse(this);
        RefreshFromModel();
    }

    public override object Model => GridDef;
    public override Point ModelLocation => new(GridDef.X, GridDef.Y);

    /// <summary>停靠在下方時位置由設計視窗排版決定（不能拖動）</summary>
    public bool IsDocked => GridDef.Dock == GridDock.Bottom;

    public override void SetModelLocation(int x, int y)
    {
        if (IsDocked) return;
        GridDef.X = x;
        GridDef.Y = y;
        Location = new Point(x, y);
    }

    public override void RefreshFromModel()
    {
        SetBounds(GridDef.X, GridDef.Y, Math.Max(GridDef.Width, MinWidth), Math.Max(GridDef.Height, MinHeight));
        Invalidate();
    }

    protected override Size ResizableSize
    {
        get => new(GridDef.Width, GridDef.Height);
        set
        {
            if (IsDocked)
            {
                // 停靠時：寬度跟著畫面；拖上邊緣改高度，底部固定不動（上邊緣不超出畫面頂端）
                int bottom = GridDef.Y + GridDef.Height;
                GridDef.Height = Math.Clamp(value.Height, MinHeight, Math.Max(MinHeight, bottom));
                GridDef.Y = bottom - GridDef.Height;
            }
            else
            {
                GridDef.Width = Math.Max(MinWidth, value.Width);
                GridDef.Height = Math.Max(MinHeight, value.Height);
            }
            RefreshFromModel();
        }
    }

    /// <summary>停靠在下方時拖上邊緣調整高度（往上拉變高）；沒停靠時拖右邊、下邊、右下角</summary>
    protected override bool ResizeFromTop => IsDocked;

    protected override (bool Right, bool Bottom) HitResize(Point local) => IsDocked
        ? (false, local.Y <= ResizeEdge)
        : (local.X >= Width - ResizeEdge, local.Y >= Height - ResizeEdge);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SystemColors.Window);

        // 標題列：單身資料表與關聯欄位
        var titleRect = new Rectangle(0, 0, Width, TitleHeight);
        using (var titleBrush = new SolidBrush(IsSelected ? Color.LightSkyBlue : SystemColors.ControlLight))
            g.FillRectangle(titleBrush, titleRect);
        string title = GridDef.IsMasterList
            ? $"單頭清單：{GridDef.Title ?? GridDef.TableName}（查詢結果，點選一列切換單頭）　點一下設定欄位"
            : $"單身：{GridDef.Title ?? GridDef.TableName}（{(string.IsNullOrWhiteSpace(GridDef.LinkText) ? "未設定關聯欄位" : "關聯：" + GridDef.LinkText)}）　點一下設定欄位";
        TextRenderer.DrawText(g, title,
            Font, Rectangle.Inflate(titleRect, -6, 0), SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // 欄位標題
        int x = 0;
        var headerRect = new Rectangle(0, TitleHeight, Width, HeaderHeight);
        g.FillRectangle(SystemBrushes.Control, headerRect);
        foreach (var c in GridDef.Columns.Where(c => c.Visible))
        {
            var cell = new Rectangle(x, TitleHeight, c.Width, HeaderHeight);
            g.DrawRectangle(SystemPens.ControlDark, cell);
            TextRenderer.DrawText(g, c.Header, Font, Rectangle.Inflate(cell, -3, 0), SystemColors.ControlText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += c.Width;
            if (x > Width) break;
        }

        // 空白資料列的格線
        for (int y = TitleHeight + HeaderHeight + RowHeight; y < Height; y += RowHeight)
            g.DrawLine(SystemPens.ControlLight, 0, y, Width, y);

        g.DrawRectangle(IsSelected ? Pens.DodgerBlue : SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
        if (IsDocked)
        {
            // 停靠時在上邊緣中央畫兩條短橫線，提示可以往上拉
            int cx = Width / 2;
            g.DrawLine(SystemPens.ControlDarkDark, cx - 20, 2, cx + 20, 2);
            g.DrawLine(SystemPens.ControlDarkDark, cx - 20, 5, cx + 20, 5);
        }
        else
        {
            ControlPaint.DrawSizeGrip(g, SystemColors.Window, Width - 16, Height - 16, 16, 16);
        }
    }
}
