using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>後台設計的按鈕：建立控制項、確認訊息、執行 SQL、開啟畫面（主畫面與編輯視窗共用）</summary>
public static class ButtonRunner
{
    public static Button CreateControl(ButtonDefinition b) =>
        new RoundButton { Text = b.Text, Bounds = new Rectangle(b.X, b.Y, Math.Max(b.Width, 40), Math.Max(b.Height, 20)), Look = ButtonLook.From(b) };

    /// <summary>套用按鈕自己設的字型、大小（要在加進畫面之後呼叫，沒設定的部分才會沿用畫面的字型）</summary>
    public static void ApplyFont(ButtonDefinition b, Control button)
    {
        if (FontPick.IsSet(b.FontName, b.FontSize)) button.Font = b.FontFor(button.Font);
    }

    /// <summary>使用者對這個畫面的權限，有沒有按鈕「需要的權限」勾選的每一項（沒勾 = 不限制）</summary>
    public static bool Allowed(ButtonDefinition b, FormPermission p)
    {
        var need = b.RequiredPermissions;
        if (need == ButtonPermission.無 || p == null) return true;
        return (!need.HasFlag(ButtonPermission.開啟) || p.Open)
            && (!need.HasFlag(ButtonPermission.編修) || p.Modify)
            && (!need.HasFlag(ButtonPermission.核准) || p.Approve)
            && (!need.HasFlag(ButtonPermission.報表) || p.Print)
            && (!need.HasFlag(ButtonPermission.輸出) || p.Export);
    }

    /// <summary>沒有權限時的提醒（按鈕變灰之外，萬一還是觸發了也擋下來）；回傳 true = 可以執行</summary>
    public static bool CheckAllowed(ButtonDefinition b, FormPermission p)
    {
        if (Allowed(b, p)) return true;
        MessageBox.Show($"你沒有按「{b.Text}」需要的權限（{b.RequiredPermissions}），請洽系統管理者。", "提醒");
        return false;
    }

    /// <summary>有設定確認訊息就先詢問；回傳 true = 可以執行</summary>
    public static bool Confirm(ButtonDefinition b) =>
        string.IsNullOrWhiteSpace(b.Confirm) ||
        MessageBox.Show(b.Confirm, b.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>
    /// 執行按鈕設定的 SQL（同一個交易，失敗全部還原）。
    /// @欄位名稱 的值由 valueOf 提供（回傳 null = NULL；格式不對時丟 FormatException）
    /// </summary>
    public static bool RunSql(ButtonDefinition b, string connectionString, Func<string, object> valueOf, string formKey = null,
                              Action<IReadOnlyDictionary<string, object>> onResult = null) =>
        RunSql(b, connectionString, new[] { valueOf }, formKey, onResult: onResult);

    /// <summary>
    /// 對多筆資料各執行一次按鈕設定的 SQL（清單多選時），全部在同一個交易裡，任何一筆失敗就全部還原；
    /// rows 的每一個元素提供那一筆的 @欄位名稱 值
    /// </summary>
    /// <param name="userSql">要執行的 SQL；null = 按鈕的「SQL」（「執行後動作」傳自己的 SQL）</param>
    /// <param name="quiet">成功時不顯示「執行完成」（「執行後動作」成功後直接接著做後續動作）</param>
    /// <param name="onResult">SQL 有 SELECT 出資料時，依按鈕的「SQL 回傳對應」交給呼叫端填進畫面上的欄位（key = 畫面欄位名稱）</param>
    public static bool RunSql(ButtonDefinition b, string connectionString, IReadOnlyList<Func<string, object>> rows, string formKey = null,
                              string userSql = null, bool quiet = false, Action<IReadOnlyDictionary<string, object>> onResult = null)
    {
        userSql ??= b.Sql;
        if (string.IsNullOrWhiteSpace(userSql))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定 SQL。", "提醒");
            return false;
        }
        try
        {
            int affected = ExecuteSql(userSql, b.SqlParams, connectionString, rows, out var firstRow, out bool queried);
            Audit.Write(formKey, null, "執行SQL", null, new { 按鈕 = b.Text, SQL = userSql, 筆數 = rows.Count, 影響 = affected });
            if (firstRow != null) onResult?.Invoke(MapResult(firstRow, b.SqlReturn));
            // 只是查值填回畫面（例如取號）時不跳「執行完成」，查到的值已經顯示在欄位上；SELECT 沒查到資料時明白告訴使用者
            if (quiet || (firstRow != null && affected == 0)) { }
            else if (queried && firstRow == null && affected == 0)
                MessageBox.Show("SQL 沒有查到資料，畫面上的欄位沒有變。\n請檢查 SQL 的條件（例如 WHERE 的資料表裡還沒有符合的資料）。", b.Text);
            else
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

    /// <summary>
    /// 在同一個交易裡執行使用者寫的 SQL（rows 每一筆各執行一次，任何一筆失敗就全部還原），回傳影響筆數。
    /// SQL 的 @名稱 先找 paramText（「名稱=值」，值可以是 @欄位、SQL 運算式或固定值，見 ParamValue），沒有就用 valueOf 取畫面上的值；
    /// SQL 有 SELECT 出資料時，firstRow = 第一個查詢結果的第一筆（欄位名稱 → 值），沒有就是 null
    /// </summary>
    public static int ExecuteSql(string userSql, string paramText, string connectionString, IReadOnlyList<Func<string, object>> rows,
                                 out Dictionary<string, object> firstRow) =>
        ExecuteSql(userSql, paramText, connectionString, rows, out firstRow, out _);

    /// <param name="queried">SQL 裡有 SELECT（有傳回查詢結果，不管有沒有資料）</param>
    public static int ExecuteSql(string userSql, string paramText, string connectionString, IReadOnlyList<Func<string, object>> rows,
                                 out Dictionary<string, object> firstRow, out bool queried)
    {
        firstRow = null;
        queried = false;
        var d = DbDialect.Of(connectionString);
        var (sql, parameters) = d.PrepareUserSql(userSql);        // @名稱 轉成這個資料庫的參數寫法
        var extra = ParsePairs(paramText).GroupBy(p => p.Left, StringComparer.OrdinalIgnoreCase)
                                         .ToDictionary(g => g.Key, g => g.Last().Right, StringComparer.OrdinalIgnoreCase);
        using var conn = d.Open(connectionString);
        using var tran = conn.BeginTransaction();
        using var cmd = d.CreateCommand(conn, sql);
        cmd.Transaction = tran;
        int affected = 0;
        foreach (var valueOf in rows)
        {
            // 傳入參數用同一條連線、同一個交易計算（看得到前面幾筆剛改的資料）
            var computed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            object ValueOf(string name)
            {
                if (!extra.TryGetValue(name, out string source)) return valueOf(name);
                if (!computed.TryGetValue(name, out var v)) computed[name] = v = ParamValue(name, source, valueOf, connectionString, conn, tran);
                return v;
            }
            var values = parameters.Select(p => (p.Parameter, Value: ValueOf(p.Name))).ToList();
            cmd.Parameters.Clear();
            foreach (var (parameter, value) in values) d.AddParam(cmd, parameter, value);
            using (var reader = cmd.ExecuteReader())
            {
                do
                {
                    if (reader.FieldCount > 0) queried = true;
                    if (firstRow == null && reader.FieldCount > 0 && reader.Read())
                    {
                        firstRow = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++) firstRow[reader.GetName(i)] = reader.GetValue(i);
                    }
                } while (reader.NextResult());
                affected += Math.Max(0, reader.RecordsAffected);
            }
        }
        tran.Commit();
        return affected;
    }

    /// <summary>
    /// SQL 查出來的那一筆依「回傳對應」轉成要填的欄位值（key = 畫面欄位名稱）：
    /// 「畫面欄位=SQL 欄位」，多組用逗號分隔；空白 = SQL 的每個欄位填進畫面上同名的欄位
    /// </summary>
    public static Dictionary<string, object> MapResult(IReadOnlyDictionary<string, object> row, string returnMap)
    {
        var map = ParsePairs(returnMap).ToList();
        if (map.Count == 0) return new Dictionary<string, object>(row, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var notFound = new List<string>();
        foreach (var (screen, column) in map)
        {
            string name = column.TrimStart('@');
            if (row.TryGetValue(name, out var v)) values[screen] = v;
            else notFound.Add(name);
        }
        if (notFound.Count > 0)
            MessageBox.Show("SQL 查出來的資料沒有這些「回傳對應」的欄位：" + string.Join("、", notFound), "提醒");
        return values;
    }

    /// <summary>有設定「執行後動作」的欄位：開窗選取帶回值後也要執行（見 AttachPickers）</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Action> afterSelectActions = new();

    /// <summary>
    /// 下拉選單的「執行後動作」：使用者選了選項（或開窗選取帶回值）後執行 SQL，@名稱 先找「執行後動作傳入參數」，再用 valueOf 取畫面上任何欄位的值；
    /// SQL 查出資料時，依「執行後動作回傳對應」填進畫面上的欄位（空白 = 同名的欄位）
    /// </summary>
    public static void AttachAfterSelect(IReadOnlyDictionary<FieldDefinition, Control> inputs, string connectionString, Func<string, object> valueOf,
                                         string formKey = null)
    {
        foreach (var (f, input) in inputs)
        {
            if (string.IsNullOrWhiteSpace(f.AfterSelectSql)) continue;
            void Run()
            {
                try
                {
                    ExecuteSql(f.AfterSelectSql, f.AfterSelectParams, connectionString, new Func<string, object>[] { valueOf }, out var row);
                    Audit.Write(formKey, null, "執行SQL", null, new { 下拉選單 = f.Label ?? f.Column, SQL = f.AfterSelectSql });
                    if (row != null)
                        FillFields(MapResult(row, f.AfterSelectReturn), inputs, reportMissing: !string.IsNullOrWhiteSpace(f.AfterSelectReturn));
                }
                catch (FormatException ex)
                {
                    MessageBox.Show(ex.Message, "提醒");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"下拉選單「{f.Label ?? f.Column}」的執行後動作失敗（已全部還原）：{ex.Message}", "錯誤",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            afterSelectActions.AddOrUpdate(input, Run);
            if (input is ComboBox combo) combo.SelectionChangeCommitted += (s, e) => Run();
        }
    }

    /// <summary>
    /// 顯示一筆資料時，用欄位的「執行後動作」把文字標籤的值帶出來（例如國別帶出國別代碼、業別代碼帶出業別名稱）：
    /// 文字標籤先恢復原本的文字；欄位有值、執行後動作是 SELECT 的才查，查出來的值只填進文字標籤，不動其他欄位、不跳訊息
    /// </summary>
    public static void RefreshTextLabels(IReadOnlyDictionary<FieldDefinition, Control> inputs, string connectionString, Func<string, object> valueOf)
    {
        var labels = inputs.Where(p => p.Key.IsTextLabel).ToDictionary(p => p.Key, p => p.Value);
        if (labels.Count == 0) return;
        foreach (var (f, input) in labels) InputHelper.SetValue(input, f.Label);
        foreach (var f in inputs.Keys.Where(f => !f.IsTextLabel && f.AfterSelectSql?.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) == true))
        {
            try
            {
                if (valueOf(f.Column) is null or DBNull || valueOf(f.Column) is string s && s.Trim() == "") continue;
                ExecuteSql(f.AfterSelectSql, f.AfterSelectParams, connectionString, new Func<string, object>[] { valueOf }, out var row);
                if (row == null) continue;
                var map = ParsePairs(f.AfterSelectReturn).ToList();
                var values = map.Count == 0 ? row
                    : map.Where(m => row.ContainsKey(m.Right.TrimStart('@'))).ToDictionary(m => m.Left, m => row[m.Right.TrimStart('@')], StringComparer.OrdinalIgnoreCase);
                FillFields(values, labels, reportMissing: false);
            }
            catch (Exception)
            {
                // 帶不出來只是少了顯示，不影響資料
            }
        }
    }

    /// <summary>
    /// 「執行前檢查」：rows 裡不符合 condition（DataTable 篩選寫法）的資料列，依原本順序；
    /// 條件寫錯時丟 EvaluateException / SyntaxErrorException
    /// </summary>
    public static List<DataRow> FailingRows(IReadOnlyList<DataRow> rows, string condition)
    {
        if (rows.Count == 0) return new List<DataRow>();
        var table = rows[0].Table.Clone();
        foreach (var row in rows) table.ImportRow(row);
        var passed = table.Select(condition).ToHashSet();
        return Enumerable.Range(0, rows.Count).Where(i => !passed.Contains(table.Rows[i])).Select(i => rows[i]).ToList();
    }

    /// <summary>SQL 裡的 @參數名稱（不含 @；@@ROWCOUNT 這類系統函數不算）</summary>
    public static IEnumerable<string> ParameterNames(string sql) =>
        Regex.Matches(sql, @"(?<!@)@([\p{L}\p{N}_]+)").Select(m => m.Groups[1].Value)
             .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 解析「名稱=值」的清單（逗號、分號或換行分隔）；左右都去掉空白，沒有等號的略過。
    /// 括號、單引號裡的逗號不算分隔（傳入參數的值可以是 SQL 運算式，例如 LEFT(@產品編號, 13)）
    /// </summary>
    public static IEnumerable<(string Left, string Right)> ParsePairs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string raw in SplitTopLevel(text))
        {
            string part = raw.Trim();
            int eq = part.IndexOf('=');
            if (eq > 0) yield return (part[..eq].Trim(), part[(eq + 1)..].Trim());
        }
    }

    /// <summary>依逗號、分號、換行切開，但括號裡、單引號字串裡的不切</summary>
    private static IEnumerable<string> SplitTopLevel(string text)
    {
        int depth = 0, start = 0;
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'') quoted = !quoted;                   // '' 連寫會切換兩次，結果不變
            else if (quoted) continue;
            else if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0 && c is ',' or '，' or ';' or '\n' or '\r')
            {
                if (i > start) yield return text[start..i];
                start = i + 1;
            }
        }
        if (start < text.Length) yield return text[start..];
    }

    private static readonly Regex FieldRef = new(@"^@[\p{L}\p{N}_]+$", RegexOptions.Compiled);
    private static readonly Regex HasFieldRef = new(@"(?<!@)@[\p{L}\p{N}_]", RegexOptions.Compiled);
    private static readonly Regex QuotedText = new(@"^'([^']|'')*'$", RegexOptions.Compiled);

    /// <summary>
    /// 傳入參數的值怎麼算：
    ///   @欄位名稱                     → 本畫面這個欄位的值（valueOf 提供）
    ///   含 @欄位名稱 或括號的運算式     → SQL 運算式，由資料庫算出（SELECT 運算式），@欄位名稱 用參數帶值，例如
    ///                                    LEFT(@產品編號, 13) + (SELECT FORMAT(ISNULL(MAX(...), 0) + 1, '00') FROM ...)
    ///   其他                          → 固定文字（要傳含括號的固定文字時用單引號括起來，例如 'A(1)'）
    /// </summary>
    /// <param name="conn">已經開好的連線（跟後面的 SQL 同一個交易時傳入，才看得到交易裡剛改的資料）；null = 另外開一條</param>
    public static object ParamValue(string name, string source, Func<string, object> valueOf, string connectionString,
                                    DbConnection conn = null, DbTransaction tran = null)
    {
        if (FieldRef.IsMatch(source)) return valueOf?.Invoke(source[1..]) ?? DBNull.Value;
        if (QuotedText.IsMatch(source)) return source[1..^1].Replace("''", "'");   // 'A(1), B'：單引號括起來的固定文字
        if (!HasFieldRef.IsMatch(source) && !source.Contains('(')) return source;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"傳入參數「{name}」是 SQL 運算式，但這個畫面沒有資料庫連線。");
        var d = DbDialect.Of(connectionString);
        var (sql, parameters) = d.PrepareUserSql("SELECT " + source + (d.Kind == DbKind.Oracle ? " FROM DUAL" : ""));
        DbConnection own = null;
        try
        {
            if (conn == null) conn = own = d.Open(connectionString);
            using var cmd = d.CreateCommand(conn, sql);
            cmd.Transaction = tran;
            foreach (var (field, parameter) in parameters)
                d.AddParam(cmd, parameter, valueOf?.Invoke(field));
            return cmd.ExecuteScalar() ?? DBNull.Value;
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"傳入參數「{name}」的 SQL 執行失敗：{ex.Message}\n{source}", ex);
        }
        finally
        {
            own?.Dispose();
        }
    }

    /// <summary>有設定「回傳對應」的開啟畫面按鈕 = 選取視窗：要在輸入中（新增、修改）才能按</summary>
    public static bool IsPicker(ButtonDefinition b) => b.Action == ButtonAction.開啟畫面 && ParsePairs(b.ReturnMap).Any();

    /// <summary>
    /// 把選取視窗帶回的值（或 SQL 查出來的值）填進本視窗的欄位；reportMissing = 本視窗沒有的欄位列出來提醒
    /// </summary>
    public static void FillFields(IReadOnlyDictionary<string, object> values, IReadOnlyDictionary<FieldDefinition, Control> inputs,
                                  bool reportMissing = true)
    {
        var missing = new List<string>();
        foreach (var (column, value) in values)
        {
            var field = inputs.Keys.FirstOrDefault(f => f.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
            if (field == null) missing.Add(column);
            else InputHelper.SetValue(inputs[field], value);
        }
        if (missing.Count > 0 && reportMissing)
            MessageBox.Show("「回傳對應」裡這些欄位不在這個視窗上，沒有帶入：" + string.Join("、", missing), "提醒");
    }

    /// <summary>有設定開窗選取的欄位（SetValue 把帶回來的值加進下拉選項用）</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> pickerInputs = new();

    public static bool HasPicker(Control input) => pickerInputs.TryGetValue(input, out _);

    /// <summary>欄位的開窗選取設定轉成「開啟畫面」按鈕（有回傳對應 = 選取視窗）；沒設定回傳 null</summary>
    public static ButtonDefinition PickerButton(FieldDefinition f) =>
        string.IsNullOrWhiteSpace(f.PickerForm) ? null : new ButtonDefinition
        {
            Text = f.Label ?? f.Column,
            Action = ButtonAction.開啟畫面,
            TargetForm = f.PickerForm.Trim(),
            OpenParams = f.PickerParams,
            ReturnMap = PickerReturnMap(f),
        };

    /// <summary>
    /// 欄位的回傳對應：空白 = 本欄位=同名欄位；只寫對方欄位名稱（沒有 =）時，第一個帶回本欄位，其他帶回同名欄位。
    /// 例如品項編號欄位寫「產品編號」= 品項編號=產品編號
    /// </summary>
    private static string PickerReturnMap(FieldDefinition f)
    {
        if (string.IsNullOrWhiteSpace(f.PickerReturn)) return $"{f.Column}={f.Column}";
        bool first = true;
        var pairs = f.PickerReturn.Split(new[] { ',', '，', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                if (part.Contains('=')) return part;
                string pair = first ? $"{f.Column}={part.TrimStart('@')}" : $"{part.TrimStart('@')}={part.TrimStart('@')}";
                first = false;
                return pair;
            });
        return string.Join(", ", pairs);
    }

    /// <summary>
    /// 有設定「開窗畫面」的欄位：點下拉（下拉選單）或雙擊、按 F4（其他輸入框）就開選取視窗；
    /// 欄位不能輸入（瀏覽中、唯讀）時不開。open 負責開啟（帶傳入參數、把回傳值填回）；
    /// 帶回值後這個欄位的值有變、又有設定「執行後動作」時，接著執行
    /// </summary>
    public static void AttachPickers(IReadOnlyDictionary<FieldDefinition, Control> inputs, Action<ButtonDefinition> open)
    {
        foreach (var (f, input) in inputs)
        {
            var b = PickerButton(f);
            if (b == null) continue;
            pickerInputs.AddOrUpdate(input, b);
            bool busy = false;
            void Fire()
            {
                if (busy || !input.Enabled || input is TextBoxBase { ReadOnly: true }) return;
                busy = true;
                try
                {
                    var before = InputHelper.GetValue(f, input);
                    open(b);
                    if (!Equals(before, InputHelper.GetValue(f, input)) && afterSelectActions.TryGetValue(input, out var after)) after();
                }
                finally { busy = false; }
            }
            if (input is ComboBox combo)
            {
                // 不顯示原本的下拉清單，改開選取視窗
                combo.IntegralHeight = false;
                combo.DropDownHeight = 1;
                combo.DropDown += (s, e) => combo.BeginInvoke(() => { combo.DroppedDown = false; Fire(); });
            }
            else
            {
                input.DoubleClick += (s, e) => Fire();
                input.KeyDown += (s, e) => { if (e.KeyCode == Keys.F4) { e.Handled = true; Fire(); } };
            }
        }
    }

    /// <summary>
    /// 使用者端的 ERP 外框設定這個，「開啟畫面」按鈕（不是選取視窗時）就開成外框裡的分頁；沒設定 = 開新視窗
    /// </summary>
    public static Action<FormDefinition, IDictionary<string, object>> OpenInShell { get; set; }

    /// <summary>
    /// 開啟另一個畫面；只寫檔名時，找 sourcePath（目前畫面定義檔）同一個資料夾的檔案。
    /// 「傳入參數」的 @欄位名稱 由 valueOf 取值，SQL 運算式用 connectionString 的資料庫計算（見 ParamValue）；有「回傳對應」時開成選取視窗（等使用者選完才回來），選的資料交給 onReturn。
    /// 「存檔後關閉並重新整理」時開成獨立視窗，關閉後有存檔過就把最後存檔那筆的主鍵（刪除時是空的）交給 onSaved
    /// </summary>
    public static void OpenForm(ButtonDefinition b, string sourcePath, IWin32Window owner,
                                Func<string, object> valueOf = null, Action<IReadOnlyDictionary<string, object>> onReturn = null,
                                Action<IReadOnlyDictionary<string, object>> onSaved = null, string connectionString = null)
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
                parameters[name] = ParamValue(name, source, valueOf, connectionString);     // @欄位、SQL 運算式或固定值
            var returns = ParsePairs(b.ReturnMap).ToList();
            if (returns.Count == 0 && b.RefreshAfterSave)
            {
                // 等使用者存檔（存檔成功會自動關閉）或關閉視窗，再讓原畫面重新整理
                using var form = new RuntimeForm(target, parameters, pickMode: false)
                {
                    CloseAfterSave = true,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.CenterParent,
                };
                form.ShowDialog(owner);
                if (form.Saved) onSaved?.Invoke(form.SavedKey ?? new Dictionary<string, object>());
                return;
            }
            if (returns.Count == 0)
            {
                if (OpenInShell != null && !b.OpenInNewWindow) OpenInShell(target, parameters);
                // ERP 外框分頁裡的畫面不是頂層視窗，不能當擁有者：改用它所在的外框視窗
                else new RuntimeForm(target, parameters, pickMode: false).Show((owner as Control)?.TopLevelControl ?? owner);
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

    /// <summary>
    /// 開啟查詢條件畫面（按鈕的畫面定義檔）：使用者輸入條件按「查詢」後帶回條件；取消或開不起來回傳 null。
    /// 條件畫面只用來輸入條件、不讀寫資料，所以不檢查它的開啟權限（使用者有本畫面的權限就能用）
    /// </summary>
    public static ConditionResult AskConditions(ButtonDefinition b, string sourcePath, IWin32Window owner)
    {
        if (string.IsNullOrWhiteSpace(b.TargetForm))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定查詢條件畫面（畫面定義檔）。", "提醒");
            return null;
        }
        try
        {
            // 登入時從系統資料庫讀已發布的畫面（看檔名），沒登入從檔案讀
            var target = FormStore.LoadForm(b.TargetForm, FolderOf(sourcePath));
            using var dialog = RuntimeForm.ForConditions(target, b.ConditionJoin == ConditionJoin.任一符合);
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ConditionResult : null;
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟查詢條件畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
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
    /// <param name="selected">明細清單上選取的資料（整筆），樣板「只印清單上選取的資料」用</param>
    public static void Print(ButtonDefinition b, string sourcePath, string connectionString, IReadOnlyList<Func<string, object>> records, IWin32Window owner,
                             IReadOnlyList<DataRow> selected = null)
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
        // 選取的資料是單身那個資料表的才拿來過濾單身
        if (selected != null && template.Detail != null && selected.Count > 0
            && !selected[0].Table.TableName.Equals(template.Detail.TableName, StringComparison.OrdinalIgnoreCase)) selected = null;
        PrintRunner.Preview(template, Path.GetFileNameWithoutExtension(b.PrintTemplate).Replace(".print", ""), connectionString, records, owner, selected);
    }

    /// <summary>
    /// 「列印來源清單」有設定時：依編輯視窗上那個明細清單選取的資料列印；沒選就提醒。
    /// 樣板以單頭為主：每筆選取的資料提供欄位值，先找選取的那筆（重新從資料庫讀整筆），沒有再找編輯視窗目前這筆（current）。
    /// 回傳 false = 這個按鈕沒有設定列印來源清單（照原本的方式印目前這筆）
    /// </summary>
    public static bool PrintFromList(ButtonDefinition b, IEnumerable<EditListView> views, string sourcePath, string connectionString,
                                     Func<string, object> current, IWin32Window owner)
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
        try
        {
            rows = rows.Select(r => ReloadFull(view.Definition, connectionString, r) ?? r).ToList();
        }
        catch (Exception) { }                                   // 讀不到整筆就用清單上的欄位
        foreach (var t in rows.Select(r => r.Table).Distinct()) t.TableName = view.Definition.TableName;   // 讓列印知道是哪個資料表的資料
        // 單頭的主鍵一定用編輯視窗目前這筆的（明細資料表可能有同名但意思不同的欄位，例如「識別」）
        var headerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { headerKeys.UnionWith(FormStore.LoadTemplate(b.PrintTemplate, FolderOf(sourcePath)).KeyColumns); }
        catch (Exception) { }                                   // 樣板讀不到時，交給 Print 顯示原因
        Print(b, sourcePath, connectionString, rows.Select(r => (Func<string, object>)(name =>
            headerKeys.Contains(name) && current?.Invoke(name) is { } key && key != DBNull.Value ? key
            : r.Table.Columns.Contains(name) && r[name] != DBNull.Value ? r[name] : current?.Invoke(name))).ToList(), owner, rows);
        return true;
    }

    /// <summary>清單上只有顯示的欄位：依主鍵重新讀整筆（列印時要用沒顯示的欄位，例如詢價日期）；沒有主鍵或讀不到回傳 null</summary>
    private static DataRow ReloadFull(EditListDefinition list, string connectionString, DataRow row)
    {
        var keys = list.Columns.Where(c => c.IsKey && !c.IsJoin).Select(c => c.Column).ToList();
        if (keys.Count == 0 || !keys.All(row.Table.Columns.Contains)) return null;
        var d = DbDialect.Of(connectionString);
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn);
        cmd.CommandText = $"SELECT * FROM {d.Table(list.TableSchema, list.TableName)} WHERE "
                        + string.Join(" AND ", keys.Select((k, i) => $"{d.Quote(k)} = {d.Param($"k{i}")}"));
        for (int i = 0; i < keys.Count; i++) d.AddParam(cmd, $"k{i}", row[keys[i]]);
        var table = d.Fill(cmd);
        return table.Rows.Count > 0 ? table.Rows[0] : null;
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
