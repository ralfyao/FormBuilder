namespace FormBuilder;

/// <summary>
/// 設計畫面上的單身 Grid 示意：標題列 + 欄位標題 + 空白列，全部自己畫（沒有子控制項，滑鼠事件才不會被吃掉）。
/// 拖曳右邊、下邊、右下角調整大小；點一下（沒有拖動）開啟欄位設定
/// </summary>
public class GridBox : DesignBox
{
    private const int HeaderHeight = 24;
    private const int RowHeight = 22;
    private const int MinWidth = 200;
    private const int MinHeight = 100;

    public GridDefinition GridDef { get; }

    // 設計畫面上不畫標題列（執行時本來就沒有）；資料表、關聯欄位等說明改成滑鼠移上去才顯示
    private readonly ToolTip tip = new();

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
        tip.SetToolTip(this, GridDef.IsMasterList
            ? $"單頭清單：{GridDef.Title ?? GridDef.TableName}（查詢結果，點選一列切換單頭）{Environment.NewLine}點一下設定欄位"
            : $"單身：{GridDef.Title ?? GridDef.TableName}（{(string.IsNullOrWhiteSpace(GridDef.LinkText) ? "未設定關聯欄位" : "關聯：" + GridDef.LinkText)}）{Environment.NewLine}點一下設定欄位");
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

        // 欄位標題
        int x = 0;
        var headerRect = new Rectangle(0, 0, Width, HeaderHeight);
        g.FillRectangle(SystemBrushes.Control, headerRect);
        foreach (var c in GridDef.Columns.Where(c => c.Visible))
        {
            var cell = new Rectangle(x, 0, c.Width, HeaderHeight);
            g.DrawRectangle(SystemPens.ControlDark, cell);
            TextRenderer.DrawText(g, c.Header, Font, Rectangle.Inflate(cell, -3, 0), SystemColors.ControlText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += c.Width;
            if (x > Width) break;
        }

        // 空白資料列的格線
        for (int y = HeaderHeight + RowHeight; y < Height; y += RowHeight)
            g.DrawLine(SystemPens.ControlLight, 0, y, Width, y);

        // 選取時用較粗的藍框標示（沒有標題列可以變色了）
        if (IsSelected)
            using (var pen = new Pen(Color.DodgerBlue, 3))
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        else
            g.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
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
