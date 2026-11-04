using Microsoft.Data.SqlClient;

namespace FormBuilder;

/// <summary>
/// 直接編輯系統資料庫的連線字串（SQL Server）：可以手動改，也可以按「連線精靈」用連線設定視窗組出來；
/// 按「確定」時先測試連得上、建好系統資料表才關閉
/// </summary>
public class ConnectionStringDialog : Form
{
    private readonly TextBox txtConn = new()
    {
        Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = false,
        Font = new Font("Consolas", 10f), Dock = DockStyle.Fill,
    };

    /// <summary>按「確定」後的連線字串（去掉前後空白）</summary>
    public string ConnectionString { get; private set; }

    public ConnectionStringDialog(string current)
    {
        Text = "修改系統資料庫連線字串";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 230);
        MinimumSize = new Size(420, 220);
        Padding = new Padding(12);

        var hint = new Label
        {
            Text = "SQL Server 連線字串，例如：Data Source=伺服器;Initial Catalog=資料庫;User ID=帳號;Password=密碼;TrustServerCertificate=True",
            Dock = DockStyle.Top, Height = 40, ForeColor = SystemColors.GrayText,
        };
        txtConn.Text = current ?? "";

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 10, 0, 0) };
        var btnWizard = new RoundButton { Text = "連線精靈…", Dock = DockStyle.Left, Width = 100 };
        var btnTest = new RoundButton { Text = "測試連線", Dock = DockStyle.Left, Width = 90 };
        var btnOk = new RoundButton { Text = "確定", Dock = DockStyle.Right, Width = 90 };
        var btnCancel = new RoundButton { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        // Dock 由後加的先排：左邊「連線精靈、測試連線」，右邊「確定、取消」
        bottom.Controls.Add(btnTest);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
        bottom.Controls.Add(btnWizard);
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);

        Controls.Add(txtConn);
        Controls.Add(hint);
        Controls.Add(bottom);
        CancelButton = btnCancel;

        btnWizard.Click += (s, e) =>
        {
            using var dlg = new ConnectionDialog(Valid(txtConn.Text.Trim()) ? txtConn.Text.Trim() : null, DbKind.SqlServer);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (dlg.DatabaseType != DbKind.SqlServer)
            {
                MessageBox.Show("系統資料庫目前只支援 SQL Server（各畫面的業務資料庫可以是 Oracle、MySQL）。", Text);
                return;
            }
            txtConn.Text = dlg.ConnectionString;
        };
        btnTest.Click += (s, e) =>
        {
            if (Test(out string error)) MessageBox.Show("連線成功。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else MessageBox.Show(error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        btnOk.Click += (s, e) => Accept();
    }

    /// <summary>連線字串的格式對（SqlConnectionStringBuilder 讀得懂）</summary>
    private static bool Valid(string cs)
    {
        if (cs == "") return false;
        try
        {
            _ = new SqlConnectionStringBuilder(cs);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>格式檢查 + 實際連線；失敗時 error 是要顯示的訊息</summary>
    private bool Test(out string error)
    {
        string cs = txtConn.Text.Trim();
        error = null;
        if (cs == "")
        {
            error = "請輸入連線字串。";
            return false;
        }
        try
        {
            _ = new SqlConnectionStringBuilder(cs);
        }
        catch (Exception ex)
        {
            error = "連線字串格式不正確：" + ex.Message;
            return false;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            using var conn = new SqlConnection(cs);
            conn.Open();
            return true;
        }
        catch (Exception ex)
        {
            error = "連不上資料庫：" + ex.Message;
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void Accept()
    {
        if (!Test(out string error))
        {
            MessageBox.Show(error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ConnectionString = txtConn.Text.Trim();
        DbDialect.Register(ConnectionString, DbKind.SqlServer);
        DialogResult = DialogResult.OK;
    }
}
