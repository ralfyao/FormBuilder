using System.Text;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// 編號規則：新增時依規則自動產生編號（例如 [BF][YYYY][MM][DD][XXX] → BF20240131001）。
/// 規則由中括號片段組成：
///   [BF]            固定文字，原樣輸出（依使用者要求手動輸入的前綴、分隔符號等）
///   [YYYY][YY][MM][DD]  新增當天的日期（可以寫在同一個括號裡，例如 [YYYYMMDD]）；[YYY] = 民國年（西元年 - 1911）
///   [XXX]           流水號，幾個 X 就是幾碼，前面補 0；到資料表查同樣前綴的最大流水號 + 1
///   @登入帳號 @登入姓名  登入的使用者（括號裡外都可以寫）
/// 流水號最多一個，可以沒有（例如 @登入帳號-[YYYYMMDD] → admin-20261113，不查資料表）；括號外的文字也當固定文字
/// </summary>
public static class NumberRule
{
    /// <summary>規則解析結果：前綴 + 流水號（Digits 碼）+ 後綴</summary>
    public sealed record Parsed(string Prefix, int Digits, string Suffix);

    private static readonly Regex Segment = new(@"\[([^\]]*)\]", RegexOptions.Compiled);
    private static readonly Regex DateToken = new("YYYY|YYY|YY|MM|DD", RegexOptions.Compiled);
    private static readonly Regex SerialToken = new("^X+$", RegexOptions.Compiled);
    private static readonly Regex SysParam = new(@"@([\p{L}\p{N}_]+)", RegexOptions.Compiled);

    /// <summary>文字裡的 @登入帳號、@登入姓名 換成登入的使用者；不是系統參數的 @名稱 原樣保留。other = 其餘文字要怎麼轉（日期片段）</summary>
    private static string ReplaceSysParams(string text, Func<string, string> other)
    {
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in SysParam.Matches(text))
        {
            if (!SysParams.TryGet(m.Groups[1].Value, out var value)) continue;
            sb.Append(other(text[pos..m.Index])).Append(Convert.ToString(value));
            pos = m.Index + m.Length;
        }
        return sb.Append(other(text[pos..])).ToString();
    }

    /// <summary>解析規則；date = 日期片段要用的日期。規則不合法時丟 FormatException（含說明）</summary>
    public static Parsed Parse(string rule, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(rule)) throw new FormatException("沒有設定編號規則。");
        var prefix = new StringBuilder();
        var suffix = new StringBuilder();
        int digits = 0;
        int pos = 0;

        void Append(string text)
        {
            if (digits == 0) prefix.Append(text);
            else suffix.Append(text);
        }

        foreach (Match m in Segment.Matches(rule))
        {
            if (m.Index > pos) Append(ReplaceSysParams(rule[pos..m.Index], s => s));   // 括號外的文字當固定文字
            pos = m.Index + m.Length;
            string body = m.Groups[1].Value;
            if (SerialToken.IsMatch(body))
            {
                if (digits > 0) throw new FormatException("編號規則只能有一個流水號片段（[XXX]）。");
                digits = body.Length;
            }
            else
            {
                // 先換登入帳號，帳號裡的 YY、MM、DD 不會被當成日期
                Append(ReplaceSysParams(body, s => DateToken.Replace(s, t => t.Value switch
                {
                    "YYYY" => date.ToString("yyyy"),
                    "YYY" => (date.Year - 1911).ToString("000"),         // 民國年（西元年 - 1911），例如 2025 → 114
                    "YY" => date.ToString("yy"),
                    "MM" => date.ToString("MM"),
                    _ => date.ToString("dd"),
                })));
            }
        }
        if (pos < rule.Length) Append(ReplaceSysParams(rule[pos..], s => s));

        if (digits > 18) throw new FormatException("流水號最多 18 碼。");
        return new Parsed(prefix.ToString(), digits, suffix.ToString());
    }

    /// <summary>
    /// 依規則產生下一個編號：到資料表找符合「前綴 + 流水號 + 後綴」格式的最大流水號，加 1。
    /// 還沒有資料時從 1 開始；流水號超過位數時丟 InvalidOperationException
    /// </summary>
    public static string Next(string connectionString, string fullTableName, string column, string rule, DateTime? today = null)
    {
        var p = Parse(rule, today ?? DateTime.Today);
        if (p.Digits == 0) return p.Prefix;                            // 沒有流水號：規則組出來的文字就是編號
        var d = DbDialect.Of(connectionString);
        string pattern = Escape(p.Prefix, d) + new string('_', p.Digits) + Escape(p.Suffix, d);
        int start = p.Prefix.Length + 1;

        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn);
        cmd.CommandText = MaxSerialSql(d, fullTableName, d.Quote(column));
        d.AddParam(cmd, "start", start);
        d.AddParam(cmd, "digits", p.Digits);
        d.AddParam(cmd, "pattern", pattern);
        object max = cmd.ExecuteScalar();
        long next = (max == null || max == DBNull.Value ? 0 : Convert.ToInt64(max)) + 1;

        string serial = next.ToString().PadLeft(p.Digits, '0');
        if (serial.Length > p.Digits)
            throw new InvalidOperationException($"流水號已經用完（{p.Digits} 碼最大到 {new string('9', p.Digits)}）。");
        return p.Prefix + serial + p.Suffix;
    }

    /// <summary>
    /// 查同樣格式的最大流水號（參數：start = 流水號起始位置、digits = 碼數、pattern = LIKE 樣式）。
    /// char 欄位會補空白，先 RTRIM；流水號位置不是純數字的資料不算
    /// </summary>
    public static string MaxSerialSql(DbDialect d, string fullTableName, string col)
    {
        string trimmed = $"RTRIM({col})", start = d.Param("start"), digits = d.Param("digits"), pattern = d.Param("pattern");
        return d.Kind switch
        {
            DbKind.Oracle => $@"SELECT MAX(TO_NUMBER(SUBSTR({trimmed}, {start}, {digits})))
                                  FROM {fullTableName}
                                 WHERE {trimmed} LIKE {pattern} ESCAPE '!'
                                   AND REGEXP_LIKE(SUBSTR({trimmed}, {start}, {digits}), '^[0-9]+$')",
            DbKind.MySql => $@"SELECT MAX(CAST(SUBSTRING({trimmed}, {start}, {digits}) AS UNSIGNED))
                                 FROM {fullTableName}
                                WHERE {trimmed} LIKE {pattern} ESCAPE '!'
                                  AND SUBSTRING({trimmed}, {start}, {digits}) REGEXP '^[0-9]+$'",
            _ => $@"SELECT MAX(CAST(SUBSTRING({trimmed}, {start}, {digits}) AS bigint))
                      FROM {fullTableName}
                     WHERE {trimmed} LIKE {pattern} ESCAPE '\'
                       AND SUBSTRING({trimmed}, {start}, {digits}) NOT LIKE '%[^0-9]%'",
        };
    }

    /// <summary>
    /// LIKE 的特殊字元加跳脫：SQL Server 搭配 ESCAPE '\'（[ 也是特殊字元）；
    /// Oracle、MySQL 搭配 ESCAPE '!'（MySQL 字串裡的 \ 本身就是跳脫字元；Oracle 跳脫一般字元會出錯）
    /// </summary>
    public static string Escape(string text, DbDialect d) => d.Kind == DbKind.SqlServer
        ? text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[")
        : text.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

    /// <summary>
    /// 新增開始時，把有設編號規則的欄位填上下一個編號（使用者仍可以改）；
    /// 產生失敗（規則錯誤、連線失敗、流水號用完）時顯示訊息，欄位維持空白
    /// </summary>
    public static void FillInputs(Dictionary<FieldDefinition, Control> inputs, string connectionString, string fullTableName)
    {
        var errors = new List<string>();
        foreach (var (f, input) in inputs)
        {
            if (string.IsNullOrWhiteSpace(f.NumberRule)) continue;
            try
            {
                InputHelper.SetValue(input, Next(connectionString, fullTableName, f.Column, f.NumberRule));
            }
            catch (Exception ex)
            {
                errors.Add($"「{f.Label}」：{ex.Message}");
            }
        }
        if (errors.Count > 0)
            MessageBox.Show("自動編號失敗：\n" + string.Join("\n", errors), "提醒");
    }
}
