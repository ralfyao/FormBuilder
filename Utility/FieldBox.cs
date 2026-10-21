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
    private readonly RoundedInputPreview input = new();     // 圓角輸入框示意（跟執行時一樣）
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
        caption.ForeColor = ColorText.LabelColor(Parent?.BackColor ?? Color.Empty);     // 畫面底色深時標籤改白字
        caption.Font = Field.LabelFontSize > 0 ? LabelGeometry.Font(Field, Font) : null;   // null = 跟畫面字型一樣
        caption.SetBounds(0, 0, labelWidth, LabelGeometry.Height(caption.Font));   // 跟執行時一樣：字放大時標籤跟著變高
        // 有設提示文字就顯示提示文字（跟執行時看到的一樣），沒有就顯示欄位名稱
        input.Text = (string.IsNullOrEmpty(Field.Placeholder) ? Field.Column : Field.Placeholder)
                   + (Field.IsKey ? " [鍵]" : "") + (Field.NoEntry ? " [只顯示]" : "") + (Field.IsReadOnly ? " [唯讀]" : "")
                   + Field.InputType switch
                   {
                       InputKind.下拉選單 => "  ▼",
                       InputKind.日期選擇 => "  [日期]",
                       InputKind.勾選框 => "  ☑",
                       InputKind.選項按鈕 => "  ◉ ○",
                       _ => "",
                   };
        input.Fill = Field.BackColor;
        input.Font = Field.FontSize > 0 ? new Font(Font.FontFamily, Field.FontSize) : Font;
        input.ForeColor = Field.BackColor.IsEmpty ? SystemColors.GrayText : ControlPaint.Dark(ButtonLook.Contrast(Field.BackColor), 0.1f);
        input.Invalidate();
        input.SetBounds(labelWidth + 4, 0, Math.Max(Field.Width, MinInputWidth), InputHeight);
        Size = new Size(labelWidth + 4 + input.Width + 2, Math.Max(InputHeight, caption.Height) + 2);
        Location = new Point(Field.X, Field.Y);
    }

    private int InputHeight => Math.Max(Field.Height, FieldDefinition.DefaultHeight);

    protected override Size ResizableSize
    {
        get => new(Field.Width, InputHeight);
        set
        {
            Field.Width = Math.Max(MinInputWidth, value.Width);
            Field.Height = Math.Max(FieldDefinition.DefaultHeight, value.Height);
            RefreshFromModel();
        }
    }

    // 拖右邊緣調整寬度、下邊緣調整高度（下拉選單、日期的高度固定，拉高只會讓輸入框外框變高）
    protected override (bool Right, bool Bottom) HitResize(Point local) => (local.X >= Width - ResizeEdge, local.Y >= Height - ResizeEdge);

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
