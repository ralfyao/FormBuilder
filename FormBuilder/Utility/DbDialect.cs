using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace FormBuilder;

/// <summary>支援的資料庫類型（畫面定義檔存的是名稱；舊檔沒有這個欄位 = SqlServer）</summary>
public enum DbKind
{
    SqlServer,
    Oracle,
    MySql,
}

/// <summary>
/// 各資料庫的連線與 SQL 方言：名稱引號、參數寫法、取前 n 筆、取回自動編號、日期比對等。
/// 程式各處只傳連線字串，連線字串對應哪一種資料庫由 Register 登記（畫面定義讀進來、連線設定按確定時），
/// 沒登記過的連線字串當成 SQL Server（舊畫面都是 SQL Server）
/// </summary>
public abstract class DbDialect
{
    public static readonly DbDialect SqlServer = new SqlServerDialect();
    public static readonly DbDialect Oracle = new OracleDialect();
    public static readonly DbDialect MySql = new MySqlDialect();

    private static readonly ConcurrentDictionary<string, DbKind> Registered = new();

    public static DbDialect For(DbKind kind) => kind switch
    {
        DbKind.Oracle => Oracle,
        DbKind.MySql => MySql,
        _ => SqlServer,
    };

    /// <summary>登記連線字串是哪一種資料庫</summary>
    public static void Register(string connectionString, DbKind kind)
    {
        if (!string.IsNullOrWhiteSpace(connectionString)) Registered[connectionString] = kind;
    }

    /// <summary>連線字串對應的方言（沒登記過 = SQL Server）</summary>
    public static DbDialect Of(string connectionString) =>
        connectionString != null && Registered.TryGetValue(connectionString, out var kind) ? For(kind) : SqlServer;

    public abstract DbKind Kind { get; }

    /// <summary>連線設定視窗下拉選單顯示的名稱</summary>
    public abstract string DisplayName { get; }

    protected abstract DbProviderFactory Factory { get; }

    // ================= 連線、命令 =================

    /// <summary>開啟連線（失敗時丟例外，連線物件已釋放）</summary>
    public DbConnection Open(string connectionString)
    {
        var conn = Factory.CreateConnection();
        try
        {
            conn.ConnectionString = connectionString;
            conn.Open();
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    /// <summary>建立命令（conn 可以先不給，之後再設定 Connection）；預覽執行（Debug）時包成 DebugCommand，出錯時顯示 SQL</summary>
    public DbCommand CreateCommand(DbConnection conn = null, string sql = null)
    {
        var cmd = NewCommand(conn);
        if (sql != null) cmd.CommandText = sql;
        return SqlDebug.Enabled ? new DebugCommand(cmd) : cmd;
    }

    /// <summary>建立這個資料庫的命令物件</summary>
    protected virtual DbCommand NewCommand(DbConnection conn) => conn != null ? conn.CreateCommand() : Factory.CreateCommand();

    /// <summary>執行查詢，結果放到 DataTable</summary>
    public DataTable Fill(DbCommand cmd)
    {
        var table = new DataTable();
        using var adapter = Factory.CreateDataAdapter();
        // DataAdapter 只收這個資料庫自己的命令型別：Debug 時把真正的命令交給它，執行前先記下 SQL
        var real = cmd is DebugCommand debug ? debug.Inner : cmd;
        SqlDebug.Executing(real);
        adapter.SelectCommand = real;
        adapter.Fill(table);
        return table;
    }

    // ================= 名稱、參數 =================

    /// <summary>資料表、欄位名稱加上引號，避免名稱有空白或保留字</summary>
    public abstract string Quote(string name);

    /// <summary>Schema.資料表（Schema 空白時只有資料表）</summary>
    public string Table(string schema, string name) =>
        string.IsNullOrEmpty(schema) ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";

    /// <summary>SQL 裡參數的前置符號（SQL Server、MySQL 是 @，Oracle 是 :）</summary>
    protected virtual string ParameterPrefix => "@";

    /// <summary>參數在 SQL 裡的寫法，例如 Param("p0") → @p0 或 :p0</summary>
    public string Param(string name) => ParameterPrefix + name;

    /// <summary>加一個參數（name 不含前置符號；null = NULL）</summary>
    public void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = ParameterName(name);
        p.Value = ConvertValue(value ?? DBNull.Value);
        cmd.Parameters.Add(p);
    }

    /// <summary>DbParameter.ParameterName 的寫法</summary>
    protected virtual string ParameterName(string name) => "@" + name;

    /// <summary>參數值轉成資料庫接受的型別（例如 Oracle 沒有 bool）</summary>
    protected virtual object ConvertValue(object value) => value;

    // ================= 方言 =================

    /// <summary>接在 SELECT 後面的「取前 n 筆」（SQL Server 的 TOP n；其他資料庫是空字串）</summary>
    public virtual string Top(int? n) => "";

    /// <summary>接在整句 SQL 最後面的「取前 n 筆」（Oracle 的 FETCH FIRST、MySQL 的 LIMIT；SQL Server 是空字串）</summary>
    public virtual string Limit(int? n) => "";

    /// <summary>FROM 子句裡資料表的別名（Oracle 不能寫 AS）</summary>
    public virtual string TableAlias(string alias) => " AS " + Quote(alias);

    /// <summary>日期時間欄位只取日期部分（查詢條件比對日期用）</summary>
    public abstract string DateOnly(string expression);

    /// <summary>
    /// 執行 INSERT，回傳自動編號欄位的新值（沒有自動編號欄位或取不到時回傳 DBNull）。
    /// insertSql 是單純的 INSERT INTO ... VALUES (...)，參數已加在 cmd 上
    /// </summary>
    public abstract object InsertReturningIdentity(DbCommand cmd, string insertSql, string identityColumn);

    // ================= 使用者寫的 SQL =================

    /// <summary>
    /// 使用者寫的 SQL（按鈕的執行 SQL、自訂查詢）裡用 @欄位名稱 當參數；轉成這個資料庫的參數寫法。
    /// 回傳轉好的 SQL，以及每個參數的（原本的欄位名稱, 要用 AddParam 加的參數名稱）
    /// </summary>
    public virtual (string Sql, List<(string Name, string Parameter)> Parameters) PrepareUserSql(string sql)
    {
        // 參數名稱一律換成 p0、p1…（Oracle 的 :參數不能用中文，MySQL 也比較保險），同名的共用一個
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, string)>();
        var sb = new StringBuilder();
        int i = 0;
        while (i < sql.Length)
        {
            char c = sql[i];
            if (c == '\'' || c == '"' || c == '`')
            {
                int end = SkipQuoted(sql, i, c);
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                int end = sql.IndexOf('\n', i);
                if (end < 0) end = sql.Length;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                int end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? sql.Length : end + 2;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (c == '@' && i + 1 < sql.Length && sql[i + 1] == '@')
            {
                // @@系統變數：原樣保留（連同後面的名稱）
                int end = i + 2;
                while (end < sql.Length && IsNameChar(sql[end])) end++;
                sb.Append(sql, i, end - i);
                i = end;
            }
            else if (c == '@' && i + 1 < sql.Length && IsNameChar(sql[i + 1]))
            {
                int end = i + 1;
                while (end < sql.Length && IsNameChar(sql[end])) end++;
                string name = sql[(i + 1)..end];
                if (!map.TryGetValue(name, out string parameter))
                {
                    parameter = "p" + map.Count;
                    map[name] = parameter;
                    list.Add((name, parameter));
                }
                sb.Append(Param(parameter));
                i = end;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return (sb.ToString(), list);
    }

    /// <summary>字串常值裡反斜線是不是跳脫字元（MySQL 預設是）</summary>
    protected virtual bool BackslashEscapes => false;

    /// <summary>跳過引號包住的字串或名稱，回傳結尾引號後面的位置（兩個引號連寫 = 一個引號字元）</summary>
    private int SkipQuoted(string sql, int start, char quote)
    {
        int i = start + 1;
        while (i < sql.Length)
        {
            if (BackslashEscapes && quote != '`' && sql[i] == '\\') { i += 2; continue; }
            if (sql[i] == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote) { i += 2; continue; }
                return i + 1;
            }
            i++;
        }
        return sql.Length;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>連線設定視窗的下拉選單用</summary>
    public override string ToString() => DisplayName;
}

/// <summary>SQL Server（Microsoft.Data.SqlClient）</summary>
public sealed class SqlServerDialect : DbDialect
{
    public override DbKind Kind => DbKind.SqlServer;
    public override string DisplayName => "SQL Server";
    protected override DbProviderFactory Factory => SqlClientFactory.Instance;

    public override string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
    public override string Top(int? n) => n.HasValue ? $"TOP {n} " : "";
    public override string DateOnly(string expression) => $"CAST({expression} AS date)";

    public override object InsertReturningIdentity(DbCommand cmd, string insertSql, string identityColumn)
    {
        cmd.CommandText = insertSql + "; SELECT CAST(SCOPE_IDENTITY() AS bigint);";
        return cmd.ExecuteScalar();
    }

    /// <summary>SQL Server 直接用 @欄位名稱 當參數名稱（跟原本一樣，不改寫 SQL）</summary>
    public override (string Sql, List<(string Name, string Parameter)> Parameters) PrepareUserSql(string sql) =>
        (sql, ButtonRunner.ParameterNames(sql).Select(n => (n, n)).ToList());
}

/// <summary>Oracle（Oracle.ManagedDataAccess.Core），需要 12c 以上（FETCH FIRST、IDENTITY 欄位）</summary>
public sealed class OracleDialect : DbDialect
{
    public override DbKind Kind => DbKind.Oracle;
    public override string DisplayName => "Oracle";
    protected override DbProviderFactory Factory => OracleClientFactory.Instance;
    protected override string ParameterPrefix => ":";
    protected override string ParameterName(string name) => name;

    /// <summary>ODP.NET 預設依順序對應參數，改成依名稱（同一個參數用兩次、順序不同才不會錯）</summary>
    protected override DbCommand NewCommand(DbConnection conn)
    {
        var cmd = base.NewCommand(conn);
        if (cmd is OracleCommand oc) oc.BindByName = true;
        return cmd;
    }

    protected override object ConvertValue(object value) => value is bool b ? (b ? 1 : 0) : value;

    /// <summary>Oracle 的單一 SQL 結尾不能有分號（PL/SQL 區塊 BEGIN … END; 例外）</summary>
    public override (string Sql, List<(string Name, string Parameter)> Parameters) PrepareUserSql(string sql)
    {
        var (prepared, parameters) = base.PrepareUserSql(sql);
        string trimmed = prepared.Trim();
        bool plsql = trimmed.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase);
        if (!plsql) trimmed = trimmed.TrimEnd(';', ' ', '\t', '\r', '\n');
        return (trimmed, parameters);
    }

    public override string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    public override string Limit(int? n) => n.HasValue ? $" FETCH FIRST {n} ROWS ONLY" : "";
    public override string TableAlias(string alias) => " " + Quote(alias);
    public override string DateOnly(string expression) => $"TRUNC({expression})";

    public override object InsertReturningIdentity(DbCommand cmd, string insertSql, string identityColumn)
    {
        if (identityColumn == null)
        {
            cmd.CommandText = insertSql;
            cmd.ExecuteNonQuery();
            return DBNull.Value;
        }
        // 用 RETURNING 取回 IDENTITY 欄位（或觸發程序用序列填的值）
        var output = new OracleParameter("new_identity", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(output);
        cmd.CommandText = $"{insertSql} RETURNING {Quote(identityColumn)} INTO :new_identity";
        cmd.ExecuteNonQuery();
        cmd.Parameters.Remove(output);
        return output.Value switch
        {
            OracleDecimal d when d.IsNull => DBNull.Value,
            OracleDecimal d => d.ToInt64(),
            null => DBNull.Value,
            var v => v,
        };
    }
}

/// <summary>MySQL / MariaDB（MySqlConnector）</summary>
public sealed class MySqlDialect : DbDialect
{
    public override DbKind Kind => DbKind.MySql;
    public override string DisplayName => "MySQL";
    protected override DbProviderFactory Factory => MySqlConnectorFactory.Instance;
    protected override bool BackslashEscapes => true;

    public override string Quote(string name) => "`" + name.Replace("`", "``") + "`";
    public override string Limit(int? n) => n.HasValue ? $" LIMIT {n}" : "";
    public override string DateOnly(string expression) => $"DATE({expression})";

    public override object InsertReturningIdentity(DbCommand cmd, string insertSql, string identityColumn)
    {
        cmd.CommandText = insertSql;
        cmd.ExecuteNonQuery();
        if (identityColumn == null) return DBNull.Value;
        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT LAST_INSERT_ID()";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
