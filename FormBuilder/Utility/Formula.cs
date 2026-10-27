using System.Globalization;
using System.Text;

namespace FormBuilder;

/// <summary>
/// 計算欄位的公式：欄位名稱和數字做 + - * /，可以用括號，例如 (達成客戶要求的能力 + 提升經營效能的企圖) * 0.5。
/// 欄位名稱有空白或運算符號時用 [ ] 括起來；欄位沒填當成 0；除以 0 或欄位不是數字時算不出來（回傳 null）
/// </summary>
public static class Formula
{
    /// <summary>公式用到的欄位名稱（不重複，照出現順序）；格式不對時丟 FormatException</summary>
    public static List<string> Names(string formula) =>
        Tokenize(formula).Where(t => t.Kind == TokenKind.Name).Select(t => t.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>檢查公式：格式不對、用到 knownNames 以外的名稱時回傳錯誤訊息；沒問題回傳 null</summary>
    public static string Check(string formula, IEnumerable<string> knownNames)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        try
        {
            new Parser(Tokenize(formula), _ => 0m).Parse();
            var known = new HashSet<string>(knownNames, StringComparer.OrdinalIgnoreCase);
            var unknown = Names(formula).Where(n => !known.Contains(n)).ToList();
            return unknown.Count == 0 ? null : "公式裡這些欄位不在畫面上：" + string.Join("、", unknown);
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>計算公式；valueOf 取欄位的值（null、DBNull、空白 = 0）。算不出來（格式不對、除以 0、欄位不是數字）回傳 null</summary>
    public static decimal? Evaluate(string formula, Func<string, object> valueOf)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        try
        {
            return new Parser(Tokenize(formula), name => ToNumber(valueOf(name))).Parse();
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or OverflowException or InvalidCastException)
        {
            return null;
        }
    }

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

    private enum TokenKind { Number, Name, Op, LParen, RParen }

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
            if ("+-*/".Contains(c)) { tokens.Add(new Token(TokenKind.Op, c.ToString())); i++; continue; }
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
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && !"+-*/()（）[]".Contains(s[i])) sb.Append(s[i++]);
            tokens.Add(new Token(TokenKind.Name, sb.ToString()));
        }
        return tokens;
    }

    /// <summary>運算式 = 項 (+|- 項)*；項 = 因子 (*|/ 因子)*；因子 = 數字 | 欄位 | (運算式) | -因子</summary>
    private sealed class Parser
    {
        private readonly List<Token> tokens;
        private readonly Func<string, decimal> valueOf;
        private int pos;

        public Parser(List<Token> tokens, Func<string, decimal> valueOf)
        {
            this.tokens = tokens;
            this.valueOf = valueOf;
        }

        public decimal Parse()
        {
            if (tokens.Count == 0) throw new FormatException("公式是空的");
            decimal value = Expression();
            if (pos < tokens.Count) throw new FormatException($"公式在「{tokens[pos].Text}」附近格式不正確");
            return value;
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
                value = op.Text == "*" ? value * right : value / right;     // 除以 0 丟 DivideByZeroException
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
                case TokenKind.LParen:
                    decimal value = Expression();
                    if (Peek is not { Kind: TokenKind.RParen }) throw new FormatException("公式的括號沒有成對");
                    pos++;
                    return value;
                case TokenKind.Op when t.Text == "-":
                    return -Factor();
                case TokenKind.Op when t.Text == "+":
                    return Factor();
                default:
                    throw new FormatException($"公式在「{t.Text}」附近格式不正確");
            }
        }
    }
}
