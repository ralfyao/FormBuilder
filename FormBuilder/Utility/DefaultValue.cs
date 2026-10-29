using System.Globalization;

namespace FormBuilder;

/// <summary>
/// 欄位的預設值：新增時自動帶入（使用者仍可以改）。
/// 可以寫固定值；日期用 NOW() / GETDATE()（現在日期時間）、TODAY()（今天），可以加減天數（TODAY()+7）；USER() = 登入帳號。
/// 也可以用代號 {今天} {現在} {使用者}（帳號）{使用者名稱} 夾在文字裡，例如「{使用者名稱} 建立」。
/// 寫 SELECT（或 WITH、EXEC）開頭的 SQL 時，到資料庫查詢，取第一筆的第一個欄位；SQL 裡用 @欄位名稱 取畫面上的值
/// </summary>
public static class DefaultValue
{
    /// <summary>預設值是 SQL（SELECT、WITH、EXEC 開頭）</summary>
    public static bool IsSql(string text) =>
        text != null && System.Text.RegularExpressions.Regex.IsMatch(text, @"^\s*(SELECT|WITH|EXEC|EXECUTE)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// 預設值文字轉成要帶入的值：整段是 {今天}、{現在} 時回傳日期（日期選擇才顯示得出來），其他回傳文字；空白回傳 null。
    /// SQL 回傳查詢結果第一筆的第一個欄位（查無資料 = null），@名稱 的值由 valueOf 提供；沒有連線字串時 SQL 不執行。SQL 錯誤會丟出例外
    /// </summary>
    public static object Resolve(string text, string connectionString = null, Func<string, object> valueOf = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string t = text.Trim();
        if (IsSql(t))
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;
            var table = SqlList.Load(connectionString, t, name => valueOf?.Invoke(name));
            if (table.Rows.Count == 0 || table.Columns.Count == 0 || table.Rows[0][0] == DBNull.Value) return null;
            return table.Rows[0][0];
        }
        if (t == "{今天}") return DateTime.Today;
        if (t == "{現在}") return DateTime.Now;
        // 像 SQL 的寫法：NOW() / GETDATE() = 現在日期時間，TODAY() = 今天；後面可以加減天數，例如 TODAY()+7、NOW()-1
        var m = System.Text.RegularExpressions.Regex.Match(t, @"^(NOW|GETDATE|SYSDATE|TODAY)(\(\))?\s*(([+-])\s*(\d+))?$",
                                                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var date = m.Groups[1].Value.Equals("TODAY", StringComparison.OrdinalIgnoreCase) ? DateTime.Today : DateTime.Now;
            if (m.Groups[3].Success) date = date.AddDays((m.Groups[4].Value == "-" ? -1 : 1) * int.Parse(m.Groups[5].Value));
            return date;
        }
        if (t.Equals("USER()", StringComparison.OrdinalIgnoreCase)) return SysContext.User?.UserId ?? Environment.UserName;
        return t.Replace("{今天}", DateTime.Today.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))
                .Replace("{現在}", DateTime.Now.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture))
                .Replace("{使用者名稱}", SysContext.User?.UserName ?? Environment.UserName)
                .Replace("{使用者}", SysContext.User?.UserId ?? Environment.UserName);
    }

    /// <summary>
    /// 新增開始時，把有設預設值的欄位填上預設值（有編號規則的欄位交給編號規則）。
    /// 先帶固定值、日期，再執行 SQL 的預設值（SQL 的 @欄位 才取得到先帶好的值）；SQL 錯誤時提醒是哪個欄位，其他欄位照常帶入
    /// </summary>
    public static void FillInputs(IReadOnlyDictionary<FieldDefinition, Control> inputs, string connectionString = null)
    {
        object ValueOf(string name)
        {
            var p = inputs.FirstOrDefault(x => x.Key.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            return p.Key == null ? null : InputHelper.GetValue(p.Key, p.Value);
        }
        var errors = new List<string>();
        foreach (var (f, input) in inputs.OrderBy(p => IsSql(p.Key.DefaultValue)))
        {
            if (!string.IsNullOrWhiteSpace(f.NumberRule)) continue;
            object value;
            try
            {
                value = Resolve(f.DefaultValue, connectionString, ValueOf);
            }
            catch (Exception ex)
            {
                errors.Add($"{(string.IsNullOrWhiteSpace(f.Label) ? f.Column : f.Label)}：{ex.Message}");
                continue;
            }
            if (value == null) continue;
            Apply(f, input, value);
        }
        if (errors.Count > 0)
            MessageBox.Show("預設值的 SQL 執行失敗（這些欄位維持空白）：" + Environment.NewLine + string.Join(Environment.NewLine, errors), "預設值");
    }

    /// <summary>把預設值填進輸入框：日期依日期選擇或日期格式顯示；格式跟欄位對不上就略過</summary>
    public static void Apply(FieldDefinition f, Control input, object value)
    {
        try
        {
            if (value is DateTime date && input is DateTimePicker picker)
            {
                // 文字欄位存日期（有日期格式）時也一樣直接設定日期
                picker.Value = date;
                if (picker.ShowCheckBox) picker.Checked = true;
            }
            else if (value is DateTime d)
            {
                InputHelper.SetValue(input, string.IsNullOrWhiteSpace(f.DateFormat) ? d.ToString(d.TimeOfDay == TimeSpan.Zero ? "yyyy/MM/dd" : "yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
                                                                                     : d.ToString(f.DateFormat, CultureInfo.InvariantCulture));
            }
            else
            {
                InputHelper.SetValue(input, value);
            }
        }
        catch (Exception)
        {
            // 預設值格式跟欄位對不上（例如數字欄位寫了文字）：略過，欄位維持空白
        }
    }
}
