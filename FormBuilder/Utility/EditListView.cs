using System.Data;

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
        foreach (var c in list.Columns.Where(c => c.Visible))
        {
            var col = GridCells.CreateColumn(c, connectionString);
            col.SortMode = DataGridViewColumnSortMode.Automatic;
            Grid.Columns.Add(col);
        }
        Grid.DataError += (s, e) => e.ThrowException = false;     // 唯讀清單：格式不符的值顯示空白
        Host = RoundedGridHost.Wrap(Grid);
        // 載入後不要自動選第一列：使用者自己點選的才算「選取」（列印選取的資料時用）
        Grid.DataBindingComplete += (s, e) => Grid.ClearSelection();
        // 點選一列：下層清單列出這一列的明細
        Grid.SelectionChanged += (s, e) => ReloadChildren(force: false);
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
                Grid.DataSource = SqlList.Load(connectionString, list.Sql, valueOf);
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
                        Grid.DataSource = null;                  // 關聯欄位還沒有值：沒有明細可列
                        return;
                    }
                    d.AddParam(cmd, $"p{i}", value);
                    conditions.Add($"{GridSql.Base(d, links[i].Detail)} = {d.Param($"p{i}")}");
                }
                cmd.CommandText = $"{select} WHERE {string.Join(" AND ", conditions)}";
            }
            table = d.Fill(cmd);
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
