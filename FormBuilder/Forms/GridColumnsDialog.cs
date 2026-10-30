using System.Data;

namespace FormBuilder;

/// <summary>
/// 單身 Grid 設定：上方設定單頭與單身的關聯欄位，下方勾選要顯示的欄位、改標題與寬度。
/// 按「確定」才寫回 GridDefinition
/// </summary>
public partial class GridColumnsDialog : Form
{
    private readonly GridDefinition grid;
    private readonly DataTable columnTable = new();
    private readonly DataTable linkTable = new();
    /// <summary>設定中的欄位與 JOIN（按「確定」才寫回 grid）；JOIN 帶進來的欄位也在 workColumns 裡</summary>
    private readonly List<GridColumnDefinition> workColumns;
    private readonly List<GridJoinDefinition> workJoins;
    private readonly HashSet<GridColumnDefinition> addedColumns = new();
    private readonly string connectionString;
    private string contextAlias;
    /// <summary>單頭清單：JOIN 加在單頭（畫面的 Joins）上，單頭主查詢一起查；null = 一般 Grid，JOIN 加在 Grid 自己身上</summary>
    private readonly List<GridJoinDefinition> masterJoins;
    /// <summary>單頭清單：畫面上的單頭欄位還在用的 JOIN（不能在這裡移除）</summary>
    private readonly HashSet<string> lockedAliases = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>單頭清單：單頭已經用掉的欄位名稱（JOIN 帶進來的欄位不能重複）</summary>
    private readonly List<string> usedNames = new();

    /// <param name="connectionString">「JOIN 其他資料表」、「編輯視窗版面」設計器的「資料表」頁籤要用的連線字串</param>
    /// <param name="forEditList">true = 設定編輯視窗上的明細清單（沒有「編輯視窗版面」按鈕）</param>
    /// <param name="saveForm">編輯視窗版面設計器「檔案 → 儲存」要呼叫的存檔動作；null = 不能存檔</param>
    /// <param name="screenFont">畫面的字型（編輯視窗沒設定字型時跟它一樣）</param>
    /// <param name="masterList">單頭清單才要傳：畫面定義（JOIN 寫到畫面的單頭 JOIN）與單頭已經用掉的欄位名稱</param>
    public GridColumnsDialog(GridDefinition gridDefinition, List<string> masterColumns, string connectionString = null, bool forEditList = false,
                             Action saveForm = null, Font screenFont = null, (FormDefinition Form, IEnumerable<string> UsedNames)? masterList = null)
    {
        InitializeComponent();
        grid = gridDefinition;
        this.connectionString = connectionString;
        workColumns = grid.Columns.ToList();
        if (grid.IsMasterList && masterList is { } m)
        {
            masterJoins = m.Form.Joins;
            lockedAliases.UnionWith(m.Form.Fields.Where(f => f.IsJoin).Select(f => f.JoinAlias));
            usedNames.AddRange(m.UsedNames);
        }
        workJoins = (masterJoins ?? grid.Joins).Select(j => j.Clone()).ToList();
        Text = $"{(forEditList ? "明細清單欄位設定" : "單身欄位設定")}：{grid.TableSchema}.{grid.TableName}";
        btnLayout.Visible = !forEditList;

        // ---- 關聯欄位：單頭欄位 = 單身欄位 ----
        linkTable.Columns.Add("Master", typeof(string));
        linkTable.Columns.Add("Detail", typeof(string));
        foreach (var (master, detail) in grid.Links) linkTable.Rows.Add(master, detail);
        colMaster.Items.AddRange(masterColumns.Union(grid.Links.Select(l => l.Master)).Distinct().OrderBy(n => n).ToArray());
        colDetail.Items.AddRange(grid.Columns.Where(c => !c.IsJoin).Select(c => c.Column).Union(grid.Links.Select(l => l.Detail)).Distinct().ToArray());
        dgvLinks.DataSource = linkTable;
        linkGroup.Visible = !grid.IsMasterList;            // 單頭清單就是單頭自己，不需要關聯欄位

        // ---- 欄位清單 ----
        columnTable.Columns.Add("Visible", typeof(bool));
        columnTable.Columns.Add("Column", typeof(string));
        columnTable.Columns.Add("Type", typeof(string));
        columnTable.Columns.Add("Header", typeof(string));
        columnTable.Columns.Add("Width", typeof(int));
        columnTable.Columns.Add("IsKey", typeof(bool));
        columnTable.Columns.Add("NoEntry", typeof(bool));
        columnTable.Columns.Add("Source", typeof(string));
        columnTable.Columns.Add("CellType", typeof(string));
        columnTable.Columns.Add("CellOptions", typeof(string));
        foreach (var c in workColumns) AddColumnRow(c);
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => { MessageBox.Show("寬度請輸入數字。", "提醒"); e.Cancel = true; };

        // ---- JOIN 其他資料表：帶進來的欄位是唯讀的，只能改顯示、標題、寬度 ----
        btnJoin.Visible = !grid.IsMasterList || masterJoins != null;     // 單頭清單的 JOIN 加在單頭主查詢上
        btnJoin.Click += (s, e) => AddJoin();
        dgvColumns.CellBeginEdit += (s, e) =>
        {
            if (e.RowIndex < 0 || Convert.ToString(dgvColumns.Rows[e.RowIndex].Cells[colSource.Index].Value) == "") return;
            if (e.ColumnIndex == colIsKey.Index || e.ColumnIndex == colNoEntry.Index) e.Cancel = true;
        };
        dgvColumns.CellMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            string column = Convert.ToString(dgvColumns.Rows[e.RowIndex].Cells[colColumn.Index].Value);
            var c = workColumns.FirstOrDefault(x => x.Column == column);
            if (c == null || !c.IsJoin) return;
            dgvColumns.CurrentCell = dgvColumns[e.ColumnIndex, e.RowIndex];
            contextAlias = c.JoinAlias;
            ctxJoin.Show(Cursor.Position);
        };
        miRemoveJoin.Click += (s, e) => RemoveJoin(contextAlias);

        // ---- 調整欄位順序：決定 Grid 的欄位由左到右的順序（編輯視窗「重新排列成一欄」也照這個順序）----
        foreach (DataGridViewColumn col in dgvColumns.Columns) col.SortMode = DataGridViewColumnSortMode.NotSortable;   // 點標題排序會跟移動打架
        btnUp.Click += (s, e) => MoveRow(-1);
        btnDown.Click += (s, e) => MoveRow(1);
        dgvColumns.KeyDown += (s, e) =>
        {
            if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                MoveRow(e.KeyCode == Keys.Up ? -1 : 1);
                e.Handled = true;
            }
        };

        btnAll.Click += (s, e) => SetAllVisible(true);
        btnNone.Click += (s, e) => SetAllVisible(false);
        btnLayout.Click += (s, e) =>
        {
            // 版面設計器有自己的確定 / 取消，確定後版面就存在 Grid 上
            // 版面設計器也可以 JOIN 其他資料表：別名、欄位名稱不能跟這裡還沒存的 JOIN 重複
            dgvColumns.EndEdit();
            var before = grid.Columns.ToList();
            using var layout = new EditLayoutDialog(grid, connectionString, saveForm, screenFont, masterJoins,
                                                    workColumns.Select(c => c.Column).Concat(usedNames), workJoins.Select(j => j.Alias));
            layout.ShowDialog(this);
            // 版面設計器 JOIN 進來的（已經加到 Grid 上了）也加進這裡，按「確定」時才不會被蓋掉
            foreach (var j in (masterJoins ?? grid.Joins).Where(j => !workJoins.Any(x => x.Alias == j.Alias)))
                workJoins.Add(j.Clone());
            foreach (var c in grid.Columns.Where(c => !workColumns.Contains(c)).ToList())
            {
                workColumns.Add(c);
                AddColumnRow(c);
            }
            // 版面設計器移除的 JOIN 欄位（已經從 Grid 拿掉了）這裡也拿掉
            foreach (var c in before.Where(c => !grid.Columns.Contains(c)))
            {
                workColumns.Remove(c);
                columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => (string)r["Column"] == c.Column)?.Delete();
            }
            columnTable.AcceptChanges();
        };
        btnOk.Click += (s, e) => Apply();
    }

    private void AddColumnRow(GridColumnDefinition c)
    {
        string type = c.MaxLength > 0 ? $"{c.DataType}({c.MaxLength})" : c.MaxLength == -1 ? $"{c.DataType}(max)" : c.DataType;
        string source = "";
        if (c.IsJoin)
        {
            var join = workJoins.FirstOrDefault(j => j.Alias == c.JoinAlias);
            source = join == null ? $"單頭 JOIN {c.JoinAlias}：{c.SourceColumn}" : $"{c.JoinAlias}：{join.TableName}.{c.SourceColumn}";   // 單頭清單上的是單頭 JOIN 的欄位
        }
        columnTable.Rows.Add(c.Visible, c.Column, type, c.Header, c.Width, c.IsKey, c.NoEntry, source, c.CellType.ToString(), c.CellOptions);
    }

    /// <summary>JOIN 其他資料表：選資料表、關聯條件、要帶進來的欄位，帶進來的欄位加到清單最後面</summary>
    private void AddJoin()
    {
        dgvColumns.EndEdit();
        int n = 1;
        while (workJoins.Any(j => j.Alias == $"J{n}")) n++;
        var localColumns = workColumns.Where(c => !c.IsJoin)
            .Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, IsPrimaryKey = c.IsKey }).ToList();
        using var dlg = new JoinDialog(connectionString, $"J{n}", localColumns, workColumns.Select(c => c.Column).Concat(usedNames));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        workJoins.Add(dlg.Join);
        foreach (var c in dlg.NewColumns)
        {
            workColumns.Add(c);
            addedColumns.Add(c);
            AddColumnRow(c);
        }
    }

    /// <summary>移除一個 JOIN，連同它帶進來的欄位</summary>
    private void RemoveJoin(string alias)
    {
        if (alias == null) return;
        if (lockedAliases.Contains(alias))
        {
            MessageBox.Show($"畫面上的單頭欄位還在用 JOIN {alias}，不能在這裡移除。不要顯示的話，取消勾選「顯示」就好。", "提醒");
            return;
        }
        var columns = workColumns.Where(c => c.JoinAlias == alias).ToList();
        if (MessageBox.Show($"確定要移除 JOIN {alias}（{columns.Count} 個欄位）嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        dgvColumns.EndEdit();
        foreach (var c in columns)
        {
            workColumns.Remove(c);
            addedColumns.Remove(c);
            var row = columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => (string)r["Column"] == c.Column);
            row?.Delete();
        }
        columnTable.AcceptChanges();
        workJoins.RemoveAll(j => j.Alias == alias);
    }

    /// <summary>目前選的欄位上移 / 下移一列（delta = -1 / 1），移完維持選取在同一個欄位</summary>
    private void MoveRow(int delta)
    {
        dgvColumns.EndEdit();
        if (dgvColumns.CurrentCell == null) return;
        int index = dgvColumns.CurrentCell.RowIndex;
        int target = index + delta;
        if (target < 0 || target >= columnTable.Rows.Count) return;
        int columnIndex = dgvColumns.CurrentCell.ColumnIndex;
        var values = columnTable.Rows[index].ItemArray;
        columnTable.Rows.RemoveAt(index);
        var moved = columnTable.NewRow();
        moved.ItemArray = values;
        columnTable.Rows.InsertAt(moved, target);
        dgvColumns.CurrentCell = dgvColumns[columnIndex, target];
    }

    private void SetAllVisible(bool visible)
    {
        dgvColumns.EndEdit();
        foreach (DataRow row in columnTable.Rows) row["Visible"] = visible;
    }

    private void Apply()
    {
        dgvColumns.EndEdit();
        dgvLinks.EndEdit();
        var links = linkTable.Rows.Cast<DataRow>()
            .Where(r => r.RowState != DataRowState.Deleted)
            .Select(r => (Master: Convert.ToString(r["Master"]).Trim(), Detail: Convert.ToString(r["Detail"]).Trim()))
            .Where(l => l.Master != "" && l.Detail != "")
            .ToList();
        if (links.Count == 0 && !grid.IsMasterList &&
            MessageBox.Show("沒有設定關聯欄位，單身會列出整張資料表的資料。確定要這樣設定嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        if (!columnTable.Rows.Cast<DataRow>().Any(r => (bool)r["Visible"]))
        {
            MessageBox.Show("至少要顯示一個欄位。", "提醒");
            return;
        }

        grid.LinkText = string.Join(", ", links.Select(l => $"{l.Master}={l.Detail}"));
        foreach (DataRow row in columnTable.Rows)
        {
            var c = workColumns.First(x => x.Column == (string)row["Column"]);
            c.Visible = (bool)row["Visible"];
            c.Header = string.IsNullOrWhiteSpace(Convert.ToString(row["Header"])) ? c.Column : (string)row["Header"];
            c.Width = Math.Clamp(row["Width"] is int w ? w : 100, 30, 800);
            // JOIN 帶進來的欄位一律只顯示、不是主鍵（不能寫回關聯資料表）
            c.IsKey = !c.IsJoin && (bool)row["IsKey"];
            c.NoEntry = c.IsJoin || (bool)row["NoEntry"];
            c.CellType = Enum.TryParse<GridCellKind>(Convert.ToString(row["CellType"]), out var kind) ? kind : GridCellKind.自動;
            c.CellOptions = string.IsNullOrWhiteSpace(Convert.ToString(row["CellOptions"])) ? null : Convert.ToString(row["CellOptions"]).Trim();
        }
        // 已經設計過編輯視窗版面時，新帶進來的欄位預設不放進編輯視窗（不然會全部疊在左上角），要用的話到版面設計器拖進去
        if (grid.HasEditLayout)
            foreach (var c in addedColumns) c.EditVisible = false;
        // 就地更新（編輯視窗明細清單的欄位清單跟這個是同一個 List）
        // 欄位順序照清單目前的順序（上移 / 下移調整的結果）
        var ordered = columnTable.Rows.Cast<DataRow>().Select(r => workColumns.First(x => x.Column == (string)r["Column"])).ToList();
        grid.Columns.Clear();
        grid.Columns.AddRange(ordered);
        if (masterJoins != null)
        {
            masterJoins.Clear();                           // 單頭清單：寫回畫面的單頭 JOIN（同一個 List，單頭欄位也在用）
            masterJoins.AddRange(workJoins);
        }
        else grid.Joins = workJoins;
        DialogResult = DialogResult.OK;
    }
}
