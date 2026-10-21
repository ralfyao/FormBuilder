namespace FormBuilder;

/// <summary>
/// 單筆資料編輯視窗：在執行畫面雙擊 Grid 的某一列時開啟，列出這筆的欄位讓使用者修改。
/// 只回傳可以修改的欄位值；存回資料庫由呼叫端處理
/// </summary>
public partial class RecordEditDialog : Form
{
    private readonly List<FieldDefinition> fields;
    private readonly Dictionary<FieldDefinition, Control> inputs;

    /// <summary>按「確定」後，可修改欄位的新值（key = 欄位名稱）</summary>
    public Dictionary<string, object> Values { get; } = new();

    /// <param name="bodySize">後台設計的欄位區大小；null = 依欄位自動決定</param>
    public RecordEditDialog(string title, List<FieldDefinition> editFields, Func<string, object> currentValue, Size? bodySize = null,
                            string connectionString = null, List<ButtonDefinition> buttons = null, string sourcePath = null,
                            List<EditListDefinition> lists = null, Color backColor = default, Font font = null)
    {
        InitializeComponent();
        body.BackColor = ColorText.BackOrDefault(backColor);               // 後台設定的編輯視窗底色（沒設定 = 預設灰色）
        if (font != null) body.Font = font;                                // 畫面設定的字型、大小
        fields = editFields;
        Text = title;
        btnOk.Click += (s, e) => Accept();

        // 欄位照後台「編輯視窗版面」設計的位置擺放（body 在 Designer.cs，欄位依定義動態產生）
        inputs = InputHelper.LayoutFields(body, fields, connectionString);     // 連線字串給下拉選單的選項 SQL 用
        foreach (var (f, input) in inputs)
        {
            InputHelper.SetValue(input, currentValue(f.Column));
            // 只顯示的欄位（主鍵、自動編號、關聯欄位等）不能改
            if (f.NoEntry || f.IsReadOnly)
            {
                if (input is TextBox tb) tb.ReadOnly = true;
                else input.Enabled = false;
            }
        }
        // 後台在編輯視窗版面放的按鈕：明細編輯只支援確定、取消、離開、執行SQL、開啟畫面
        foreach (var b in buttons ?? new List<ButtonDefinition>())
        {
            var button = ButtonRunner.CreateControl(b);
            button.Enabled = b.Action is ButtonAction.確定 or ButtonAction.取消 or ButtonAction.離開 or ButtonAction.執行SQL or ButtonAction.開啟畫面;
            button.Click += (s, e) =>
            {
                if (!ButtonRunner.Confirm(b)) return;
                switch (b.Action)
                {
                    case ButtonAction.確定: Accept(); break;
                    case ButtonAction.取消:
                    case ButtonAction.離開: DialogResult = DialogResult.Cancel; break;
                    case ButtonAction.執行SQL:
                        ButtonRunner.RunSql(b, connectionString, name =>
                        {
                            var f = fields.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                            return f != null ? InputHelper.GetValue(f, inputs[f]) : currentValue(name);
                        });
                        break;
                    case ButtonAction.開啟畫面:
                        ButtonRunner.OpenForm(b, sourcePath, this, name =>
                        {
                            var f = fields.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                            return f != null ? InputHelper.GetValue(f, inputs[f]) : currentValue(name);
                        }, values => ButtonRunner.FillFields(values, inputs));
                        break;
                }
            };
            body.Controls.Add(button);
        }
        // 後台在編輯視窗版面放的明細清單（唯讀，依這筆的關聯欄位值列出明細）
        foreach (var l in lists ?? new List<EditListDefinition>())
        {
            var view = new EditListView(l, connectionString);
            body.Controls.Add(view.Host);                   // 圓角外框
            view.Reload(currentValue);
        }
        var content = bodySize ?? InputHelper.ContentSize(fields, 360);
        var buttonExtent = ButtonRunner.Extent(buttons ?? new List<ButtonDefinition>());
        var listExtent = EditListView.Extent(lists ?? new List<EditListDefinition>());
        content = new Size(Math.Max(content.Width, Math.Max(buttonExtent.Width, listExtent.Width)),
                           Math.Max(content.Height, Math.Max(buttonExtent.Height, listExtent.Height)));

        // 視窗大小照版面；超過螢幕可用範圍時縮到螢幕內，欄位區可以捲動
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(Math.Max(content.Width, 360), screen.Width - 40),
                              Math.Min(content.Height + bottom.Height, screen.Height - 80));
    }

    private void Accept()
    {
        Values.Clear();
        try
        {
            foreach (var f in fields.Where(f => !f.NoEntry))
                Values[f.Column] = InputHelper.GetValue(f, inputs[f]);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }
        var empty = fields.Where(f => !f.NoEntry && !f.IsNullable && Values[f.Column] == DBNull.Value).Select(f => f.Label).ToList();
        if (empty.Count > 0)
        {
            MessageBox.Show("請輸入：" + string.Join("、", empty), "提醒");
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
