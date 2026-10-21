using System.Text.RegularExpressions;

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
    public static bool RunSql(ButtonDefinition b, string connectionString, Func<string, object> valueOf, string formKey = null) =>
        RunSql(b, connectionString, new[] { valueOf }, formKey);

    /// <summary>
    /// 對多筆資料各執行一次按鈕設定的 SQL（清單多選時），全部在同一個交易裡，任何一筆失敗就全部還原；
    /// rows 的每一個元素提供那一筆的 @欄位名稱 值
    /// </summary>
    public static bool RunSql(ButtonDefinition b, string connectionString, IReadOnlyList<Func<string, object>> rows, string formKey = null)
    {
        if (string.IsNullOrWhiteSpace(b.Sql))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定 SQL。", "提醒");
            return false;
        }
        try
        {
            var d = DbDialect.Of(connectionString);
            var (sql, parameters) = d.PrepareUserSql(b.Sql);      // @欄位名稱 轉成這個資料庫的參數寫法
            using var conn = d.Open(connectionString);
            using var tran = conn.BeginTransaction();
            using var cmd = d.CreateCommand(conn, sql);
            cmd.Transaction = tran;
            int affected = 0;
            foreach (var valueOf in rows)
            {
                cmd.Parameters.Clear();
                foreach (var (name, parameter) in parameters)
                    d.AddParam(cmd, parameter, valueOf(name));
                affected += Math.Max(0, cmd.ExecuteNonQuery());
            }
            tran.Commit();
            Audit.Write(formKey, null, "執行SQL", null, new { 按鈕 = b.Text, SQL = b.Sql, 筆數 = rows.Count, 影響 = affected });
            MessageBox.Show(rows.Count > 1 ? $"已對選取的 {rows.Count} 筆資料執行完成（影響 {affected} 筆）。" : $"執行完成（影響 {affected} 筆）。", b.Text);
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

    /// <summary>解析「名稱=值」的清單（逗號、分號或換行分隔）；左右都去掉空白，沒有等號的略過</summary>
    public static IEnumerable<(string Left, string Right)> ParsePairs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string part in text.Split(new[] { ',', '，', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) yield return (part[..eq].Trim(), part[(eq + 1)..].Trim());
        }
    }

    /// <summary>有設定「回傳對應」的開啟畫面按鈕 = 選取視窗：要在輸入中（新增、修改）才能按</summary>
    public static bool IsPicker(ButtonDefinition b) => b.Action == ButtonAction.開啟畫面 && ParsePairs(b.ReturnMap).Any();

    /// <summary>把選取視窗帶回的值填進本視窗的欄位；本視窗沒有的欄位列出來提醒</summary>
    public static void FillFields(IReadOnlyDictionary<string, object> values, IReadOnlyDictionary<FieldDefinition, Control> inputs)
    {
        var missing = new List<string>();
        foreach (var (column, value) in values)
        {
            var field = inputs.Keys.FirstOrDefault(f => f.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
            if (field == null) missing.Add(column);
            else InputHelper.SetValue(inputs[field], value);
        }
        if (missing.Count > 0)
            MessageBox.Show("「回傳對應」裡這些欄位不在這個視窗上，沒有帶入：" + string.Join("、", missing), "提醒");
    }

    /// <summary>
    /// 使用者端的 ERP 外框設定這個，「開啟畫面」按鈕（不是選取視窗時）就開成外框裡的分頁；沒設定 = 開新視窗
    /// </summary>
    public static Action<FormDefinition, IDictionary<string, object>> OpenInShell { get; set; }

    /// <summary>
    /// 開啟另一個畫面；只寫檔名時，找 sourcePath（目前畫面定義檔）同一個資料夾的檔案。
    /// 「傳入參數」的 @欄位名稱 由 valueOf 取值；有「回傳對應」時開成選取視窗（等使用者選完才回來），選的資料交給 onReturn
    /// </summary>
    public static void OpenForm(ButtonDefinition b, string sourcePath, IWin32Window owner,
                                Func<string, object> valueOf = null, Action<IReadOnlyDictionary<string, object>> onReturn = null)
    {
        if (string.IsNullOrWhiteSpace(b.TargetForm))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定要開啟的畫面定義檔。", "提醒");
            return;
        }
        try
        {
            // 登入時從系統資料庫讀已發布的畫面（看檔名），沒登入從檔案讀
            var target = FormStore.LoadForm(b.TargetForm, FolderOf(sourcePath));
            if (!SysContext.PermissionFor(target.FormKey).Open)
            {
                MessageBox.Show($"你沒有開啟「{target.Title}」的權限，請洽系統管理者。", "提醒");
                return;
            }
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, source) in ParsePairs(b.OpenParams))
                parameters[name] = source.StartsWith('@') ? valueOf?.Invoke(source[1..]) ?? DBNull.Value : source;
            var returns = ParsePairs(b.ReturnMap).ToList();
            if (returns.Count == 0)
            {
                if (OpenInShell != null) OpenInShell(target, parameters);
                else new RuntimeForm(target, parameters, pickMode: false).Show(owner);
                return;
            }
            // 選取視窗：使用者選一筆後關閉，依「回傳對應」把那筆的值帶回來
            using var picker = new RuntimeForm(target, parameters, pickMode: true);
            if (picker.ShowDialog(owner) != DialogResult.OK || picker.PickedRow == null) return;
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var notFound = new List<string>();
            foreach (var (mine, theirs) in returns)
            {
                string column = theirs.TrimStart('@');
                if (picker.PickedRow.Table.Columns.Contains(column)) values[mine] = picker.PickedRow[column];
                else notFound.Add(column);
            }
            if (notFound.Count > 0)
                MessageBox.Show("開啟的畫面查不到這些「回傳對應」的欄位：" + string.Join("、", notFound), "提醒");
            onReturn?.Invoke(values);
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>檔名轉成完整路徑：只寫檔名時放在 folder（畫面定義檔的資料夾）；空白回傳 null</summary>
    public static string ResolvePath(string file, string folder)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        return Path.IsPathRooted(file) ? file : Path.Combine(folder ?? FormDefinition.DefaultFolder, file);
    }

    /// <summary>畫面定義檔所在的資料夾（還沒存檔的畫面用預設的 Forms 資料夾）</summary>
    public static string FolderOf(string sourcePath) =>
        string.IsNullOrWhiteSpace(sourcePath) ? FormDefinition.DefaultFolder : Path.GetDirectoryName(sourcePath);

    /// <summary>列印報表：依按鈕的列印樣板預覽列印；records 每個元素提供一筆資料的欄位值（每筆一頁）</summary>
    public static void Print(ButtonDefinition b, string sourcePath, string connectionString, IReadOnlyList<Func<string, object>> records, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(b.PrintTemplate))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設計列印樣板：請在設計畫面雙擊這個按鈕，設計好後儲存。", "列印");
            return;
        }
        PrintTemplate template;
        try
        {
            template = FormStore.LoadTemplate(b.PrintTemplate, FolderOf(sourcePath));     // 登入時從系統資料庫讀
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + Environment.NewLine + "請先在設計畫面雙擊這個按鈕，設計並儲存列印樣板。", "列印");
            return;
        }
        PrintRunner.Preview(template, Path.GetFileNameWithoutExtension(b.PrintTemplate).Replace(".print", ""), connectionString, records, owner);
    }

    /// <summary>
    /// 「列印來源清單」有設定時：印編輯視窗上那個明細清單選取的資料（每筆一頁）；沒選就提醒。
    /// 回傳 false = 這個按鈕沒有設定列印來源清單（照原本的方式印目前這筆）
    /// </summary>
    public static bool PrintFromList(ButtonDefinition b, IEnumerable<EditListView> views, string sourcePath, string connectionString, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(b.PrintListTable)) return false;
        var view = views.FirstOrDefault(v => v.Definition.TableName.Equals(b.PrintListTable.Trim(), StringComparison.OrdinalIgnoreCase));
        if (view == null)
        {
            MessageBox.Show($"這個視窗上找不到列印來源清單「{b.PrintListTable}」，請到編輯視窗版面檢查按鈕的設定。", "列印");
            return true;
        }
        var rows = view.SelectedRows();
        if (rows.Count == 0)
        {
            MessageBox.Show($"請先在下方「{view.Definition.Title ?? view.Definition.TableName}」清單選取要列印的資料（按住 Ctrl 可以選多筆）。", "列印",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        Print(b, sourcePath, connectionString, rows.Select(r => (Func<string, object>)(name => r.Table.Columns.Contains(name) ? r[name] : null)).ToList(), owner);
        return true;
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
