using System.Data;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// SQL 清單：資料來自後台寫的 SELECT（唯讀）。SQL 裡用 @欄位名稱 取單頭目前這筆的值，
/// 例如 SELECT 品項編號, 單價 FROM B廠商供料 WHERE 廠商編號 = @廠商編號
/// </summary>
public static class SqlList
{
    /// <summary>讀 SQL 結果的欄位（只取結構，不讀資料；@參數都當 NULL）</summary>
    public static List<GridColumnDefinition> ReadColumns(string connectionString, string sql)
    {
        var d = DbDialect.Of(connectionString);
        var (prepared, parameters) = d.PrepareUserSql(sql);
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn, prepared);
        foreach (var (_, parameter) in parameters) d.AddParam(cmd, parameter, DBNull.Value);
        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
        var columns = new List<GridColumnDefinition>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            if (string.IsNullOrWhiteSpace(name) || columns.Any(c => c.Column.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            var type = reader.GetFieldType(i);
            columns.Add(new GridColumnDefinition
            {
                Column = name,
                Header = name,
                DataType = TypeName(type),
                IsNullable = true,
                NoEntry = true,
                Width = type == typeof(string) ? 140 : 90,
            });
        }
        return columns;
    }

    /// <summary>執行 SQL 讀出資料；@參數的值由 valueOf 提供（沒有 = NULL）</summary>
    public static DataTable Load(string connectionString, string sql, Func<string, object> valueOf)
    {
        valueOf = SysParams.With(valueOf);                    // @登入帳號、@登入姓名
        var d = DbDialect.Of(connectionString);
        var (prepared, parameters) = d.PrepareUserSql(sql);
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn, prepared);
        foreach (var (name, parameter) in parameters) d.AddParam(cmd, parameter, valueOf(name) ?? DBNull.Value);
        return d.Fill(cmd);
    }

    /// <summary>
    /// SQL 清單有 JOIN 其他資料表時：自己寫的 SQL 當子查詢（別名 T0），外層 LEFT JOIN 關聯資料表帶出 JOIN 欄位；
    /// 沒有 JOIN 欄位就是原本的 SQL。@參數照常使用
    /// </summary>
    public static string Compose(string connectionString, string sql, IList<GridColumnDefinition> columns, IList<GridJoinDefinition> joins)
    {
        if (joins == null || joins.Count == 0 || !columns.Any(c => c.IsJoin)) return sql;
        var d = DbDialect.Of(connectionString);
        var select = columns.Where(c => !c.ScreenOnly && (!c.IsJoin || joins.Any(j => j.Alias == c.JoinAlias))).ToList();
        return GridSql.Select(d, Subquery(d, sql), select, joins);
    }

    /// <summary>
    /// 自己寫的 SELECT 包成子查詢「( … )」，外層可以再接別名、JOIN、WHERE。
    /// SQL Server 的子查詢不能有 ORDER BY（除非有 TOP 或 OFFSET）：加上 OFFSET 0 ROWS；WITH 開頭的不能包，丟 InvalidOperationException
    /// </summary>
    public static string Subquery(DbDialect d, string sql)
    {
        string inner = sql.Trim().TrimEnd(';').TrimEnd();
        if (Regex.IsMatch(inner, @"^\s*WITH\b", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("SQL 用 WITH 開頭時不能當子查詢（外層要再 JOIN 或加查詢條件），請改寫成 SELECT 開頭。");
        string outer = TopLevel(inner);
        if (d.Kind == DbKind.SqlServer && Regex.IsMatch(outer, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(outer, @"^\s*SELECT\s+(ALL\s+|DISTINCT\s+)?TOP\b", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(outer, @"\bOFFSET\b", RegexOptions.IgnoreCase))
            inner += "\nOFFSET 0 ROWS";
        return "(" + inner + "\n)";                    // 換行再括號：SQL 最後一行是 -- 註解時才不會把括號註解掉
    }

    /// <summary>SQL 最外層的文字：括號、字串、[識別名稱]、註解裡的內容換成空白（找最外層的 ORDER BY、TOP 用）</summary>
    private static string TopLevel(string sql)
    {
        var sb = new System.Text.StringBuilder(sql.Length);
        int depth = 0;
        for (int i = 0; i < sql.Length; i++)
        {
            char ch = sql[i];
            if (ch == '\'' || ch == '[' || ch == '"')
            {
                char close = ch == '[' ? ']' : ch;
                int end = sql.IndexOf(close, i + 1);
                if (end < 0) end = sql.Length - 1;
                sb.Append(' ', end - i + 1);
                i = end;
                continue;
            }
            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                int end = sql.IndexOf('\n', i);
                if (end < 0) end = sql.Length - 1;
                sb.Append(' ', end - i + 1);
                i = end;
                continue;
            }
            if (ch == '(') depth++;
            sb.Append(depth > 0 ? ' ' : ch);
            if (ch == ')' && depth > 0) depth--;
        }
        return sb.ToString();
    }

    /// <summary>.NET 型別 → Grid 欄位用的資料型別名稱（決定顯示方式：日期、數字、勾選框）</summary>
    private static string TypeName(Type type) =>
        type == typeof(DateTime) ? "datetime"
        : type == typeof(bool) ? "bit"
        : type == typeof(int) || type == typeof(short) || type == typeof(byte) ? "int"
        : type == typeof(long) ? "bigint"
        : type == typeof(decimal) ? "decimal"
        : type == typeof(double) || type == typeof(float) ? "float"
        : "nvarchar";
}
