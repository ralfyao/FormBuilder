using Microsoft.Data.SqlClient;

namespace FormBuilder;

/// <summary>讀取 SQL Server 的資料表與欄位結構</summary>
public static class SchemaReader
{
    public static List<(string Schema, string Name)> GetTables(string connectionString)
    {
        var tables = new List<(string, string)>();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"
            SELECT TABLE_SCHEMA, TABLE_NAME
              FROM INFORMATION_SCHEMA.TABLES
             WHERE TABLE_TYPE = 'BASE TABLE'
             ORDER BY TABLE_SCHEMA, TABLE_NAME", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add((reader.GetString(0), reader.GetString(1)));
        return tables;
    }

    public static List<ColumnInfo> GetColumns(string connectionString, string schema, string table)
    {
        var columns = new List<ColumnInfo>();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        // 主鍵來自 TABLE_CONSTRAINTS；自動編號用 COLUMNPROPERTY；欄位說明取 MS_Description 擴充屬性
        using var cmd = new SqlCommand(@"
            SELECT c.COLUMN_NAME,
                   c.DATA_TYPE,
                   ISNULL(c.CHARACTER_MAXIMUM_LENGTH, 0)                          AS MaxLength,
                   CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END               AS IsNullable,
                   ISNULL(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                                         c.COLUMN_NAME, 'IsIdentity'), 0)          AS IsIdentity,
                   CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END               AS IsPrimaryKey,
                   CASE WHEN c.COLUMN_DEFAULT IS NULL THEN 0 ELSE 1 END            AS HasDefault,
                   CAST(ep.value AS nvarchar(200))                                 AS Description
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
             WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
             ORDER BY c.ORDINAL_POSITION", conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                MaxLength = reader.GetInt32(2),
                IsNullable = reader.GetInt32(3) == 1,
                IsIdentity = reader.GetInt32(4) == 1,
                IsPrimaryKey = reader.GetInt32(5) == 1,
                HasDefault = reader.GetInt32(6) == 1,
                Description = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }
        return columns;
    }
}
