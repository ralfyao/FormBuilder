using System.Globalization;
using System.Text;

namespace FormBuilder;

/// <summary>
/// 計算欄位的公式：欄位名稱和數字做 + - * /，可以用括號，例如 (達成客戶要求的能力 + 提升經營效能的企圖) * 0.5。
/// 欄位名稱有空白或運算符號時用 [ ] 括起來；欄位沒填當成 0；除以 0 或欄位不是數字時算不出來（回傳 null）。
/// 彙總函數（明細清單的執行後動作用）：SUM、AVG、COUNT、MAX、MIN、STD（樣本標準差）、STDP（母體標準差），
/// 括號裡是明細欄位，例如 SUM(金額) * 1.05。
/// 文字串接（像 Access）：用 &amp; 把文字、欄位、算式接起來，文字用 "雙引號" 或 '單引號' 括起來，例如 100 - 單價1 / 單價 * 100 &amp; "%"；
/// &amp; 的優先順序最低（先算完 + - * / 再接），單獨一個欄位接起來時用它原本的文字（不會當成數字）
/// </summary>
public static class Formula
{
    /// <summary>公式用到的欄位名稱（不重複，照出現順序）；格式不對時丟 FormatException</summary>
    public static List<string> Names(string formula) =>
        Tokenize(formula).Where(t => t.Kind == TokenKind.Name).Select(t => t.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>彙總函數括號裡的明細欄位（不重複，照出現順序）；格式不對時丟 FormatException</summary>
    public static List<string> AggregateColumns(string formula) =>
        Tokenize(formula).Where(t => t.Kind == TokenKind.Arg).Select(t => t.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>支援的彙總函數</summary>
    public static readonly string[] Aggregates = { "SUM", "AVG", "COUNT", "MAX", "MIN", "STD", "STDP" };

    /// <summary>公式有用到彙總函數（SUM、AVG…）：要從明細加總，Grid 同一列的公式不算這種</summary>
    public static bool HasAggregate(string formula)
    {
        try { return Tokenize(formula).Any(t => t.Kind == TokenKind.Func); }
        catch (FormatException) { return false; }
    }

    /// <summary>
    /// 彙總一欄明細的值（空白的列不算，COUNT 算有值的列數）。沒有任何值時 SUM、COUNT = 0，其他算不出來（丟 FormatException）
    /// </summary>
    public static decimal Aggregate(string function, IEnumerable<object> values)
    {
        var numbers = values.Where(v => v is not (null or DBNull) && !(v is string s && s.Trim() == "")).Select(ToNumber).ToList();
        switch (function.ToUpperInvariant())
        {
            case "SUM": return numbers.Sum();
            case "COUNT": return numbers.Count;
        }
        if (numbers.Count == 0) throw new FormatException($"{function} 沒有資料");
        switch (function.ToUpperInvariant())
        {
            case "AVG": return numbers.Average();
            case "MAX": return numbers.Max();
            case "MIN": return numbers.Min();
            case "STD":
            case "STDP":
            {
                bool sample = function.Equals("STD", StringComparison.OrdinalIgnoreCase);
                if (sample && numbers.Count < 2) throw new FormatException("STD 至少要 2 筆");
                decimal avg = numbers.Average();
                double variance = (double)numbers.Sum(n => (n - avg) * (n - avg)) / (sample ? numbers.Count - 1 : numbers.Count);
                return (decimal)Math.Sqrt(variance);
            }
            default: throw new FormatException($"公式不支援 {function}( )");
        }
    }

    /// <summary>文字公式：有用 &amp; 串接或 "引號" 文字，結果是文字（用 EvaluateText 算）</summary>
    public static bool IsText(string formula)
    {
        try { return Tokenize(formula).Any(t => t.Kind == TokenKind.Text || t is { Kind: TokenKind.Op, Text: "&" }); }
        catch (FormatException) { return false; }
    }

    /// <summary>檢查公式：格式不對、用到 knownNames 以外的名稱時回傳錯誤訊息；沒問題回傳 null</summary>
    public static string Check(string formula, IEnumerable<string> knownNames)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        try
        {
            var parser = new Parser(Tokenize(formula), _ => 0m, (_, _) => 1m, _ => "");
            if (IsText(formula)) parser.ParseText(v => v.ToString(CultureInfo.InvariantCulture));
            else parser.Parse();
            var known = new HashSet<string>(knownNames, StringComparer.OrdinalIgnoreCase);
            var unknown = Names(formula).Where(n => !known.Contains(n)).ToList();
            return unknown.Count == 0 ? null : "公式裡這些欄位不在畫面上：" + string.Join("、", unknown);
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// 計算公式；valueOf 取欄位的值（null、DBNull、空白 = 0），aggregateOf(函數, 明細欄位) 算彙總函數（null = 不能用彙總函數）。
    /// 算不出來（格式不對、除以 0、欄位不是數字）回傳 null
    /// </summary>
    public static decimal? Evaluate(string formula, Func<string, object> valueOf, Func<string, string, decimal> aggregateOf = null)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        try
        {
            return new Parser(Tokenize(formula), name => ToNumber(valueOf(name)),
                              aggregateOf ?? ((f, name) => throw new FormatException($"這裡不能用 {f}({name})"))).Parse();
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }

    /// <summary>
    /// 計算文字公式（有 &amp; 串接）：算式的結果照 decimals 格式化（&lt; 0 = 自動），單獨的欄位用它原本的文字（沒填 = 空字串）。
    /// 算不出來回傳 null
    /// </summary>
    public static string EvaluateText(string formula, Func<string, object> valueOf, int decimals, Func<string, string, decimal> aggregateOf = null)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        try
        {
            return new Parser(Tokenize(formula), name => ToNumber(valueOf(name)),
                              aggregateOf ?? ((f, name) => throw new FormatException($"這裡不能用 {f}({name})")),
                              name => ToText(valueOf(name), decimals))
                .ParseText(v => Format(v, decimals));
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }

    /// <summary>欄位值接進文字：沒填 = 空字串，數字照小數位數，日期 yyyy/MM/dd（有時間才帶時間）</summary>
    private static string ToText(object value, int decimals) => value switch
    {
        null or DBNull => "",
        string s => s.TrimEnd(),
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy/MM/dd") : d.ToString("yyyy/MM/dd HH:mm"),
        bool b => b ? "是" : "否",
        decimal or double or float or int or long or short or byte => Format(Convert.ToDecimal(value, CultureInfo.InvariantCulture), decimals),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture),
    };

    /// <summary>欄位值轉成數字：沒填 = 0，勾選框 = 1 / 0，文字要是數字</summary>
    private static decimal ToNumber(object value) => value switch
    {
        null or DBNull => 0m,
        bool b => b ? 1m : 0m,
        string s when s.Trim() == "" => 0m,
        string s => decimal.Parse(s.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture),
        _ => Convert.ToDecimal(value, CultureInfo.CurrentCulture),
    };

    /// <summary>計算結果轉成要顯示的文字：decimals &lt; 0 = 自動（最多 4 位小數，去掉尾端的 0）</summary>
    public static string Format(decimal value, int decimals) =>
        decimals >= 0 ? Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.CurrentCulture)
                      : Math.Round(value, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>Func = 彙總函數（後面接括號）；Arg = 彙總函數括號裡的明細欄位名稱；Text = 引號括起來的文字</summary>
    private enum TokenKind { Number, Name, Op, LParen, RParen, Func, Arg, Text }

    private record Token(TokenKind Kind, string Text);

    private static List<Token> Tokenize(string formula)
    {
        var tokens = new List<Token>();
        string s = formula ?? "";
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if ("+-*/&".Contains(c)) { tokens.Add(new Token(TokenKind.Op, c.ToString())); i++; continue; }
            if (c is '"' or '\'')
            {
                // 文字：同一種引號括起來，裡面連打兩個引號 = 一個引號
                var text = new StringBuilder();
                for (i++; ; i++)
                {
                    if (i >= s.Length) throw new FormatException($"公式的文字少了結尾的 {c}");
                    if (s[i] != c) { text.Append(s[i]); continue; }
                    if (i + 1 < s.Length && s[i + 1] == c) { text.Append(c); i++; continue; }
                    break;
                }
                tokens.Add(new Token(TokenKind.Text, text.ToString()));
                i++;
                continue;
            }
            if (c == '(' || c == '（') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
            if (c == ')' || c == '）') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }
            if (c == '[')
            {
                int end = s.IndexOf(']', i + 1);
                if (end < 0) throw new FormatException("公式的 [ 沒有對應的 ]");
                string name = s[(i + 1)..end].Trim();
                if (name == "") throw new FormatException("公式裡有空的 [ ]");
                tokens.Add(new Token(TokenKind.Name, name));
                i = end + 1;
                continue;
            }
            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(new Token(TokenKind.Number, s[start..i]));
                continue;
            }
            // 欄位名稱：連續到空白、運算符號、括號為止（中文欄位名稱不用括起來）
            var sb = new StringBuilder();
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && !"+-*/&()（）[]\"'".Contains(s[i])) sb.Append(s[i++]);
            tokens.Add(new Token(TokenKind.Name, sb.ToString()));
        }
        // 名稱後面接左括號的是彙總函數，括號裡的是明細欄位
        for (int t = 0; t + 1 < tokens.Count; t++)
        {
            if (tokens[t].Kind != TokenKind.Name || tokens[t + 1].Kind != TokenKind.LParen) continue;
            string function = tokens[t].Text.ToUpperInvariant();
            if (!Aggregates.Contains(function))
                throw new FormatException($"公式不支援「{tokens[t].Text}( )」，可以用：{string.Join("、", Aggregates)}");
            if (t + 3 >= tokens.Count || tokens[t + 2].Kind != TokenKind.Name || tokens[t + 3].Kind != TokenKind.RParen)
                throw new FormatException($"{function} 的寫法是 {function}(明細欄位)，例如 {function}(金額)");
            tokens[t] = new Token(TokenKind.Func, function);
            tokens[t + 2] = new Token(TokenKind.Arg, tokens[t + 2].Text);
        }
        return tokens;
    }

    /// <summary>
    /// 文字 = 片段 (&amp; 片段)*；片段 = "文字" | 單獨的欄位 | 運算式；
    /// 運算式 = 項 (+|- 項)*；項 = 因子 (*|/ 因子)*；因子 = 數字 | 欄位 | 函數(明細欄位) | (運算式) | -因子
    /// </summary>
    private sealed class Parser
    {
        private readonly List<Token> tokens;
        private readonly Func<string, decimal> valueOf;
        private readonly Func<string, string, decimal> aggregateOf;
        private readonly Func<string, string> textOf;
        private int pos;

        /// <param name="textOf">欄位原本的文字（文字公式裡單獨接起來的欄位用）</param>
        public Parser(List<Token> tokens, Func<string, decimal> valueOf, Func<string, string, decimal> aggregateOf, Func<string, string> textOf = null)
        {
            this.tokens = tokens;
            this.valueOf = valueOf;
            this.aggregateOf = aggregateOf;
            this.textOf = textOf ?? (name => valueOf(name).ToString(CultureInfo.CurrentCulture));
        }

        public decimal Parse()
        {
            if (tokens.Count == 0) throw new FormatException("公式是空的");
            decimal value = Expression();
            if (pos < tokens.Count) throw new FormatException($"公式在「{tokens[pos].Text}」附近格式不正確");
            return value;
        }

        /// <param name="format">算式結果轉成文字</param>
        public string ParseText(Func<decimal, string> format)
        {
            if (tokens.Count == 0) throw new FormatException("公式是空的");
            var text = new StringBuilder(Piece(format));
            while (Peek is { Kind: TokenKind.Op, Text: "&" })
            {
                pos++;
                text.Append(Piece(format));
            }
            if (pos < tokens.Count) throw new FormatException($"公式在「{tokens[pos].Text}」附近格式不正確");
            return text.ToString();
        }

        private string Piece(Func<decimal, string> format)
        {
            var t = Peek ?? throw new FormatException("公式不完整（& 後面少了文字或欄位）");
            bool alone = pos + 1 >= tokens.Count || tokens[pos + 1] is { Kind: TokenKind.Op, Text: "&" };
            if (t.Kind == TokenKind.Text && alone) { pos++; return t.Text; }
            if (t.Kind == TokenKind.Name && alone) { pos++; return textOf(t.Text); }
            return format(Expression());
        }

        private Token Peek => pos < tokens.Count ? tokens[pos] : null;

        private decimal Expression()
        {
            decimal value = Term();
            while (Peek is { Kind: TokenKind.Op, Text: "+" or "-" } op)
            {
                pos++;
                decimal right = Term();
                value = op.Text == "+" ? value + right : value - right;
            }
            return value;
        }

        private decimal Term()
        {
            decimal value = Factor();
            while (Peek is { Kind: TokenKind.Op, Text: "*" or "/" } op)
            {
                pos++;
                decimal right = Factor();
                value = op.Text == "*" ? value * right : value / (right == 0 ? 1 : right);// 除以 0 丟 DivideByZeroException -> 改為:如分母0則分母改為1
            }
            return value;
        }

        private decimal Factor()
        {
            var t = Peek ?? throw new FormatException("公式不完整（最後少了數字或欄位）");
            pos++;
            switch (t.Kind)
            {
                case TokenKind.Number:
                    if (!decimal.TryParse(t.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                        throw new FormatException($"公式裡的數字「{t.Text}」格式不正確");
                    return number;
                case TokenKind.Name:
                    return valueOf(t.Text);
                case TokenKind.Func:
                    pos += 3;                                    // (、明細欄位、) 在 Tokenize 檢查過了
                    return aggregateOf(t.Text, tokens[pos - 2].Text);
                case TokenKind.LParen:
                    decimal value = Expression();
                    if (Peek is { Kind: TokenKind.Op, Text: "&" }) throw new FormatException("& 不能放在括號裡，請把括號拿掉（& 本來就最後才接）");
                    if (Peek is not { Kind: TokenKind.RParen }) throw new FormatException("公式的括號沒有成對");
                    pos++;
                    return value;
                case TokenKind.Op when t.Text == "-":
                    return -Factor();
                case TokenKind.Op when t.Text == "+":
                    return Factor();
                case TokenKind.Text:
                    throw new FormatException($"文字「{t.Text}」只能用 & 接起來，不能做 + - * /");
                case TokenKind.Op when t.Text == "&":
                    throw new FormatException("& 要放在兩段文字或算式中間，而且不能放在括號裡");
                default:
                    throw new FormatException($"公式在「{t.Text}」附近格式不正確");
            }
        }
    }
}
