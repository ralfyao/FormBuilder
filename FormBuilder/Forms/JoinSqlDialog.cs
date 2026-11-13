using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// 欄位設定的「來源（JOIN）」點一下：顯示並直接修改這個 JOIN 的 SQL（LEFT JOIN 來源 AS 別名 ON 條件）。
/// 來源可以改成子查詢（例如先 GROUP BY 避免明細重複），ON 可以寫任何條件；寫成「T0.欄位 = 別名.欄位 AND …」時存成一般的關聯條件，
/// 其他寫法存成自訂 SQL。即時檢查欄位、型別（一般關聯條件時），「測試」實際查前 20 筆看會不會出錯；也可以移除這個 JOIN
/// </summary>
public class JoinSqlDialog : Form
{
    private readonly Label lblHead = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5F), ForeColor = Color.DimGray };
    private readonly TextBox txtJoin = new() { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5F), AcceptsReturn = true, AcceptsTab = true };
    private readonly Label lblCheck = new() { Dock = DockStyle.Fill };
    private readonly GridJoinDefinition join;
    private readonly string mainSchema, mainTable, connectionString;
    private readonly DbDialect d;
    private readonly Dictionary<string, string> localTypes, remoteTypes;
    private readonly List<string> pickedColumns;

    /// <summary>true = 使用者按了「移除這個 JOIN」</summary>
    public bool RemoveRequested { get; private set; }

    /// <summary>按「確定」後的結果（寫回 JOIN 用）</summary>
    public string LinkText { get; private set; }
    public string SourceSql { get; private set; }
    public string OnSql { get; private set; }

    /// <param name="localColumns">本身資料表的欄位與型別</param>
    /// <param name="pickedColumns">這個 JOIN 帶進來的欄位（例如 J4.[數量] AS [數量]）</param>
    public JoinSqlDialog(GridJoinDefinition join, string mainSchema, string mainTable, IEnumerable<(string Name, string Type)> localColumns,
                         IEnumerable<string> pickedColumns, string connectionString, bool canRemove)
    {
        this.join = join;
        this.mainSchema = mainSchema;
        this.mainTable = mainTable;
        this.connectionString = connectionString;
        d = DbDialect.Of(connectionString);
        this.pickedColumns = pickedColumns.ToList();
        localTypes = localColumns.Where(c => c.Name != null).GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                 .ToDictionary(g => g.Key, g => g.First().Type, StringComparer.OrdinalIgnoreCase);
        remoteTypes = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var c in SchemaReader.GetColumns(connectionString, join.TableSchema, join.TableName)) remoteTypes[c.Name] = c.DataType;
        }
        catch (Exception) { }                               // 讀不到結構就不檢查型別

        Text = $"JOIN {join.Alias}：{join.TableName}";
        Font = new Font("Microsoft JhengHei UI", 9.5F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 520);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "查詢的前半段（由欄位設定產生，不能改）：本身資料表的別名是 T0", Dock = DockStyle.Fill });
        layout.Controls.Add(lblHead);
        layout.Controls.Add(new Label
        {
            Text = $"JOIN 的部分（可以改）：LEFT JOIN 來源 AS {join.Alias} ON 條件。來源可以改成子查詢 ( SELECT … GROUP BY … )，條件用 T0.欄位、{join.Alias}.欄位",
            Dock = DockStyle.Fill, AutoEllipsis = true,
        });
        layout.Controls.Add(txtJoin);
        layout.Controls.Add(lblCheck);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnCancel = new Button { Text = "取消", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
        var btnOk = new Button { Text = "確定", Width = 90, Height = 30 };
        var btnTest = new Button { Text = "測試（查前 20 筆）", Width = 150, Height = 30 };
        var btnCopy = new Button { Text = "複製整段 SQL", Width = 120, Height = 30 };
        var btnReset = new Button { Text = "還原成資料表", Width = 120, Height = 30 };
        var btnRemove = new Button { Text = "移除這個 JOIN…", Width = 130, Height = 30, Visible = canRemove };
        buttons.Controls.AddRange(new Control[] { btnCancel, btnOk, btnTest, btnCopy, btnReset, btnRemove });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        CancelButton = btnCancel;

        lblHead.Text = "SELECT T0.*" + string.Concat(this.pickedColumns.Select(c => ", " + c)) + Environment.NewLine
                     + $"FROM {d.Table(mainSchema, mainTable)}{d.TableAlias(GridSql.BaseAlias)}";
        txtJoin.Text = JoinText(join.SourceText(d), GridSql.OnText(d, join));
        txtJoin.TextChanged += (s, e) => ShowCheck();
        Shown += (s, e) => { txtJoin.Select(txtJoin.TextLength, 0); txtJoin.Focus(); };
        btnCopy.Click += (s, e) => Clipboard.SetText(FullSql(null));
        btnReset.Click += (s, e) =>
        {
            var plain = new GridJoinDefinition { Alias = join.Alias, TableSchema = join.TableSchema, TableName = join.TableName, LinkText = LinkTextOrDefault() };
            txtJoin.Text = JoinText(plain.SourceText(d), GridSql.OnText(d, plain));
        };
        btnTest.Click += (s, e) => Test();
        btnOk.Click += (s, e) =>
        {
            if (!Parse(out string source, out string on, out string error))
            {
                MessageBox.Show(error, Text);
                return;
            }
            string problems = Check(source, on, out bool fatal);
            if (fatal && MessageBox.Show(problems + "\n\n仍要使用這個 JOIN 嗎？", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            Store(source, on);
            DialogResult = DialogResult.OK;
        };
        btnRemove.Click += (s, e) =>
        {
            RemoveRequested = true;
            DialogResult = DialogResult.OK;
        };
        ShowCheck();
    }

    private string JoinText(string source, string on) =>
        $"LEFT JOIN {source}{d.TableAlias(join.Alias)}" + Environment.NewLine + "       ON " + Regex.Replace(on, @"\s+AND\s+", Environment.NewLine + "      AND ", RegexOptions.IgnoreCase);

    private string FullSql(int? top) =>
        $"SELECT {(top is int n ? d.Top(n) : "")}T0.*" + string.Concat(pickedColumns.Select(c => ", " + c)) + Environment.NewLine
        + $"FROM {d.Table(mainSchema, mainTable)}{d.TableAlias(GridSql.BaseAlias)}" + Environment.NewLine + txtJoin.Text.Trim()
        + (top is int m ? d.Limit(m) : "");

    private string LinkTextOrDefault() => !string.IsNullOrWhiteSpace(join.LinkText) ? join.LinkText : "";

    /// <summary>拆出來源與 ON 條件：LEFT JOIN 來源 [AS] 別名 ON 條件</summary>
    private bool Parse(out string source, out string on, out string error)
    {
        source = on = error = null;
        string alias = Regex.Escape(join.Alias);
        var m = Regex.Match(txtJoin.Text.Trim().TrimEnd(';'),
            $@"^\s*LEFT\s+(?:OUTER\s+)?JOIN\s+(?<src>.+?)\s+(?:AS\s+)?[\[""`]?{alias}[\]""`]?\s+ON\s+(?<on>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
        {
            error = $"JOIN 要寫成：LEFT JOIN 來源 AS {join.Alias} ON 條件（別名要維持 {join.Alias}，欄位設定用這個別名取值）";
            return false;
        }
        source = m.Groups["src"].Value.Trim();
        on = m.Groups["on"].Value.Trim();
        return true;
    }

    /// <summary>ON 條件是「T0.欄位 = 別名.欄位 AND …」時轉回一般的關聯條件；不是就回傳 null</summary>
    private List<(string Master, string Detail)> AsLinks(string on)
    {
        var pairs = new List<(string, string)>();
        string alias = Regex.Escape(join.Alias);
        foreach (string part in Regex.Split(on, @"\s+AND\s+", RegexOptions.IgnoreCase))
        {
            var m = Regex.Match(part.Trim(), $@"^(?:[\[""`]?(?<la>\w+)[\]""`]?\.)?[\[""`]?(?<l>[^\]""`.=\s]+)[\]""`]?\s*=\s*[\[""`]?{alias}[\]""`]?\.[\[""`]?(?<r>[^\]""`.=\s]+)[\]""`]?$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            string la = m.Groups["la"].Value;
            string master = la == "" || la.Equals(GridSql.BaseAlias, StringComparison.OrdinalIgnoreCase) ? m.Groups["l"].Value : $"{la}.{m.Groups["l"].Value}";
            pairs.Add((master, m.Groups["r"].Value));
        }
        return pairs.Count > 0 ? pairs : null;
    }

    /// <summary>是不是原本的資料表（不是子查詢）</summary>
    private bool IsPlainTable(string source)
    {
        string Norm(string s) => Regex.Replace(s ?? "", @"[\[\]""`\s]", "").ToLowerInvariant();
        return Norm(source) == Norm(join.FullTableName(d)) || Norm(source) == Norm(join.TableName);
    }

    private void Store(string source, string on)
    {
        var links = AsLinks(on);
        SourceSql = IsPlainTable(source) ? null : source;
        if (links != null)
        {
            LinkText = string.Join(", ", links.Select(l => $"{l.Master}={l.Detail}"));
            OnSql = null;
        }
        else
        {
            LinkText = LinkTextOrDefault();
            OnSql = on;
        }
    }

    private void ShowCheck()
    {
        if (!Parse(out string source, out string on, out string error))
        {
            lblCheck.ForeColor = Color.Firebrick;
            lblCheck.Text = error;
            return;
        }
        string problems = Check(source, on, out _);
        lblCheck.ForeColor = problems == null ? Color.DarkGreen : Color.Firebrick;
        lblCheck.Text = problems ?? (AsLinks(on) != null && IsPlainTable(source)
            ? "關聯條件的欄位都找得到，兩邊型別也相容"
            : "自訂的 JOIN（子查詢或特殊條件）：存成自訂 SQL，按「測試」實際查一次確認不會出錯");
    }

    /// <summary>一般關聯條件時檢查欄位、型別；沒問題回傳 null。fatal = 執行時一定會出錯</summary>
    private string Check(string source, string on, out bool fatal)
    {
        fatal = false;
        var links = AsLinks(on);
        if (links == null || !IsPlainTable(source)) return null;         // 自訂 SQL 靠「測試」
        var notes = new List<string>();
        foreach (var (local, remote) in links)
        {
            if (local.Contains('.')) continue;                            // 別的 JOIN 帶進來的欄位，不檢查
            bool hasLocal = localTypes.TryGetValue(local, out var lt);
            remoteTypes.TryGetValue(remote, out var rt);
            if (!hasLocal) { notes.Add($"本身沒有「{local}」這個欄位"); fatal = true; continue; }
            if (remoteTypes.Count > 0 && rt == null) { notes.Add($"「{join.TableName}」沒有「{remote}」這個欄位"); fatal = true; continue; }
            if (lt != null && rt != null && Kind(lt) != "?" && Kind(rt) != "?" && Kind(lt) != Kind(rt))
            {
                notes.Add($"{local}（{lt}）= {remote}（{rt}）型別不同，執行時會出現「轉換成資料類型 {(Kind(lt) == "文字" ? rt : lt)} 時失敗」");
                fatal = true;
            }
        }
        return notes.Count == 0 ? null : string.Join("；", notes);
    }

    /// <summary>實際查前 20 筆（只讀），看 SQL 會不會出錯、JOIN 之後有沒有變多筆</summary>
    private void Test()
    {
        try
        {
            using var conn = d.Open(connectionString);
            using var cmd = d.CreateCommand(conn, FullSql(20));
            var table = d.Fill(cmd);
            lblCheck.ForeColor = Color.DarkGreen;
            lblCheck.Text = $"SQL 執行成功，讀到 {table.Rows.Count} 筆（最多 20 筆）。";
        }
        catch (Exception ex)
        {
            lblCheck.ForeColor = Color.Firebrick;
            lblCheck.Text = "SQL 執行失敗：" + ex.Message;
        }
    }

    private static string Kind(string type) => (type ?? "").ToLowerInvariant() switch
    {
        "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "uniqueidentifier" => "文字",
        "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" or "bit" => "數字",
        "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time" => "日期",
        _ => "?",
    };
}
