using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormBuilder;

/// <summary>主鍵欄位：欄位名稱，以及是否為自動編號（新增後要取回資料庫產生的值）</summary>
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

    // 顏色存成文字（#RRGGBB 或顏色名稱），JSON 才好讀；空白 = 預設的灰色
    [Browsable(false)]
    public string BackColorHtml { get; set; }

    [JsonIgnore, Category("畫面"), DisplayName("背景色"), DefaultValue(typeof(Color), ""),
     Description("畫面（設計畫面與執行畫面）的底色；沒設定（Empty）就用預設的灰色。在屬性上按右鍵選「重設」可以還原。欄位標籤會依底色深淺自動用黑字或白字")]
    public Color BackColor
    {
        get => ColorText.Parse(BackColorHtml);
        set => BackColorHtml = ColorText.Format(value);
    }

    [Category("字型"), DisplayName("字型"), TypeConverter(typeof(FontNameConverter)),
     Description("整個畫面（標籤、輸入框、Grid、按鈕，以及雙擊開啟的編輯視窗）用的字型；空白 = 預設的微軟正黑體。可以從清單挑選電腦上有的字型")]
    public string FontName { get; set; }

    [Category("字型"), DisplayName("字型大小"), DefaultValue(0f),
     Description("整個畫面的文字大小（點），0 = 預設（9）。個別欄位設定的「字體大小」會優先；字放大後請把輸入框高度一起拉高")]
    public float FontSize { get; set; }

    /// <summary>畫面用的字型：有設定字型或大小就用設定的，沒有就用 fallback（視窗原本的字型）；字型不存在時也用 fallback</summary>
    public Font ScreenFont(Font fallback)
    {
        if (string.IsNullOrWhiteSpace(FontName) && FontSize <= 0) return fallback;
        try
        {
            var family = string.IsNullOrWhiteSpace(FontName) ? fallback.FontFamily : new FontFamily(FontName.Trim());
            return new Font(family, FontSize > 0 ? Math.Clamp(FontSize, 6, 72) : fallback.Size, fallback.Style);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private string connectionString;
    private DbKind databaseType;

    [Category("資料"), DisplayName("資料庫類型"), ReadOnly(true), JsonConverter(typeof(JsonStringEnumConverter))]
    public DbKind DatabaseType
    {
        get => databaseType;
        set
        {
            databaseType = value;
            DbDialect.Register(connectionString, value);
        }
    }

    // 屬性面板不顯示連線字串（資料庫路徑不外露）；要換連線用上方的「設定連線」
    [Browsable(false)]
    public string ConnectionString
    {
        get => connectionString;
        set
        {
            connectionString = value;
            DbDialect.Register(value, databaseType);      // 其他地方只拿到連線字串，靠登記查出是哪種資料庫
        }
    }

    /// <summary>這個畫面連的資料庫的 SQL 方言</summary>
    [Browsable(false), JsonIgnore]
    public DbDialect Dialect => DbDialect.For(DatabaseType);

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
    public string FullTableName => Dialect.Table(TableSchema, TableName);

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

    /// <summary>畫面代號（定義檔的檔名，不含副檔名）：權限、選單、稽核紀錄用；從系統資料庫讀的是發布時的代號</summary>
    [Browsable(false), JsonIgnore]
    public string FormKey { get; set; }

    public static FormDefinition Load(string path)
    {
        var def = JsonSerializer.Deserialize<FormDefinition>(File.ReadAllText(path), JsonOptions);
        def.SourcePath = Path.GetFullPath(path);
        def.FormKey = Path.GetFileNameWithoutExtension(path);
        return def;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, ToJson());
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static FormDefinition FromJson(string json) => JsonSerializer.Deserialize<FormDefinition>(json, JsonOptions);
}

/// <summary>屬性面板的「字型」下拉清單：列出這台電腦上安裝的字型（也可以直接打字）</summary>
public class FontNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) =>
        new(FontFamily.Families.Select(f => f.Name).Prepend("").ToArray());
}

/// <summary>標籤的高度：至少 26，字放大時跟著變高（文字垂直置中，對齊輸入框）</summary>
public static class LabelGeometry
{
    public static int Height(Font font) => Math.Max(FieldDefinition.DefaultHeight, font.Height + 8);

    /// <summary>標籤的字型：欄位有設「標籤字體大小」就用它，沒有就跟畫面字型一樣</summary>
    public static Font Font(FieldDefinition f, Font baseFont) =>
        f.LabelFontSize > 0 ? new Font(baseFont.FontFamily, Math.Clamp(f.LabelFontSize, 6, 72), baseFont.Style) : baseFont;
}

/// <summary>顏色與文字互轉（定義檔存文字）；沒設定 = Color.Empty，代表用預設的底色</summary>
public static class ColorText
{
    public static Color Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Color.Empty;
        try { return ColorTranslator.FromHtml(text); }
        catch { return Color.Empty; }
    }

    public static string Format(Color color) => color.IsEmpty ? null : ColorTranslator.ToHtml(color);

    /// <summary>畫面底色：有設定用設定的，沒有就用預設的控制項灰色</summary>
    public static Color BackOrDefault(Color color) => color.IsEmpty ? SystemColors.Control : color;

    /// <summary>標籤文字顏色：底色太深用白字，太淺用黑字</summary>
    public static Color LabelColor(Color back) => ButtonLook.Contrast(BackOrDefault(back));
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

    [Category("畫面"), DisplayName("輸入框高度"),
     Description("預設 26；拉高（超過 32）後文字框會變成多行輸入，適合備註；下拉選單、日期的高度固定。設計畫面上拖欄位的下邊緣也可以調整")]
    public int Height { get; set; } = DefaultHeight;

    public const int DefaultHeight = 26;

    [Category("畫面"), DisplayName("標籤寬度"), Description("輸入框左邊標籤區的寬度；設計畫面上拖欄位的左邊緣也可以調整")]
    public int LabelWidth { get; set; } = FieldBox.DefaultLabelWidth;

    [Category("畫面"), DisplayName("提示文字 (Placeholder)"), Description("輸入框空白時顯示的灰色提示文字（文字、數字欄位才有）")]
    public string Placeholder { get; set; }

    [Category("輸入方式"), DisplayName("輸入方式"),
     Description("自動：依資料型別用文字框、勾選框或日期；下拉選單、選項按鈕：從選項中挑選；日期選擇：日期挑選器；勾選框：打勾 / 不打勾")]
    public InputKind InputType { get; set; } = InputKind.自動;

    [Category("編號"), DisplayName("編號規則"),
     Description("新增時自動產生編號（可再手動修改）。用中括號組成：[BF] 固定文字；[YYYY][MM][DD] 新增當天的日期；"
               + "[XXX] 流水號，幾個 X 就是幾碼，到資料表查同樣前綴的最大流水號 + 1。例如 [BF][YYYY][MM][DD][XXX] → BF20240131001。不需要自動編號就留空白")]
    public string NumberRule { get; set; }

    [Category("編號"), DisplayName("預設值"),
     Description("新增時自動帶入的值（使用者仍可以改）。可以寫固定值；日期用 NOW()（現在日期時間）、TODAY()（今天），可以加減天數，例如 TODAY()+7；"
               + "USER() = 登入帳號。也可以用 {今天} {現在} {使用者} {使用者名稱} 夾在文字裡。有設「編號規則」時以編號規則為準")]
    public string DefaultValue { get; set; }

    [Category("日期選擇"), DisplayName("日期格式"),
     Description("文字、數字欄位用日期選擇時，存進資料庫的格式（ERP 常見的是 yyyyMMdd）；沒填就用 yyyyMMdd。date、datetime 欄位不需要")]
    public string DateFormat { get; set; }

    [Category("勾選框"), DisplayName("勾選時的值"), Description("非 bit 欄位用勾選框時，打勾存進資料庫的值，預設 1；例如 Y")]
    public string CheckedValue { get; set; } = "1";

    [Category("勾選框"), DisplayName("取消時的值"), Description("非 bit 欄位用勾選框時，沒打勾存進資料庫的值，預設 0；例如 N")]
    public string UncheckedValue { get; set; } = "0";

    [Category("下拉選單"), DisplayName("固定選項"),
     Description("下拉選單、選項按鈕的選項，用逗號或換行分隔；值和顯示文字不同時寫「值=顯示」，例如：Y=是,N=否"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Items { get; set; }

    [Category("下拉選單"), DisplayName("選項 SQL"),
     Description("從資料庫取選項：第一欄是值，第二欄（可省略）是顯示文字，例如：SELECT 代碼, 名稱 FROM 幣別"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string ItemsSql { get; set; }

    [Category("下拉選單"), DisplayName("可自行輸入"), Description("True：選項以外的值也可以直接打字輸入")]
    public bool AllowFreeText { get; set; }

    [Category("開窗選取"), DisplayName("開窗畫面"),
     Description("有設定時，點這個欄位的下拉（或在文字框雙擊、按 F4）就開啟這個畫面當選取視窗，選一筆後把值帶回來。只寫檔名時，會找跟目前畫面同一個資料夾的檔案"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string PickerForm { get; set; }

    [Category("開窗選取"), DisplayName("傳入參數"),
     Description("開窗時帶給對方畫面的值（同按鈕的「傳入參數」）：「對方欄位=@本畫面欄位」或「對方欄位=固定值」，例如：廠商編號=@廠商編號")]
    public string PickerParams { get; set; }

    [Category("開窗選取"), DisplayName("回傳對應"),
     Description("選好後帶回哪些值（同按鈕的「回傳對應」）：「本畫面欄位=對方欄位」，可以一次帶回多個欄位，例如：品項編號=品項編號, 採購單位=單位。"
               + "空白 = 只帶回這個欄位（對方畫面同名的欄位）；只寫對方的欄位名稱（例如：產品編號）= 把對方的這個欄位帶回這個欄位")]
    public string PickerReturn { get; set; }

    [Category("畫面"), DisplayName("只顯示（NOENTRY）"), Description("使用者不能輸入，存檔時也不會寫回資料庫")]
    public bool NoEntry { get; set; }

    [Category("畫面"), DisplayName("唯讀"),
     Description("新增、修改時使用者不能改這個欄位，但值照樣存進資料庫（例如自動編號、帶入的預設值）；查詢時仍然可以輸入條件。跟「只顯示」的差別是：唯讀的值會存檔")]
    public bool IsReadOnly { get; set; }

    [Category("外觀"), DisplayName("字體大小"), DefaultValue(0f),
     Description("輸入框文字的大小（點），0 = 預設（9）。字放大時請一起把「輸入框高度」拉高，文字才不會被切掉")]
    public float FontSize { get; set; }

    [Category("外觀"), DisplayName("標籤字體大小"), DefaultValue(0f),
     Description("輸入框左邊標籤文字的大小（點），0 = 跟畫面的字型大小一樣。字放大時請一起把「標籤寬度」拉寬")]
    public float LabelFontSize { get; set; }

    // 顏色存成文字（#RRGGBB 或顏色名稱），JSON 才好讀；空白 = 預設的白底
    [Browsable(false)]
    public string BackColorHtml { get; set; }

    [JsonIgnore, Category("外觀"), DisplayName("底色"), DefaultValue(typeof(Color), ""),
     Description("輸入框的底色；沒設定（Empty）就是預設的白底。在屬性上按右鍵選「重設」可以還原。日期選擇只有外框會變色")]
    public Color BackColor
    {
        get => ColorText.Parse(BackColorHtml);
        set => BackColorHtml = ColorText.Format(value);
    }
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

    /// <summary>SQL 清單：資料來自這個 SELECT（唯讀，可以用 @單頭欄位）；空白 = 一般的資料表 Grid</summary>
    [Browsable(false)]
    public string Sql { get; set; }

    [Browsable(false), JsonIgnore]
    public bool IsSqlList => !string.IsNullOrWhiteSpace(Sql);

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

    /// <summary>單身資料表的全部欄位（Visible 決定是否顯示在 Grid 上）；JOIN 帶進來的欄位也在這裡（JoinAlias 不是空的）</summary>
    [Browsable(false)]
    public List<GridColumnDefinition> Columns { get; set; } = new();

    /// <summary>JOIN 的其他資料表（帶進唯讀欄位，例如用品號帶出品名）</summary>
    [Browsable(false)]
    public List<GridJoinDefinition> Joins { get; set; } = new();

    public string FullTableName(DbDialect d) => d.Table(TableSchema, TableName);

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

    /// <summary>編輯視窗的底色（版面設計器的「背景色」）；空白 = 預設的灰色</summary>
    [Browsable(false)]
    public string EditBackColorHtml { get; set; }

    /// <summary>編輯視窗的字型、字型大小（版面設計器的「字型」）；空白 / 0 = 跟畫面一樣</summary>
    [Browsable(false)]
    public string EditFontName { get; set; }

    [Browsable(false)]
    public float EditFontSize { get; set; }

    /// <summary>編輯視窗用的字型：有設定就用設定的，沒有就用 fallback（畫面的字型）</summary>
    public Font EditFont(Font fallback)
    {
        if (string.IsNullOrWhiteSpace(EditFontName) && EditFontSize <= 0) return fallback;
        try
        {
            var family = string.IsNullOrWhiteSpace(EditFontName) ? fallback.FontFamily : new FontFamily(EditFontName.Trim());
            return new Font(family, EditFontSize > 0 ? Math.Clamp(EditFontSize, 6, 72) : fallback.Size, fallback.Style);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    [Browsable(false), JsonIgnore]
    public Color EditBackColor
    {
        get => ColorText.Parse(EditBackColorHtml);
        set => EditBackColorHtml = ColorText.Format(value);
    }

    /// <summary>編輯視窗上的明細清單（版面設計器從「資料表」頁籤拖進來的）</summary>
    [Browsable(false)]
    public List<EditListDefinition> EditLists { get; set; } = new();

    /// <summary>
    /// 生效欄位：編輯視窗的「生效」把它改成 1、「取消生效」改成 0，生效的資料才能「列印」。
    /// 空白 = 不使用（不顯示生效 / 取消生效）
    /// </summary>
    [Browsable(false)]
    public string EffectiveColumn { get; set; }

    /// <summary>欄位值算不算「生效」：1、True、Y</summary>
    public static bool IsEffectiveValue(object value) => value switch
    {
        null or DBNull => false,
        bool b => b,
        string s => s.Trim() is "1" or "Y" or "y" || s.Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
        IConvertible c => c.ToDecimal(null) == 1,
        _ => false,
    };

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
    public List<(string Master, string Detail)> Links => ParseLinks(LinkText);

    /// <summary>解析「單頭欄位=單身欄位, ...」文字；格式不對的片段略過</summary>
    public static List<(string Master, string Detail)> ParseLinks(string text) =>
        (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2 && p[0] != "" && p[1] != "")
            .Select(p => (p[0], p[1]))
            .ToList();
}

/// <summary>Grid 的一個 JOIN：用本身資料表的欄位對應關聯資料表的欄位（LEFT JOIN，沒對到的列照樣顯示，欄位空白）</summary>
public class GridJoinDefinition
{
    /// <summary>SQL 裡的別名（J1、J2…），欄位的 JoinAlias 指向它</summary>
    public string Alias { get; set; }
    public string TableSchema { get; set; }
    public string TableName { get; set; }

    /// <summary>關聯條件：本身欄位=關聯資料表欄位，多組用逗號分隔（Master = 本身欄位，Detail = 關聯資料表欄位）</summary>
    public string LinkText { get; set; } = "";

    public string FullTableName(DbDialect d) => d.Table(TableSchema, TableName);

    [JsonIgnore]
    public List<(string Master, string Detail)> Links => GridDefinition.ParseLinks(LinkText);

    public GridJoinDefinition Clone() => (GridJoinDefinition)MemberwiseClone();
}

/// <summary>
/// 組 Grid 的 SELECT（單身、編輯視窗明細清單共用）：本身資料表固定用別名 T0，
/// JOIN 的資料表用各自的別名，JOIN 帶進來的欄位用 AS 取顯示用的欄位名稱
/// </summary>
public static class GridSql
{
    public const string BaseAlias = "T0";

    /// <summary>本身資料表的欄位（加上別名，避免跟 JOIN 的資料表欄位同名時不明確）</summary>
    public static string Base(DbDialect d, string column) => $"{d.Quote(BaseAlias)}.{d.Quote(column)}";

    private static string Expr(DbDialect d, GridColumnDefinition c) =>
        c.IsJoin ? $"{d.Quote(c.JoinAlias)}.{d.Quote(c.SourceColumn)} AS {d.Quote(c.Column)}" : Base(d, c.Column);

    /// <summary>FROM 子句：只 JOIN 有欄位被選到的資料表</summary>
    public static string From(DbDialect d, string fullTableName, IEnumerable<GridJoinDefinition> joins, IEnumerable<GridColumnDefinition> columns)
    {
        var used = columns.Where(c => c.IsJoin).Select(c => c.JoinAlias).ToHashSet();
        var sql = new System.Text.StringBuilder(fullTableName + d.TableAlias(BaseAlias));
        foreach (var j in joins.Where(j => used.Contains(j.Alias)))
            sql.Append($" LEFT JOIN {j.FullTableName(d)}{d.TableAlias(j.Alias)} ON "
                     + string.Join(" AND ", j.Links.Select(l => $"{Base(d, l.Master)} = {d.Quote(j.Alias)}.{d.Quote(l.Detail)}")));
        return sql.ToString();
    }

    /// <summary>
    /// SELECT 欄位 FROM 資料表 [LEFT JOIN ...]（還沒有 WHERE、ORDER BY）。
    /// 要取前 n 筆時，SQL Server 的 TOP n 已經放在這裡；Oracle、MySQL 要在整句最後加上 d.Limit(n)
    /// </summary>
    public static string Select(DbDialect d, string fullTableName, IList<GridColumnDefinition> columns, IEnumerable<GridJoinDefinition> joins, int? top = null) =>
        $"SELECT {d.Top(top)}{string.Join(", ", columns.Select(c => Expr(d, c)))} FROM {From(d, fullTableName, joins, columns)}";
}

/// <summary>
/// 編輯視窗上的明細清單：對應另一個資料表，依關聯欄位（編輯視窗欄位 = 明細欄位）只列出目前這筆的明細，唯讀顯示
/// </summary>
public class EditListDefinition
{
    [Category("資料"), DisplayName("Schema"), ReadOnly(true)]
    public string TableSchema { get; set; }

    [Category("資料"), DisplayName("資料表"), ReadOnly(true)]
    public string TableName { get; set; }

    [Category("資料"), DisplayName("關聯欄位"), Description("編輯視窗欄位=明細欄位，多組用逗號分隔，例如：廠商編號=廠商編號。點一下清單可以用視窗設定")]
    public string LinkText { get; set; } = "";

    [Category("畫面"), DisplayName("標題")]
    public string Title { get; set; }

    [Category("畫面"), DisplayName("X")]
    public int X { get; set; }

    [Category("畫面"), DisplayName("Y")]
    public int Y { get; set; }

    [Category("畫面"), DisplayName("寬度")]
    public int Width { get; set; } = 400;

    [Category("畫面"), DisplayName("高度")]
    public int Height { get; set; } = 150;

    /// <summary>明細資料表的全部欄位（Visible 決定是否顯示在清單上）；JOIN 帶進來的欄位也在這裡</summary>
    [Browsable(false)]
    public List<GridColumnDefinition> Columns { get; set; } = new();

    /// <summary>JOIN 的其他資料表</summary>
    [Browsable(false)]
    public List<GridJoinDefinition> Joins { get; set; } = new();

    public string FullTableName(DbDialect d) => d.Table(TableSchema, TableName);

    [Browsable(false), JsonIgnore]
    public List<(string Master, string Detail)> Links => GridDefinition.ParseLinks(LinkText);

    /// <summary>複製一份（版面設計器先改複本，按「確定」才寫回）</summary>
    public EditListDefinition Clone() =>
        JsonSerializer.Deserialize<EditListDefinition>(JsonSerializer.Serialize(this));
}

/// <summary>欄位的輸入方式</summary>
public enum InputKind
{
    /// <summary>依資料型別：文字框、勾選框、日期</summary>
    自動,
    /// <summary>下拉選單（ComboBox）</summary>
    下拉選單,
    /// <summary>日期選擇（DateTimePicker）：文字、數字欄位存日期時，依「日期格式」轉換</summary>
    日期選擇,
    /// <summary>勾選框（CheckBox）：非 bit 欄位用「勾選時的值」「取消時的值」轉換</summary>
    勾選框,
    /// <summary>選項按鈕（RadioButton）：選項同下拉選單的固定選項 / 選項 SQL，一次只能選一個</summary>
    選項按鈕,
}

/// <summary>按鈕按下去要做的事</summary>
public enum ButtonAction
{
    查詢, 新增, 修改, 刪除, 首筆, 上筆, 下筆, 末筆, 確定, 取消, 單身, 離開,
    /// <summary>執行自訂 SQL，@欄位名稱 帶入目前這筆的值</summary>
    執行SQL,
    /// <summary>開啟另一個畫面定義檔</summary>
    開啟畫面,
    /// <summary>用自訂 SELECT 查詢：@欄位名稱 帶入畫面輸入框的值，結果顯示在單頭（清單）</summary>
    自訂查詢,
    /// <summary>依列印樣板預覽列印目前這筆（清單多選時每筆一頁）；設計畫面雙擊按鈕開啟列印樣板設計</summary>
    列印報表,
}

/// <summary>屬性面板「列印來源清單」的下拉清單：編輯視窗版面設計器把版面上的明細清單資料表放進 Available</summary>
public class PrintListConverter : StringConverter
{
    public static List<string> Available { get; set; } = new();
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new(Available.Prepend("").ToArray());
}

/// <summary>畫面上的按鈕（類似 Visual Studio 從工具箱拖進來的 Button）</summary>
public class ButtonDefinition
{
    [Category("按鈕"), DisplayName("文字")]
    public string Text { get; set; } = "按鈕";

    [Category("按鈕"), DisplayName("動作"),
     Description("內建動作跟工具列的按鈕一樣；執行SQL、自訂查詢 要填下方的 SQL；開啟畫面 要填畫面定義檔")]
    public ButtonAction Action { get; set; } = ButtonAction.查詢;

    [Category("按鈕"), DisplayName("執行前確認訊息"), Description("有填才會先詢問使用者，按「是」才執行，例如：確定要停用這個客戶嗎？")]
    public string Confirm { get; set; }

    [Category("執行SQL"), DisplayName("SQL"),
     Description("執行SQL：同一個交易執行，@欄位名稱 帶入目前這筆的值，例如 UPDATE C客戶設定 SET 停用 = 1 WHERE 識別 = @識別。"
               + "自訂查詢：按一下輸入條件、再按一下（或確定）查詢，@欄位名稱 帶入輸入框的值（文字沒填 = 空字串），"
               + "例如 SELECT * FROM B廠商設定 WHERE 廠商名稱 LIKE '%' + @廠商名稱 + '%'"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Sql { get; set; }

    [Category("開啟畫面"), DisplayName("畫面定義檔"),
     Description("動作 = 開啟畫面 時開啟；只寫檔名時，會找跟目前畫面同一個資料夾的檔案"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string TargetForm { get; set; }

    [Category("開啟畫面"), DisplayName("傳入參數"),
     Description("開啟時帶給對方畫面的值，用逗號分隔「對方欄位=@本畫面欄位」或「對方欄位=固定值」，例如：廠商編號=@廠商編號。"
               + "對方畫面上有同名的欄位時，會當成查詢條件自動查詢；對方的「執行SQL」也可以用 @名稱 取得"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string OpenParams { get; set; }

    [Category("開啟畫面"), DisplayName("回傳對應"),
     Description("有填時，開啟的畫面變成「選取視窗」：在清單雙擊（或按「選取帶回」）就關閉，把選的那筆資料填回本畫面的欄位。"
               + "用逗號分隔「本畫面欄位=對方欄位」，例如：聯絡人=姓名, 職稱=職稱。本畫面在新增、修改（主畫面也包含查詢）時才能按，帶回後再按「確定」存檔"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string ReturnMap { get; set; }

    [Category("列印報表"), DisplayName("列印樣板檔"),
     Description("動作 = 列印報表 時使用的樣板（*.print.json）。在設計畫面上雙擊這個按鈕，就會開啟列印樣板設計（第一次儲存時會建立樣板檔）；只寫檔名時，會找跟目前畫面同一個資料夾的檔案")]
    public string PrintTemplate { get; set; }

    [Category("列印報表"), DisplayName("列印來源清單"), TypeConverter(typeof(PrintListConverter)),
     Description("空白 = 印目前這筆（主檔）。選編輯視窗上的明細清單（資料表名稱）= 依清單上選取的資料列印（可多選；沒選會提醒）。"
               + "樣板一樣以主檔（單頭）為主，選取那筆的欄位（例如詢價日期）可以放到樣板上，或在「設定單身」拿來對應單身")]
    public string PrintListTable { get; set; }

    // 顏色存成文字（#RRGGBB 或顏色名稱），JSON 才好讀；空白 = 預設外觀
    [Browsable(false)]
    public string BackColorHtml { get; set; }

    [Browsable(false)]
    public string ForeColorHtml { get; set; }

    [JsonIgnore, Category("外觀"), DisplayName("背景色"), DefaultValue(typeof(Color), ""),
     Description("按鈕的底色；沒設定（Empty）就用預設的白底。在屬性上按右鍵選「重設」可以還原")]
    public Color BackColor
    {
        get => ParseColor(BackColorHtml);
        set => BackColorHtml = value.IsEmpty ? null : ColorTranslator.ToHtml(value);
    }

    [JsonIgnore, Category("外觀"), DisplayName("文字顏色"), DefaultValue(typeof(Color), ""),
     Description("按鈕文字與圖示的顏色；沒設定（Empty）時，有背景色會自動選黑或白，沒有背景色用預設的黑字")]
    public Color ForeColor
    {
        get => ParseColor(ForeColorHtml);
        set => ForeColorHtml = value.IsEmpty ? null : ColorTranslator.ToHtml(value);
    }

    [Category("外觀"), DisplayName("圖示"), Description("文字左邊的圖示（Segoe 圖示字型）；文字留空就只顯示圖示")]
    public ButtonIcon Icon { get; set; }

    [Category("外觀"), DisplayName("圖片檔"),
     Description("自己的圖示檔（png、jpg、bmp、ico），有設定時優先於上面的圖示；建議用完整路徑"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string IconFile { get; set; }

    private static Color ParseColor(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Color.Empty;
        try { return ColorTranslator.FromHtml(html); }
        catch { return Color.Empty; }
    }

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

/// <summary>按鈕可選的圖示（用 Windows 內建的 Segoe 圖示字型，不用另外準備圖檔）</summary>
public enum ButtonIcon
{
    無, 儲存, 搜尋, 新增, 刪除, 編輯, 列印, 重新整理, 匯出, 確定, 取消, 首筆, 上筆, 下筆, 末筆, 設定, 資訊,
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

    /// <summary>Grid 儲存格的輸入方式：自動（依資料型別）、文字框、下拉選單、勾選框、日期選擇</summary>
    public GridCellKind CellType { get; set; }
    /// <summary>
    /// 依輸入方式：下拉選單 = 固定選項（值=顯示，逗號分隔）或 SELECT 開頭的選項 SQL；
    /// 勾選框 = 勾選值/取消值（例如 Y/N，預設 1/0）；日期選擇 = 文字、數字欄位的日期格式（預設 yyyyMMdd）
    /// </summary>
    public string CellOptions { get; set; }

    /// <summary>JOIN 帶進來的欄位：這個欄位來自哪一個 JOIN（GridJoinDefinition.Alias）；null = 本身資料表的欄位</summary>
    public string JoinAlias { get; set; }
    /// <summary>JOIN 帶進來的欄位在關聯資料表裡的原始欄位名稱（Column 是顯示用的欄位名稱，不重複）</summary>
    public string SourceColumn { get; set; }

    [JsonIgnore]
    public bool IsJoin => !string.IsNullOrEmpty(JoinAlias);

    // ---- 雙擊 Grid 開啟的編輯視窗版面 ----
    /// <summary>編輯視窗是否顯示這個欄位；null = 跟 Grid 的「顯示」一樣</summary>
    public bool? EditVisible { get; set; }
    public int EditX { get; set; }
    public int EditY { get; set; }
    /// <summary>編輯視窗的輸入框寬度；0 = 依 Grid 欄寬自動決定</summary>
    public int EditWidth { get; set; }
    /// <summary>編輯視窗的輸入框高度；0 = 預設高度</summary>
    public int EditHeight { get; set; }
    /// <summary>編輯視窗的欄位標籤；null = 用 Grid 欄位標題</summary>
    public string EditLabel { get; set; }
    /// <summary>編輯視窗的標籤寬度；0 = 預設寬度</summary>
    public int EditLabelWidth { get; set; }
    /// <summary>編輯視窗的輸入方式與下拉選單選項（同 FieldDefinition）</summary>
    public InputKind EditInputType { get; set; }
    public string EditItems { get; set; }
    public string EditItemsSql { get; set; }
    public bool EditAllowFreeText { get; set; }
    /// <summary>編輯視窗的開窗選取（同 FieldDefinition.PickerForm / PickerParams / PickerReturn）</summary>
    public string EditPickerForm { get; set; }
    public string EditPickerParams { get; set; }
    public string EditPickerReturn { get; set; }
    /// <summary>編輯視窗的日期選擇格式、勾選框的值（同 FieldDefinition）</summary>
    public string EditDateFormat { get; set; }
    /// <summary>編輯視窗新增時的自動編號規則（同 FieldDefinition.NumberRule）</summary>
    public string EditNumberRule { get; set; }
    /// <summary>編輯視窗新增時的預設值（同 FieldDefinition.DefaultValue）；單身 Grid 新增明細時也用它</summary>
    public string EditDefaultValue { get; set; }
    /// <summary>編輯視窗欄位是否唯讀（同 FieldDefinition.IsReadOnly）</summary>
    public bool EditReadOnly { get; set; }
    public string EditCheckedValue { get; set; }
    public string EditUncheckedValue { get; set; }
    /// <summary>編輯視窗輸入框的提示文字（Placeholder）</summary>
    public string EditPlaceholder { get; set; }
    /// <summary>編輯視窗輸入框的字體大小、底色（同 FieldDefinition.FontSize / BackColorHtml）</summary>
    public float EditFontSize { get; set; }
    public string EditBackColorHtml { get; set; }
    /// <summary>編輯視窗標籤的字體大小（同 FieldDefinition.LabelFontSize）</summary>
    public float EditLabelFontSize { get; set; }
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
