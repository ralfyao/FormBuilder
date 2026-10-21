using System.Data;
using System.Text;

namespace FormBuilder;

/// <summary>稽核紀錄查詢（管理者用）：依日期、使用者、畫面、動作篩選，點一列在下方看完整內容，可以匯出 CSV</summary>
public partial class AuditLogForm : Form
{
    private DataTable table;

    public AuditLogForm()
    {
        InitializeComponent();
        dtpFrom.Value = DateTime.Today.AddDays(-7);
        dtpTo.Value = DateTime.Today;
        cboAction.SelectedIndex = 0;
        btnQuery.Click += (s, e) => RunQuery();
        btnExport.Click += (s, e) => Export();
        dgvLog.SelectionChanged += (s, e) =>
            txtDetail.Text = dgvLog.CurrentRow?.DataBoundItem is DataRowView v
                ? $"{v["時間"]:yyyy/MM/dd HH:mm:ss}　{v["使用者"]}　{v["動作"]}　{v["畫面"]}　{v["資料表"]}{Environment.NewLine}主鍵：{v["主鍵"]}{Environment.NewLine}{v["內容"]}"
                : "";
        Shown += (s, e) => RunQuery();
    }

    private void RunQuery()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            string action = cboAction.SelectedIndex <= 0 ? null : (string)cboAction.SelectedItem;
            table = SysDb.QueryAudit(SysContext.Connection, dtpFrom.Value.Date, dtpTo.Value.Date.AddDays(1), txtUser.Text, txtForm.Text, action);
            dgvLog.DataSource = table;
            if (dgvLog.Columns["時間"] is { } time) { time.DefaultCellStyle.Format = "yyyy/MM/dd HH:mm:ss"; time.Width = 140; }
            if (dgvLog.Columns["內容"] is { } detail) detail.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            lblCount.Text = table.Rows.Count >= 5000 ? "超過 5000 筆，只顯示最新的 5000 筆，請縮小日期範圍" : $"{table.Rows.Count} 筆";
        }
        catch (Exception ex)
        {
            MessageBox.Show("查詢失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>匯出成 CSV（UTF-8 含 BOM，Excel 直接開中文不會亂碼）</summary>
    private void Export()
    {
        if (table == null || table.Rows.Count == 0) return;
        using var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"稽核紀錄_{DateTime.Today:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        static string Cell(object v) => "\"" + (v is DateTime d ? d.ToString("yyyy/MM/dd HH:mm:ss") : Convert.ToString(v)).Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Cell(c.ColumnName))));
        foreach (DataRow r in table.Rows) sb.AppendLine(string.Join(",", r.ItemArray.Select(Cell)));
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        MessageBox.Show($"已匯出 {table.Rows.Count} 筆。", Text);
    }
}
