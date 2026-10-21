using System.Data;

namespace FormBuilder;

/// <summary>
/// JOIN 其他資料表：選資料表、設定關聯條件（Grid 欄位 = 關聯資料表欄位）、勾選要帶進來的欄位。
/// 按「確定」後，結果在 Join（JOIN 定義）與 NewColumns（帶進來的唯讀欄位）
/// </summary>
public partial class JoinDialog : Form
{
    private readonly string connectionString;
    private readonly string alias;
    private readonly HashSet<string> usedNames;
    private readonly List<(string Schema, string Name)> allTables = new();
    private readonly DataTable linkTable = new();
    private readonly DataTable pickTable = new();
    private List<ColumnInfo> remoteColumns = new();
    private (string Schema, string Name)? selectedTable;
    private readonly List<ColumnInfo> localColumns;
    /// <summary>列印樣板的單身：一筆單頭本來就對到多筆單身，不用提醒主鍵沒放進條件</summary>
    private readonly bool forDetail;

    public GridJoinDefinition Join { get; private set; }
    public List<GridColumnDefinition> NewColumns { get; } = new();

    /// <param name="alias">這個 JOIN 在 SQL 裡的別名（J1、J2…）</param>
    /// <param name="localColumns">Grid 本身資料表的欄位（關聯條件左邊的選項）</param>
    /// <param name="existingColumnNames">Grid 已經有的欄位名稱，帶進來的欄位不能重複</param>
    /// <param name="forDetail">列印樣板設定單身用（左邊 = 單頭欄位，右邊 = 單身欄位）</param>
    public JoinDialog(string connectionString, string alias, List<ColumnInfo> localColumns, IEnumerable<string> existingColumnNames, bool forDetail = false)
    {
        InitializeComponent();
        this.forDetail = forDetail;
        if (forDetail) Text = "設定單身：選單身資料表、對應條件（單頭欄位 = 單身欄位）、勾選要印的欄位";
        this.connectionString = connectionString;
        this.alias = alias;
        this.localColumns = localColumns;
        usedNames = new HashSet<string>(existingColumnNames, StringComparer.OrdinalIgnoreCase);

        linkTable.Columns.Add("Local", typeof(string));
        linkTable.Columns.Add("Remote", typeof(string));
        colLocal.Items.AddRange(localColumns.Select(c => c.Name).ToArray());
        dgvLinks.DataSource = linkTable;

        pickTable.Columns.Add("Pick", typeof(bool));
        pickTable.Columns.Add("Name", typeof(string));
        pickTable.Columns.Add("Type", typeof(string));
        pickTable.Columns.Add("Header", typeof(string));
        dgvPick.DataSource = pickTable;
        dgvLinks.DataError += (s, e) => e.Cancel = true;

        txtFilter.TextChanged += (s, e) => FillTables();
        lstTables.SelectedIndexChanged += (s, e) => OnTableSelected();
        btnOk.Click += (s, e) => Apply();
        Load += (s, e) => LoadTables();
    }

    private void LoadTables()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("沒有資料庫連線，不能 JOIN 其他資料表。請先在主畫面連線。", "提醒");
            DialogResult = DialogResult.Cancel;
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            allTables.AddRange(SchemaReader.GetTables(connectionString));
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取資料表清單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        FillTables();
    }

    private sealed record TableItem(string Schema, string Name)
    {
        public override string ToString() => $"{Schema}.{Name}";
    }

    private void FillTables()
    {
        string keyword = txtFilter.Text.Trim();
        lstTables.BeginUpdate();
        lstTables.Items.Clear();
        foreach (var t in allTables.Where(t => keyword == "" || t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            lstTables.Items.Add(new TableItem(t.Schema, t.Name));
        lstTables.EndUpdate();
    }

    /// <summary>選了資料表：讀它的欄位，自動推測關聯條件（跟新增單身一樣：同名欄位、ERP 命名規則）</summary>
    private void OnTableSelected()
    {
        if (lstTables.SelectedItem is not TableItem t) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            remoteColumns = SchemaReader.GetColumns(connectionString, t.Schema, t.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取欄位失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        selectedTable = (t.Schema, t.Name);

        colRemote.Items.Clear();
        colRemote.Items.AddRange(remoteColumns.Select(c => c.Name).ToArray());
        linkTable.Clear();
        foreach (var (local, remote) in GridDefinition.ParseLinks(DesignerForm.GuessLinks(localColumns, remoteColumns)))
            linkTable.Rows.Add(local, remote);

        pickTable.Clear();
        foreach (var c in remoteColumns)
        {
            string type = c.MaxLength > 0 ? $"{c.DataType}({c.MaxLength})" : c.MaxLength == -1 ? $"{c.DataType}(max)" : c.DataType;
            pickTable.Rows.Add(false, c.Name, type, string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description);
        }
    }

    private void Apply()
    {
        dgvLinks.EndEdit();
        dgvPick.EndEdit();
        if (selectedTable == null)
        {
            MessageBox.Show("請先選擇要 JOIN 的資料表。", "提醒");
            return;
        }
        var links = linkTable.Rows.Cast<DataRow>()
            .Select(r => (Local: Convert.ToString(r["Local"]).Trim(), Remote: Convert.ToString(r["Remote"]).Trim()))
            .Where(l => l.Local != "" && l.Remote != "")
            .ToList();
        if (links.Count == 0)
        {
            MessageBox.Show("請設定至少一組關聯條件（Grid 欄位 = 關聯資料表欄位）。", "提醒");
            return;
        }
        var picked = pickTable.Rows.Cast<DataRow>().Where(r => (bool)r["Pick"]).ToList();
        if (picked.Count == 0)
        {
            MessageBox.Show("請勾選至少一個要帶進來的欄位。", "提醒");
            return;
        }
        // 關聯資料表的主鍵沒有全部放進條件，一筆可能對到多筆，Grid 的列會重複
        var primaryKeys = remoteColumns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        if (!forDetail && primaryKeys.Count > 0 && !primaryKeys.All(k => links.Any(l => l.Remote.Equals(k, StringComparison.OrdinalIgnoreCase))) &&
            MessageBox.Show($"關聯資料表的主鍵（{string.Join("、", primaryKeys)}）沒有全部放進關聯條件，同一筆可能對到多筆資料，造成 Grid 的列重複。\n\n仍要加入嗎？",
                            "提醒", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        Join = new GridJoinDefinition
        {
            Alias = alias,
            TableSchema = selectedTable.Value.Schema,
            TableName = selectedTable.Value.Name,
            LinkText = string.Join(", ", links.Select(l => $"{l.Local}={l.Remote}")),
        };
        foreach (var row in picked)
        {
            string source = (string)row["Name"];
            var info = remoteColumns.First(c => c.Name == source);
            // 顯示用的欄位名稱不能跟 Grid 已有的欄位重複：重複時加上資料表名稱，還是重複就加編號
            string name = source;
            if (!usedNames.Add(name))
            {
                name = $"{Join.TableName}_{source}";
                for (int n = 2; !usedNames.Add(name); n++) name = $"{Join.TableName}_{source}{n}";
            }
            NewColumns.Add(new GridColumnDefinition
            {
                Column = name,
                DataType = info.DataType,
                MaxLength = info.MaxLength,
                IsNullable = true,                      // LEFT JOIN 對不到時是空白
                Header = string.IsNullOrWhiteSpace(Convert.ToString(row["Header"])) ? name : (string)row["Header"],
                Width = Math.Clamp(TypeHelper.SuggestWidth(info), 60, 200),
                NoEntry = true,                         // 關聯資料表的欄位只顯示，不寫回資料庫
                JoinAlias = alias,
                SourceColumn = source,
            });
        }
        DialogResult = DialogResult.OK;
    }
}
