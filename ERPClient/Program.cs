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
        // 使用者端不提供系統設定：系統資料庫由管理者用 ERP Studio 設定，或由 IT 在安裝時放 config.json
        if (config.GetConnection() == null)
        {
            MessageBox.Show("這台電腦還沒有設定系統連線，請洽系統管理者。", ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
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
}
