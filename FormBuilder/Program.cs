namespace FormBuilder;

internal static class Program
{
    /// <summary>
    /// 不帶參數：ERP Studio（檢查更新 → 第一次使用時跑系統設定 → 登入 → 主選單）。
    /// FormBuilder.exe --designer：直接開後台設計視窗（開發用，不登入，畫面從檔案讀寫）。
    /// FormBuilder.exe --user [畫面資料夾]：舊的畫面清單（不登入，從資料夾讀畫面定義檔）。
    /// FormBuilder.exe 畫面.json：直接開啟該畫面（不登入）。
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0] == "--designer")
        {
            Application.Run(new DesignerForm());
            return;
        }
        if (args.Length > 0 && args[0] == "--user")
        {
            string folder = args.Length > 1 ? args[1] : FormDefinition.DefaultFolder;
            Application.Run(new LauncherForm(folder));
            return;
        }
        if (args.Length > 0)
        {
            Application.Run(new RuntimeForm(FormDefinition.Load(args[0])));
            return;
        }

        var config = SysConfig.Load();
        if (Updater.CheckAndUpdate(config)) return;          // 有新版本：關閉程式，由更新程序換檔後重新啟動
        if (config.GetConnection() == null)
        {
            using var setup = new SysSetupForm(config);
            if (setup.ShowDialog() != DialogResult.OK) return;
        }
        // 登入 → 主選單；按「登出」回到登入畫面
        while (true)
        {
            using (var login = new LoginForm(config))
                if (login.ShowDialog() != DialogResult.OK) return;
            var menu = new MainMenuForm(config);
            Application.Run(menu);
            SysContext.SignOut();
            if (!menu.LoggedOut) return;
        }
    }
}
