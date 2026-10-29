namespace FormBuilder;

/// <summary>
/// 編輯視窗版面上的明細清單示意：標題列 + 欄位標題 + 空白列，全部自己畫（沒有子控制項，滑鼠事件才不會被吃掉）。
/// 拖曳右邊、下邊、右下角調整大小；點一下（沒有拖動）開啟欄位設定
/// </summary>
public class EditListBox : DesignBox
{
    private const int HeaderHeight = 24;
    private const int RowHeight = 22;
    public const int MinWidth = 160;
    public const int MinHeight = 80;

    public EditListDefinition ListDef { get; }

    // 設計畫面上不畫標題列（執行時本來就沒有）；資料表、關聯欄位等說明改成滑鼠移上去才顯示
    private readonly ToolTip tip = new();

    public EditListBox(EditListDefinition list)
    {
        ListDef = list;
        DoubleBuffered = true;
        AttachMouse(this);
        RefreshFromModel();
    }

    public override object Model => ListDef;
    public override Point ModelLocation => new(ListDef.X, ListDef.Y);

    public override void SetModelLocation(int x, int y)
    {
        ListDef.X = x;
        ListDef.Y = y;
        Location = new Point(x, y);
    }

    public override void RefreshFromModel()
    {
        SetBounds(ListDef.X, ListDef.Y, Math.Max(ListDef.Width, MinWidth), Math.Max(ListDef.Height, MinHeight));
        string link = ListDef.IsSqlList ? "SQL 清單，@名稱 取編輯視窗的欄位值"
                    : string.IsNullOrWhiteSpace(ListDef.LinkText) ? "未設定關聯欄位" : "關聯：" + ListDef.LinkText;
        if (!string.IsNullOrWhiteSpace(ListDef.ParentList)) link = $"上層清單 {ListDef.ParentList}，{link}";
        tip.SetToolTip(this, $"明細清單：{ListDef.Title ?? ListDef.TableName}（{link}）{Environment.NewLine}點一下設定欄位");
        Invalidate();
    }

    protected override Size ResizableSize
    {
        get => new(ListDef.Width, ListDef.Height);
        set
        {
            ListDef.Width = Math.Max(MinWidth, value.Width);
            ListDef.Height = Math.Max(MinHeight, value.Height);
            RefreshFromModel();
        }
    }

    protected override (bool Right, bool Bottom) HitResize(Point local) =>
        (local.X >= Width - ResizeEdge, local.Y >= Height - ResizeEdge);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // 圓角、欄位標題、橫線與直線（跟執行時的明細清單一樣）；選取時藍色粗框
        GridCells.PaintPreview(g, this, ListDef.Columns, HeaderHeight, RowHeight, IsSelected);
        ControlPaint.DrawSizeGrip(g, SystemColors.Window, Width - 16, Height - 16, 16, 16);
    }
}
