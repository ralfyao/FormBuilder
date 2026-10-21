namespace FormBuilder;

/// <summary>通用的輸入對話框：依呼叫時給的欄位（標籤、是否為密碼、預設值）產生輸入框，按「確定」回傳各欄位的值</summary>
public partial class PromptDialog : Form
{
    public record Field(string Label, string Value = "", bool Password = false, bool ReadOnly = false);

    private readonly List<TextBox> boxes = new();
    /// <summary>按「確定」前的檢查：回傳錯誤訊息就不關閉（null = 通過）</summary>
    private readonly Func<string[], string> validate;

    /// <summary>按「確定」後各欄位的值（去掉前後空白；密碼不去空白）</summary>
    public string[] Values { get; private set; }

    public PromptDialog(string title, IEnumerable<Field> fieldList, Func<string[], string> validate = null)
    {
        InitializeComponent();
        Text = title;
        this.validate = validate;
        foreach (var f in fieldList)
        {
            int row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fields.Controls.Add(new Label { Text = f.Label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            var box = new TextBox { Text = f.Value ?? "", UseSystemPasswordChar = f.Password, ReadOnly = f.ReadOnly, Dock = DockStyle.Fill, Margin = new Padding(6, 6, 0, 0) };
            box.Tag = f;
            boxes.Add(box);
            fields.Controls.Add(box, 1, row);
        }
        ClientSize = new Size(ClientSize.Width, boxes.Count * 34 + 8 + bottom.Height + Padding.Vertical + 8);
        btnOk.Click += (s, e) =>
        {
            var values = boxes.Select(b => ((Field)b.Tag).Password ? b.Text : b.Text.Trim()).ToArray();
            string error = validate?.Invoke(values);
            if (error != null)
            {
                MessageBox.Show(error, Text);
                return;
            }
            Values = values;
            DialogResult = DialogResult.OK;
        };
        Shown += (s, e) => boxes.FirstOrDefault(b => !b.ReadOnly)?.Focus();
    }

    /// <summary>一行呼叫：顯示對話框，取消時回傳 null</summary>
    public static string[] Ask(IWin32Window owner, string title, Func<string[], string> validate, params Field[] fieldList)
    {
        using var dlg = new PromptDialog(title, fieldList, validate);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Values : null;
    }
}
