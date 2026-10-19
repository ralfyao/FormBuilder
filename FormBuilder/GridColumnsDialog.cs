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

    public GridColumnsDialog(GridDefinition gridDefinition, List<string> masterColumns)
    {
        InitializeComponent();
        grid = gridDefinition;
        Text = $"單身欄位設定：{grid.TableSchema}.{grid.TableName}";

        // ---- 關聯欄位：單頭欄位 = 單身欄位 ----
        linkTable.Columns.Add("Master", typeof(string));
        linkTable.Columns.Add("Detail", typeof(string));
        foreach (var (master, detail) in grid.Links) linkTable.Rows.Add(master, detail);
        colMaster.Items.AddRange(masterColumns.Union(grid.Links.Select(l => l.Master)).Distinct().OrderBy(n => n).ToArray());
        colDetail.Items.AddRange(grid.Columns.Select(c => c.Column).Union(grid.Links.Select(l => l.Detail)).Distinct().ToArray());
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
        foreach (var c in grid.Columns)
        {
            string type = c.MaxLength > 0 ? $"{c.DataType}({c.MaxLength})" : c.MaxLength == -1 ? $"{c.DataType}(max)" : c.DataType;
            columnTable.Rows.Add(c.Visible, c.Column, type, c.Header, c.Width, c.IsKey, c.NoEntry);
        }
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => { MessageBox.Show("寬度請輸入數字。", "提醒"); e.Cancel = true; };

        btnAll.Click += (s, e) => SetAllVisible(true);
        btnNone.Click += (s, e) => SetAllVisible(false);
        btnLayout.Click += (s, e) =>
        {
            // 版面設計器有自己的確定 / 取消，確定後版面就存在 Grid 上
            using var layout = new EditLayoutDialog(grid);
            layout.ShowDialog(this);
        };
        btnOk.Click += (s, e) => Apply();
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
            var c = grid.Columns.First(x => x.Column == (string)row["Column"]);
            c.Visible = (bool)row["Visible"];
            c.Header = string.IsNullOrWhiteSpace(Convert.ToString(row["Header"])) ? c.Column : (string)row["Header"];
            c.Width = Math.Clamp(row["Width"] is int w ? w : 100, 30, 800);
            c.IsKey = (bool)row["IsKey"];
            c.NoEntry = (bool)row["NoEntry"];
        }
        DialogResult = DialogResult.OK;
    }
}
