using System.Data;

namespace FormBuilder;

/// <summary>
/// SQL 清單設定：標題、SELECT（可以用 @單頭欄位）、按「讀取欄位」從 SQL 結果帶出欄位，
/// 再勾選要顯示的欄位、改標題與寬度；也可以 JOIN 其他資料表帶出欄位（SQL 當子查詢，外層 LEFT JOIN）。按「確定」寫回 GridDefinition
/// </summary>
public partial class SqlListDialog : Form
{
    private readonly GridDefinition grid;
    private readonly string connectionString;
    private readonly DataTable columnTable = new();
    /// <summary>JOIN（按「確定」才寫回）與 JOIN 帶進來的欄位（欄位名稱 → 欄位）</summary>
    private readonly List<GridJoinDefinition> workJoins;
    private readonly Dictionary<string, GridColumnDefinition> joinColumns = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="masterColumns">單頭的欄位（提示可以用的 @參數）</param>
    /// <param name="allowLayout">主畫面的 SQL 清單：有「編輯視窗版面…」按鈕（編輯視窗裡的 SQL 清單沒有）</param>
    /// <param name="screenFont">畫面字型（編輯視窗版面設計器預覽用）</param>
    public SqlListDialog(GridDefinition grid, string connectionString, IEnumerable<string> masterColumns, bool allowLayout = false, Font screenFont = null)
    {
        InitializeComponent();
        this.grid = grid;
        this.connectionString = connectionString;
        txtTitle.Text = grid.Title;
        txtSql.Text = grid.Sql;
        var names = masterColumns.ToList();
        lblParams.Text = names.Count == 0 ? "可以用的 @參數：（還沒有單頭欄位）"
                                          : "可以用的 @參數：" + string.Join("、", names.Select(n => "@" + n));

        columnTable.Columns.Add("Visible", typeof(bool));
        columnTable.Columns.Add("Column", typeof(string));
        columnTable.Columns.Add("Header", typeof(string));
        columnTable.Columns.Add("Width", typeof(int));
        columnTable.Columns.Add("Source", typeof(string));
        workJoins = grid.Joins.Select(j => j.Clone()).ToList();
        foreach (var c in grid.Columns.Where(c => c.IsJoin && workJoins.Any(j => j.Alias == c.JoinAlias))) joinColumns[c.Column] = c;
        foreach (var c in grid.Columns.Where(c => !c.ScreenOnly && (!c.IsJoin || joinColumns.ContainsKey(c.Column))))   // 編輯視窗的畫面欄位不是 SQL 的欄位
            columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Source", HeaderText = "來源（JOIN）", ReadOnly = true, Width = 220 });
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => e.Cancel = true;
        // Delete：選的是 JOIN 帶進來的欄位時，移除那個 JOIN（連同它帶進來的欄位）
        dgvColumns.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Delete || dgvColumns.IsCurrentCellInEditMode) return;
            if (dgvColumns.CurrentRow?.DataBoundItem is DataRowView v && joinColumns.TryGetValue((string)v.Row["Column"], out var jc))
            {
                RemoveJoin(jc.JoinAlias);
                e.Handled = true;
            }
        };

        var btnJoin = new Button { Text = "JOIN 其他資料表…", AutoSize = true, Height = 30 };
        btnJoin.Click += (s, e) => AddJoin();
        bottomPanel.Controls.Add(btnJoin);
        btnReadColumns.Click += (s, e) => ReadColumns();
        btnOk.Click += (s, e) => Apply();
        if (allowLayout)
        {
            // 編輯視窗版面：先把目前的 SQL、欄位套用到 SQL 清單，再開版面設計器（版面設計器有自己的確定 / 取消）
            var btnLayout = new Button { Text = "編輯視窗版面…", AutoSize = true, Height = 30 };
            btnLayout.Click += (s, e) =>
            {
                if (!ApplyColumns()) return;
                using var layout = new EditLayoutDialog(grid, connectionString, screenFont: screenFont);
                layout.ShowDialog(this);
            };
            bottomPanel.Controls.Add(btnLayout);
        }
    }

    /// <summary>執行 SQL 取欄位結構：原本有的欄位保留標題、寬度、顯示設定，新的欄位加在後面</summary>
    private bool ReadColumns()
    {
        if (string.IsNullOrWhiteSpace(txtSql.Text))
        {
            MessageBox.Show("請先輸入 SQL（SELECT …）。", "提醒");
            return false;
        }
        List<GridColumnDefinition> columns;
        try
        {
            Cursor = Cursors.WaitCursor;
            columns = SqlList.ReadColumns(connectionString, txtSql.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("SQL 執行失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        // 先把原本的設定複製出來：Clear() 之後原本的 DataRow 就讀不到值了
        var old = columnTable.Rows.Cast<DataRow>()
            .GroupBy(r => (string)r["Column"], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (Visible: g.First()["Visible"], Header: g.First()["Header"], Width: g.First()["Width"]), StringComparer.OrdinalIgnoreCase);
        columnTable.Clear();
        foreach (var c in columns.Where(c => !joinColumns.ContainsKey(c.Column)))
        {
            if (old.TryGetValue(c.Column, out var o)) columnTable.Rows.Add(o.Visible, c.Column, o.Header, o.Width, "");
            else columnTable.Rows.Add(true, c.Column, c.Column, c.Width, "");
        }
        // JOIN 帶進來的欄位接在後面
        foreach (var c in joinColumns.Values)
        {
            if (old.TryGetValue(c.Column, out var o)) columnTable.Rows.Add(o.Visible, c.Column, o.Header, o.Width, SourceText(c));
            else columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        }
        readColumns = columns;
        return true;
    }

    /// <summary>最近一次讀到的欄位（資料型別用）</summary>
    private List<GridColumnDefinition> readColumns;

    private void Apply()
    {
        if (ApplyColumns()) DialogResult = DialogResult.OK;
    }

    /// <summary>標題、SQL、欄位設定寫回 SQL 清單；SQL 有問題時提醒並回傳 false</summary>
    private bool ApplyColumns()
    {
        dgvColumns.EndEdit();
        if (string.IsNullOrWhiteSpace(txtSql.Text))
        {
            MessageBox.Show("請輸入 SQL（SELECT …）。", "提醒");
            return false;
        }
        // SQL 改了沒按「讀取欄位」：先讀一次，確認 SQL 能執行
        if ((readColumns == null || txtSql.Text.Trim() != (grid.Sql ?? "").Replace("\r\n", "\n").Trim()) && !ReadColumns()) return false;
        if (columnTable.Rows.Count == 0)
        {
            MessageBox.Show("SQL 沒有傳回任何欄位。", "提醒");
            return false;
        }
        var types = (readColumns ?? new()).ToDictionary(c => c.Column, StringComparer.OrdinalIgnoreCase);
        grid.Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "SQL 清單" : txtTitle.Text.Trim();
        grid.Sql = txtSql.Text.Trim().Replace("\n", "\r\n");      // RichTextBox 的換行是 \n，存回 \r\n
        grid.Columns = columnTable.Rows.Cast<DataRow>().Select(r =>
        {
            string name = (string)r["Column"];
            var existing = grid.Columns.FirstOrDefault(c => c.Column.Equals(name, StringComparison.OrdinalIgnoreCase) && !c.IsJoin);
            var c = joinColumns.TryGetValue(name, out var joined) ? joined
                  : existing ?? new GridColumnDefinition { Column = name, IsNullable = true, NoEntry = true };
            if (!c.IsJoin && types.TryGetValue(name, out var t)) c.DataType = t.DataType;
            c.ScreenOnly = false;                          // SQL 現在有這個欄位了
            c.Visible = r["Visible"] is true;
            c.Header = string.IsNullOrWhiteSpace(Convert.ToString(r["Header"])) ? name : Convert.ToString(r["Header"]);
            c.Width = r["Width"] is int w && w > 0 ? Math.Clamp(w, 30, 600) : 100;
            return c;
        }).Concat(grid.Columns.Where(c => c.ScreenOnly && !columnTable.Rows.Cast<DataRow>().Any(r => ((string)r["Column"]).Equals(c.Column, StringComparison.OrdinalIgnoreCase))))
          .ToList();                                       // 編輯視窗的畫面欄位保留（SQL 有同名欄位時就用 SQL 的）
        grid.Joins = workJoins.Where(j => grid.Columns.Any(c => c.JoinAlias == j.Alias)).ToList();
        return true;
    }

    private string SourceText(GridColumnDefinition c)
    {
        if (!c.IsJoin) return "";
        var join = workJoins.FirstOrDefault(j => j.Alias == c.JoinAlias);
        return join == null ? c.JoinAlias : $"{c.JoinAlias}：{join.TableName}.{c.SourceColumn}";
    }

    /// <summary>JOIN 其他資料表：用 SQL 的欄位對應關聯資料表的欄位，帶進來的欄位加在最後面</summary>
    private void AddJoin()
    {
        dgvColumns.EndEdit();
        if (columnTable.Rows.Count == 0 && !ReadColumns()) return;
        int n = 1;
        while (workJoins.Any(j => j.Alias == $"J{n}")) n++;
        var types = (readColumns ?? grid.Columns).ToDictionary(c => c.Column, c => c.DataType, StringComparer.OrdinalIgnoreCase);
        var names = columnTable.Rows.Cast<DataRow>().Select(r => (string)r["Column"]).ToList();
        var localColumns = names.Where(name => !joinColumns.ContainsKey(name))
            .Select(name => new ColumnInfo { Name = name, DataType = types.TryGetValue(name, out var t) ? t : "nvarchar" }).ToList();
        using var dlg = new JoinDialog(connectionString, $"J{n}", localColumns, names);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        workJoins.Add(dlg.Join);
        foreach (var c in dlg.NewColumns)
        {
            joinColumns[c.Column] = c;
            columnTable.Rows.Add(true, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        }
    }

    /// <summary>移除一個 JOIN，連同它帶進來的欄位</summary>
    private void RemoveJoin(string alias)
    {
        var columns = joinColumns.Values.Where(c => c.JoinAlias == alias).ToList();
        if (MessageBox.Show($"確定要移除 JOIN {alias}（{columns.Count} 個欄位）嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        dgvColumns.EndEdit();
        foreach (var c in columns)
        {
            joinColumns.Remove(c.Column);
            columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => ((string)r["Column"]).Equals(c.Column, StringComparison.OrdinalIgnoreCase))?.Delete();
        }
        columnTable.AcceptChanges();
        workJoins.RemoveAll(j => j.Alias == alias);
    }
}
