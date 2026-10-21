namespace FormBuilder;

/// <summary>登入：驗證帳號密碼（錯太多次鎖定）；管理者重設過密碼、或第一次登入時要先改密碼</summary>
public partial class LoginForm : Form
{
    private readonly SysConfig config;

    /// <param name="productName">登入畫面上顯示的產品名稱（後台工具是 ERP Studio，使用者端是 ERP 系統）</param>
    public LoginForm(SysConfig config, string productName = "ERP Studio")
    {
        InitializeComponent();
        this.config = config;
        lblTitle.Text = productName;
        Text = $"登入　{productName} {Updater.CurrentVersion}";
        txtUser.Text = config.LastUser ?? "";
        btnLogin.Click += (s, e) => DoLogin();
        lnkSetup.LinkClicked += (s, e) =>
        {
            using var setup = new SysSetupForm(config);
            if (setup.ShowDialog(this) == DialogResult.OK) ShowServer();
        };
        Shown += (s, e) => (txtUser.Text == "" ? txtUser : txtPwd).Focus();
        ShowServer();
    }

    private void ShowServer()
    {
        string cs = config.GetConnection();
        lblServer.Text = cs == null ? "尚未設定系統資料庫" : "系統資料庫：" + ConnectionDialog.Mask(cs);
    }

    private void DoLogin()
    {
        string cs = config.GetConnection();
        if (cs == null)
        {
            MessageBox.Show("尚未設定系統資料庫，請按「系統設定」。", Text);
            return;
        }
        string userId = txtUser.Text.Trim();
        if (userId == "" || txtPwd.Text == "")
        {
            MessageBox.Show("請輸入帳號與密碼。", Text);
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            var (user, mustChange, error) = SysDb.Login(cs, userId, txtPwd.Text);
            Cursor = Cursors.Default;
            if (user == null)
            {
                MessageBox.Show(error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPwd.SelectAll();
                txtPwd.Focus();
                return;
            }
            if (mustChange && !ChangePassword(this, cs, user.UserId, forced: true)) return;
            SysContext.SignIn(user, cs);
            config.LastUser = user.UserId;
            try { config.Save(); } catch (Exception) { }
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show("登入失敗（連不到系統資料庫？）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 變更密碼：forced = 登入時被要求改（剛輸入過舊密碼，不用再問）；平常從主選單改要先輸入舊密碼。
    /// 回傳 true = 改好了
    /// </summary>
    public static bool ChangePassword(IWin32Window owner, string cs, string userId, bool forced)
    {
        var fields = new List<PromptDialog.Field>();
        if (!forced) fields.Add(new PromptDialog.Field("目前密碼", Password: true));
        fields.Add(new PromptDialog.Field("新密碼", Password: true));
        fields.Add(new PromptDialog.Field("確認新密碼", Password: true));
        var values = PromptDialog.Ask(owner, forced ? "第一次登入，請設定新密碼" : "變更密碼", v =>
        {
            int i = 0;
            if (!forced && !SysDb.VerifyPassword(cs, userId, v[i++])) return "目前密碼不正確。";
            string pwd = v[i], confirm = v[i + 1];
            if (PasswordHasher.CheckStrength(pwd) is { } weak) return weak;
            return pwd == confirm ? null : "兩次輸入的新密碼不一樣。";
        }, fields.ToArray());
        if (values == null) return false;
        SysDb.SetPassword(cs, userId, values[^2], mustChange: false);
        SysDb.WriteAudit(cs, userId, null, "SYS_User", "變更密碼", "UserId=" + userId, null);
        MessageBox.Show("密碼已變更。", "變更密碼");
        return true;
    }
}
