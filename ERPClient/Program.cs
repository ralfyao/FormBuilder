using FormBuilder;

namespace ERPClient;

internal static class Program
{
    public const string ProductName = "ERP 系統";

    /// <summary>
    /// ERP 系統（使用者端）：檢查更新 → 第一次使用時設定系統資料庫 → 登入 → ERP 外框（選單 + 分頁）。
    /// 按「登出」回到登入畫面。系統資料庫的設定跟 ERP Studio 共用（%ProgramData%\ERPStudio\config.json）
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var config = SysConfig.Load();
        if (Updater.CheckAndUpdate(config)) return;          // 有新版本：關閉程式，由更新程序換檔後重新啟動
        // 使用者端不提供「系統設定」（帳號、權限在 ERP Studio 管），但可以設定系統資料庫連線：
        // 登入畫面的「修改連線…」（要先通過管理者驗證）；第一次沒有連線時，輸入連線字串或指定連線設定檔
        if (config.GetConnection() == null && !SetupConnection(config)) return;
        while (true)
        {
            using (var login = new LoginForm(config, ProductName, allowSetup: false, allowConnection: true))
                if (login.ShowDialog() != DialogResult.OK) return;
            var shell = new ErpShellForm(config);
            Application.Run(shell);
            SysContext.SignOut();
            if (!shell.LoggedOut) return;
        }
    }

    /// <summary>
    /// 還沒有系統連線：輸入連線字串（存在這台電腦，加密）或指定連線設定檔；使用者取消回傳 false（結束程式）
    /// </summary>
    private static bool SetupConnection(SysConfig config)
    {
        string problem = string.IsNullOrWhiteSpace(config.ConnectionFile) ? null : config.ApplyConnectionFile();
        if (config.GetConnection() != null) return true;
        var answer = MessageBox.Show((problem ?? "這台電腦還沒有設定系統連線。") + Environment.NewLine + Environment.NewLine
                                     + "是 = 輸入系統資料庫的連線（伺服器、資料庫、帳號密碼）" + Environment.NewLine
                                     + "否 = 指定連線設定檔（請向系統管理者詢問檔案位置，例如 \\\\server\\ERPStudio\\config.json）" + Environment.NewLine
                                     + "取消 = 結束",
                                     ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.No) return ChooseConnectionFile(config);
        while (true)
        {
            using var dlg = new ConnectionStringDialog(null);           // 直接寫連線字串，或按「連線精靈」組
            if (dlg.ShowDialog() != DialogResult.OK) return false;
            try
            {
                if (SysDb.UserCount(dlg.ConnectionString) == 0)
                {
                    MessageBox.Show("這個資料庫還沒有 ERP 帳號，請系統管理者先用 ERP Studio 設定系統資料庫。", ProductName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("這個連線不能用（連不上，或不是 ERP 的系統資料庫）：" + ex.Message, ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                continue;
            }
            config.SetConnection(dlg.ConnectionString);
            try
            {
                config.Save();                           // 存在 %ProgramData%\ERPStudio\config.json（加密，只有這台電腦解得開）
            }
            catch (Exception ex)
            {
                MessageBox.Show("連線可以用，但這台電腦的設定存不起來，下次啟動要再設定一次：" + ex.Message, ProductName,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return true;
        }
    }

    /// <summary>
    /// 還沒有系統連線：請使用者選連線設定檔（含明碼 SystemConnection 的 config.json），讀得到連線就記住路徑；
    /// 使用者取消回傳 false（結束程式）
    /// </summary>
    private static bool ChooseConnectionFile(SysConfig config)
    {
        // 之前指定過、但現在讀不到（例如網路磁碟沒連上）：先說明原因
        string problem = string.IsNullOrWhiteSpace(config.ConnectionFile) ? null : config.ApplyConnectionFile();
        string intro = problem ?? "這台電腦還沒有設定系統連線。";
        while (true)
        {
            if (MessageBox.Show(intro + Environment.NewLine + Environment.NewLine
                                + "要指定連線設定檔嗎？（請向系統管理者詢問檔案位置，例如 \\\\server\\ERPStudio\\config.json）",
                                ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                return false;
            using var dialog = new OpenFileDialog
            {
                Title = "選擇連線設定檔",
                Filter = "連線設定檔 (*.json)|*.json|所有檔案 (*.*)|*.*",
                FileName = "config.json",
            };
            if (!string.IsNullOrWhiteSpace(config.ConnectionFile))
            {
                try { dialog.InitialDirectory = Path.GetDirectoryName(config.ConnectionFile); } catch (Exception) { }
            }
            if (dialog.ShowDialog() != DialogResult.OK) return false;
            config.ConnectionFile = dialog.FileName;
            string error = config.ApplyConnectionFile();
            if (error == null)
            {
                try
                {
                    config.Save();                       // 記住連線設定檔的路徑（連線本身不存進本機）
                }
                catch (Exception ex)
                {
                    MessageBox.Show("連線設定檔可以用，但這台電腦的設定存不起來，下次啟動要再選一次：" + ex.Message, ProductName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return true;
            }
            intro = error;
        }
    }
}
