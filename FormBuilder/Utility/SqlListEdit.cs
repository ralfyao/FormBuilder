using System.Data;

namespace FormBuilder;

/// <summary>
/// SQL 清單的「雙擊編輯資料」：SQL 清單本身唯讀，設了要修改的資料表時，雙擊一列用主鍵對應讀出那張表的那一筆，
/// 開編輯視窗修改後存回那張表
/// </summary>
public static class SqlListEdit
{
    /// <summary>解析「Schema.資料表」（可以有中括號）；沒寫 Schema 時到資料庫找同名的資料表</summary>
    public static (string Schema, string Table) ResolveTable(string connectionString, string text)
    {
        static string Strip(string s) => s.Trim().Trim('[', ']', '"', '`');
        string t = text.Trim();
        int dot = t.IndexOf("].[", StringComparison.Ordinal) is int i and >= 0 ? i + 1 : t.IndexOf('.');
        if (dot > 0) return (Strip(t[..dot]), Strip(t[(dot + 1)..]));
        string name = Strip(t);
        var match = SchemaReader.GetTables(connectionString).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match.Name == null) throw new InvalidOperationException($"資料庫裡找不到資料表「{name}」。");
        return (match.Schema, match.Name);
    }

    /// <summary>資料表的欄位（自動編號、timestamp 只顯示）</summary>
    public static List<GridColumnDefinition> Columns(string connectionString, string schema, string table) =>
        SchemaReader.GetColumns(connectionString, schema, table).Select(c => new GridColumnDefinition
        {
            Column = c.Name,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            IsNullable = c.IsNullable,
            IsIdentity = c.IsIdentity,
            IsKey = c.IsPrimaryKey,
            Header = string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description,
            Width = Math.Clamp(TypeHelper.SuggestWidth(c), 60, 200),
            NoEntry = c.IsIdentity || c.DataType is "timestamp" or "rowversion",
            Visible = true,
        }).ToList();

    /// <summary>
    /// 主鍵對應（清單欄位, 資料表欄位）：有設「主鍵對應」用設定的；沒設就用資料表的主鍵，清單上要有同名的欄位
    /// </summary>
    public static List<(string List, string Table)> Keys(string keyText, List<GridColumnDefinition> columns, DataTable listTable)
    {
        var pairs = GridDefinition.ParseLinks(keyText ?? "");
        if (pairs.Count > 0)
        {
            var missing = pairs.Where(p => !listTable.Columns.Contains(p.Master)).Select(p => p.Master).ToList();
            if (missing.Count > 0) throw new InvalidOperationException("「主鍵對應」的清單欄位，SQL 查詢結果裡沒有：" + string.Join("、", missing));
            return pairs;
        }
        var keys = columns.Where(c => c.IsKey).Select(c => c.Column).ToList();
        if (keys.Count == 0) throw new InvalidOperationException("這張資料表沒有主鍵，請在 SQL 清單的「主鍵對應」設定「清單欄位=資料表欄位」。");
        var absent = keys.Where(k => !listTable.Columns.Contains(k)).ToList();
        if (absent.Count > 0)
            throw new InvalidOperationException($"SQL 查詢結果裡沒有資料表的主鍵欄位：{string.Join("、", absent)}。請把它加進 SELECT，或在「主鍵對應」設定「清單欄位=資料表欄位」。");
        return keys.Select(k => (k, k)).ToList();
    }

    /// <summary>「資料表欄位 = 清單那一列的值」條件，參數名稱 k0、k1…</summary>
    public static string Where(DbDialect d, System.Data.Common.DbCommand cmd, List<(string List, string Table)> keys, DataRow listRow)
    {
        var conditions = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            d.AddParam(cmd, $"k{i}", listRow[keys[i].List]);
            conditions.Add($"{d.Quote(keys[i].Table)} = {d.Param($"k{i}")}");
        }
        return string.Join(" AND ", conditions);
    }
}
