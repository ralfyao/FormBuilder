using System.Data;
using System.Data.Common;

namespace FormBuilder;

/// <summary>
/// 執行時編輯視窗上的明細清單（唯讀 DataGridView）：依關聯欄位（編輯視窗欄位 = 明細欄位）列出目前這筆的明細。
/// 單頭編輯視窗（RecordNavigatorDialog）與單身編輯視窗（RecordEditDialog）共用
/// </summary>
public class EditListView
{
    /// <summary>沒設關聯欄位時最多列出幾筆，避免一次把整張大表讀進來</summary>
    private const int MaxRowsWithoutLink = 1000;

    private readonly EditListDefinition list;
    private readonly string connectionString;
    /// <summary>清單的定義（資料表、欄位、關聯）</summary>
    public EditListDefinition Definition => list;
    public DataGridView Grid { get; }
    /// <summary>加到畫面上的是這個圓角外框（Grid 在裡面）</summary>
    public Control Host { get; }

    /// <summary>上層清單（定義的「上層清單」）：有的話，關聯欄位的左邊取上層清單選取那一列的值；null = 取編輯視窗目前這筆</summary>
    private EditListView parent;
    /// <summary>以這個清單為上層清單的明細清單：點這個清單的一列時跟著重新載入</summary>
    private readonly List<EditListView> children = new();
    /// <summary>上次帶給下層清單的那一列（同一列不用重新查）</summary>
    private DataRow lastParentRow;
    /// <summary>上次載入用的取值方式（自訂按鈕執行 SQL 後重新載入用）</summary>
    private Func<string, object> lastValueOf;

    public EditListView(EditListDefinition definition, string connectionString)
    {
        list = definition;
        this.connectionString = connectionString;
        Grid = new DataGridView
        {
            Bounds = new Rectangle(list.X, list.Y, Math.Max(list.Width, EditListBox.MinWidth), Math.Max(list.Height, EditListBox.MinHeight)),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window,
        };
        GridCells.ApplyGridLines(Grid);
        // 欄位依「輸入方式」顯示：下拉選單顯示選項文字、勾選框顯示打勾、日期依格式顯示
        foreach (var c in list.Columns.Where(GridCells.ShownInGrid))
        {
            var col = GridCells.CreateColumn(c, connectionString);
            col.SortMode = DataGridViewColumnSortMode.Automatic;
            Grid.Columns.Add(col);
        }
        Grid.DataError += (s, e) =>
        {
            e.ThrowException = false;                            // 瀏覽時：格式不符的值顯示空白
            if (editing && !GridCells.IgnoreDisplayError(Grid, e) && e.Context.HasFlag(DataGridViewDataErrorContexts.Commit))
                MessageBox.Show($"第 {e.RowIndex + 1} 列「{Grid.Columns[e.ColumnIndex].HeaderText}」的格式不正確。", "提醒");
        };
        // 輸入中：欄位的執行後動作（@名稱 先取同一列，再取編輯視窗上的欄位）
        GridCells.HookAfterSelect(Grid, connectionString, name => MasterValueOf?.Invoke(name));
        Host = RoundedGridHost.Wrap(Grid);
        // 載入後不要自動選第一列：使用者自己點選的才算「選取」（列印選取的資料時用）
        Grid.DataBindingComplete += (s, e) => Grid.ClearSelection();
        // 點選一列：下層清單列出這一列的明細
        Grid.SelectionChanged += (s, e) => ReloadChildren(force: false);
        // 資料有變動（重新載入、輸入、刪除）就通知（執行後動作重算）
        Grid.DataSourceChanged += (s, e) =>
        {
            if (Grid.DataSource is DataTable table)
            {
                table.ColumnChanged += (s2, e2) => Changed?.Invoke(this);
                table.RowChanged += (s2, e2) => { if (e2.Action is DataRowAction.Add or DataRowAction.Delete or DataRowAction.Rollback) Changed?.Invoke(this); };
                table.RowDeleted += (s2, e2) => Changed?.Invoke(this);
            }
            Changed?.Invoke(this);
        };
    }

    /// <summary>
    /// 依定義的「上層清單」（資料表名稱）把清單串起來；找不到、指到自己或繞成一圈的不串（照舊依編輯視窗目前這筆列出）。
    /// 建好所有清單、還沒 Reload 前呼叫
    /// </summary>
    public static void Connect(IList<EditListView> views)
    {
        foreach (var view in views)
        {
            string name = view.list.ParentList?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var parent = views.FirstOrDefault(v => v != view && v.list.TableName.Equals(name, StringComparison.OrdinalIgnoreCase));
            bool loop = false;
            for (var p = parent; p != null && !loop; p = p.parent) loop = p == view;
            if (parent == null || loop) continue;
            view.parent = parent;
            parent.children.Add(view);
        }
    }

    /// <summary>
    /// 清單的「雙擊開啟畫面」：雙擊一列開啟設定的畫面，傳入參數的 @名稱 先取那一列的值，清單上沒有的交給 fallback（視窗上的欄位、目前這筆）。
    /// 沒設定畫面就不動作
    /// </summary>
    public void HookOpenForm(string sourcePath, IWin32Window owner, Func<string, object> fallback, string connectionString)
    {
        // 自訂按鈕欄位：執行SQL、開啟畫面用那一列的值，SQL 執行成功後重新載入清單
        GridCells.HookCustomColumns(Grid, (c, row) =>
        {
            bool? done = ButtonRunner.RunRowButton(c.Button, row, fallback, sourcePath, owner, connectionString);
            if (done == null) MessageBox.Show("明細清單上的按鈕只能用「執行SQL」或「開啟畫面」。", "提醒");
            else if (done == true) Reload(lastValueOf);
        }, connectionString);
        var button = ButtonRunner.ListOpenButton(list.Title ?? list.TableName, list.OpenForm, list.OpenParams);
        if (button == null) return;
        Grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0 || Grid.Rows[e.RowIndex].DataBoundItem is not DataRowView view) return;
            var row = view.Row;
            ButtonRunner.OpenForm(button, sourcePath, owner, name => row.Table.Columns.Contains(name) ? row[name] : fallback(name),
                                  connectionString: connectionString);
        };
    }

    // ================= 新增、修改單頭時直接輸入明細 =================

    /// <summary>輸入中（編輯視窗新增、修改）：清單可以新增、修改、刪除明細，按確定時跟單頭一起存</summary>
    private bool editing;

    /// <summary>編輯視窗上的欄位值（新增明細的預設值、執行後動作的 @名稱）</summary>
    public Func<string, object> MasterValueOf { get; set; }

    /// <summary>可以直接輸入明細：資料表清單（不是 SQL 清單）、沒有上層清單、有設關聯欄位</summary>
    public bool Editable => !list.IsSqlList && parent == null && list.Links.Count > 0;

    private DataTable EditTable => Grid.DataSource as DataTable;

    private bool IsLinkColumn(string column) => list.Links.Any(l => l.Detail.Equals(column, StringComparison.OrdinalIgnoreCase));

    /// <summary>進入 / 離開輸入：輸入中清單可以新增、修改、刪除（只顯示、自動編號、JOIN、公式、關聯欄位不能輸入）</summary>
    public void SetEditing(bool on)
    {
        if (!Editable) return;
        editing = on;
        if (on && EditTable == null) Load(lastValueOf ?? (_ => null));   // 新增中關聯欄位還沒有值：載入空的清單（只要欄位）
        Grid.ReadOnly = !on;
        Grid.AllowUserToAddRows = on && EditTable != null;
        Grid.AllowUserToDeleteRows = on;
        Grid.RowHeadersVisible = on;                             // 輸入中點列首選取整列，按 Delete 刪除
        Grid.SelectionMode = on ? DataGridViewSelectionMode.RowHeaderSelect : DataGridViewSelectionMode.FullRowSelect;
        foreach (DataGridViewColumn col in Grid.Columns)
            if (col.Tag is GridColumnDefinition c && (c.NoEntry || c.IsIdentity || c.IsJoin || c.IsCustom || c.HasFormula || IsLinkColumn(c.Column)))
                col.ReadOnly = true;
    }

    /// <summary>結束正在輸入的格子，拿掉最後那一列什麼都沒填的新增列；回傳有異動的列</summary>
    private List<DataRow> PendingRows()
    {
        if (!editing || EditTable is not { } table) return new List<DataRow>();
        Grid.EndEdit();
        if (Grid.BindingContext?[table] is CurrencyManager cm) cm.EndCurrentEdit();
        foreach (var r in table.Rows.Cast<DataRow>().Where(r => r.RowState == DataRowState.Added).ToList())
            if (list.Columns.Where(c => !c.IsIdentity && !IsLinkColumn(c.Column) && table.Columns.Contains(c.Column))
                            .All(c => r[c.Column] == DBNull.Value || r[c.Column] is string s && s.Trim() == ""))
                table.Rows.Remove(r);
        return table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Unchanged).ToList();
    }

    /// <summary>存檔前的檢查：沒有主鍵不能改、刪；顯示在清單上、不允許 NULL 的欄位要填；有問題時提醒並回傳 false</summary>
    public bool ValidatePending()
    {
        var changed = PendingRows();
        if (changed.Count == 0) return true;
        string name = $"明細「{list.Title ?? list.TableName}」";
        if (!list.Columns.Any(c => c.IsKey && !c.IsJoin) && changed.Any(r => r.RowState is DataRowState.Modified or DataRowState.Deleted))
        {
            MessageBox.Show($"{name}沒有設定主鍵，只能新增明細，不能修改或刪除。請按「取消」放棄修改的部分。", "提醒");
            return false;
        }
        foreach (var r in changed.Where(r => r.RowState != DataRowState.Deleted))
        {
            var empty = list.Columns.FirstOrDefault(c => c.Visible && !c.IsNullable && !c.IsIdentity && !c.NoEntry && !c.IsJoin && !c.ScreenOnly
                                                      && !IsLinkColumn(c.Column) && r.Table.Columns.Contains(c.Column) && r[c.Column] == DBNull.Value);
            if (empty != null)
            {
                MessageBox.Show($"{name}第 {r.Table.Rows.IndexOf(r) + 1} 列請輸入「{empty.Header ?? empty.Column}」。", "提醒");
                Grid.Focus();
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 單頭存好後存明細：新增的列帶入關聯欄位（masterValueOf 取單頭的值）後 INSERT、改過的 UPDATE、刪掉的 DELETE，同一個交易，
    /// 失敗就全部不變（清單上的輸入還在）。回傳異動筆數；失敗時丟例外
    /// </summary>
    public int SavePending(Func<string, object> masterValueOf)
    {
        var changed = PendingRows();
        if (changed.Count == 0) return 0;
        var table = EditTable;
        foreach (var r in changed.Where(r => r.RowState == DataRowState.Added))
            foreach (var (master, detail) in list.Links)
                if (table.Columns.Contains(detail)) r[detail] = masterValueOf(master) ?? DBNull.Value;
        var d = DbDialect.Of(connectionString);
        var keys = list.Columns.Where(c => c.IsKey && !c.IsJoin).ToList();
        // 寫得回資料表的欄位：本身資料表的欄位（JOIN、畫面欄位、自訂欄位不寫）
        var own = list.Columns.Where(c => !c.IsJoin && !c.ScreenOnly && !c.IsCustom && !c.IsIdentity && table.Columns.Contains(c.Column)).ToList();
        string tableName = list.FullTableName(d);
        using var conn = d.Open(connectionString);
        using var tran = conn.BeginTransaction();
        foreach (var r in changed)
        {
            using var cmd = d.CreateCommand(conn);
            cmd.Transaction = tran;
            switch (r.RowState)
            {
                case DataRowState.Added:
                {
                    // 空值不寫，讓資料庫預設值生效；關聯欄位一定要寫
                    var cols = own.Where(c => IsLinkColumn(c.Column) || (!c.NoEntry && r[c.Column] != DBNull.Value)).ToList();
                    for (int i = 0; i < cols.Count; i++) d.AddParam(cmd, $"p{i}", r[cols[i].Column]);
                    cmd.CommandText = $"INSERT INTO {tableName} ({string.Join(", ", cols.Select(c => d.Quote(c.Column)))}) "
                                    + $"VALUES ({string.Join(", ", cols.Select((c, i) => d.Param($"p{i}")))})";
                    break;
                }
                case DataRowState.Modified:
                {
                    var cols = own.Where(c => !c.NoEntry && !c.IsKey && !IsLinkColumn(c.Column)).ToList();
                    if (cols.Count == 0) continue;
                    for (int i = 0; i < cols.Count; i++) d.AddParam(cmd, $"p{i}", r[cols[i].Column]);
                    cmd.CommandText = $"UPDATE {tableName} SET {string.Join(", ", cols.Select((c, i) => $"{d.Quote(c.Column)} = {d.Param($"p{i}")}"))} "
                                    + "WHERE " + OriginalKeyCondition(d, cmd, r, keys);
                    break;
                }
                case DataRowState.Deleted:
                    cmd.CommandText = $"DELETE FROM {tableName} WHERE " + OriginalKeyCondition(d, cmd, r, keys);
                    break;
            }
            cmd.ExecuteNonQuery();
        }
        tran.Commit();
        return changed.Count;
    }

    private static string OriginalKeyCondition(DbDialect d, DbCommand cmd, DataRow row, List<GridColumnDefinition> keys)
    {
        var conditions = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            conditions.Add($"{d.Quote(keys[i].Column)} = {d.Param($"k{i}")}");
            d.AddParam(cmd, $"k{i}", row[keys[i].Column, DataRowVersion.Original]);
        }
        return string.Join(" AND ", conditions);
    }

    /// <summary>可以輸入的清單資料：公式欄位跟著重算、新增的列先帶入預設值</summary>
    private void PrepareEditTable(DataTable table)
    {
        GridCells.HookFormulas(table, list.Columns);
        table.TableNewRow += (s, e) =>
        {
            foreach (var c in list.Columns.Where(c => !string.IsNullOrWhiteSpace(c.EditDefaultValue) && e.Row.Table.Columns.Contains(c.Column)))
            {
                try
                {
                    e.Row[c.Column] = DefaultValue.Resolve(c.EditDefaultValue, connectionString, name =>
                        e.Row.Table.Columns.Contains(name) && e.Row[name] != DBNull.Value ? e.Row[name] : MasterValueOf?.Invoke(name)) ?? DBNull.Value;
                }
                catch (Exception) { }                           // 型別對不上、SQL 錯誤就不帶
            }
        };
    }

    /// <summary>清單的資料有變動（重新載入、輸入、新增、刪除）</summary>
    public event Action<EditListView> Changed;

    /// <summary>清單上的資料有這個欄位（彙總函數找明細欄位用）</summary>
    public bool HasColumn(string column) => Grid.DataSource is DataTable t && t.Columns.Contains(column)
                                            || list.Columns.Any(c => c.Column.Equals(column, StringComparison.OrdinalIgnoreCase));

    /// <summary>彙總一欄明細（刪掉的列不算；清單沒有資料 = 沒有任何值）</summary>
    public decimal Aggregate(string function, string column)
    {
        var rows = Grid.DataSource is DataTable t && t.Columns.Contains(column)
            ? t.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted).Select(r => r[column])
            : Enumerable.Empty<object>();
        return Formula.Aggregate(function, rows);
    }

    /// <summary>上層清單選取的那一列（第一個選取的）；沒有選取 = null</summary>
    private DataRow ParentRow() => parent?.SelectedRows().FirstOrDefault();

    private void ReloadChildren(bool force)
    {
        if (children.Count == 0) return;
        var row = SelectedRows().FirstOrDefault();
        if (!force && row == lastParentRow) return;
        lastParentRow = row;
        foreach (var child in children) child.Reload(null);
    }

    /// <summary>
    /// 重新載入：valueOf 回傳編輯視窗目前這筆的欄位值；關聯欄位的值是空的（例如新增中）就顯示空清單。
    /// 有上層清單時不用 valueOf，改取上層清單選取那一列的值（沒選取就是空清單）。下層清單跟著重新載入。
    /// 查詢失敗時顯示訊息，不影響編輯視窗其他功能
    /// </summary>
    public void Reload(Func<string, object> valueOf)
    {
        lastValueOf = valueOf;
        try
        {
            Load(valueOf);
        }
        finally
        {
            ReloadChildren(force: true);
        }
    }

    private void Load(Func<string, object> valueOf)
    {
        if (parent != null)
        {
            var row = ParentRow();
            valueOf = col => row != null && row.Table.Columns.Contains(col) ? row[col] : null;
            if (row == null)
            {
                Grid.DataSource = null;                          // 上層清單還沒點選：沒有明細可列
                return;
            }
        }
        var links = list.Links;
        var table = new DataTable();
        try
        {
            if (list.IsSqlList)
            {
                // SQL 清單：@名稱 取編輯視窗（或上層清單）的值，沒有的當 NULL
                Grid.DataSource = SqlList.Load(connectionString, SqlList.Compose(connectionString, list.Sql, list.Columns, list.Joins), valueOf);
                return;
            }
            var d = DbDialect.Of(connectionString);
            using var conn = d.Open(connectionString);
            using var cmd = d.CreateCommand(conn);
            var visible = list.Columns.Where(c => c.Visible).ToList();
            if (visible.Count == 0)
            {
                Grid.DataSource = null;
                return;
            }
            // 主鍵就算沒顯示也要查出來（列印選取的那筆時用主鍵讀資料）
            visible.AddRange(list.Columns.Where(c => c.IsKey && !c.IsJoin && !visible.Contains(c)));
            // 下層清單關聯要用的欄位（SQL 清單是 @參數）就算沒顯示也要查出來
            var childLinks = children.SelectMany(v => v.list.IsSqlList ? ButtonRunner.ParameterNames(v.list.Sql) : v.list.Links.Select(l => l.Master))
                                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
            visible.AddRange(list.Columns.Where(c => childLinks.Contains(c.Column) && !visible.Contains(c)));
            if (Editable) visible.AddRange(list.Columns.Where(c => IsLinkColumn(c.Column) && !c.IsJoin && !visible.Contains(c)));   // 新增明細時要帶入關聯欄位
            // 本身資料表固定用別名 T0（有 JOIN 時，欄位名稱才不會跟關聯資料表衝突）
            string select = GridSql.Select(d, list.FullTableName(d), visible, list.Joins, links.Count == 0 ? MaxRowsWithoutLink : null);
            if (links.Count == 0)
            {
                cmd.CommandText = select + d.Limit(MaxRowsWithoutLink);
            }
            else
            {
                var conditions = new List<string>();
                for (int i = 0; i < links.Count; i++)
                {
                    object value = valueOf(links[i].Master);
                    if (value == null || value == DBNull.Value || (value is string s && s.Length == 0))
                    {
                        if (editing)
                        {
                            conditions.Add("1 = 0");             // 新增中：只要欄位，讓使用者可以輸入明細
                            continue;
                        }
                        Grid.DataSource = null;                  // 關聯欄位還沒有值：沒有明細可列
                        return;
                    }
                    d.AddParam(cmd, $"p{i}", value);
                    conditions.Add($"{GridSql.Base(d, links[i].Detail)} = {d.Param($"p{i}")}");
                }
                cmd.CommandText = $"{select} WHERE {string.Join(" AND ", conditions)}";
            }
            table = d.Fill(cmd);
            if (Editable) PrepareEditTable(table);
            Grid.DataSource = table;
        }
        catch (Exception ex)
        {
            Grid.DataSource = null;
            MessageBox.Show($"讀取明細清單「{list.Title ?? list.TableName}」失敗：{ex.Message}", "提醒");
        }
    }

    /// <summary>清單上使用者選取的資料列（由上到下）</summary>
    public List<DataRow> SelectedRows() =>
        Grid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.DataBoundItem is DataRowView).OrderBy(r => r.Index)
            .Select(r => ((DataRowView)r.DataBoundItem).Row).ToList();

    /// <summary>清單所在範圍（算視窗大小用）</summary>
    public static Size Extent(IEnumerable<EditListDefinition> lists)
    {
        int w = 0, h = 0;
        foreach (var l in lists)
        {
            w = Math.Max(w, l.X + Math.Max(l.Width, EditListBox.MinWidth) + 20);
            h = Math.Max(h, l.Y + Math.Max(l.Height, EditListBox.MinHeight) + 20);
        }
        return new Size(w, h);
    }
}
