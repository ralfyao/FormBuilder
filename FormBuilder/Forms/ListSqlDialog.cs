using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;

namespace FormBuilder;

/// <summary>
/// 單頭清單的「清單 SQL」：打開時列出清單目前實際執行的 SELECT（還沒改過就是系統自動組的），直接修改；
/// 「改回系統產生」= 清空，回到自動組；「檢查 SQL」= 執行一次取欄位，提醒少了哪些畫面要用的欄位
/// </summary>
public class ListSqlDialog : Form
{
    private readonly SqlTextBox txtSql;
    private readonly string generated;
    private readonly Func<string, List<GridColumnDefinition>> readColumns;
    private readonly IReadOnlyCollection<string> required;

    /// <summary>按確定後的清單 SQL；null = 用系統自動組的</summary>
    public string Sql { get; private set; }

    /// <param name="current">目前的清單 SQL（null = 還沒改過）</param>
    /// <param name="generated">系統自動組的 SELECT</param>
    /// <param name="required">畫面要用到的欄位（SQL 要 SELECT 出來）</param>
    /// <param name="readColumns">執行 SQL 取欄位（檢查用）；null = 沒有連線</param>
    public ListSqlDialog(string current, string generated, IReadOnlyCollection<string> required, Func<string, List<GridColumnDefinition>> readColumns)
    {
        this.generated = generated;
        this.required = required;
        this.readColumns = readColumns;
        Text = "清單 SQL";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(6, 6, 6, 0),
            Text = (string.IsNullOrWhiteSpace(current) ? "目前清單執行的是系統自動組的 SQL（如下），直接修改後按「確定」就改用你的 SQL。" : "目前清單執行的是你改過的 SQL。")
                 + Environment.NewLine
                 + "查詢欄位的條件、排序會套在外層，所以要 SELECT 出主鍵和畫面上用到的欄位；可以用 @畫面欄位、@登入帳號。雙擊編輯、存檔照樣用主鍵讀寫資料表。",
        };
        txtSql = new SqlTextBox { Dock = DockStyle.Fill, Text = (string.IsNullOrWhiteSpace(current) ? generated : current).Replace("\r\n", "\n") };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(84, 30) };
        var btnOk = new Button { Text = "確定", Size = new Size(84, 30) };
        var btnCheck = new Button { Text = "檢查 SQL", AutoSize = true, Height = 30, Enabled = readColumns != null };
        var btnReset = new Button { Text = "改回系統產生", AutoSize = true, Height = 30 };
        buttons.Controls.AddRange(new Control[] { btnCancel, btnOk, btnCheck, btnReset });
        btnOk.Click += (s, e) => Accept();
        btnCheck.Click += (s, e) => Check(showOk: true);
        btnReset.Click += (s, e) => txtSql.Text = generated.Replace("\r\n", "\n");
        CancelButton = btnCancel;

        Controls.Add(txtSql);
        Controls.Add(buttons);
        Controls.Add(hint);
    }

    private static string Normalize(string sql) => System.Text.RegularExpressions.Regex.Replace(sql ?? "", @"\s+", " ").Trim();

    /// <summary>執行一次取欄位：SQL 有錯、少了畫面要用的欄位時提醒；回傳 SQL 能不能執行</summary>
    private bool Check(bool showOk)
    {
        if (readColumns == null) return true;
        List<GridColumnDefinition> columns;
        try
        {
            Cursor = Cursors.WaitCursor;
            columns = readColumns(txtSql.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("SQL 執行失敗：" + ex.Message, "清單 SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        var names = columns.Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(r => !names.Contains(r)).ToList();
        if (missing.Count > 0)
            MessageBox.Show("SQL 沒有 SELECT 出這些畫面要用的欄位，查詢時會出錯：" + Environment.NewLine + string.Join("、", missing), "清單 SQL",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (showOk)
            MessageBox.Show($"SQL 可以執行，共 {columns.Count} 個欄位。", "清單 SQL");
        return true;
    }

    private void Accept()
    {
        string text = txtSql.Text.Trim();
        // 沒改（或清空）= 用系統自動組的，之後畫面欄位變了也會跟著變
        if (text.Length == 0 || Normalize(text) == Normalize(generated))
        {
            Sql = null;
            DialogResult = DialogResult.OK;
            return;
        }
        if (!Check(showOk: false)) return;
        Sql = text.Replace("\n", "\r\n");
        DialogResult = DialogResult.OK;
    }
}

/// <summary>屬性面板「清單 SQL」的「…」：開清單 SQL 視窗（系統自動組的 SQL 由畫面設計提供）</summary>
public class ListSqlEditor : UITypeEditor
{
    /// <summary>畫面設計提供：清單目前自動組的 SQL、要用到的欄位、執行 SQL 取欄位</summary>
    public static Func<GridDefinition, (string Sql, IReadOnlyCollection<string> Required)> Generate { get; set; }
    public static Func<string, List<GridColumnDefinition>> ReadColumns { get; set; }

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not GridDefinition grid || !grid.IsMasterList || Generate == null)
        {
            MessageBox.Show("只有單頭清單可以設定清單 SQL。", "清單 SQL");
            return value;
        }
        var (sql, required) = Generate(grid);
        using var dlg = new ListSqlDialog(value as string, sql, required, ReadColumns);
        var service = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
        var result = service != null ? service.ShowDialog(dlg) : dlg.ShowDialog();
        return result == DialogResult.OK ? dlg.Sql : value;
    }
}
