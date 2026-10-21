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
        tip.SetToolTip(this, GridDef.IsSqlList
            ? $"SQL 清單：{GridDef.Title}（唯讀，切換單頭時依 SQL 重查）{Environment.NewLine}點一下設定 SQL 與欄位"
            : GridDef.IsMasterList
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
        // 圓角、欄位標題、橫線與直線（跟執行時的 Grid 一樣）；選取時藍色粗框
        GridCells.PaintPreview(g, this, GridDef.Columns, HeaderHeight, RowHeight, IsSelected);
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
