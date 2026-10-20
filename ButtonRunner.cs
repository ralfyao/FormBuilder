using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace FormBuilder;

/// <summary>後台設計的按鈕：建立控制項、確認訊息、執行 SQL、開啟畫面（主畫面與編輯視窗共用）</summary>
public static class ButtonRunner
{
    public static Button CreateControl(ButtonDefinition b) =>
        new RoundButton { Text = b.Text, Bounds = new Rectangle(b.X, b.Y, Math.Max(b.Width, 40), Math.Max(b.Height, 20)), Look = ButtonLook.From(b) };

    /// <summary>有設定確認訊息就先詢問；回傳 true = 可以執行</summary>
    public static bool Confirm(ButtonDefinition b) =>
        string.IsNullOrWhiteSpace(b.Confirm) ||
        MessageBox.Show(b.Confirm, b.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>
    /// 執行按鈕設定的 SQL（同一個交易，失敗全部還原）。
    /// @欄位名稱 的值由 valueOf 提供（回傳 null = NULL；格式不對時丟 FormatException）
    /// </summary>
    public static bool RunSql(ButtonDefinition b, string connectionString, Func<string, object> valueOf)
    {
        if (string.IsNullOrWhiteSpace(b.Sql))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定 SQL。", "提醒");
            return false;
        }
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = b.Sql;
            foreach (string name in ParameterNames(b.Sql))
                cmd.Parameters.AddWithValue("@" + name, valueOf(name) ?? DBNull.Value);
            int rows = cmd.ExecuteNonQuery();
            tran.Commit();
            MessageBox.Show(rows >= 0 ? $"執行完成（影響 {rows} 筆）。" : "執行完成。", b.Text);
            return true;
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show("執行失敗（已全部還原）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>SQL 裡的 @參數名稱（不含 @；@@ROWCOUNT 這類系統函數不算）</summary>
    public static IEnumerable<string> ParameterNames(string sql) =>
        Regex.Matches(sql, @"(?<!@)@([\p{L}\p{N}_]+)").Select(m => m.Groups[1].Value)
             .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>開啟另一個畫面；只寫檔名時，找 sourcePath（目前畫面定義檔）同一個資料夾的檔案</summary>
    public static void OpenForm(ButtonDefinition b, string sourcePath, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(b.TargetForm))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定要開啟的畫面定義檔。", "提醒");
            return;
        }
        string path = b.TargetForm;
        if (!Path.IsPathRooted(path))
        {
            string folder = sourcePath != null ? Path.GetDirectoryName(sourcePath) : FormDefinition.DefaultFolder;
            path = Path.Combine(folder, path);
            if (!File.Exists(path) && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += ".json";
        }
        try
        {
            new RuntimeForm(FormDefinition.Load(path)).Show(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>按鈕所在範圍（算視窗大小用）</summary>
    public static Size Extent(IEnumerable<ButtonDefinition> buttons)
    {
        int w = 0, h = 0;
        foreach (var b in buttons)
        {
            w = Math.Max(w, b.X + Math.Max(b.Width, 40) + 20);
            h = Math.Max(h, b.Y + Math.Max(b.Height, 20) + 20);
        }
        return new Size(w, h);
    }
}
