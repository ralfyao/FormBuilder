using System.Globalization;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// 日期欄位的公式（不用連線資料庫）：用另一個日期欄位或今天加減天數，例如
/// 日期選擇1 + 7、[開工日] - 3、DATEADD(DAY, 7, @日期選擇1)、DATEADD(WEEK, 1, 日期選擇1)、DATEADD(MONTH, 1, TODAY())。
/// 來源可以是畫面欄位（可加 @ 或 [ ]），或 NOW()、TODAY()、GETDATE()；單位 DAY、WEEK、MONTH、YEAR（含 DD、WK、MM、YY 等縮寫）
/// </summary>
public static class DateFormula
{
    private static readonly Regex AddPattern = new(@"^DATEADD\s*\(\s*(\w+)\s*,\s*([+-]?\s*\d+)\s*,\s*(.+?)\s*\)$", RegexOptions.IgnoreCase);
    private static readonly Regex PlusPattern = new(@"^(.+?)\s*([+-])\s*(\d+)$");
    private static readonly Regex TodayPattern = new(@"^(NOW|GETDATE|SYSDATE|SYSDATETIME|TODAY)\s*\(\s*\)$", RegexOptions.IgnoreCase);

    /// <summary>欄位是日期欄位（日期選擇或 date / datetime 型別）且有公式：公式照日期算</summary>
    public static bool IsDateField(FieldDefinition f) =>
        f.HasFormula && (f.InputType == InputKind.日期選擇 || TypeHelper.IsDate(f.DataType));

    /// <summary>拆開公式：來源（欄位名稱或 NOW() 等）、單位、數量；寫法不對回傳 false</summary>
    private static bool TryParse(string formula, out string source, out string unit, out int amount)
    {
        source = unit = null;
        amount = 0;
        if (string.IsNullOrWhiteSpace(formula)) return false;
        string t = formula.Trim();
        var m = AddPattern.Match(t);
        if (m.Success)
        {
            unit = m.Groups[1].Value.ToUpperInvariant();
            amount = int.Parse(m.Groups[2].Value.Replace(" ", ""), CultureInfo.InvariantCulture);
            source = m.Groups[3].Value;
        }
        else if ((m = PlusPattern.Match(t)).Success && !t.StartsWith("DATEADD", StringComparison.OrdinalIgnoreCase))
        {
            unit = "DAY";
            amount = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) * (m.Groups[2].Value == "-" ? -1 : 1);
            source = m.Groups[1].Value;
        }
        else
        {
            unit = "DAY";
            source = t;                       // 只寫來源（例如 日期選擇1、TODAY()）= 同一天
        }
        source = source.Trim();
        if (source.StartsWith('[') && source.EndsWith(']')) source = source[1..^1].Trim();
        source = source.TrimStart('@');
        return source.Length > 0 && IsUnit(unit);
    }

    private static bool IsUnit(string unit) => unit is "DAY" or "DD" or "D" or "DAYOFYEAR" or "DY" or "Y" or "WEEK" or "WK" or "WW"
                                                     or "MONTH" or "MM" or "M" or "YEAR" or "YY" or "YYYY";

    /// <summary>日期加減：單位 DAY / WEEK / MONTH / YEAR（含縮寫）</summary>
    public static DateTime Add(DateTime date, string unit, int amount) => unit.ToUpperInvariant() switch
    {
        "WEEK" or "WK" or "WW" => date.AddDays(7 * amount),
        "MONTH" or "MM" or "M" => date.AddMonths(amount),
        "YEAR" or "YY" or "YYYY" => date.AddYears(amount),
        _ => date.AddDays(amount),
    };

    /// <summary>公式用到的畫面欄位（NOW() 等不算）；寫法不對回傳空的</summary>
    public static List<string> Names(string formula) =>
        TryParse(formula, out var source, out _, out _) && !TodayPattern.IsMatch(source) ? new List<string> { source } : new List<string>();

    /// <summary>檢查寫法：對 = null，不對回傳說明（設計畫面提示用）</summary>
    public static string Check(string formula, IEnumerable<string> knownNames)
    {
        if (!TryParse(formula, out var source, out _, out _))
            return "日期公式寫法不對，例如：日期選擇1 + 7、DATEADD(DAY, 7, 日期選擇1)、DATEADD(MONTH, 1, TODAY())";
        if (TodayPattern.IsMatch(source) || knownNames.Contains(source, StringComparer.OrdinalIgnoreCase)) return null;
        return $"日期公式裡的欄位「{source}」不在畫面上";
    }

    /// <summary>算出日期；來源欄位沒填 = null。valueOf 取畫面欄位的值（DateTime 或日期文字）</summary>
    public static DateTime? Evaluate(string formula, Func<string, object> valueOf, string dateFormat = null)
    {
        if (!TryParse(formula, out var source, out var unit, out int amount))
            throw new FormatException("日期公式寫法不對，例如：日期選擇1 + 7、DATEADD(DAY, 7, 日期選擇1)");
        DateTime? start;
        var today = TodayPattern.Match(source);
        if (today.Success) start = today.Groups[1].Value.Equals("TODAY", StringComparison.OrdinalIgnoreCase) ? DateTime.Today : DateTime.Now;
        else start = ToDate(valueOf(source), dateFormat);
        return start == null ? null : Add(start.Value, unit, amount);
    }

    private static DateTime? ToDate(object value, string dateFormat)
    {
        switch (value)
        {
            case null or DBNull: return null;
            case DateTime d: return d;
            case DateTimeOffset o: return o.DateTime;
        }
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        var formats = new[] { dateFormat, "yyyyMMdd", "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss" }
                      .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        return DateTime.TryParse(text, out var parsed) ? parsed : null;
    }
}
