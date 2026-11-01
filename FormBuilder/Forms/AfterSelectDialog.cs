namespace FormBuilder;

/// <summary>
/// 單身欄位的「執行後動作」：這一格的值改了（下拉選單選了、輸入完離開）就執行 SQL，
/// @名稱 取同一列的欄位（沒有就取單頭），SQL 查出資料時第一筆依「回傳對應」帶進同一列的欄位
/// </summary>
public class AfterSelectDialog : Form
{
    private readonly SqlTextBox txtSql = new() { Dock = DockStyle.Fill };
    private readonly TextBox txtParams = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox txtReturn = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };

    public string Sql => Clean(txtSql.Text);
    public string Params => Clean(txtParams.Text);
    public string Return => Clean(txtReturn.Text);

    /// <param name="rowColumns">同一列可以用的欄位（顯示在說明裡）</param>
    public AfterSelectDialog(string title, string sql, string paramText, string returnMap, IEnumerable<string> rowColumns)
    {
        Text = $"執行後動作：{title}";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 560);
        txtSql.Text = sql ?? "";
        txtParams.Text = paramText ?? "";
        txtReturn.Text = returnMap ?? "";

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "這一格的值改了（下拉選單選了、輸入完離開）就執行 SQL。@名稱 取同一列的欄位，沒有的話取單頭欄位。\n"
                 + "例如：SELECT 品名規格, 單位, 單價 FROM 產品 WHERE 產品編號 = @產品編號\n"
                 + "同一列的欄位：" + string.Join("、", rowColumns),
        });
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "SQL（查出資料時，第一筆帶進同一列的欄位；清空 = 不執行）" });
        layout.Controls.Add(txtSql);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "傳入參數：「名稱=值」，多組用逗號或換行分隔；沒列的 @名稱 直接取同名欄位的值" });
        layout.Controls.Add(txtParams);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "回傳對應：「單身欄位=SQL 欄位」，例如 單價=PRICE, 單位=UNIT；空白 = SQL 的每個欄位帶進同名的單身欄位" });
        layout.Controls.Add(txtReturn);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);

        Controls.Add(layout);
        Controls.Add(bottom);
        CancelButton = btnCancel;                         // SQL 可以換行，Enter 不要直接確定
    }

    private static string Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
