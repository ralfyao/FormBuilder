using System.Data.Common;

namespace FormBuilder;

/// <summary>
/// 讀取資料表與欄位結構（SQL Server、Oracle、MySQL）。
/// 欄位型別統一轉成 SQL Server 的型別名稱（int、nvarchar、datetime…），畫面其他地方（TypeHelper）只要認一套
/// </summary>
public static class SchemaReader
{
    public static List<(string Schema, string Name)> GetTables(string connectionString)
    {
        var d = DbDialect.Of(connectionString);
        var tables = new List<(string, string)>();
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn, d.Kind switch
        {
            // Oracle：Schema = 擁有者；系統帳號（ORACLE_MAINTAINED）的資料表不列
            DbKind.Oracle => @"
                SELECT t.OWNER, t.TABLE_NAME
                  FROM ALL_TABLES t
                  JOIN ALL_USERS u ON u.USERNAME = t.OWNER
                 WHERE u.ORACLE_MAINTAINED = 'N'
                   AND t.NESTED = 'NO'
                   AND t.SECONDARY = 'N'
                   AND t.TABLE_NAME NOT LIKE 'BIN$%'
                 ORDER BY t.OWNER, t.TABLE_NAME",
            // MySQL：Schema = 資料庫，只列連線指定的資料庫
            DbKind.MySql => @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                  FROM information_schema.TABLES
                 WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = DATABASE()
                 ORDER BY TABLE_SCHEMA, TABLE_NAME",
            _ => @"
                SELECT TABLE_SCHEMA, TABLE_NAME
                  FROM INFORMATION_SCHEMA.TABLES
                 WHERE TABLE_TYPE = 'BASE TABLE'
                 ORDER BY TABLE_SCHEMA, TABLE_NAME",
        });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add((Convert.ToString(reader.GetValue(0)), Convert.ToString(reader.GetValue(1))));
        return tables;
    }

    public static List<ColumnInfo> GetColumns(string connectionString, string schema, string table)
    {
        var d = DbDialect.Of(connectionString);
        var columns = new List<ColumnInfo>();
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn, d.Kind switch
        {
            DbKind.Oracle => OracleColumnsSql,
            DbKind.MySql => MySqlColumnsSql,
            _ => SqlServerColumnsSql,
        });
        d.AddParam(cmd, "schema_name", schema);
        d.AddParam(cmd, "table_name", table);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string dataType = Text(reader, 1);
            int? precision = Int(reader, 8);
            int? scale = Int(reader, 9);
            string columnType = Text(reader, 10);
            long maxLength = Int64(reader, 2);
            columns.Add(new ColumnInfo
            {
                Name = Text(reader, 0),
                DataType = d.Kind switch
                {
                    DbKind.Oracle => OracleType(dataType, precision, scale),
                    DbKind.MySql => MySqlType(dataType, columnType),
                    _ => dataType,
                },
                MaxLength = maxLength > int.MaxValue ? -1 : (int)maxLength,     // MySQL longtext 這類超過 int 的當成 max
                IsNullable = Int64(reader, 3) == 1,
                IsIdentity = Int64(reader, 4) == 1,
                IsPrimaryKey = Int64(reader, 5) == 1,
                HasDefault = Int64(reader, 6) == 1,
                Description = reader.IsDBNull(7) || Text(reader, 7) == "" ? null : Text(reader, 7),
            });
        }
        return columns;
    }

    // 三種資料庫查出來的欄位順序一樣：
    // 0 名稱、1 型別、2 長度、3 可 NULL、4 自動編號、5 主鍵、6 有預設值、7 說明、8 精確度、9 小數位數、10 完整型別（MySQL 用）

    /// <summary>SQL Server：主鍵來自 TABLE_CONSTRAINTS；自動編號用 COLUMNPROPERTY；欄位說明取 MS_Description 擴充屬性</summary>
    private const string SqlServerColumnsSql = @"
            SELECT c.COLUMN_NAME,
                   c.DATA_TYPE,
                   ISNULL(c.CHARACTER_MAXIMUM_LENGTH, 0)                          AS MaxLength,
                   CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END               AS IsNullable,
                   ISNULL(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                         c.COLUMN_NAME, 'IsIdentity'), 0)          AS IsIdentity,
                   CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END               AS IsPrimaryKey,
                   CASE WHEN c.COLUMN_DEFAULT IS NULL THEN 0 ELSE 1 END            AS HasDefault,
                   CAST(ep.value AS nvarchar(200))                                 AS Description,
                   c.NUMERIC_PRECISION,
                   c.NUMERIC_SCALE,
                   NULL                                                            AS ColumnType
              FROM INFORMATION_SCHEMA.COLUMNS c
              LEFT JOIN (SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                           FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                           JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                             ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                            AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                            AND tc.TABLE_NAME = ku.TABLE_NAME
                          WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY') k
                ON k.TABLE_SCHEMA = c.TABLE_SCHEMA AND k.TABLE_NAME = c.TABLE_NAME AND k.COLUMN_NAME = c.COLUMN_NAME
              LEFT JOIN sys.extended_properties ep
                ON ep.major_id = OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME))
               AND ep.minor_id = COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                                c.COLUMN_NAME, 'ColumnId')
               AND ep.class = 1
               AND ep.name = 'MS_Description'
             WHERE c.TABLE_SCHEMA = @schema_name AND c.TABLE_NAME = @table_name
             ORDER BY c.ORDINAL_POSITION";

    /// <summary>
    /// Oracle：ALL_TAB_COLUMNS；主鍵來自 ALL_CONSTRAINTS（P）；自動編號 = 12c 的 IDENTITY 欄位；
    /// 預設值用 DEFAULT_LENGTH 判斷（DATA_DEFAULT 是 LONG，不能放在運算式裡）；說明取 ALL_COL_COMMENTS
    /// </summary>
    private const string OracleColumnsSql = @"
            SELECT c.COLUMN_NAME,
                   c.DATA_TYPE,
                   CASE WHEN c.DATA_TYPE IN ('CHAR', 'NCHAR', 'VARCHAR2', 'NVARCHAR2') THEN c.CHAR_LENGTH
                        WHEN c.DATA_TYPE IN ('CLOB', 'NCLOB') THEN -1 ELSE 0 END    AS MaxLength,
                   CASE WHEN c.NULLABLE = 'Y' THEN 1 ELSE 0 END                     AS IsNullable,
                   CASE WHEN c.IDENTITY_COLUMN = 'YES' THEN 1 ELSE 0 END            AS IsIdentity,
                   CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END                AS IsPrimaryKey,
                   CASE WHEN NVL(c.DEFAULT_LENGTH, 0) > 0 THEN 1 ELSE 0 END         AS HasDefault,
                   cm.COMMENTS                                                      AS Description,
                   c.DATA_PRECISION,
                   c.DATA_SCALE,
                   NULL                                                             AS ColumnType
              FROM ALL_TAB_COLUMNS c
              LEFT JOIN (SELECT cc.OWNER, cc.TABLE_NAME, cc.COLUMN_NAME
                           FROM ALL_CONSTRAINTS ac
                           JOIN ALL_CONS_COLUMNS cc
                             ON cc.OWNER = ac.OWNER AND cc.CONSTRAINT_NAME = ac.CONSTRAINT_NAME
                          WHERE ac.CONSTRAINT_TYPE = 'P') k
                ON k.OWNER = c.OWNER AND k.TABLE_NAME = c.TABLE_NAME AND k.COLUMN_NAME = c.COLUMN_NAME
              LEFT JOIN ALL_COL_COMMENTS cm
                ON cm.OWNER = c.OWNER AND cm.TABLE_NAME = c.TABLE_NAME AND cm.COLUMN_NAME = c.COLUMN_NAME
             WHERE c.OWNER = :schema_name AND c.TABLE_NAME = :table_name
             ORDER BY c.COLUMN_ID";

    /// <summary>MySQL：information_schema.COLUMNS（主鍵看 COLUMN_KEY，自動編號看 EXTRA 的 auto_increment）</summary>
    private const string MySqlColumnsSql = @"
            SELECT COLUMN_NAME,
                   DATA_TYPE,
                   IFNULL(CHARACTER_MAXIMUM_LENGTH, 0)                              AS MaxLength,
                   CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END                  AS IsNullable,
                   CASE WHEN EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END        AS IsIdentity,
                   CASE WHEN COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END                   AS IsPrimaryKey,
                   CASE WHEN COLUMN_DEFAULT IS NULL THEN 0 ELSE 1 END               AS HasDefault,
                   COLUMN_COMMENT                                                   AS Description,
                   NUMERIC_PRECISION,
                   NUMERIC_SCALE,
                   COLUMN_TYPE
              FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @schema_name AND TABLE_NAME = @table_name
             ORDER BY ORDINAL_POSITION";

    /// <summary>Oracle 型別轉成 SQL Server 的型別名稱</summary>
    public static string OracleType(string type, int? precision, int? scale)
    {
        type = (type ?? "").ToUpperInvariant();
        if (type.StartsWith("TIMESTAMP")) return "datetime2";
        if (type.StartsWith("INTERVAL DAY")) return "time";
        return type switch
        {
            // NUMBER 沒有小數位數（含 INTEGER）當整數；位數太多或有小數當 decimal
            "NUMBER" => scale == 0 && (precision == null || precision <= 18) ? "bigint" : "decimal",
            "FLOAT" or "BINARY_DOUBLE" => "float",
            "BINARY_FLOAT" => "real",
            "VARCHAR2" or "VARCHAR" => "varchar",
            "NVARCHAR2" => "nvarchar",
            "CHAR" => "char",
            "NCHAR" => "nchar",
            "CLOB" or "LONG" => "text",
            "NCLOB" => "ntext",
            "DATE" => "datetime",              // Oracle 的 DATE 含時間
            _ => type.ToLowerInvariant(),
        };
    }

    /// <summary>MySQL 型別轉成 SQL Server 的型別名稱（tinyint(1) 是 MySQL 的布林）</summary>
    public static string MySqlType(string type, string columnType)
    {
        type = (type ?? "").ToLowerInvariant();
        if (type == "tinyint" && (columnType ?? "").StartsWith("tinyint(1)", StringComparison.OrdinalIgnoreCase)) return "bit";
        return type switch
        {
            "tinyint" => "tinyint",
            "mediumint" or "year" => "int",
            "double" => "float",
            "float" => "real",
            "timestamp" => "datetime",
            "tinytext" or "mediumtext" or "longtext" => "text",
            "boolean" or "bool" => "bit",
            _ => type,
        };
    }

    private static string Text(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
    private static long Int64(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));
    private static int? Int(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
}
