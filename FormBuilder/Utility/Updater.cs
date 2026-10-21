using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace FormBuilder;

/// <summary>
/// 自動更新：啟動時看「更新來源資料夾」的 version.json，版本比自己新就問使用者要不要更新。
/// 更新方式：程式關閉後，由一個暫時的批次檔把來源資料夾的 files 複製到安裝資料夾，再重新啟動。
/// 來源資料夾的結構（由 installer\build-release.ps1 產生）：
///   version.json   { "Version": "1.0.1", "Notes": "更新說明" }
///   files\         新版的全部檔案
/// </summary>
public static class Updater
{
    public static Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);

    private sealed class VersionInfo
    {
        public string Version { get; set; }
        public string Notes { get; set; }
        /// <summary>這是哪個程式的新版（FormBuilder = ERP Studio、ERPClient = ERP 系統）；跟自己不同就不更新</summary>
        public string App { get; set; }
    }

    /// <summary>自己是哪個程式（執行檔名稱）</summary>
    private static string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "FormBuilder";

    /// <summary>回傳 true = 要更新（程式應該馬上結束）</summary>
    public static bool CheckAndUpdate(SysConfig config)
    {
        string source = config.UpdateSource;
        if (string.IsNullOrWhiteSpace(source)) return false;
        try
        {
            // ERP Studio 和 ERP 系統共用同一個更新來源：各自放在「更新來源\程式名稱」子資料夾（舊的放法是直接放在更新來源）
            string folder = Path.Combine(source, AppName);
            if (!Directory.Exists(folder)) folder = source;
            string versionFile = Path.Combine(folder, "version.json");
            string files = Path.Combine(folder, "files");
            if (!File.Exists(versionFile) || !Directory.Exists(files)) return false;     // 更新來源連不到（例如離線）就照常啟動
            var info = JsonSerializer.Deserialize<VersionInfo>(File.ReadAllText(versionFile));
            if (info == null || !Version.TryParse(info.Version, out var latest) || latest <= CurrentVersion) return false;
            if (!string.IsNullOrEmpty(info.App) && !info.App.Equals(AppName, StringComparison.OrdinalIgnoreCase)) return false;   // 別的程式的新版

            string notes = string.IsNullOrWhiteSpace(info.Notes) ? "" : Environment.NewLine + Environment.NewLine + info.Notes;
            if (MessageBox.Show($"有新版本 {latest}（目前 {CurrentVersion}），要現在更新嗎？{notes}", "ERP Studio 更新",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return false;
            StartUpdate(files);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("檢查更新失敗，先用目前的版本：" + ex.Message, "ERP Studio 更新");
            return false;
        }
    }

    /// <summary>寫一個暫時的批次檔：等這個程式結束 → 複製新版檔案 → 重新啟動</summary>
    private static void StartUpdate(string sourceFiles)
    {
        string installDir = AppContext.BaseDirectory.TrimEnd('\\');
        string exe = Environment.ProcessPath ?? Path.Combine(installDir, "FormBuilder.exe");
        int pid = Environment.ProcessId;
        string script = Path.Combine(Path.GetTempPath(), $"ERPStudio-update-{pid}.cmd");
        File.WriteAllText(script, $"""
            @echo off
            chcp 65001 > nul
            title ERP Studio 更新中…
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" > nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak > nul
                goto wait
            )
            robocopy "{sourceFiles}" "{installDir}" /E /R:3 /W:2 /NFL /NDL /NJH /NJS > nul
            start "" "{exe}"
            del "%~f0"
            """, new System.Text.UTF8Encoding(false));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { WindowStyle = ProcessWindowStyle.Minimized, UseShellExecute = true });
    }
}
