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
        // 使用者端不提供系統設定：系統資料庫由管理者用 ERP Studio 設定，或由 IT 在安裝時放 config.json；
        // 都沒有時讓使用者指定連線設定檔（例如 IT 放在網路磁碟的 config.json），記住路徑，之後每次啟動從那裡讀
        if (config.GetConnection() == null && !ChooseConnectionFile(config)) return;
        while (true)
        {
            using (var login = new LoginForm(config, ProductName, allowSetup: false))
                if (login.ShowDialog() != DialogResult.OK) return;
            var shell = new ErpShellForm(config);
            Application.Run(shell);
            SysContext.SignOut();
            if (!shell.LoggedOut) return;
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
