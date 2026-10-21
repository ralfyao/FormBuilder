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
    public DataGridView Grid { get; }
    /// <summary>加到畫面上的是這個圓角外框（Grid 在裡面）</summary>
    public Control Host { get; }

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
    }

    /// <summary>
    /// 重新載入：valueOf 回傳編輯視窗目前這筆的欄位值；關聯欄位的值是空的（例如新增中）就顯示空清單。
    /// 查詢失敗時顯示訊息，不影響編輯視窗其他功能
    /// </summary>
    public void Reload(Func<string, object> valueOf)
    {
        var links = list.Links;
        var table = new DataTable();
        try
        {
            var d = DbDialect.Of(connectionString);
            using var conn = d.Open(connectionString);
            using var cmd = d.CreateCommand(conn);
            var visible = list.Columns.Where(c => c.Visible).ToList();
            if (visible.Count == 0)
            {
                Grid.DataSource = null;
                return;
            }
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
