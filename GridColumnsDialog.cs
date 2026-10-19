using System.Data;

namespace FormBuilder;

/// <summary>
/// 單身 Grid 設定：上方設定單頭與單身的關聯欄位，下方勾選要顯示的欄位、改標題與寬度。
/// 按「確定」才寫回 GridDefinition
/// </summary>
public class GridColumnsDialog : Form
{
    private readonly GridDefinition grid;
    private readonly DataTable columnTable = new();
    private readonly DataTable linkTable = new();
    private readonly DataGridView dgvColumns = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoGenerateColumns = false };
    private readonly DataGridView dgvLinks = new() { Dock = DockStyle.Fill, RowHeadersWidth = 24, AutoGenerateColumns = false };

    public GridColumnsDialog(GridDefinition gridDefinition, List<string> masterColumns)
    {
        grid = gridDefinition;
        Text = $"單身欄位設定：{grid.TableSchema}.{grid.TableName}";
        Font = new Font("Microsoft JhengHei UI", 9F);
        Size = new Size(760, 640);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        // ---- 關聯欄位：單頭欄位 = 單身欄位 ----
        linkTable.Columns.Add("Master", typeof(string));
        linkTable.Columns.Add("Detail", typeof(string));
        foreach (var (master, detail) in grid.Links) linkTable.Rows.Add(master, detail);
        var masterItems = masterColumns.Union(grid.Links.Select(l => l.Master)).Distinct().OrderBy(n => n).ToArray();
        var detailItems = grid.Columns.Select(c => c.Column).Union(grid.Links.Select(l => l.Detail)).Distinct().ToArray();
        var colMaster = new DataGridViewComboBoxColumn { HeaderText = "單頭欄位", DataPropertyName = "Master", Width = 250, FlatStyle = FlatStyle.Flat };
        colMaster.Items.AddRange(masterItems);
        var colDetail = new DataGridViewComboBoxColumn { HeaderText = "單身欄位", DataPropertyName = "Detail", Width = 250, FlatStyle = FlatStyle.Flat };
        colDetail.Items.AddRange(detailItems);
        dgvLinks.Columns.Add(colMaster);
        dgvLinks.Columns.Add(colDetail);
        dgvLinks.DataSource = linkTable;

        var linkGroup = new GroupBox { Text = "關聯欄位（單身只列出「單身欄位 = 目前單頭欄位值」的資料；新增明細時自動帶入）", Dock = DockStyle.Top, Height = 150, Padding = new Padding(6) };
        linkGroup.Controls.Add(dgvLinks);

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
        dgvColumns.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "顯示", DataPropertyName = "Visible", Width = 50 });
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "欄位", DataPropertyName = "Column", Width = 140, ReadOnly = true });
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "型別", DataPropertyName = "Type", Width = 110, ReadOnly = true });
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "標題", DataPropertyName = "Header", Width = 180 });
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "寬度", DataPropertyName = "Width", Width = 60 });
        dgvColumns.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "主鍵", DataPropertyName = "IsKey", Width = 50 });
        dgvColumns.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "只顯示", DataPropertyName = "NoEntry", Width = 60 });
        dgvColumns.DataSource = columnTable;
        dgvColumns.DataError += (s, e) => { MessageBox.Show("寬度請輸入數字。", "提醒"); e.Cancel = true; };

        var btnAll = new Button { Text = "全選", Width = 70, Dock = DockStyle.Left };
        btnAll.Click += (s, e) => SetAllVisible(true);
        var btnNone = new Button { Text = "全不選", Width = 70, Dock = DockStyle.Left };
        btnNone.Click += (s, e) => SetAllVisible(false);
        var btnLayout = new Button { Text = "編輯視窗版面…", Width = 120, Dock = DockStyle.Left };
        btnLayout.Click += (s, e) =>
        {
            // 版面設計器有自己的確定 / 取消，確定後版面就存在 Grid 上
            using var layout = new EditLayoutDialog(grid);
            layout.ShowDialog(this);
        };
        var btnOk = new Button { Text = "確定", Width = 90, Dock = DockStyle.Right };
        btnOk.Click += (s, e) => Apply();
        var btnCancel = new Button { Text = "取消", Width = 90, Dock = DockStyle.Right, DialogResult = DialogResult.Cancel };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(6) };
        bottom.Controls.Add(btnLayout);
        bottom.Controls.Add(btnNone);
        bottom.Controls.Add(btnAll);
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);
        CancelButton = btnCancel;

        var columnGroup = new GroupBox { Text = "Grid 欄位（勾選要顯示的欄位；主鍵用來修改、刪除明細）", Dock = DockStyle.Fill, Padding = new Padding(6) };
        columnGroup.Controls.Add(dgvColumns);

        Controls.Add(columnGroup);
        if (!grid.IsMasterList) Controls.Add(linkGroup);    // 單頭清單就是單頭自己，不需要關聯欄位
        Controls.Add(bottom);
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
