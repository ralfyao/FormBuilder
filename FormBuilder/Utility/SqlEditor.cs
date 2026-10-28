using System.ComponentModel;
using System.Drawing.Design;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms.Design;

namespace FormBuilder;

/// <summary>
/// 寫 SQL 用的文字框：關鍵字、函數、字串、註解、@參數用不同顏色顯示。
/// 打開時（設定 Text）就上色，之後每次輸入、貼上也重新上色；貼上只取純文字
/// </summary>
public class SqlTextBox : RichTextBox
{
    private static readonly Color KeywordColor = Color.Blue;
    private static readonly Color FunctionColor = Color.Magenta;
    private static readonly Color StringColor = Color.FromArgb(163, 21, 21);
    private static readonly Color CommentColor = Color.Green;
    private static readonly Color ParameterColor = Color.FromArgb(0, 128, 128);

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "IS", "NULL", "LIKE", "AS", "ON", "JOIN", "LEFT", "RIGHT",
        "INNER", "OUTER", "FULL", "CROSS", "APPLY", "GROUP", "BY", "ORDER", "HAVING", "DISTINCT", "TOP", "INSERT", "INTO",
        "VALUES", "UPDATE", "SET", "DELETE", "CASE", "WHEN", "THEN", "ELSE", "END", "EXISTS", "BETWEEN", "UNION", "ALL",
        "ASC", "DESC", "WITH", "OVER", "PARTITION", "LIMIT", "OFFSET", "FETCH", "NEXT", "FIRST", "ROWS", "ROW", "ONLY",
        "BEGIN", "COMMIT", "ROLLBACK", "TRAN", "TRANSACTION", "DECLARE", "IF", "EXEC", "EXECUTE", "RETURNING", "ESCAPE",
        "INT", "BIGINT", "NVARCHAR", "VARCHAR", "DECIMAL", "DATE", "DATETIME", "BIT", "MERGE", "USING", "MATCHED", "OUTPUT",
    };

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "SUM", "MAX", "MIN", "AVG", "ISNULL", "COALESCE", "NULLIF", "CAST", "CONVERT", "TRY_CONVERT", "TRY_CAST",
        "FORMAT", "SUBSTRING", "SUBSTR", "RTRIM", "LTRIM", "TRIM", "UPPER", "LOWER", "LEN", "LENGTH", "REPLACE", "CHARINDEX",
        "CONCAT", "GETDATE", "SYSDATETIME", "DATEADD", "DATEDIFF", "DATEPART", "YEAR", "MONTH", "DAY", "ROUND", "ABS",
        "NVL", "TO_CHAR", "TO_DATE", "TO_NUMBER", "IFNULL", "NOW", "ROW_NUMBER", "RANK", "STUFF", "ISNUMERIC", "IIF",
    };

    // 註解、字串、[名稱]、@參數、字詞；依序比對，先比對到的優先（字串裡的關鍵字不上色）
    private static readonly Regex Tokens = new(
        @"(?<comment>--[^\n]*|/\*.*?(\*/|$))|(?<string>N?'([^']|'')*('|$))|(?<bracket>\[[^\]\n]*\]?)|(?<param>(?<!@)@[\p{L}\p{N}_]+)|(?<word>[\p{L}_][\p{L}\p{N}_]*)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private bool coloring;

    public SqlTextBox()
    {
        AcceptsTab = true;
        DetectUrls = false;
        WordWrap = false;
        ScrollBars = RichTextBoxScrollBars.Both;
        Font = new Font("Consolas", 10F);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Colorize();                       // 打開時已經有的 SQL 也要上色
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (!coloring) Colorize();
    }

    /// <summary>Ctrl+V、Shift+Insert 只貼純文字，才不會把別處的字型、顏色貼進來</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is (Keys.Control | Keys.V) or (Keys.Shift | Keys.Insert))
        {
            if (Clipboard.ContainsText()) SelectedText = Clipboard.GetText().Replace("\r\n", "\n");
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    private const int WM_SETREDRAW = 0x000B;

    /// <summary>整段重新上色（保留游標位置與捲動位置，畫面不閃）</summary>
    public void Colorize()
    {
        if (!IsHandleCreated || coloring) return;
        coloring = true;
        int start = SelectionStart, length = SelectionLength;
        SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        try
        {
            string text = Text;                       // RichTextBox 的換行是 \n，位置跟 Select 一致
            SelectAll();
            SelectionColor = ForeColor;
            foreach (Match m in Tokens.Matches(text))
            {
                Color? color =
                    m.Groups["comment"].Success ? CommentColor :
                    m.Groups["string"].Success ? StringColor :
                    m.Groups["param"].Success ? ParameterColor :
                    m.Groups["word"].Success && Keywords.Contains(m.Value) ? KeywordColor :
                    m.Groups["word"].Success && Functions.Contains(m.Value) ? FunctionColor :
                    null;
                if (color == null) continue;
                Select(m.Index, m.Length);
                SelectionColor = color.Value;
            }
            Select(start, length);
            SelectionColor = ForeColor;               // 接著打的字用一般顏色，打完會再重新上色
        }
        finally
        {
            SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            Invalidate();
            coloring = false;
        }
    }
}

/// <summary>
/// 屬性面板的 SQL 編輯器（取代 MultilineStringEditor）：按右邊的下拉箭頭打開可以拉大的 SQL 文字框，
/// 每次打開都會把關鍵字上色；點旁邊或按 Esc 以外的方式關閉就寫回（Esc = 不改）
/// </summary>
public class SqlEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;

    public override bool IsDropDownResizable => true;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (provider?.GetService(typeof(IWindowsFormsEditorService)) is not IWindowsFormsEditorService service) return value;
        string original = (value as string ?? "").Replace("\r\n", "\n");
        bool cancelled = false;
        using var box = new SqlTextBox { Text = original, Size = new Size(460, 220), BorderStyle = BorderStyle.None };
        box.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            cancelled = true;
            service.CloseDropDown();
        };
        service.DropDownControl(box);
        if (cancelled || box.Text == original) return value;
        // 換行存成 \r\n（跟原本的多行文字編輯器一樣）；清空 = null
        return box.Text.Trim().Length == 0 ? null : box.Text.Replace("\n", "\r\n");
    }
}
