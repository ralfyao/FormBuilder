using System.Data;

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
