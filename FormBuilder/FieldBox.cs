namespace FormBuilder;

/// <summary>
/// 設計畫面上的一個欄位示意（標籤 + 輸入框）：
/// 拖右邊緣調整輸入框寬度；拖左邊緣調整標籤寬度（輸入框位置不動）
/// </summary>
public class FieldBox : DesignBox
{
    public const int DefaultLabelWidth = 100;
    private const int MinInputWidth = 30;
    private const int MaxLabelWidth = 400;

    public FieldDefinition Field { get; }

    private readonly Label caption = new() { TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
    private readonly Label input = new() { BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private int pressX, pressLabelWidth;

    public FieldBox(FieldDefinition field)
    {
        Field = field;
        Controls.Add(caption);
        Controls.Add(input);
        AttachMouse(this);
        AttachMouse(caption);
        AttachMouse(input);
        RefreshFromModel();
    }

    public override object Model => Field;
    public override Point ModelLocation => new(Field.X, Field.Y);

    /// <summary>輸入框左緣與欄位左緣的距離（= 標籤寬度 + 間隔）</summary>
    public int InputOffset => Math.Clamp(Field.LabelWidth, 0, MaxLabelWidth) + 4;

    /// <summary>對齊基準線用輸入框左緣</summary>
    public override int AlignX => Field.X + InputOffset;

    public override void SetModelLocation(int x, int y)
    {
        Field.X = x;
        Field.Y = y;
        Location = new Point(x, y);
    }

    protected override void OnSelectedChanged() => BackColor = IsSelected ? Color.LightSkyBlue : Color.Transparent;

    public override void RefreshFromModel()
    {
        int labelWidth = Math.Clamp(Field.LabelWidth, 0, MaxLabelWidth);
        caption.Text = Field.Label;
        caption.SetBounds(0, 2, labelWidth, 22);
        // 有設提示文字就顯示提示文字（跟執行時看到的一樣），沒有就顯示欄位名稱
        input.Text = (string.IsNullOrEmpty(Field.Placeholder) ? Field.Column : Field.Placeholder)
                   + (Field.IsKey ? " [鍵]" : "") + (Field.NoEntry ? " [只顯示]" : "")
                   + (Field.InputType == InputKind.下拉選單 ? "  ▼" : "");
        input.SetBounds(labelWidth + 4, 0, Math.Max(Field.Width, MinInputWidth), 26);
        Size = new Size(labelWidth + 4 + input.Width + 2, 28);
        Location = new Point(Field.X, Field.Y);
    }

    protected override Size ResizableSize
    {
        get => new(Field.Width, 0);
        set
        {
            Field.Width = Math.Max(MinInputWidth, value.Width);
            RefreshFromModel();
        }
    }

    protected override (bool Right, bool Bottom) HitResize(Point local) => (local.X >= Width - ResizeEdge, false);

    // ---- 拖左邊緣：標籤變寬 / 變窄，輸入框留在原位 ----
    protected override bool HitLeftResize(Point local) => local.X <= ResizeEdge;

    protected override void BeginLeftResize()
    {
        pressX = Field.X;
        pressLabelWidth = Field.LabelWidth;
    }

    protected override void ApplyLeftResize(int dx)
    {
        // 往右拖 = 標籤變窄；往左拖 = 標籤變寬（不超出畫面左邊）
        int labelWidth = Math.Clamp(pressLabelWidth - dx, 0, Math.Min(MaxLabelWidth, pressLabelWidth + pressX));
        Field.X = pressX + (pressLabelWidth - labelWidth);
        Field.LabelWidth = labelWidth;
        RefreshFromModel();
    }
}
