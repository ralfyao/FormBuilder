using System.ComponentModel;
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormBuilder;

/// <summary>紙張大小</summary>
public enum PrintPaper { A4, A5, A3, Letter }

/// <summary>列印樣板上的元件種類</summary>
public enum PrintItemKind
{
    /// <summary>固定文字（標題、說明）</summary>
    文字,
    /// <summary>資料表（或 JOIN 進來的）欄位的值</summary>
    欄位,
    圖片,
    線條,
    方框,
    /// <summary>單身的資料逐筆列成表格（單身在樣板設計器左邊按「設定單身」）</summary>
    明細表格,
}

/// <summary>
/// 列印樣板（存成 *.print.json）：紙張、資料來源（主畫面的資料表 + JOIN 其他資料表）、樣板上的元件。
/// 位置與大小的單位是 1/96 吋（= 設計畫面上的 1 像素），列印時換算成實際尺寸
/// </summary>
public class PrintTemplate
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Category("紙張"), DisplayName("紙張大小"), JsonConverter(typeof(JsonStringEnumConverter))]
    public PrintPaper Paper { get; set; } = PrintPaper.A4;

    [Category("紙張"), DisplayName("橫印")]
    public bool Landscape { get; set; }

    [Category("資料"), DisplayName("Schema"), ReadOnly(true)]
    public string TableSchema { get; set; }

    [Category("資料"), DisplayName("資料表"), ReadOnly(true), Description("主畫面的資料表；列印時依主鍵讀出目前這筆（清單多選時每筆一頁）")]
    public string TableName { get; set; }

    /// <summary>主鍵欄位：列印時用來讀出要印的那筆資料</summary>
    [Category("資料"), DisplayName("主鍵"), ReadOnly(true), TypeConverter(typeof(ListToTextConverter))]
    public List<string> KeyColumns { get; set; } = new();

    /// <summary>JOIN 其他資料表（同 Grid 的 JOIN：LEFT JOIN，沒對到的欄位空白）</summary>
    [Browsable(false)]
    public List<GridJoinDefinition> Joins { get; set; } = new();

    /// <summary>JOIN 帶進來的欄位（可以拖到樣板上）</summary>
    [Browsable(false)]
    public List<GridColumnDefinition> JoinColumns { get; set; } = new();

    /// <summary>
    /// 單身：另一個資料表，依「單頭欄位=單身欄位」的對應讀出這張單據的明細（例如 廠商編號=廠商編號, 詢價日期=詢價日期），
    /// 由「明細表格」逐筆印出。null = 沒有單身
    /// </summary>
    [Browsable(false)]
    public GridJoinDefinition Detail { get; set; }

    /// <summary>單身的欄位：單身資料表本身的欄位，或 DetailJoins 帶進來的欄位（JoinAlias + SourceColumn，同 Grid）</summary>
    [Browsable(false)]
    public List<GridColumnDefinition> DetailColumns { get; set; } = new();

    /// <summary>單身再 JOIN 的資料表（例如帶入品名）；從編輯視窗的明細清單帶過來時跟清單一樣</summary>
    [Browsable(false)]
    public List<GridJoinDefinition> DetailJoins { get; set; } = new();

    [Category("單身"), DisplayName("單身資料表"), JsonIgnore, Description("在左邊「欄位」頁籤按「設定單身」選資料表與對應")]
    public string DetailTableText => Detail == null ? "（未設定）" : $"{Detail.TableSchema}.{Detail.TableName}";

    [Category("單身"), DisplayName("單頭與單身的對應"), JsonIgnore,
     Description("單頭欄位=單身欄位，多組用逗號分隔，例如：廠商編號=廠商編號, 詢價日期=詢價日期。對應的值都相同的單身資料印在同一張；"
               + "清單選了多筆、對應的值相同時只印一張")]
    public string DetailLinkText
    {
        get => Detail?.LinkText ?? "";
        set { if (Detail != null) Detail.LinkText = value ?? ""; }
    }

    /// <summary>
    /// 列印時由列印來源提供、不從資料庫讀的欄位（例如按鈕印明細清單選取的那筆時，那筆的詢價日期）：
    /// 不是單頭資料表的欄位、也不是 JOIN 的欄位；樣板設計器依列印來源清單的欄位算出來
    /// </summary>
    [Browsable(false)]
    public List<string> RecordColumns { get; set; } = new();

    /// <summary>單身資料表的主鍵（「只印清單上選取的資料」用來比對是哪幾筆）</summary>
    [Browsable(false)]
    public List<string> DetailKeyColumns { get; set; } = new();

    [Category("單身"), DisplayName("只印清單上選取的資料"),
     Description("True = 按鈕依編輯視窗明細清單選取的資料列印時，單身只印有選取的那幾筆（清單要是同一個資料表）；False = 印對應到的全部單身")]
    public bool DetailSelectedOnly { get; set; }

    [Category("單身"), DisplayName("單身排序"), Description("單身資料的排序欄位，多個用逗號分隔，可加 DESC，例如：品項編號；空白 = 不指定")]
    public string DetailOrderBy { get; set; }

    [Browsable(false)]
    public List<PrintItem> Items { get; set; } = new();

    /// <summary>紙張大小（1/96 吋）</summary>
    [Browsable(false), JsonIgnore]
    public Size PageSize
    {
        get
        {
            var (w, h) = Paper switch
            {
                PrintPaper.A5 => (559, 794),
                PrintPaper.A3 => (1123, 1587),
                PrintPaper.Letter => (816, 1056),
                _ => (794, 1123),
            };
            return Landscape ? new Size(h, w) : new Size(w, h);
        }
    }

    public static PrintTemplate Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) => File.WriteAllText(path, ToJson());
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static PrintTemplate FromJson(string json) => JsonSerializer.Deserialize<PrintTemplate>(json, JsonOptions);

    /// <summary>樣板裡有沒有 JOIN 這個資料表（自動轉換主資料表時用）</summary>
    public bool HasJoinTo(string table) => Joins.Any(j => j.TableName.Equals(table ?? "", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 換主資料表：樣板原本以 A 為主、JOIN 了 B，改成以 B 為主、JOIN A（例如原本以廠商為主 JOIN 供料，改成以供料為主 JOIN 廠商）。
    /// 樣板上的欄位都保留：原本 B 的欄位變成主資料表的欄位，原本 A 的欄位改用 JOIN 帶入，關聯欄位直接用 B 的。
    /// newTableColumns = B 的全部欄位名稱（判斷 A 的欄位名稱會不會跟 B 撞名）；回傳要提醒使用者的事（空的 = 沒有）
    /// </summary>
    public List<string> RebaseTo(string newSchema, string newTable, IEnumerable<string> newKeys, ICollection<string> newTableColumns)
    {
        var notes = new List<string>();
        var j = Joins.First(x => x.TableName.Equals(newTable, StringComparison.OrdinalIgnoreCase));
        var links = j.Links;
        string alias = "M1";
        for (int n = 2; Joins.Any(x => x.Alias == alias); n++) alias = "M" + n;
        // 原本：A.Master = B.Detail；換過來：B.Detail = A.Master
        var master = new GridJoinDefinition
        {
            Alias = alias, TableSchema = TableSchema, TableName = TableName,
            LinkText = string.Join(", ", links.Select(l => $"{l.Detail}={l.Master}")),
        };
        var oldJoinColumns = JoinColumns.Where(c => c.JoinAlias == j.Alias).ToDictionary(c => c.Column, StringComparer.OrdinalIgnoreCase);
        var otherJoinColumns = JoinColumns.Where(c => c.JoinAlias != j.Alias).Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newJoinColumns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items.Where(i => i.Kind == PrintItemKind.欄位 && !string.IsNullOrWhiteSpace(i.Column)))
        {
            if (oldJoinColumns.TryGetValue(item.Column, out var jc))
            {
                item.Column = jc.SourceColumn;                                  // 原本 JOIN 進來的 B 欄位 → 主資料表的欄位
                continue;
            }
            if (otherJoinColumns.Contains(item.Column)) continue;             // 其他 JOIN 的欄位不動
            var link = links.FirstOrDefault(l => l.Master.Equals(item.Column, StringComparison.OrdinalIgnoreCase));
            if (link != default)
            {
                item.Column = link.Detail;                                      // 關聯欄位：值一樣，直接用 B 的
                continue;
            }
            // 原本 A 的欄位 → 用 JOIN 帶入；名稱跟 B 的欄位撞名時改名
            string source = item.Column;
            if (!newJoinColumns.TryGetValue(source, out var col))
            {
                string name = newTableColumns.Contains(source) ? source + "_主檔" : source;
                col = new GridColumnDefinition { Column = name, Header = name, SourceColumn = source, JoinAlias = alias };
                newJoinColumns[source] = col;
            }
            item.Column = col.Column;
        }
        var others = Joins.Where(x => x != j).ToList();
        if (others.Count > 0)
            notes.Add("其他 JOIN（" + string.Join("、", others.Select(o => o.TableName)) + "）的關聯條件原本是用「" + TableName
                      + "」的欄位，換主資料表後可能對不上，請在樣板設計器檢查，必要時移除後重新 JOIN。");
        Joins.Remove(j);
        JoinColumns.RemoveAll(c => c.JoinAlias == j.Alias);
        Joins.Insert(0, master);
        JoinColumns.AddRange(newJoinColumns.Values);
        TableSchema = newSchema;
        TableName = newTable;
        KeyColumns = newKeys.ToList();
        return notes;
    }

    /// <summary>
    /// 樣板上用到的欄位（給 SELECT 用）：JOIN 的欄位用 JOIN 定義，其他當成主資料表的欄位；主鍵、單身對應的單頭欄位一定要有。
    /// 由列印來源提供的欄位（RecordColumns）不從資料庫讀
    /// </summary>
    public List<GridColumnDefinition> UsedColumns()
    {
        var record = RecordColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = Items.Where(i => i.Kind == PrintItemKind.欄位 && !string.IsNullOrWhiteSpace(i.Column)).Select(i => i.Column)
                         .Concat(Detail?.Links.Select(l => l.Master) ?? Enumerable.Empty<string>())
                         .Where(n => !record.Contains(n)).Concat(KeyColumns)
                         .Distinct(StringComparer.OrdinalIgnoreCase);
        return names.Select(n => JoinColumns.FirstOrDefault(c => c.Column.Equals(n, StringComparison.OrdinalIgnoreCase))
                                 ?? new GridColumnDefinition { Column = n }).ToList();
    }
}

/// <summary>屬性面板上把字串清單顯示成「A, B」</summary>
public class ListToTextConverter : TypeConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType == typeof(string);
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) =>
        value is IEnumerable<string> list ? string.Join(", ", list) : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>屬性面板「欄位」的下拉清單：列印樣板設計器把可用的欄位放進 Available</summary>
public class PrintColumnConverter : StringConverter
{
    public static List<string> Available { get; set; } = new();
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new(Available.ToArray());
}

/// <summary>列印樣板上的一個元件</summary>
public class PrintItem
{
    [Category("內容"), DisplayName("種類"), ReadOnly(true), JsonConverter(typeof(JsonStringEnumConverter))]
    public PrintItemKind Kind { get; set; }

    [Category("內容"), DisplayName("文字"), Description("文字：直接印出來；欄位：印在值前面的文字（例如「廠商名稱：」），可以留空白")]
    public string Text { get; set; }

    [Category("內容"), DisplayName("欄位"), TypeConverter(typeof(PrintColumnConverter)), Description("種類 = 欄位 時印出這個欄位的值（可以從清單挑，包含 JOIN 進來的欄位）")]
    public string Column { get; set; }

    [Category("內容"), DisplayName("格式"), Description("數字、日期的顯示格式，例如 #,##0.00、yyyy/MM/dd；空白 = 預設")]
    public string Format { get; set; }

    /// <summary>圖片內容（PNG / JPG 的 Base64），存在樣板檔裡，搬到別台電腦也印得出來</summary>
    [Browsable(false)]
    public string ImageBase64 { get; set; }

    private string imageFile;

    [Category("內容"), DisplayName("圖片檔"), JsonIgnore, Description("種類 = 圖片 時，選一個圖片檔（會存進樣板檔）；圖片依框的大小等比例縮放"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string ImageFile
    {
        get => imageFile ?? (string.IsNullOrEmpty(ImageBase64) ? "" : "（已內嵌）");
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !File.Exists(value)) return;
            ImageBase64 = Convert.ToBase64String(File.ReadAllBytes(value));
            imageFile = value;
        }
    }

    [Category("位置"), DisplayName("X")] public int X { get; set; }
    [Category("位置"), DisplayName("Y")] public int Y { get; set; }
    [Category("位置"), DisplayName("寬度")] public int Width { get; set; } = 200;
    [Category("位置"), DisplayName("高度")] public int Height { get; set; } = 30;

    [Category("外觀"), DisplayName("字型"), TypeConverter(typeof(FontNameConverter)), Description("空白 = 微軟正黑體")]
    public string FontName { get; set; }

    [Category("外觀"), DisplayName("字型大小"), Description("點數，預設 10")]
    public float FontSize { get; set; } = 10;

    [Category("外觀"), DisplayName("粗體")]
    public bool Bold { get; set; }

    [Category("外觀"), DisplayName("對齊"), JsonConverter(typeof(JsonStringEnumConverter))]
    public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Left;

    [Category("外觀"), DisplayName("外框"), Description("文字、欄位外面加框線（表格格子）")]
    public bool Border { get; set; }

    [Category("外觀"), DisplayName("線條粗細"), Description("線條、方框、外框的粗細（像素）")]
    public float LineWidth { get; set; } = 1;

    [Category("明細表格"), DisplayName("表格欄位"),
     Description("種類 = 明細表格 時，表格的欄位（從左邊拖單身的欄位到表格上也會加進來）；寬度依比例分配到表格的寬度")]
    public List<PrintTableColumn> TableColumns { get; set; } = new();

    [Category("明細表格"), DisplayName("列高"), Description("表格每一列（含標題列）的最小高度；表格框放不下的資料接著印在下一頁")]
    public int RowHeight { get; set; } = 26;

    [Category("明細表格"), DisplayName("自動換行"), Description("內容比欄寬長時換行，列高自動加高（完整印出）；關掉 = 單行，太長的用 … 截斷")]
    public bool WrapText { get; set; } = true;

    public PrintItem Clone()
    {
        var copy = (PrintItem)MemberwiseClone();
        copy.TableColumns = TableColumns.Select(c => c.Clone()).ToList();
        return copy;
    }
}

/// <summary>明細表格的一個欄位</summary>
public class PrintTableColumn
{
    [DisplayName("欄位"), TypeConverter(typeof(PrintDetailColumnConverter)), Description("單身的欄位")]
    public string Column { get; set; }

    [DisplayName("標題"), Description("空白 = 欄位名稱")]
    public string Header { get; set; }

    [DisplayName("寬度"), Description("相對寬度：依比例分配到表格的寬度")]
    public int Width { get; set; } = 100;

    [DisplayName("格式"), Description("數字、日期的顯示格式，例如 #,##0.00、yyyy/MM/dd")]
    public string Format { get; set; }

    [DisplayName("對齊"), JsonConverter(typeof(JsonStringEnumConverter))]
    public HorizontalAlignment Align { get; set; } = HorizontalAlignment.Left;

    public PrintTableColumn Clone() => (PrintTableColumn)MemberwiseClone();
    public override string ToString() => string.IsNullOrWhiteSpace(Header) ? Column ?? "（未選欄位）" : Header;
}

/// <summary>明細表格「欄位」的下拉清單：列印樣板設計器把單身的欄位放進 Available</summary>
public class PrintDetailColumnConverter : StringConverter
{
    public static List<string> Available { get; set; } = new();
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new(Available.ToArray());
}

/// <summary>要印的一張單據：單頭一筆 + 依對應讀出的單身（沒有單身 = null）</summary>
public sealed record PrintSheet(DataRow Header, DataTable Detail);

/// <summary>畫列印樣板的元件（設計畫面、預覽列印、實際列印共用，畫出來的樣子才會一樣）</summary>
public static class PrintRenderer
{
    private const string DefaultFont = "Microsoft JhengHei UI";
    private static readonly Dictionary<string, Image> imageCache = new();

    /// <summary>字型用「1/96 吋」為單位建立，跟位置的單位一致（點數 × 96 / 72）</summary>
    public static Font CreateFont(PrintItem item)
    {
        float size = Math.Clamp(item.FontSize <= 0 ? 10 : item.FontSize, 4, 200) * 96f / 72f;
        var style = item.Bold ? FontStyle.Bold : FontStyle.Regular;
        try { return new Font(string.IsNullOrWhiteSpace(item.FontName) ? DefaultFont : item.FontName, size, style, GraphicsUnit.Pixel); }
        catch (ArgumentException) { return new Font(DefaultFont, size, style, GraphicsUnit.Pixel); }
    }

    public static Image GetImage(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (imageCache.TryGetValue(base64, out var cached)) return cached;
        Image img = null;
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(base64));
            using var raw = Image.FromStream(ms);
            img = new Bitmap(raw);
        }
        catch (Exception) { }
        imageCache[base64] = img;
        return img;
    }

    /// <summary>欄位值轉成要印的文字（有設格式用格式；日期沒設格式時，沒有時間就只印日期）</summary>
    public static string FormatValue(object value, string format)
    {
        if (value == null || value == DBNull.Value) return "";
        if (!string.IsNullOrWhiteSpace(format) && value is IFormattable f)
        {
            try { return f.ToString(format, CultureInfo.CurrentCulture); } catch (FormatException) { }
        }
        if (value is DateTime d) return d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy/MM/dd") : d.ToString("yyyy/MM/dd HH:mm");
        if (value is bool b) return b ? "是" : "否";
        return Convert.ToString(value, CultureInfo.CurrentCulture)?.TrimEnd();
    }

    /// <summary>
    /// 畫一個元件；row = 要印的那筆資料，null = 設計畫面（欄位顯示成 {欄位名稱}）。
    /// 明細表格：detail = 單身資料，page = 單身的第幾頁（放不下的接著印下一頁）
    /// </summary>
    public static void DrawItem(Graphics g, PrintItem item, RectangleF rect, DataRow row, DataTable detail = null, int page = 0)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var pen = new Pen(Color.Black, Math.Max(0.5f, item.LineWidth));
        switch (item.Kind)
        {
            case PrintItemKind.明細表格:
                DrawTable(g, item, rect, pen, row == null, detail, page);
                return;
            case PrintItemKind.線條:
                // 比較寬就畫橫線，比較高就畫直線（畫在框的中間）
                if (rect.Width >= rect.Height) g.DrawLine(pen, rect.Left, rect.Top + rect.Height / 2, rect.Right, rect.Top + rect.Height / 2);
                else g.DrawLine(pen, rect.Left + rect.Width / 2, rect.Top, rect.Left + rect.Width / 2, rect.Bottom);
                return;
            case PrintItemKind.方框:
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                return;
            case PrintItemKind.圖片:
            {
                var img = GetImage(item.ImageBase64);
                if (img == null)
                {
                    if (row == null) DrawPlaceholder(g, rect, "圖片（在屬性面板選圖片檔）");
                    return;
                }
                // 等比例縮放，置中放在框裡
                float scale = Math.Min(rect.Width / img.Width, rect.Height / img.Height);
                var size = new SizeF(img.Width * scale, img.Height * scale);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, rect.X + (rect.Width - size.Width) / 2, rect.Y + (rect.Height - size.Height) / 2, size.Width, size.Height);
                return;
            }
        }

        string text = item.Kind == PrintItemKind.文字 ? item.Text ?? ""
                    : (item.Text ?? "") + (row == null ? "{" + (item.Column ?? "未選欄位") + "}"
                                         : row.Table.Columns.Contains(item.Column ?? "") ? FormatValue(row[item.Column], item.Format) : "");
        using var font = CreateFont(item);
        using var format = new StringFormat
        {
            Alignment = item.Align switch { HorizontalAlignment.Center => StringAlignment.Center, HorizontalAlignment.Right => StringAlignment.Far, _ => StringAlignment.Near },
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        // 框的高度放得下兩行以上才換行，否則單行
        if (rect.Height < font.GetHeight(g) * 1.8f) format.FormatFlags |= StringFormatFlags.NoWrap;
        var textRect = RectangleF.Inflate(rect, -3, 0);
        g.DrawString(text, font, row == null && item.Kind == PrintItemKind.欄位 ? Brushes.DarkBlue : Brushes.Black, textRect, format);
        if (item.Border) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static void DrawPlaceholder(Graphics g, RectangleF rect, string text)
    {
        using var pen = new Pen(Color.Silver) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        using var font = new Font(DefaultFont, 12, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.Gray, rect, format);
    }

    /// <summary>量文字大小用（字型以像素為單位，跟畫的時候一樣）</summary>
    private static readonly Graphics measure = Graphics.FromImage(new Bitmap(1, 1));

    /// <summary>明細表格的欄位、欄的左右位置</summary>
    private static (List<PrintTableColumn> Columns, List<float> Xs) TableColumnsOf(PrintItem item, RectangleF rect)
    {
        var columns = item.TableColumns.Where(c => !string.IsNullOrWhiteSpace(c.Column)).ToList();
        float total = columns.Sum(c => Math.Max(1, c.Width));
        var xs = new List<float> { rect.Left };
        foreach (var c in columns) xs.Add(xs[^1] + rect.Width * Math.Max(1, c.Width) / total);
        return (columns, xs);
    }

    private static string CellText(DataTable detail, DataRow data, PrintTableColumn column) =>
        detail.Columns.Contains(column.Column) ? FormatValue(data[column.Column], column.Format) : "";

    private static StringFormat CellFormat(PrintItem item, HorizontalAlignment align)
    {
        var format = new StringFormat
        {
            Alignment = align switch { HorizontalAlignment.Center => StringAlignment.Center, HorizontalAlignment.Right => StringAlignment.Far, _ => StringAlignment.Near },
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        if (!item.WrapText) format.FormatFlags |= StringFormatFlags.NoWrap;
        return format;
    }

    /// <summary>一列的高度：自動換行時依最長的格子加高（至少「列高」）</summary>
    private static float RowHeightOf(PrintItem item, IList<string> texts, List<float> xs, Font font)
    {
        float min = Math.Max(8, item.RowHeight);
        if (!item.WrapText) return min;
        float height = min;
        lock (measure)
        {
            for (int c = 0; c < texts.Count; c++)
            {
                if (string.IsNullOrEmpty(texts[c])) continue;
                using var format = CellFormat(item, HorizontalAlignment.Left);
                var size = measure.MeasureString(texts[c], font, (int)Math.Max(1, xs[c + 1] - xs[c] - 6), format);
                height = Math.Max(height, (float)Math.Ceiling(size.Height) + 6);
            }
        }
        return height;
    }

    /// <summary>
    /// 明細表格每一頁印哪幾筆（依每列實際高度分頁，表格框放不下就換頁；每頁至少一筆）。
    /// 回傳每頁的（開始, 筆數）；沒有資料回傳一頁 0 筆
    /// </summary>
    public static List<(int Start, int Count)> TablePages(PrintItem item, DataTable detail)
    {
        var pages = new List<(int, int)>();
        int rows = detail?.Rows.Count ?? 0;
        if (rows == 0) return new() { (0, 0) };
        var rect = new RectangleF(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height));
        var (columns, xs) = TableColumnsOf(item, rect);
        if (columns.Count == 0) return new() { (0, 0) };
        using var font = CreateFont(item);
        using var bold = new Font(font, FontStyle.Bold);
        float header = RowHeightOf(item, columns.Select(c => c.ToString()).ToList(), xs, bold);
        int start = 0;
        float used = header;
        for (int r = 0; r < rows; r++)
        {
            float h = RowHeightOf(item, columns.Select(c => CellText(detail, detail.Rows[r], c)).ToList(), xs, font);
            if (r > start && used + h > rect.Height)
            {
                pages.Add((start, r - start));
                start = r;
                used = header;
            }
            used += h;
        }
        pages.Add((start, rows - start));
        return pages;
    }

    /// <summary>
    /// 明細表格：標題列 + 單身資料，格線框起來；欄寬依「寬度」比例分配到表格的寬度，內容太長時換行、列自動加高。
    /// 設計畫面（design = true）顯示標題列和兩列 {欄位名稱}；page = 單身的第幾頁
    /// </summary>
    private static void DrawTable(Graphics g, PrintItem item, RectangleF rect, Pen pen, bool design, DataTable detail, int page)
    {
        var (columns, xs) = TableColumnsOf(item, rect);
        if (columns.Count == 0)
        {
            if (design) DrawPlaceholder(g, rect, "明細表格（從左邊拖單身的欄位到這裡）");
            return;
        }
        using var font = CreateFont(item);
        using var bold = new Font(font, FontStyle.Bold);
        var lines = new List<float> { rect.Top };
        void Row(IList<string> texts, Font f, Func<int, HorizontalAlignment> align, Brush brush)
        {
            float top = lines[^1];
            float h = RowHeightOf(item, texts, xs, f);
            for (int c = 0; c < columns.Count; c++)
            {
                using var format = CellFormat(item, align(c));
                g.DrawString(texts[c], f, brush, RectangleF.Inflate(new RectangleF(xs[c], top, xs[c + 1] - xs[c], h), -3, -2), format);
            }
            lines.Add(top + h);
        }

        Row(columns.Select(c => c.ToString()).ToList(), bold, c => HorizontalAlignment.Center, Brushes.Black);      // 標題列
        if (design)
        {
            for (int r = 0; r < 2; r++) Row(columns.Select(c => "{" + c.Column + "}").ToList(), font, c => columns[c].Align, Brushes.DarkBlue);
        }
        else if (detail != null)
        {
            var pages = TablePages(item, detail);
            if (page >= pages.Count) return;                        // 這個表格的資料比較少，這頁不用印
            var (start, count) = pages[page];
            for (int r = start; r < start + count; r++)
                Row(columns.Select(c => CellText(detail, detail.Rows[r], c)).ToList(), font, c => columns[c].Align, Brushes.Black);
        }
        // 格線
        foreach (float y in lines) g.DrawLine(pen, rect.Left, y, xs[^1], y);
        foreach (float x in xs) g.DrawLine(pen, x, rect.Top, x, lines[^1]);
    }

    /// <summary>畫整頁（預覽與列印）：g 的座標要已經換算成 1/96 吋；page = 這張單據的第幾頁（單身很多時一張單據印好幾頁）</summary>
    public static void DrawPage(Graphics g, PrintTemplate template, PrintSheet sheet, int page = 0)
    {
        foreach (var item in template.Items)
            DrawItem(g, item, new RectangleF(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height)), sheet.Header, sheet.Detail, page);
    }

    /// <summary>一張單據要印幾頁：單身超過明細表格放得下的高度時接著印下一頁</summary>
    public static int PageCount(PrintTemplate template, PrintSheet sheet)
    {
        var tables = template.Items.Where(i => i.Kind == PrintItemKind.明細表格).ToList();
        if (sheet.Detail == null || sheet.Detail.Rows.Count == 0 || tables.Count == 0) return 1;
        return tables.Max(t => TablePages(t, sheet.Detail).Count);
    }
}

/// <summary>執行時：依樣板讀資料、預覽列印、列印</summary>
public static class PrintRunner
{
    /// <summary>
    /// 預覽列印（預覽視窗上可以直接按列印）：records 每個元素提供一筆資料的主鍵值（name => value），每筆印一頁
    /// </summary>
    /// <param name="selected">明細清單上選取的資料（整筆）；樣板設定「只印清單上選取的資料」時，單身只印這幾筆</param>
    public static void Preview(PrintTemplate template, string title, string connectionString, IReadOnlyList<Func<string, object>> records, IWin32Window owner,
                               IReadOnlyList<DataRow> selected = null)
    {
        if (records.Count == 0)
        {
            MessageBox.Show("請先選一筆要列印的資料。", "列印");
            return;
        }
        List<PrintSheet> sheets;
        try
        {
            sheets = LoadSheets(template, connectionString, records, selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取列印資料失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (sheets.Count == 0)
        {
            MessageBox.Show("讀不到要列印的資料（可能已被刪除）。", "列印");
            return;
        }
        Audit.Write(null, template.TableName, "列印", string.Join(" ; ", sheets.Select(s => Audit.KeyText(template.KeyColumns.Select(k => new KeyValuePair<string, object>(k, s.Header[k]))))), title);
        using var doc = CreateDocument(template, sheets, title);
        ShowPreview(doc, owner, title);
    }

    /// <summary>
    /// 讀出要印的單據：依主鍵讀單頭；有設定單身時，再依「單頭欄位=單身欄位」讀出單身。
    /// 對應的值相同的單頭只印一張（例如清單選了同一廠商、同一詢價日期的兩筆，印一張詢價單）
    /// </summary>
    public static List<PrintSheet> LoadSheets(PrintTemplate t, string connectionString, IReadOnlyList<Func<string, object>> records,
                                              IReadOnlyList<DataRow> selected = null)
    {
        var headers = new List<DataRow>();
        foreach (var record in records)
        {
            var row = LoadRows(t, connectionString, new[] { record }).FirstOrDefault();
            if (row == null) continue;
            // 列印來源提供的欄位（例如明細清單選取那筆的詢價日期）放進單頭這筆，樣板與單身對應都用得到
            foreach (string name in t.RecordColumns)
            {
                if (row.Table.Columns.Contains(name)) continue;
                row.Table.Columns.Add(name, typeof(object));
                row[name] = record(name) ?? DBNull.Value;
            }
            headers.Add(row);
        }
        var links = t.Detail?.Links ?? new();
        if (links.Count == 0) return headers.Select(r => new PrintSheet(r, null)).ToList();
        var sheets = new List<PrintSheet>();
        var seen = new HashSet<string>();
        foreach (var header in headers)
        {
            string identity = string.Join("\u001f", links.Select(l => header.Table.Columns.Contains(l.Master) ? Convert.ToString(header[l.Master], CultureInfo.InvariantCulture) : ""));
            if (!seen.Add(identity)) continue;
            var detail = LoadDetail(t, connectionString, header);
            if (t.DetailSelectedOnly && selected != null && detail != null) detail = OnlySelected(t, detail, selected);
            sheets.Add(new PrintSheet(header, detail));
        }
        return sheets;
    }

    /// <summary>單身只留清單上選取的那幾筆（用單身的主鍵比對；選取的資料沒有主鍵欄位時不過濾）</summary>
    private static DataTable OnlySelected(PrintTemplate t, DataTable detail, IReadOnlyList<DataRow> selected)
    {
        var keys = t.DetailKeyColumns.Where(k => detail.Columns.Contains(k)).ToList();
        if (keys.Count == 0 || keys.Count != t.DetailKeyColumns.Count || !selected.All(r => keys.All(r.Table.Columns.Contains))) return detail;
        string Identity(DataRow r) => string.Join("\u001f", keys.Select(k => Convert.ToString(r[k], CultureInfo.InvariantCulture)));
        var wanted = selected.Select(Identity).ToHashSet();
        var result = detail.Clone();
        foreach (DataRow r in detail.Rows)
            if (wanted.Contains(Identity(r))) result.ImportRow(r);
        return result;
    }

    /// <summary>
    /// 依單頭這筆的值讀出單身（單頭欄位=單身欄位，值是空的就比對 IS NULL）。
    /// sample = 設計時預覽：單頭沒有的欄位（列印來源提供的）不當條件
    /// </summary>
    public static DataTable LoadDetail(PrintTemplate t, string connectionString, DataRow header, bool sample = false)
    {
        var detail = t.Detail;
        if (detail == null || detail.Links.Count == 0) return null;
        var d = DbDialect.Of(connectionString);
        var columns = DetailSelectColumns(t);
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn);
        var conditions = new List<string>();
        for (int i = 0; i < detail.Links.Count; i++)
        {
            var (master, col) = detail.Links[i];
            if (sample && !header.Table.Columns.Contains(master)) continue;
            object value = header.Table.Columns.Contains(master) ? header[master] : null;
            if (value == null || value == DBNull.Value)
            {
                conditions.Add($"{GridSql.Base(d, col)} IS NULL");
                continue;
            }
            conditions.Add($"{GridSql.Base(d, col)} = {d.Param($"d{i}")}");
            d.AddParam(cmd, $"d{i}", value);
        }
        string order = OrderByClause(d, t.DetailOrderBy, columns);
        string where = conditions.Count == 0 ? "" : " WHERE " + string.Join(" AND ", conditions);
        int? top = conditions.Count == 0 ? 50 : null;                  // 預覽時沒有條件：只看前 50 筆
        cmd.CommandText = GridSql.Select(d, detail.FullTableName(d), columns, t.DetailJoins, top) + where + order + (top == null ? "" : d.Limit(50));
        return d.Fill(cmd);
    }

    /// <summary>
    /// 單身 SELECT 的欄位：單身資料表本身的欄位、DetailJoins 的欄位。
    /// 舊的樣板（用 JOIN 對話框設定的單身）欄位掛在單身自己的別名上，當成本身的欄位
    /// </summary>
    private static List<GridColumnDefinition> DetailSelectColumns(PrintTemplate t)
    {
        var joins = t.DetailJoins.Select(j => j.Alias).ToHashSet();
        var columns = t.DetailColumns.Where(c => !string.IsNullOrWhiteSpace(c.Column))
            .Select(c => c.IsJoin && !joins.Contains(c.JoinAlias)
                ? new GridColumnDefinition { Column = c.SourceColumn ?? c.Column, Header = c.Header }
                : c).ToList();
        // 對應的單身欄位不一定有選，SELECT 不用；欄位一個都沒有時至少讀對應欄位，表格才知道有幾筆
        if (columns.Count == 0)
            columns = (t.Detail?.Links ?? new()).Select(l => new GridColumnDefinition { Column = l.Detail }).ToList();
        // 只印選取的資料時要用主鍵比對，主鍵沒選也要讀出來
        if (t.DetailSelectedOnly)
            columns.AddRange(t.DetailKeyColumns.Where(k => !columns.Any(c => c.Column.Equals(k, StringComparison.OrdinalIgnoreCase)))
                                               .Select(k => new GridColumnDefinition { Column = k }));
        return columns;
    }

    /// <summary>「品項編號, 單價 DESC」→ ORDER BY（只接受 ASC / DESC）；JOIN 帶進來的欄位用 JOIN 的別名</summary>
    private static string OrderByClause(DbDialect d, string text, List<GridColumnDefinition> columns)
    {
        var parts = (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p =>
            {
                var words = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool desc = words.Length > 1 && words[^1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
                bool asc = words.Length > 1 && words[^1].Equals("ASC", StringComparison.OrdinalIgnoreCase);
                string name = string.Join(" ", desc || asc ? words[..^1] : words);
                // 也可以寫欄位標題（例如「品名」→ 品名規格）
                var byHeader = columns.FirstOrDefault(c => !c.Column.Equals(name, StringComparison.OrdinalIgnoreCase)
                                                          && string.Equals(c.Header, name, StringComparison.OrdinalIgnoreCase)
                                                          && !columns.Any(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase)));
                if (byHeader != null) name = byHeader.Column;
                var join = columns.FirstOrDefault(c => c.IsJoin && c.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                return (join != null ? $"{d.Quote(join.JoinAlias)}.{d.Quote(join.SourceColumn)}" : GridSql.Base(d, name)) + (desc ? " DESC" : "");
            }).ToList();
        return parts.Count == 0 ? "" : " ORDER BY " + string.Join(", ", parts);
    }

    /// <summary>依主鍵讀出每一筆要印的資料（含 JOIN 進來的欄位）</summary>
    public static List<DataRow> LoadRows(PrintTemplate t, string connectionString, IReadOnlyList<Func<string, object>> records)
    {
        if (t.KeyColumns.Count == 0) throw new InvalidOperationException("列印樣板沒有主鍵欄位，不知道要印哪一筆。");
        var d = DbDialect.Of(connectionString);
        string select = GridSql.Select(d, d.Table(t.TableSchema, t.TableName), t.UsedColumns(), t.Joins);
        var rows = new List<DataRow>();
        using var conn = d.Open(connectionString);
        foreach (var valueOf in records)
        {
            using var cmd = d.CreateCommand(conn);
            var conditions = new List<string>();
            for (int i = 0; i < t.KeyColumns.Count; i++)
            {
                conditions.Add($"{GridSql.Base(d, t.KeyColumns[i])} = {d.Param($"k{i}")}");
                d.AddParam(cmd, $"k{i}", valueOf(t.KeyColumns[i]) ?? DBNull.Value);
            }
            cmd.CommandText = $"{select} WHERE {string.Join(" AND ", conditions)}";
            var table = d.Fill(cmd);
            if (table.Rows.Count > 0) rows.Add(table.Rows[0]);
        }
        return rows;
    }

    /// <summary>讀主資料表的第一筆和它的單身（設計時預覽用）；沒有資料回傳 null</summary>
    public static PrintSheet LoadSample(PrintTemplate t, string connectionString)
    {
        var d = DbDialect.Of(connectionString);
        DataTable table;
        using (var conn = d.Open(connectionString))
        using (var cmd = d.CreateCommand(conn, GridSql.Select(d, d.Table(t.TableSchema, t.TableName), t.UsedColumns(), t.Joins, 1) + d.Limit(1)))
            table = d.Fill(cmd);
        if (table.Rows.Count == 0) return null;
        return new PrintSheet(table.Rows[0], LoadDetail(t, connectionString, table.Rows[0], sample: true));
    }

    /// <summary>建立列印文件：每張單據一頁（單身放不下時接著印下一頁）；紙張大小、方向照樣板</summary>
    public static PrintDocument CreateDocument(PrintTemplate template, IList<PrintSheet> sheets, string name)
    {
        var pages = sheets.SelectMany(s => Enumerable.Range(0, PrintRenderer.PageCount(template, s)).Select(p => (Sheet: s, Page: p))).ToList();
        var doc = new PrintDocument { DocumentName = name };
        Size page = template.PageSize;
        // 紙張：用 1/100 吋指定（樣板單位是 1/96 吋）；印表機有同名紙張時用印表機的
        var size = new Size((int)Math.Round(page.Width * 100 / 96.0), (int)Math.Round(page.Height * 100 / 96.0));
        if (template.Landscape) size = new Size(size.Height, size.Width);       // 紙張本身是直的，方向另外設
        try
        {
            var match = doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
                .FirstOrDefault(p => p.PaperName.StartsWith(template.Paper.ToString(), StringComparison.OrdinalIgnoreCase));
            doc.DefaultPageSettings.PaperSize = match ?? new PaperSize(template.Paper.ToString(), size.Width, size.Height);
        }
        catch (InvalidPrinterException)
        {
            doc.DefaultPageSettings.PaperSize = new PaperSize(template.Paper.ToString(), size.Width, size.Height);
        }
        doc.DefaultPageSettings.Landscape = template.Landscape;
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        int index = 0;
        doc.BeginPrint += (s, e) => index = 0;
        doc.PrintPage += (s, e) =>
        {
            var g = e.Graphics;
            g.PageUnit = GraphicsUnit.Display;                              // 印表機上 Display = 1/100 吋
            // 位置是從紙張左上角算的；印表機的原點在可列印範圍的左上角，要扣掉不能印的邊界
            // （預覽的原點就是紙張左上角，不用扣）
            if (doc.PrintController?.IsPreview != true)
                g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
            g.ScaleTransform(100f / 96f, 100f / 96f);                      // 1/96 吋 → 1/100 吋
            PrintRenderer.DrawPage(g, template, pages[index].Sheet, pages[index].Page);
            index++;
            e.HasMorePages = index < pages.Count;
        };
        return doc;
    }

    /// <summary>預覽列印視窗（工具列的印表機按鈕可以直接列印）；沒有預設印表機時先讓使用者挑一台</summary>
    public static void ShowPreview(PrintDocument doc, IWin32Window owner, string title)
    {
        if (!doc.PrinterSettings.IsValid)
        {
            using var pick = new PrintDialog { Document = doc, UseEXDialog = true };
            if (pick.ShowDialog(owner) != DialogResult.OK) return;
        }
        try
        {
            using var preview = new PrintPreviewDialog { Document = doc, Width = 1000, Height = 800, Text = "預覽列印：" + title };
            ((Form)preview).WindowState = FormWindowState.Maximized;
            preview.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show("列印失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
