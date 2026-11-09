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
        foreach (var rowValueOf in rows)
        {
            var valueOf = SysParams.With(rowValueOf);         // @登入帳號、@登入姓名
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
        // 文字標籤有預設值（固定文字或 SQL）就顯示預設值；之後欄位的執行後動作有帶出值的話以執行後動作為準
        foreach (var (f, input) in labels.Where(p => !string.IsNullOrWhiteSpace(p.Key.DefaultValue)))
        {
            try
            {
                if (DefaultValue.Resolve(f.DefaultValue, connectionString, valueOf) is { } value) DefaultValue.Apply(f, input, value);
            }
            catch (Exception)
            {
                // 預設值的 SQL 錯誤：維持原本的文字
            }
        }
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
    /// 顯示一筆資料後，執行每個元件的「顯示後動作」（只能是 SELECT / WITH 開頭的查詢）：查出來的第一筆依回傳對應填進畫面上的元件，
    /// 只是顯示，不跳訊息（查不到就不動；SQL 錯了在「SQL 紀錄（Debug）」看得到）
    /// </summary>
    public static void RunAfterShow(IReadOnlyDictionary<FieldDefinition, Control> inputs, string connectionString, Func<string, object> valueOf)
    {
        foreach (var f in inputs.Keys.Where(f => !string.IsNullOrWhiteSpace(f.AfterShowSql)).ToList())
        {
            string sql = f.AfterShowSql.TrimStart();
            if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                ExecuteSql(f.AfterShowSql, f.AfterShowParams, connectionString, new Func<string, object>[] { valueOf }, out var row);
                if (row == null) continue;
                var map = ParsePairs(f.AfterShowReturn).ToList();
                var values = map.Count == 0 ? row
                    : map.Where(m => row.ContainsKey(m.Right.TrimStart('@'))).ToDictionary(m => m.Left, m => row[m.Right.TrimStart('@')], StringComparer.OrdinalIgnoreCase);
                FillFields(values, inputs, reportMissing: false);
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
    /// <summary>
    /// 按鈕的「執行前檢查」：rows 每一筆都要符合條件才回傳 true；沒有資料（顯示 emptyMessage）、不符合、條件寫錯時提醒並回傳 false。
    /// labelOf = 不符合的資料要怎麼列出來（例如單號）；null = 不列（只檢查目前這筆時）
    /// </summary>
    public static bool PassesPreCheck(ButtonDefinition b, IReadOnlyList<DataRow> rows, Func<DataRow, string> labelOf, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(b.PreCheck)) return true;
        if (rows.Count == 0)
        {
            MessageBox.Show(emptyMessage, b.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        List<DataRow> failing;
        try
        {
            if (UsesBlocks(b)) return RunBlocks(b, rows, labelOf);
            failing = FailingRows(rows, b.PreCheck);
        }
        catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException)
        {
            MessageBox.Show($"按鈕「{b.Text}」的執行前檢查寫法有誤：{ex.Message}", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (failing.Count == 0) return true;
        string message = string.IsNullOrWhiteSpace(b.PreCheckMessage) ? $"選取的資料不符合條件：{b.PreCheck}" : b.PreCheckMessage;
        if (labelOf != null)
            message += Environment.NewLine + Environment.NewLine + "不符合的資料：" + string.Join("、", failing.Take(10).Select(labelOf))
                     + (failing.Count > 10 ? $" 等 {failing.Count} 筆" : "");
        MessageBox.Show(message, b.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>執行前檢查是用設定視窗的擋下規則設的（而且執行前檢查沒被直接改過）</summary>
    public static bool UsesBlocks(ButtonDefinition b) =>
        b.PreCheckBlocks is { Count: > 0 } && ConditionText.PassCondition(b.PreCheckBlocks) == b.PreCheck?.Trim();

    /// <summary>
    /// 擋下規則：由上往下，第一條有資料符合的規則跳它的訊息。「顯示訊息並停止」回傳 false；
    /// 「詢問是否繼續」按「是」就接著檢查下一條，按「否」回傳 false。都沒擋下回傳 true
    /// </summary>
    private static bool RunBlocks(ButtonDefinition b, IReadOnlyList<DataRow> rows, Func<DataRow, string> labelOf)
    {
        foreach (var block in b.PreCheckBlocks)
        {
            var passing = FailingRows(rows, block.Condition).ToHashSet();          // 規則不成立的資料
            var hit = rows.Where(r => !passing.Contains(r)).ToList();
            if (hit.Count == 0) continue;
            string message = !string.IsNullOrWhiteSpace(block.Message) ? block.Message.Trim()
                           : !string.IsNullOrWhiteSpace(b.PreCheckMessage) ? b.PreCheckMessage : $"符合不能執行的條件：{block.Condition}";
            if (labelOf != null)
                message += Environment.NewLine + Environment.NewLine + "符合的資料：" + string.Join("、", hit.Take(10).Select(labelOf))
                         + (hit.Count > 10 ? $" 等 {hit.Count} 筆" : "");
            if (block.Action == PreCheckAction.詢問是否繼續)
            {
                if (MessageBox.Show(message + Environment.NewLine + Environment.NewLine + "要繼續執行嗎？", b.Text,
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
                continue;
            }
            MessageBox.Show(message, b.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    public static List<DataRow> FailingRows(IReadOnlyList<DataRow> rows, string condition)
    {
        if (rows.Count == 0) return new List<DataRow>();
        var table = rows[0].Table.Clone();
        foreach (var row in rows) table.ImportRow(row);
        var passed = table.Select(condition).ToHashSet();
        return Enumerable.Range(0, rows.Count).Where(i => !passed.Contains(table.Rows[i])).Select(i => rows[i]).ToList();
    }

    /// <summary>
    /// Grid 自訂按鈕欄位：按下某一列的按鈕。@欄位 先取那一列的值（含自訂勾選框、日期），沒有再交給 fallback（畫面上的欄位等）。
    /// 依序做 執行前檢查、確認訊息、執行後動作，再做 執行SQL 或 開啟畫面。
    /// 回傳：true = 有執行 SQL 成功（呼叫端重新讀取資料）；false = 沒執行或失敗；null = 其他動作，交給呼叫端照畫面按鈕執行
    /// </summary>
    public static bool? RunRowButton(ButtonDefinition b, DataRow row, Func<string, object> fallback, string sourcePath, IWin32Window owner,
                                     string connectionString, string formKey = null, Action<IReadOnlyDictionary<string, object>> onResult = null)
    {
        if (b == null)
        {
            MessageBox.Show("這個按鈕還沒有設定動作（到單身欄位設定，在按鈕欄位上按右鍵設定）。", "提醒");
            return false;
        }
        if (b.Action is not (ButtonAction.執行SQL or ButtonAction.開啟畫面)) return null;
        object ValueOf(string name) => row.Table.Columns.Contains(name) ? row[name] : fallback?.Invoke(name);
        if (!string.IsNullOrWhiteSpace(b.PreCheck))
        {
            try
            {
                if (UsesBlocks(b))
                {
                    if (!RunBlocks(b, new[] { row }, null)) return false;
                }
                else if (FailingRows(new[] { row }, b.PreCheck).Count > 0)
                {
                    MessageBox.Show(string.IsNullOrWhiteSpace(b.PreCheckMessage) ? $"這一列不符合條件：{b.PreCheck}" : b.PreCheckMessage,
                                    b.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException)
            {
                MessageBox.Show($"按鈕「{b.Text}」的執行前檢查寫法有誤：{ex.Message}", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
        if (!Confirm(b)) return false;
        bool changed = false;
        IReadOnlyDictionary<string, object> preResult = null;
        if (!string.IsNullOrWhiteSpace(b.PreActionSql))
        {
            if (!RunSql(b, connectionString, new Func<string, object>[] { ValueOf }, formKey, b.PreActionSql, quiet: true, onResult: values => preResult = values)) return false;
            changed = true;
        }
        if (b.Action == ButtonAction.執行SQL)
            return RunSql(b, connectionString, new Func<string, object>[] { ValueOf }, formKey, onResult: onResult) || changed;
        // 開啟畫面的傳入參數先找「執行後動作」SQL 查出來的值；回傳對應（選取視窗）帶回的值交給呼叫端；
        // 「存檔後關閉並重新整理」存檔過就算有改資料，讓呼叫端重新讀取
        OpenForm(b, sourcePath, owner, name => TryGetResult(preResult, name, out var v) ? v : ValueOf(name),
                 onReturn: onResult, onSaved: _ => changed = true, connectionString: connectionString);
        return changed;
    }

    /// <summary>「執行後動作」SQL 查出來的第一筆有沒有這個欄位（不分大小寫）；開啟畫面的傳入參數優先用它</summary>
    public static bool TryGetResult(IReadOnlyDictionary<string, object> result, string name, out object value)
    {
        value = null;
        if (result == null) return false;
        var key = result.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (key == null) return false;
        value = result[key] == DBNull.Value ? null : result[key];
        return true;
    }

    /// <summary>SQL 裡的 @參數名稱（不含 @；@@ROWCOUNT 這類系統函數、SQL 裡自己 DECLARE 的變數不算）</summary>
    public static IEnumerable<string> ParameterNames(string sql)
    {
        var declared = DeclaredVariables(sql);
        return Regex.Matches(sql, @"(?<!@)@([\p{L}\p{N}_]+)").Select(m => m.Groups[1].Value)
                    .Where(n => !declared.Contains(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// SQL 裡 DECLARE 宣告的變數名稱（不含 @），例如 DECLARE @NB nvarchar(20)、DECLARE @a int = 1, @b date、DECLARE @t TABLE (…)。
    /// 一個 DECLARE 宣告到分號或下一個敘述為止，只算括號外、逗號後面接的 @名稱（DECLARE @a int = ISNULL(@x, 0) 的 @x 還是參數）
    /// </summary>
    private static HashSet<string> DeclaredVariables(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 每個 DECLARE 往後看到分號、或括號外的下一個敘述（DECLARE、SET、SELECT…；T-SQL 常不寫分號，一行一個 DECLARE）為止
        var next = new Regex(@"^(?:DECLARE|SET|SELECT|INSERT|UPDATE|DELETE|IF|ELSE|WHILE|BEGIN|END|EXEC|EXECUTE|WITH|MERGE|RETURN|PRINT|THROW|RAISERROR|OPEN|FETCH|CLOSE|DEALLOCATE)\b",
                             RegexOptions.IgnoreCase);
        foreach (Match m in Regex.Matches(sql, @"\bDECLARE\b", RegexOptions.IgnoreCase))
        {
            int depth = 0;
            bool start = true;                              // DECLARE 之後、或括號外的逗號之後：下一個 @名稱是宣告的變數
            for (int i = m.Index + m.Length; i < sql.Length; i++)
            {
                char c = sql[i];
                if (c == ';') break;
                if (depth == 0 && char.IsLetter(c) && !char.IsLetterOrDigit(sql[i - 1]) && sql[i - 1] != '_' && sql[i - 1] != '@'
                    && next.IsMatch(sql.AsSpan(i).ToString())) break;
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                else if (c == ',' && depth == 0) start = true;
                else if (c == '@' && start)
                {
                    var name = Regex.Match(sql[(i + 1)..], @"^[\p{L}\p{N}_]+");
                    if (name.Success) names.Add(name.Value);
                    start = false;
                }
                else if (!char.IsWhiteSpace(c)) start = false;
            }
        }
        return names;
    }

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
        valueOf = SysParams.With(valueOf);                    // @登入帳號、@登入姓名
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
    public static bool IsPicker(ButtonDefinition b) => b.Action == ButtonAction.開啟畫面 && (ParsePairs(b.ReturnMap).Any() || IsGridPicker(b));

    /// <summary>有設定「帶回到 Grid」的開啟畫面按鈕：選取視窗可以多選，選的每一筆新增成那個 Grid 的一列</summary>
    public static bool IsGridPicker(ButtonDefinition b) => b.Action == ButtonAction.開啟畫面 && !string.IsNullOrWhiteSpace(b.ReturnGrid);

    /// <summary>
    /// 選取視窗帶回的每一筆新增成 grid 的一列（新增列的預設值、關聯欄位照一般新增列帶），值依欄位型別轉換；
    /// grid 沒有的欄位、型別轉不過去的值略過並提醒。回傳新增幾列
    /// </summary>
    public static int AddGridRows(DataGridView grid, IReadOnlyList<IReadOnlyDictionary<string, object>> rows)
    {
        if (grid.DataSource is not DataTable table)
        {
            MessageBox.Show("這個 Grid 現在還不能新增資料。", "提醒");
            return 0;
        }
        grid.EndEdit();
        if (grid.BindingContext?[table] is CurrencyManager cm) cm.EndCurrentEdit();
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var bad = new List<string>();
        foreach (var values in rows)
        {
            var row = table.NewRow();                      // TableNewRow：預設值、關聯欄位
            foreach (var (column, value) in values)
            {
                if (!table.Columns.Contains(column)) { missing.Add(column); continue; }
                var col = table.Columns[column];
                if (col.ReadOnly || col.AutoIncrement) continue;
                try
                {
                    row[col] = value == null || value == DBNull.Value ? DBNull.Value
                             : col.DataType.IsInstanceOfType(value) ? value
                             : Convert.ChangeType(value, Nullable.GetUnderlyingType(col.DataType) ?? col.DataType);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                {
                    bad.Add($"{column} = {value}");
                }
            }
            table.Rows.Add(row);
        }
        if (missing.Count > 0 || bad.Count > 0)
            MessageBox.Show((missing.Count > 0 ? "Grid 上沒有這些欄位，沒有帶入：" + string.Join("、", missing) + Environment.NewLine : "")
                          + (bad.Count > 0 ? "這些值的型別跟 Grid 欄位對不上，沒有帶入：" + string.Join("、", bad.Distinct().Take(10)) : ""), "提醒");
        // 捲到剛帶進來的列
        int last = grid.Rows.Cast<DataGridViewRow>().LastOrDefault(r => !r.IsNewRow)?.Index ?? -1;
        try { if (last >= 0) grid.FirstDisplayedScrollingRowIndex = Math.Max(0, last - Math.Max(0, grid.DisplayedRowCount(false) - 1)); }
        catch (InvalidOperationException) { }                // Grid 還沒顯示、高度是 0
        return rows.Count;
    }

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

    /// <summary>清單的「雙擊開啟畫面」轉成「開啟畫面」按鈕；沒設定回傳 null</summary>
    public static ButtonDefinition ListOpenButton(string title, string openForm, string openParams) =>
        string.IsNullOrWhiteSpace(openForm) ? null : new ButtonDefinition
        {
            Text = title,
            Action = ButtonAction.開啟畫面,
            TargetForm = openForm.Trim(),
            OpenParams = openParams,
        };

    /// <summary>
    /// 按鈕的「來源清單」：@名稱 先取那個清單選取那一列（第一個選取的）的值，清單上沒有的交給 fallback。
    /// 沒設來源清單直接回傳 fallback；找不到清單或沒選取時提醒並回傳 null（呼叫端就不要執行）
    /// </summary>
    public static Func<string, object> WithSourceList(ButtonDefinition b, IEnumerable<EditListView> views, Func<string, object> fallback)
    {
        string name = b.SourceList?.Trim();
        if (string.IsNullOrEmpty(name)) return fallback;
        var view = views.FirstOrDefault(v => v.Definition.TableName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (view == null)
        {
            MessageBox.Show($"這個視窗上找不到來源清單「{name}」，請到編輯視窗版面檢查按鈕「{b.Text}」的設定。", "提醒");
            return null;
        }
        var row = view.SelectedRows().FirstOrDefault();
        if (row == null)
        {
            MessageBox.Show($"請先在「{view.Definition.Title ?? view.Definition.TableName}」清單選取一列。", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        return n => row.Table.Columns.Contains(n) ? row[n] : fallback(n);
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
    public static Action<FormDefinition, IDictionary<string, object>, bool> OpenInShell { get; set; }

    /// <summary>
    /// 開啟另一個畫面；只寫檔名時，找 sourcePath（目前畫面定義檔）同一個資料夾的檔案。
    /// 「傳入參數」的 @欄位名稱 由 valueOf 取值，SQL 運算式用 connectionString 的資料庫計算（見 ParamValue）；有「回傳對應」時開成選取視窗（等使用者選完才回來），選的資料交給 onReturn。
    /// 「存檔後關閉並重新整理」時開成獨立視窗，關閉後有存檔過就把最後存檔那筆的主鍵（刪除時是空的）交給 onSaved。
    /// 有「帶回到 Grid」時開成可以多選的選取視窗，選的每一筆（key = Grid 欄位名稱）交給 onReturnRows
    /// </summary>
    public static void OpenForm(ButtonDefinition b, string sourcePath, IWin32Window owner,
                                Func<string, object> valueOf = null, Action<IReadOnlyDictionary<string, object>> onReturn = null,
                                Action<IReadOnlyDictionary<string, object>> onSaved = null, string connectionString = null,
                                Action<IReadOnlyList<IReadOnlyDictionary<string, object>>> onReturnRows = null)
    {
        string targetFile, caseParams;
        try
        {
            (targetFile, caseParams) = ChooseTargetForm(b, valueOf);  // 「依條件開啟」第一個符合的畫面，都不符合用畫面定義檔
        }
        catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException)
        {
            MessageBox.Show($"按鈕「{b.Text}」的「依條件開啟」條件寫法有誤：{ex.Message}", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(targetFile))
        {
            MessageBox.Show(string.IsNullOrWhiteSpace(b.TargetFormCases)
                ? $"按鈕「{b.Text}」還沒有設定要開啟的畫面定義檔。"
                : $"目前的資料不符合按鈕「{b.Text}」任何一個「依條件開啟」的條件，也沒有設定預設的畫面定義檔。", "提醒");
            return;
        }
        try
        {
            // 登入時從系統資料庫讀已發布的畫面（看檔名），沒登入從檔案讀
            var target = FormStore.LoadForm(targetFile, FolderOf(sourcePath));
            if (!SysContext.PermissionFor(target.FormKey).Open)
            {
                MessageBox.Show($"你沒有開啟「{target.Title}」的權限，請洽系統管理者。", "提醒");
                return;
            }
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, source) in ParsePairs(b.OpenParams))
                parameters[name] = ParamValue(name, source, valueOf, connectionString);     // @欄位、SQL 運算式或固定值
            foreach (var (name, source) in ParsePairs(caseParams))                           // 符合的那一列「帶入參數」，同名的以它為準
                parameters[name] = ParamValue(name, source, valueOf, connectionString);
            if (IsGridPicker(b))
            {
                // 帶回到 Grid：選取視窗可以多選，每一筆依「帶回 Grid 欄位對應」轉成一列（空白 = 全部欄位，同名的帶進 Grid）
                using var multi = new RuntimeForm(target, parameters, pickMode: true) { MultiPick = true };
                if (multi.ShowDialog(owner) != DialogResult.OK || multi.PickedRows.Count == 0) return;
                var map = ParsePairs(b.ReturnGridMap).Select(p => (Mine: p.Left, Theirs: p.Right.TrimStart('@'))).ToList();
                var absent = map.Where(p => !multi.PickedRows[0].Table.Columns.Contains(p.Theirs)).Select(p => p.Theirs).Distinct().ToList();
                if (absent.Count > 0)
                    MessageBox.Show("開啟的畫面查不到這些「帶回 Grid 欄位對應」的欄位：" + string.Join("、", absent), "提醒");
                var picked = multi.PickedRows.Select(r => (IReadOnlyDictionary<string, object>)(map.Count > 0
                        ? map.Where(p => r.Table.Columns.Contains(p.Theirs)).GroupBy(p => p.Mine, StringComparer.OrdinalIgnoreCase)
                             .ToDictionary(g => g.Key, g => r[g.First().Theirs], StringComparer.OrdinalIgnoreCase)
                        : r.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName, c => r[c], StringComparer.OrdinalIgnoreCase)))
                    .ToList();
                onReturnRows?.Invoke(picked);
                return;
            }
            var returns = ParsePairs(b.ReturnMap).ToList();
            if (returns.Count == 0 && b.RefreshAfterSave)
            {
                // 等使用者存檔（存檔成功會自動關閉）或關閉視窗，再讓原畫面重新整理
                using var form = new RuntimeForm(target, parameters, pickMode: false)
                {
                    CloseAfterSave = true,
                    StartInsert = b.OpenAsInsert,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.CenterParent,
                };
                form.ShowDialog(owner);
                if (form.Saved) onSaved?.Invoke(form.SavedKey ?? new Dictionary<string, object>());
                return;
            }
            if (returns.Count == 0)
            {
                if (OpenInShell != null && !b.OpenInNewWindow) OpenInShell(target, parameters, b.OpenAsInsert);
                // ERP 外框分頁裡的畫面不是頂層視窗，不能當擁有者：改用它所在的外框視窗
                else new RuntimeForm(target, parameters, pickMode: false) { StartInsert = b.OpenAsInsert }.Show((owner as Control)?.TopLevelControl ?? owner);
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
    /// 「依條件開啟」：每行「條件 => 畫面定義檔」，後面可以接「| 帶入參數」（開這個畫面時另外帶的傳入參數，「對方欄位=值」用逗號分隔）。
    /// 用條件後面第一個不在單引號字串裡的 =&gt; 切開（條件裡的 &gt;= 不是）；檔名不會有 |，所以畫面後面第一個 | 之後是帶入參數
    /// </summary>
    public static IEnumerable<(string Condition, string File, string Params)> ParseFormCases(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int arrow = -1;
            bool quoted = false;
            for (int i = 0; i < line.Length - 1 && arrow < 0; i++)
            {
                if (line[i] == '\'') quoted = !quoted;
                else if (!quoted && line[i] == '=' && line[i + 1] == '>') arrow = i;
            }
            if (arrow <= 0) continue;
            string condition = line[..arrow].Trim(), rest = line[(arrow + 2)..];
            int pipe = rest.IndexOf('|');
            string file = (pipe < 0 ? rest : rest[..pipe]).Trim().Trim('"');
            string parameters = pipe < 0 ? null : rest[(pipe + 1)..].Trim();
            if (condition.Length > 0 && file.Length > 0) yield return (condition, file, string.IsNullOrEmpty(parameters) ? null : parameters);
        }
    }

    /// <summary>「依條件開啟」的一行：條件 => 畫面定義檔 | 帶入參數（沒有帶入參數就不寫 |）；帶入參數裡的換行改成逗號，才不會拆成兩行</summary>
    public static string FormatFormCase(string condition, string file, string parameters)
    {
        string p = string.Join(", ", (parameters ?? "").Split('\n').Select(s => s.Trim().TrimEnd(',')).Where(s => s.Length > 0));
        return $"{condition} => {file}" + (p.Length > 0 ? " | " + p : "");
    }

    /// <summary>
    /// 按鈕要開的畫面：「依條件開啟」由上往下第一個條件成立的畫面（和那一列的帶入參數）；都不成立用「畫面定義檔」。
    /// 條件裡的欄位名稱用 valueOf 取值（畫面上的欄位、目前這筆）；條件寫錯時丟 EvaluateException / SyntaxErrorException
    /// </summary>
    public static (string File, string Params) ChooseTargetForm(ButtonDefinition b, Func<string, object> valueOf)
    {
        foreach (var (condition, file, parameters) in ParseFormCases(b.TargetFormCases))
            if (ConditionHolds(condition, valueOf)) return (file, parameters);
        return (b.TargetForm?.Trim(), null);
    }

    /// <summary>條件運算式裡不是欄位名稱的字</summary>
    private static readonly HashSet<string> ExpressionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "LIKE", "IN", "IS", "NULL", "TRUE", "FALSE", "ISNULL", "LEN", "IIF", "TRIM", "SUBSTRING", "CONVERT",
    };

    /// <summary>
    /// 用 DataTable 運算式判斷條件（寫法同「執行前檢查」）：把條件裡用到的欄位名稱（[名稱] 或一般名稱，單引號字串裡的不算）
    /// 用 valueOf 取值做成一筆資料再判斷；取不到值的欄位當成 NULL
    /// </summary>
    public static bool ConditionHolds(string condition, Func<string, object> valueOf)
    {
        string bare = Regex.Replace(condition, "'(?:[^']|'')*'", "''");
        var names = Regex.Matches(bare, @"\[([^\]]+)\]|(?<![\p{L}\p{N}_.#])([\p{L}_][\p{L}\p{N}_]*)")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .Where(n => !ExpressionWords.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var table = new DataTable();
        var values = names.Select(n => (Name: n, Value: valueOf?.Invoke(n))).ToList();
        foreach (var (name, value) in values)
            table.Columns.Add(name, value is null or DBNull ? typeof(string) : value.GetType());
        var row = table.NewRow();
        foreach (var (name, value) in values) row[name] = value ?? DBNull.Value;
        table.Rows.Add(row);
        return table.Select(condition).Length > 0;
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

    /// <summary>
    /// 檔名轉成完整路徑：只寫檔名或相對路徑時以 folder（畫面定義檔的資料夾）為準；空白回傳 null。
    /// 完整路徑在這台電腦上找不到時（從別台電腦搬過來的舊畫面），改找 folder 裡同檔名的檔案
    /// </summary>
    public static string ResolvePath(string file, string folder)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        folder ??= FormDefinition.DefaultFolder;
        if (!Path.IsPathRooted(file)) return Path.GetFullPath(Path.Combine(folder, file));
        if (File.Exists(file) || File.Exists(file + ".json")) return file;
        string local = Path.Combine(folder, Path.GetFileName(file));
        return File.Exists(local) || File.Exists(local + ".json") ? local : file;
    }

    /// <summary>完整路徑改成相對於 folder 的相對路徑（同一資料夾就只剩檔名）；不同磁碟時保留完整路徑，空白回傳 null</summary>
    public static string RelativePath(string file, string folder)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        if (!Path.IsPathRooted(file) || string.IsNullOrEmpty(folder)) return file;
        if (!string.Equals(Path.GetPathRoot(Path.GetFullPath(file)), Path.GetPathRoot(Path.GetFullPath(folder)), StringComparison.OrdinalIgnoreCase)) return file;
        return Path.GetRelativePath(folder, file);
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
