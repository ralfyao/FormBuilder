namespace FormBuilder;

/// <summary>
/// 系統設定精靈（第一次使用、或登入畫面按「系統設定」）：選系統資料庫、建立資料表、
/// 系統資料庫還沒有帳號時建立第一個管理者帳號、設定自動更新的來源資料夾
/// </summary>
public partial class SysSetupForm : Form
{
    private readonly SysConfig config;
    private string connection;

    public SysSetupForm(SysConfig config)
    {
        InitializeComponent();
        this.config = config;
        connection = config.GetConnection();
        txtUpdate.Text = config.UpdateSource ?? "";
        btnPickConn.Click += (s, e) => PickConnection();
        btnOk.Click += (s, e) => Finish();
        ShowConnection();
    }

    private void PickConnection()
    {
        using var dlg = new ConnectionDialog(connection, DbKind.SqlServer);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (dlg.DatabaseType != DbKind.SqlServer)
        {
            MessageBox.Show("系統資料庫目前只支援 SQL Server（各畫面的業務資料庫可以是 Oracle、MySQL）。", Text);
            return;
        }
        connection = dlg.ConnectionString;
        ShowConnection();
    }

    /// <summary>顯示連線（密碼遮起來）；系統資料庫已經有帳號時不用再建管理者</summary>
    private void ShowConnection()
    {
        txtConn.Text = connection == null ? "" : ConnectionDialog.Describe(connection);    // 不顯示資料庫路徑；要改按「設定連線」
        grpAdmin.Enabled = true;
        if (connection == null) return;
        try
        {
            SysDb.EnsureSchema(connection);
            if (SysDb.UserCount(connection) > 0)
            {
                grpAdmin.Enabled = false;
                grpAdmin.Text = "這個系統資料庫已經有帳號，完成設定後用既有帳號登入";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("建立系統資料表失敗（請確認帳號有建立資料表的權限）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Finish()
    {
        if (connection == null)
        {
            MessageBox.Show("請先設定系統資料庫連線。", Text);
            return;
        }
        try
        {
            SysDb.EnsureSchema(connection);
            if (SysDb.UserCount(connection) == 0)
            {
                string id = txtAdminId.Text.Trim(), name = txtAdminName.Text.Trim();
                if (id == "" || name == "")
                {
                    MessageBox.Show("請輸入管理者帳號與姓名。", Text);
                    return;
                }
                if (PasswordHasher.CheckStrength(txtPwd.Text) is { } weak)
                {
                    MessageBox.Show(weak, Text);
                    return;
                }
                if (txtPwd.Text != txtPwd2.Text)
                {
                    MessageBox.Show("兩次輸入的密碼不一樣。", Text);
                    return;
                }
                SysDb.CreateUser(connection, id, name, txtPwd.Text, isAdmin: true, mustChange: false);
                SysDb.WriteAudit(connection, id, null, "SYS_User", "新增帳號", "UserId=" + id, "系統設定精靈建立的第一個管理者");
                config.LastUser = id;
            }
            config.SetConnection(connection);
            config.UpdateSource = string.IsNullOrWhiteSpace(txtUpdate.Text) ? null : txtUpdate.Text.Trim();
            config.Save();
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("設定失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
