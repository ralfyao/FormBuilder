using System.Data;

namespace FormBuilder;

/// <summary>
/// SQL 清單設定：標題、SELECT（可以用 @單頭欄位）、按「讀取欄位」從 SQL 結果帶出欄位，
/// 再勾選要顯示的欄位、改標題與寬度。按「確定」寫回 GridDefinition
/// </summary>
public partial class SqlListDialog : Form
{
    private readonly GridDefinition grid;
    private readonly string connectionString;
    private readonly DataTable columnTable = new();

    /// <param name="masterColumns">單頭的欄位（提示可以用的 @參數）</param>
    public SqlListDialog(GridDefinition grid, string connectionString, IEnumerable<string> masterColumns)
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
        foreach (var c in grid.Columns) columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width);
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => e.Cancel = true;

        btnReadColumns.Click += (s, e) => ReadColumns();
        btnOk.Click += (s, e) => Apply();
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
        foreach (var c in columns)
        {
            if (old.TryGetValue(c.Column, out var o)) columnTable.Rows.Add(o.Visible, c.Column, o.Header, o.Width);
            else columnTable.Rows.Add(true, c.Column, c.Column, c.Width);
        }
        readColumns = columns;
        return true;
    }

    /// <summary>最近一次讀到的欄位（資料型別用）</summary>
    private List<GridColumnDefinition> readColumns;

    private void Apply()
    {
        dgvColumns.EndEdit();
        if (string.IsNullOrWhiteSpace(txtSql.Text))
        {
            MessageBox.Show("請輸入 SQL（SELECT …）。", "提醒");
            return;
        }
        // SQL 改了沒按「讀取欄位」：先讀一次，確認 SQL 能執行
        if ((readColumns == null || txtSql.Text != grid.Sql) && !ReadColumns()) return;
        if (columnTable.Rows.Count == 0)
        {
            MessageBox.Show("SQL 沒有傳回任何欄位。", "提醒");
            return;
        }
        var types = (readColumns ?? new()).ToDictionary(c => c.Column, StringComparer.OrdinalIgnoreCase);
        grid.Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "SQL 清單" : txtTitle.Text.Trim();
        grid.Sql = txtSql.Text.Trim();
        grid.Columns = columnTable.Rows.Cast<DataRow>().Select(r =>
        {
            string name = (string)r["Column"];
            var existing = grid.Columns.FirstOrDefault(c => c.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            var c = existing ?? new GridColumnDefinition { Column = name, IsNullable = true, NoEntry = true };
            if (types.TryGetValue(name, out var t)) c.DataType = t.DataType;
            c.Visible = r["Visible"] is true;
            c.Header = string.IsNullOrWhiteSpace(Convert.ToString(r["Header"])) ? name : Convert.ToString(r["Header"]);
            c.Width = r["Width"] is int w && w > 0 ? Math.Clamp(w, 30, 600) : 100;
            return c;
        }).ToList();
        DialogResult = DialogResult.OK;
    }
}
