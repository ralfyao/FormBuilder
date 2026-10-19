using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormBuilder;

/// <summary>主鍵欄位：欄位名稱，以及是否為自動編號（新增後要用 SCOPE_IDENTITY 取回）</summary>
public record KeyColumn(string Column, bool IsIdentity);

/// <summary>一個畫面的定義：對應一個資料表，存成 JSON 檔（類似 4GL 的 .per）</summary>
public class FormDefinition
{
    /// <summary>畫面定義檔預設放在執行檔旁的 Forms 資料夾</summary>
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, "Forms");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 中文直接存成中文，不轉成 \uXXXX
    };

    [Category("畫面"), DisplayName("畫面標題")]
    public string Title { get; set; } = "新畫面";

    [Category("畫面"), DisplayName("寬度")]
    public int Width { get; set; } = 800;

    [Category("畫面"), DisplayName("高度")]
    public int Height { get; set; } = 500;

    [Category("資料"), DisplayName("連線字串"), ReadOnly(true)]
    public string ConnectionString { get; set; }

    [Category("資料"), DisplayName("Schema"), ReadOnly(true)]
    public string TableSchema { get; set; }

    [Category("資料"), DisplayName("資料表"), ReadOnly(true)]
    public string TableName { get; set; }

    [Browsable(false)]
    public List<FieldDefinition> Fields { get; set; } = new();

    /// <summary>單身 Grid（目前一個畫面只放一個）</summary>
    [Browsable(false)]
    public List<GridDefinition> Grids { get; set; } = new();

    /// <summary>畫面上的按鈕（從工具箱拖進來的）</summary>
    [Browsable(false)]
    public List<ButtonDefinition> Buttons { get; set; } = new();

    /// <summary>這個定義是從哪個檔案讀進來的（「開啟畫面」按鈕用來找同資料夾的其他畫面）</summary>
    [Browsable(false), JsonIgnore]
    public string SourcePath { get; set; }

    [Browsable(false), JsonIgnore]
    public string FullTableName => $"{SqlName.Quote(TableSchema)}.{SqlName.Quote(TableName)}";

    /// <summary>
    /// 單頭主鍵：上方單頭欄位勾了主鍵的，加上單頭清單 Grid 裡的主鍵欄位。
    /// 主鍵（例如自動編號的識別碼）不一定會放在上方欄位，只放在清單裡也要算
    /// </summary>
    [Browsable(false), JsonIgnore]
    public List<KeyColumn> MasterKeys =>
        Fields.Where(f => f.IsKey).Select(f => new KeyColumn(f.Column, f.IsIdentity))
            .Concat(Grids.Where(g => g.IsMasterList).SelectMany(g => g.Columns).Where(c => c.IsKey).Select(c => new KeyColumn(c.Column, c.IsIdentity)))
            .DistinctBy(k => k.Column, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static FormDefinition Load(string path)
    {
        var def = JsonSerializer.Deserialize<FormDefinition>(File.ReadAllText(path), JsonOptions);
        def.SourcePath = Path.GetFullPath(path);
        return def;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}

/// <summary>畫面上的一個欄位：對應資料表的一個欄位</summary>
public class FieldDefinition
{
    [Category("資料"), DisplayName("欄位名稱"), ReadOnly(true)]
    public string Column { get; set; }

    [Category("資料"), DisplayName("資料型別"), ReadOnly(true)]
    public string DataType { get; set; }

    [Category("資料"), DisplayName("最大長度"), ReadOnly(true), Description("-1 表示 max")]
    public int MaxLength { get; set; }

    [Category("資料"), DisplayName("允許 NULL"), ReadOnly(true)]
    public bool IsNullable { get; set; }

    [Category("資料"), DisplayName("自動編號"), ReadOnly(true)]
    public bool IsIdentity { get; set; }

    [Category("資料"), DisplayName("主鍵"), Description("修改、刪除時用來找到這筆資料的欄位；沒有主鍵的畫面只能新增")]
    public bool IsKey { get; set; }

    [Category("畫面"), DisplayName("標籤")]
    public string Label { get; set; }

    [Category("畫面"), DisplayName("X")]
    public int X { get; set; }

    [Category("畫面"), DisplayName("Y")]
    public int Y { get; set; }

    [Category("畫面"), DisplayName("輸入框寬度")]
    public int Width { get; set; } = 160;

    [Category("畫面"), DisplayName("標籤寬度"), Description("輸入框左邊標籤區的寬度；設計畫面上拖欄位的左邊緣也可以調整")]
    public int LabelWidth { get; set; } = FieldBox.DefaultLabelWidth;

    [Category("畫面"), DisplayName("提示文字 (Placeholder)"), Description("輸入框空白時顯示的灰色提示文字（文字、數字欄位才有）")]
    public string Placeholder { get; set; }

    [Category("下拉選單"), DisplayName("輸入方式"), Description("自動：依資料型別用文字框、勾選框或日期；下拉選單：從選項中挑選")]
    public InputKind InputType { get; set; } = InputKind.自動;

    [Category("下拉選單"), DisplayName("固定選項"),
     Description("用逗號或換行分隔；值和顯示文字不同時寫「值=顯示」，例如：Y=是,N=否"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Items { get; set; }

    [Category("下拉選單"), DisplayName("選項 SQL"),
     Description("從資料庫取選項：第一欄是值，第二欄（可省略）是顯示文字，例如：SELECT 代碼, 名稱 FROM 幣別"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string ItemsSql { get; set; }

    [Category("下拉選單"), DisplayName("可自行輸入"), Description("True：選項以外的值也可以直接打字輸入")]
    public bool AllowFreeText { get; set; }

    [Category("畫面"), DisplayName("只顯示（NOENTRY）"), Description("使用者不能輸入，存檔時也不會寫回資料庫")]
    public bool NoEntry { get; set; }
}

/// <summary>
/// 畫面上的 Grid，兩種用途：
/// 單身：對應另一個資料表，依關聯欄位只列出目前單頭的明細（類似 4GL t 類程式的單身）；
/// 單頭清單：對應單頭自己的資料表，列出查詢結果，點選一列就切換單頭
/// </summary>
public class GridDefinition
{
    [Category("資料"), DisplayName("單頭清單"), ReadOnly(true), Description("True = 列出單頭自己的查詢結果；False = 單身明細")]
    public bool IsMasterList { get; set; }

    [Category("資料"), DisplayName("Schema"), ReadOnly(true)]
    public string TableSchema { get; set; }

    [Category("資料"), DisplayName("單身資料表"), ReadOnly(true)]
    public string TableName { get; set; }

    [Category("資料"), DisplayName("關聯欄位"), Description("單頭欄位=單身欄位，多組用逗號分隔，例如：單號=單號")]
    public string LinkText { get; set; } = "";

    [Category("畫面"), DisplayName("標題")]
    public string Title { get; set; }

    [Category("畫面"), DisplayName("停靠 (Dock)"),
     Description("Bottom：固定在畫面最下方，寬度跟著視窗，只能調整高度；None：照設計時的位置與大小。有多個 Bottom 時，先放的在最下面")]
    public GridDock Dock { get; set; } = GridDock.Bottom;

    [Category("畫面"), DisplayName("X")]
    public int X { get; set; }

    [Category("畫面"), DisplayName("Y")]
    public int Y { get; set; }

    [Category("畫面"), DisplayName("寬度")]
    public int Width { get; set; } = 600;

    [Category("畫面"), DisplayName("高度")]
    public int Height { get; set; } = 200;

    /// <summary>單身資料表的全部欄位（Visible 決定是否顯示在 Grid 上）</summary>
    [Browsable(false)]
    public List<GridColumnDefinition> Columns { get; set; } = new();

    [Browsable(false), JsonIgnore]
    public string FullTableName => $"{SqlName.Quote(TableSchema)}.{SqlName.Quote(TableName)}";

    [Browsable(false), JsonIgnore]
    public List<GridColumnDefinition> KeyColumns => Columns.Where(c => c.IsKey).ToList();

    /// <summary>true = 編輯視窗的版面已在後台設計過（欄位照 EditX / EditY 擺放）</summary>
    [Browsable(false)]
    public bool HasEditLayout { get; set; }

    /// <summary>編輯視窗欄位區的大小（版面設計器拖邊緣調整）；0 = 依欄位自動決定</summary>
    [Browsable(false)]
    public int EditFormWidth { get; set; }

    [Browsable(false)]
    public int EditFormHeight { get; set; }

    [Browsable(false), JsonIgnore]
    public Size? EditFormSize => HasEditLayout && EditFormWidth > 0 && EditFormHeight > 0
        ? new Size(EditFormWidth, EditFormHeight) : null;

    /// <summary>編輯視窗上的按鈕（版面設計器從工具箱拖進來的）</summary>
    [Browsable(false)]
    public List<ButtonDefinition> EditButtons { get; set; } = new();

    /// <summary>雙擊 Grid 開啟的編輯視窗要顯示的欄位</summary>
    [Browsable(false), JsonIgnore]
    public List<GridColumnDefinition> EditColumns => Columns.Where(c => c.EditVisible ?? c.Visible).ToList();

    /// <summary>還沒設計過版面時，把編輯視窗的欄位由上往下排成一欄</summary>
    public void EnsureEditLayout()
    {
        if (HasEditLayout) return;
        ArrangeEditLayout(EditColumns);
    }

    /// <summary>把指定欄位由上往下排成一欄（版面設計器的「重新排列」也用這個）</summary>
    public static void ArrangeEditLayout(IEnumerable<GridColumnDefinition> columns)
    {
        int y = 12;
        foreach (var c in columns)
        {
            c.EditX = 10;
            c.EditY = y;
            if (c.EditWidth <= 0) c.EditWidth = Math.Clamp(c.Width, 120, 400);
            y += 32;
        }
    }

    /// <summary>解析關聯欄位文字，回傳（單頭欄位, 單身欄位）清單；格式不對的片段略過</summary>
    [Browsable(false), JsonIgnore]
    public List<(string Master, string Detail)> Links =>
        (LinkText ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2 && p[0] != "" && p[1] != "")
            .Select(p => (p[0], p[1]))
            .ToList();
}

/// <summary>欄位的輸入方式</summary>
public enum InputKind
{
    /// <summary>依資料型別：文字框、勾選框、日期</summary>
    自動,
    /// <summary>下拉選單（ComboBox）</summary>
    下拉選單,
}

/// <summary>按鈕按下去要做的事</summary>
public enum ButtonAction
{
    查詢, 新增, 修改, 刪除, 首筆, 上筆, 下筆, 末筆, 確定, 取消, 單身, 離開,
    /// <summary>執行自訂 SQL，@欄位名稱 帶入目前這筆的值</summary>
    執行SQL,
    /// <summary>開啟另一個畫面定義檔</summary>
    開啟畫面,
}

/// <summary>畫面上的按鈕（類似 Visual Studio 從工具箱拖進來的 Button）</summary>
public class ButtonDefinition
{
    [Category("按鈕"), DisplayName("文字")]
    public string Text { get; set; } = "按鈕";

    [Category("按鈕"), DisplayName("動作"),
     Description("內建動作跟工具列的按鈕一樣；執行SQL 要填下方的 SQL；開啟畫面 要填畫面定義檔")]
    public ButtonAction Action { get; set; } = ButtonAction.查詢;

    [Category("按鈕"), DisplayName("執行前確認訊息"), Description("有填才會先詢問使用者，按「是」才執行，例如：確定要停用這個客戶嗎？")]
    public string Confirm { get; set; }

    [Category("執行SQL"), DisplayName("SQL"),
     Description("動作 = 執行SQL 時執行（同一個交易）。@欄位名稱 會帶入目前這筆的值，例如：UPDATE C客戶設定 SET 停用 = 1 WHERE 識別 = @識別"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Sql { get; set; }

    [Category("開啟畫面"), DisplayName("畫面定義檔"),
     Description("動作 = 開啟畫面 時開啟；只寫檔名時，會找跟目前畫面同一個資料夾的檔案"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string TargetForm { get; set; }

    [Category("畫面"), DisplayName("X")]
    public int X { get; set; }

    [Category("畫面"), DisplayName("Y")]
    public int Y { get; set; }

    [Category("畫面"), DisplayName("寬度")]
    public int Width { get; set; } = 90;

    [Category("畫面"), DisplayName("高度")]
    public int Height { get; set; } = 30;

    /// <summary>複製一份（版面設計器先改複本，按「確定」才寫回）</summary>
    public ButtonDefinition Clone() => (ButtonDefinition)MemberwiseClone();
}

/// <summary>從工具箱拖曳元件時帶的資料</summary>
public class ToolboxDragData
{
    public string Kind { get; set; }
}

/// <summary>Grid 停靠方式</summary>
public enum GridDock
{
    /// <summary>照設計時的位置與大小</summary>
    None,
    /// <summary>固定在畫面最下方，寬度跟著視窗</summary>
    Bottom,
}

/// <summary>單身 Grid 上的一個欄位</summary>
public class GridColumnDefinition
{
    public string Column { get; set; }
    public string DataType { get; set; }
    public int MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsKey { get; set; }
    public string Header { get; set; }
    public int Width { get; set; } = 100;
    public bool Visible { get; set; } = true;
    /// <summary>只顯示，不能輸入，也不寫回資料庫</summary>
    public bool NoEntry { get; set; }

    // ---- 雙擊 Grid 開啟的編輯視窗版面 ----
    /// <summary>編輯視窗是否顯示這個欄位；null = 跟 Grid 的「顯示」一樣</summary>
    public bool? EditVisible { get; set; }
    public int EditX { get; set; }
    public int EditY { get; set; }
    /// <summary>編輯視窗的輸入框寬度；0 = 依 Grid 欄寬自動決定</summary>
    public int EditWidth { get; set; }
    /// <summary>編輯視窗的欄位標籤；null = 用 Grid 欄位標題</summary>
    public string EditLabel { get; set; }
    /// <summary>編輯視窗的標籤寬度；0 = 預設寬度</summary>
    public int EditLabelWidth { get; set; }
    /// <summary>編輯視窗的輸入方式與下拉選單選項（同 FieldDefinition）</summary>
    public InputKind EditInputType { get; set; }
    public string EditItems { get; set; }
    public string EditItemsSql { get; set; }
    public bool EditAllowFreeText { get; set; }
    /// <summary>編輯視窗輸入框的提示文字（Placeholder）</summary>
    public string EditPlaceholder { get; set; }
}

/// <summary>從樹狀清單拖曳整個資料表時帶的資料</summary>
public class TableDragData
{
    public string Schema { get; set; }
    public string Table { get; set; }
}

/// <summary>從資料庫讀到的欄位結構（設計視窗左下方的欄位屬性面板也顯示這些）</summary>
public class ColumnInfo
{
    [DisplayName("欄位名稱"), ReadOnly(true)]
    public string Name { get; set; }

    [DisplayName("說明"), ReadOnly(true)]
    public string Description { get; set; }

    [DisplayName("資料型別"), ReadOnly(true)]
    public string DataType { get; set; }

    [DisplayName("最大長度"), ReadOnly(true), Description("-1 表示 max；數字、日期型別為 0")]
    public int MaxLength { get; set; }

    [DisplayName("主鍵"), ReadOnly(true)]
    public bool IsPrimaryKey { get; set; }

    [DisplayName("允許 NULL"), ReadOnly(true)]
    public bool IsNullable { get; set; }

    [DisplayName("自動編號"), ReadOnly(true)]
    public bool IsIdentity { get; set; }

    [DisplayName("有預設值"), ReadOnly(true)]
    public bool HasDefault { get; set; }

    /// <summary>新增時一定要給值的欄位：不允許 NULL、不是自動編號、也沒有預設值</summary>
    [DisplayName("新增時必填"), ReadOnly(true)]
    public bool IsRequiredOnInsert => !IsNullable && !IsIdentity && !HasDefault;
}

/// <summary>拖曳時帶的資料：哪個資料表的哪個欄位</summary>
public class ColumnDragData
{
    public string Schema { get; set; }
    public string Table { get; set; }
    public ColumnInfo Column { get; set; }
}

public static class SqlName
{
    /// <summary>資料表、欄位名稱加上中括號，避免名稱有空白或保留字</summary>
    public static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
}

/// <summary>SQL Server 資料型別分類與文字轉換</summary>
public static class TypeHelper
{
    private static readonly string[] IntegerTypes = { "int", "bigint", "smallint", "tinyint" };
    private static readonly string[] DecimalTypes = { "decimal", "numeric", "money", "smallmoney" };
    private static readonly string[] FloatTypes = { "float", "real" };
    private static readonly string[] DateTypes = { "date", "datetime", "datetime2", "smalldatetime" };
    private static readonly string[] StringTypes = { "char", "varchar", "nchar", "nvarchar", "text", "ntext" };

    public static bool IsBool(string t) => t == "bit";
    public static bool IsDate(string t) => DateTypes.Contains(t);
    public static bool IsString(string t) => StringTypes.Contains(t);
    public static bool IsNumber(string t) => IntegerTypes.Contains(t) || DecimalTypes.Contains(t) || FloatTypes.Contains(t);

    /// <summary>把輸入框的文字轉成資料庫型別；格式不對時丟 FormatException</summary>
    public static object Parse(string text, string dataType)
    {
        text = text.Trim();
        if (IntegerTypes.Contains(dataType)) return long.Parse(text, NumberStyles.Number, CultureInfo.CurrentCulture);
        if (DecimalTypes.Contains(dataType)) return decimal.Parse(text, NumberStyles.Number, CultureInfo.CurrentCulture);
        if (FloatTypes.Contains(dataType)) return double.Parse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture);
        if (dataType == "uniqueidentifier") return Guid.Parse(text);
        if (dataType == "time") return TimeSpan.Parse(text, CultureInfo.CurrentCulture);
        return text;
    }

    /// <summary>依型別建議輸入框寬度</summary>
    public static int SuggestWidth(ColumnInfo c)
    {
        if (IsBool(c.DataType)) return 30;
        if (IsDate(c.DataType)) return 160;
        if (IsNumber(c.DataType)) return 120;
        if (c.MaxLength > 0) return Math.Clamp(c.MaxLength * 8 + 10, 80, 400);
        return 300;
    }
}
