namespace FormBuilder;

internal static class Program
{
    /// <summary>
    /// 不帶參數：開啟後台設計視窗。
    /// FormBuilder.exe --user [畫面資料夾]：開啟使用者的畫面清單（未指定資料夾時用執行檔旁的 Forms）。
    /// FormBuilder.exe 畫面.json：直接開啟該畫面給使用者操作。
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0] == "--user")
        {
            string folder = args.Length > 1 ? args[1] : FormDefinition.DefaultFolder;
            Application.Run(new LauncherForm(folder));
        }
        else if (args.Length > 0)
        {
            Application.Run(new RuntimeForm(FormDefinition.Load(args[0])));
        }
        else
        {
            Application.Run(new DesignerForm());
        }
    }
}
