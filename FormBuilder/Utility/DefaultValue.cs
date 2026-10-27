using System.Globalization;

namespace FormBuilder;

/// <summary>
/// 欄位的預設值：新增時自動帶入（使用者仍可以改）。
/// 可以寫固定值；日期用 NOW() / GETDATE()（現在日期時間）、TODAY()（今天），可以加減天數（TODAY()+7）；USER() = 登入帳號。
/// 也可以用代號 {今天} {現在} {使用者}（帳號）{使用者名稱} 夾在文字裡，例如「{使用者名稱} 建立」
/// </summary>
public static class DefaultValue
{
    /// <summary>預設值文字轉成要帶入的值：整段是 {今天}、{現在} 時回傳日期（日期選擇才顯示得出來），其他回傳文字；空白回傳 null</summary>
    public static object Resolve(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string t = text.Trim();
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

    /// <summary>新增開始時，把有設預設值的欄位填上預設值（有編號規則的欄位交給編號規則）</summary>
    public static void FillInputs(IReadOnlyDictionary<FieldDefinition, Control> inputs)
    {
        foreach (var (f, input) in inputs)
        {
            if (!string.IsNullOrWhiteSpace(f.NumberRule)) continue;
            object value = Resolve(f.DefaultValue);
            if (value == null) continue;
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
}
