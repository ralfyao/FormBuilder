using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>查詢條件的比對方式</summary>
public enum QueryOp
{
    /// <summary>完全相等</summary>
    Equal,
    /// <summary>文字有 * 或 ? 萬用字元（值已經換成 % 和 _）</summary>
    Like,
    /// <summary>只比對日期部分，不管時間</summary>
    SameDate,
}

/// <summary>一個查詢條件：查詢時在輸入框填了值的欄位</summary>
public record QueryCondition(FieldDefinition Field, QueryOp Op, object Value)
{
    /// <summary>
    /// 組成 WHERE 條件（不含 WHERE），參數依序叫 q0、q1…；exprOf 提供每個條件在 SQL 裡的欄位寫法。
    /// 沒有條件回傳空字串；任一符合（OR）時整組加括號
    /// </summary>
    public static string ToSql(IReadOnlyList<QueryCondition> conditions, DbCommand cmd, DbDialect d,
                               Func<QueryCondition, string> exprOf, bool matchAny = false)
    {
        var parts = new List<string>();
        for (int i = 0; i < conditions.Count; i++)
        {
            var c = conditions[i];
            string col = exprOf(c), q = d.Param($"q{i}");
            parts.Add(c.Op switch
            {
                QueryOp.Like => $"{col} LIKE {q}",
                QueryOp.SameDate => $"{d.DateOnly(col)} = {q}",
                _ => $"{col} = {q}",
            });
            d.AddParam(cmd, $"q{i}", c.Value);
        }
        if (parts.Count == 0) return "";
        return matchAny ? "(" + string.Join(" OR ", parts) + ")" : string.Join(" AND ", parts);
    }

    /// <summary>
    /// 這個值符不符合條件（只放 SQL 清單的畫面在程式裡篩選用）；
    /// 比對方式跟 SQL Server 預設一樣：文字不分大小寫、忽略字尾空白，NULL 不符合任何條件
    /// </summary>
    public bool Matches(object value)
    {
        if (value == null || value == DBNull.Value) return false;
        switch (Op)
        {
            case QueryOp.Like:
                // 值已經是 SQL 的萬用字元：% = 任意字、_ = 一個字
                string pattern = "^" + Regex.Escape(Convert.ToString(Value) ?? "").Replace("%", ".*").Replace("_", ".") + "$";
                return Regex.IsMatch(Text(value), pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            case QueryOp.SameDate:
                return value is DateTime d && Value is DateTime day && d.Date == day.Date;
            default:
                if (IsNumber(value) && IsNumber(Value)) return Convert.ToDecimal(value) == Convert.ToDecimal(Value);
                if (value is DateTime || Value is DateTime) return Equals(value, Value);
                return string.Equals(Text(value), Text(Value), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Text(object value) => (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").TrimEnd();

    private static bool IsNumber(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}

/// <summary>查詢條件畫面按「查詢」帶回來的內容</summary>
public sealed class ConditionResult
{
    /// <summary>有填值的欄位組成的條件</summary>
    public List<QueryCondition> Conditions { get; init; } = new();
    /// <summary>true = 任一符合（OR）；false = 全部符合（AND）</summary>
    public bool MatchAny { get; init; }
    /// <summary>條件畫面上每個欄位的值（按鈕有填 SQL 時，@欄位名稱 用這些值；文字沒填 = 空字串）</summary>
    public Dictionary<string, object> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
