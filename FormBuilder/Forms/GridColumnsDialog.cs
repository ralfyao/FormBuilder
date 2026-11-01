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
    /// <summary>自訂按鈕欄位改過的按鈕動作（按「確定」才寫回欄位）</summary>
    private readonly Dictionary<GridColumnDefinition, ButtonDefinition> editedButtons = new();

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
        colDetail.Items.AddRange(grid.Columns.Where(c => !c.IsJoin && !c.ScreenOnly).Select(c => c.Column).Union(grid.Links.Select(l => l.Detail)).Distinct().ToArray());
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
        columnTable.Columns.Add("Formula", typeof(string));
        columnTable.Columns.Add("Decimals", typeof(string));
        columnTable.Columns.Add("AfterSelect", typeof(string));          // 執行後動作的 SQL 摘要（顯示用）
        columnTable.Columns.Add("AfterSql", typeof(string));
        columnTable.Columns.Add("AfterParams", typeof(string));
        columnTable.Columns.Add("AfterReturn", typeof(string));
        foreach (var c in workColumns) AddColumnRow(c);
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => { MessageBox.Show("寬度請輸入數字。", "提醒"); e.Cancel = true; };

        // ---- JOIN 其他資料表：帶進來的欄位是唯讀的，只能改顯示、標題、寬度 ----
        btnJoin.Visible = !grid.IsMasterList || masterJoins != null;     // 單頭清單的 JOIN 加在單頭主查詢上
        btnJoin.Click += (s, e) => AddJoin();
        dgvColumns.CellBeginEdit += (s, e) =>
        {
            if (e.RowIndex < 0 || Convert.ToString(dgvColumns.Rows[e.RowIndex].Cells[colSource.Index].Value) == "") return;
            // 自訂文字框 / 數字框可以設公式、小數位數
            if (IsCustomText(ColumnAt(e.RowIndex)) && (e.ColumnIndex == colFormula.Index || e.ColumnIndex == colDecimals.Index)) return;
            if (e.ColumnIndex == colIsKey.Index || e.ColumnIndex == colNoEntry.Index || e.ColumnIndex == colFormula.Index || e.ColumnIndex == colDecimals.Index) e.Cancel = true;
            // 自訂欄位：類型建立時就決定了；「選項 / 格式」只有按鈕用來改按鈕文字
            if (ColumnAt(e.RowIndex) is { IsCustom: true } c &&
                (e.ColumnIndex == colCellType.Index || e.ColumnIndex == colCellOptions.Index && c.CellType != GridCellKind.按鈕))
                e.Cancel = true;
        };
        // 「按鈕」只給自訂欄位用，資料表的欄位選了就改回自動
        dgvColumns.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != colCellType.Index || ColumnAt(e.RowIndex) is not { IsCustom: false }) return;
            if (Convert.ToString(dgvColumns.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) != nameof(GridCellKind.按鈕)) return;
            BeginInvoke(() => columnTable.Rows[e.RowIndex]["CellType"] = nameof(GridCellKind.自動));
            MessageBox.Show("「按鈕」只能用在自訂欄位，請按「新增自訂欄位…」加一個按鈕欄位。", "提醒");
        };
        dgvColumns.CellMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            var c = ColumnAt(e.RowIndex);
            if (c == null || !c.IsJoin && !c.IsCustom) return;
            dgvColumns.CurrentCell = dgvColumns[e.ColumnIndex, e.RowIndex];
            if (c.IsCustom)
            {
                miEditButton.Visible = c.CellType == GridCellKind.按鈕;
                miEditValueSql.Visible = IsCustomText(c);
                ctxCustom.Show(Cursor.Position);
                return;
            }
            contextAlias = c.JoinAlias;
            ctxJoin.Show(Cursor.Position);
        };
        miRemoveJoin.Click += (s, e) => RemoveJoin(contextAlias);

        // ---- 自訂欄位：勾選框、按鈕、日期選擇，不是資料表的欄位（不查詢、不存檔）----
        btnCustom.Click += (s, e) => AddCustom();
        miEditButton.Click += (s, e) => EditButton(CurrentColumn());
        miRemoveCustom.Click += (s, e) => RemoveCustom(CurrentColumn());
        miEditValueSql.Click += (s, e) => EditValueSql(CurrentColumn());
        ctxCustom.Items.AddRange(new ToolStripItem[] { miEditButton, miEditValueSql, miRemoveCustom });
        dgvColumns.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == colFormula.Index)
            {
                EditFormula(e.RowIndex);
                return;
            }
            if (e.RowIndex >= 0 && e.ColumnIndex == colAfterSelect.Index)
            {
                EditAfterSelect(e.RowIndex);
                return;
            }
            if (e.RowIndex >= 0 && ColumnAt(e.RowIndex) is { IsCustom: true, CellType: GridCellKind.按鈕 } c && e.ColumnIndex != colHeader.Index && e.ColumnIndex != colCellOptions.Index)
                EditButton(c);
            else if (e.RowIndex >= 0 && ColumnAt(e.RowIndex) is { } t && IsCustomText(t) && e.ColumnIndex != colHeader.Index && e.ColumnIndex != colDecimals.Index)
                EditValueSql(t);
        };

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
            var layout = new EditLayoutDialog(grid, connectionString, saveForm, screenFont, masterJoins,
                                              workColumns.Select(c => c.Column).Concat(usedNames), workJoins.Select(j => j.Alias));
            // 不用 ShowDialog：版面設計器開著時還可以切到其他畫面對照；這個視窗先停用，版面設計器關掉才同步回來
            Enabled = false;
            layout.FormClosed += (s, e) =>
            {
                Enabled = true;
                if (!IsDisposed) SyncFromLayout(before);
            };
            layout.Show(this);
        };
        btnOk.Click += (s, e) => Apply();
        btnCancel.Click += (s, e) => { if (!Modal) Close(); };      // 非強制回應視窗設 DialogResult 不會自己關
    }

    /// <summary>版面設計器關掉後，把它在 Grid 上加的 JOIN、欄位，移除的 JOIN 欄位同步到這裡的清單</summary>
    private void SyncFromLayout(List<GridColumnDefinition> before)
    {
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
        // 版面設計器也可以改「執行後動作」（同一個設定），以版面設計器存的為準
        foreach (DataRow row in columnTable.Rows)
        {
            if (workColumns.FirstOrDefault(x => x.Column == (string)row["Column"]) is not { } c) continue;
            row["AfterSql"] = (object)c.EditAfterSelectSql ?? DBNull.Value;
            row["AfterParams"] = (object)c.EditAfterSelectParams ?? DBNull.Value;
            row["AfterReturn"] = (object)c.EditAfterSelectReturn ?? DBNull.Value;
            row["AfterSelect"] = (object)AfterSelectSummary(c.EditAfterSelectSql) ?? DBNull.Value;
        }
    }

    private readonly ContextMenuStrip ctxCustom = new();
    private readonly ToolStripMenuItem miEditButton = new("設定按鈕動作…");
    private readonly ToolStripMenuItem miRemoveCustom = new("移除這個自訂欄位");
    private readonly ToolStripMenuItem miEditValueSql = new("設定顯示資料的 SQL…");
    /// <summary>自訂文字框 / 數字框改過的顯示 SQL（按「確定」才寫回欄位）</summary>
    private readonly Dictionary<GridColumnDefinition, string> editedValueSql = new();

    /// <summary>自訂的文字框 / 數字框：只顯示資料，值由顯示 SQL 或公式帶出來</summary>
    private static bool IsCustomText(GridColumnDefinition c) => c is { IsCustom: true, CellType: GridCellKind.文字框 };

    /// <summary>自訂文字框 / 數字框：設定顯示資料的 SQL（@欄位 取同一列）</summary>
    private void EditValueSql(GridColumnDefinition c)
    {
        if (!IsCustomText(c)) return;
        dgvColumns.EndEdit();
        string sql = editedValueSql.TryGetValue(c, out var edited) ? edited : c.ValueSql;
        var rowColumns = workColumns.Where(x => x != c && !(x.IsCustom && x.CellType == GridCellKind.按鈕)).Select(x => x.Column);
        if (!CustomColumnDialog.EditValueSql(this, c.Header ?? c.Column, ref sql, rowColumns)) return;
        editedValueSql[c] = sql;
        var row = columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => (string)r["Column"] == c.Column);
        if (row != null) row["Source"] = CustomSource(c, sql);
    }

    /// <summary>自訂欄位在「來源」欄顯示的說明</summary>
    private static string CustomSource(GridColumnDefinition c, string valueSql) =>
        IsCustomText(c) && !string.IsNullOrWhiteSpace(valueSql) ? "自訂欄位，SQL：" + AfterSelectSummary(valueSql) : "自訂欄位（不查詢、不存檔）";

    /// <summary>清單第 rowIndex 列對應的欄位</summary>
    private GridColumnDefinition ColumnAt(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= dgvColumns.Rows.Count) return null;
        string column = Convert.ToString(dgvColumns.Rows[rowIndex].Cells[colColumn.Index].Value);
        return workColumns.FirstOrDefault(x => x.Column == column);
    }

    private GridColumnDefinition CurrentColumn() => dgvColumns.CurrentCell == null ? null : ColumnAt(dgvColumns.CurrentCell.RowIndex);

    /// <summary>新增自訂欄位（勾選框、按鈕、日期選擇），加到清單最後面；按鈕接著設定動作</summary>
    private void AddCustom()
    {
        dgvColumns.EndEdit();
        using var dlg = new CustomColumnDialog(workColumns.Select(c => c.Column).Concat(usedNames));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var c = dlg.Column;
        workColumns.Add(c);
        AddColumnRow(c);
        dgvColumns.CurrentCell = dgvColumns[colHeader.Index, dgvColumns.Rows.Count - 1];
        if (c.CellType == GridCellKind.按鈕) EditButton(c);
    }

    private void EditButton(GridColumnDefinition c)
    {
        if (c is not { IsCustom: true, CellType: GridCellKind.按鈕 }) return;
        dgvColumns.EndEdit();
        var rowColumns = workColumns.Where(x => x.CellType != GridCellKind.按鈕 || !x.IsCustom).Select(x => x.Column);
        var edited = CustomColumnDialog.EditButton(this, editedButtons.TryGetValue(c, out var b) ? b : c.Button, c.Header ?? c.Column, rowColumns);
        if (edited != null) editedButtons[c] = edited;
    }

    /// <summary>公式編輯視窗：左邊列出同一列的其他欄位（按鈕欄位除外）</summary>
    private void EditFormula(int rowIndex)
    {
        var c = ColumnAt(rowIndex);
        if (c == null || c.IsJoin || c.IsCustom && !IsCustomText(c)) return;     // JOIN、自訂欄位（文字框 / 數字框除外）不能設公式
        dgvColumns.EndEdit();
        var fields = columnTable.Rows.Cast<DataRow>()
            .Where(r => (string)r["Column"] != c.Column)
            .Select(r => workColumns.First(x => x.Column == (string)r["Column"]))
            .Where(x => !(x.IsCustom && x.CellType == GridCellKind.按鈕))
            .Select(x => (x.Column, x.Header ?? x.Column));
        using var dlg = new FormulaDialog(Convert.ToString(columnTable.Rows[rowIndex]["Formula"]), fields, "同一列的欄位（雙擊插入）");
        if (dlg.ShowDialog(this) == DialogResult.OK) columnTable.Rows[rowIndex]["Formula"] = dlg.Formula == "" ? DBNull.Value : dlg.Formula;
    }

    /// <summary>執行後動作：這一格的值改了就執行 SQL，查出來的值帶進同一列（跟編輯視窗版面的「執行後動作」是同一個設定）</summary>
    private void EditAfterSelect(int rowIndex)
    {
        var c = ColumnAt(rowIndex);
        if (c == null || c.IsJoin || c.IsCustom || c.NoEntry) return;      // 不能輸入的欄位不會改值
        dgvColumns.EndEdit();
        var row = columnTable.Rows[rowIndex];
        var rowColumns = workColumns.Where(x => !(x.IsCustom && x.CellType == GridCellKind.按鈕)).Select(x => x.Column);
        using var dlg = new AfterSelectDialog(c.Header ?? c.Column, Convert.ToString(row["AfterSql"]), Convert.ToString(row["AfterParams"]),
                                              Convert.ToString(row["AfterReturn"]), rowColumns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        row["AfterSql"] = (object)dlg.Sql ?? DBNull.Value;
        row["AfterParams"] = (object)dlg.Params ?? DBNull.Value;
        row["AfterReturn"] = (object)dlg.Return ?? DBNull.Value;
        row["AfterSelect"] = (object)AfterSelectSummary(dlg.Sql) ?? DBNull.Value;
    }

    private static string AfterSelectSummary(string sql) =>
        string.IsNullOrWhiteSpace(sql) ? null : string.Join(" ", sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    private void RemoveCustom(GridColumnDefinition c)
    {
        if (c is not { IsCustom: true }) return;
        if (MessageBox.Show($"確定要移除自訂欄位「{c.Header ?? c.Column}」嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        dgvColumns.EndEdit();
        workColumns.Remove(c);
        editedButtons.Remove(c);
        editedValueSql.Remove(c);
        columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => (string)r["Column"] == c.Column)?.Delete();
        columnTable.AcceptChanges();
    }

    private void AddColumnRow(GridColumnDefinition c)
    {
        string type = c.IsCustom ? "（自訂）" : c.MaxLength > 0 ? $"{c.DataType}({c.MaxLength})" : c.MaxLength == -1 ? $"{c.DataType}(max)" : c.DataType;
        string source = c.IsCustom ? CustomSource(c, c.ValueSql) : "";
        if (c.IsJoin)
        {
            var join = workJoins.FirstOrDefault(j => j.Alias == c.JoinAlias);
            source = join == null ? $"單頭 JOIN {c.JoinAlias}：{c.SourceColumn}" : $"{c.JoinAlias}：{join.TableName}.{c.SourceColumn}";   // 單頭清單上的是單頭 JOIN 的欄位
        }
        columnTable.Rows.Add(c.Visible, c.Column, type, c.Header, c.Width, c.IsKey, c.NoEntry, source, c.CellType.ToString(), c.CellOptions,
                             c.Formula, c.Decimals >= 0 ? c.Decimals.ToString() : null,
                             AfterSelectSummary(c.EditAfterSelectSql), c.EditAfterSelectSql, c.EditAfterSelectParams, c.EditAfterSelectReturn);
    }

    /// <summary>JOIN 其他資料表：選資料表、關聯條件、要帶進來的欄位，帶進來的欄位加到清單最後面</summary>
    private void AddJoin()
    {
        dgvColumns.EndEdit();
        int n = 1;
        while (workJoins.Any(j => j.Alias == $"J{n}")) n++;
        var localColumns = workColumns.Where(c => !c.IsJoin && !c.ScreenOnly)
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
            // 自訂欄位一律不存檔、類型不變
            c.IsKey = !c.IsJoin && !c.IsCustom && (bool)row["IsKey"];
            c.NoEntry = c.IsJoin || c.IsCustom || (bool)row["NoEntry"];
            if (!c.IsCustom)
                c.CellType = Enum.TryParse<GridCellKind>(Convert.ToString(row["CellType"]), out var kind) && kind != GridCellKind.按鈕 ? kind : GridCellKind.自動;
            if (editedButtons.TryGetValue(c, out var button)) c.Button = button;
            c.CellOptions = string.IsNullOrWhiteSpace(Convert.ToString(row["CellOptions"])) ? null : Convert.ToString(row["CellOptions"]).Trim();
            if (editedValueSql.TryGetValue(c, out var valueSql)) c.ValueSql = valueSql;
            c.Formula = c.IsJoin || c.IsCustom && !IsCustomText(c) || string.IsNullOrWhiteSpace(Convert.ToString(row["Formula"])) ? null : Convert.ToString(row["Formula"]).Trim();
            c.EditAfterSelectSql = Convert.ToString(row["AfterSql"]) is { Length: > 0 } sql ? sql : null;
            c.EditAfterSelectParams = Convert.ToString(row["AfterParams"]) is { Length: > 0 } ps ? ps : null;
            c.EditAfterSelectReturn = Convert.ToString(row["AfterReturn"]) is { Length: > 0 } ret ? ret : null;
            c.Decimals = int.TryParse(Convert.ToString(row["Decimals"]), out int decimals) && decimals >= 0 ? Math.Min(decimals, 10) : -1;
            if (c.IsCustom && c.Button != null) c.Button.Text = GridCells.ButtonText(c);     // 確認訊息、錯誤提醒的標題用按鈕文字
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
        if (!Modal) Close();
    }
}
