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

    /// <summary>樣板上用到的欄位（給 SELECT 用）：JOIN 的欄位用 JOIN 定義，其他當成主資料表的欄位；主鍵一定要有</summary>
    public List<GridColumnDefinition> UsedColumns()
    {
        var names = Items.Where(i => i.Kind == PrintItemKind.欄位 && !string.IsNullOrWhiteSpace(i.Column)).Select(i => i.Column)
                         .Concat(KeyColumns).Distinct(StringComparer.OrdinalIgnoreCase);
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

    public PrintItem Clone() => (PrintItem)MemberwiseClone();
}

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

    /// <summary>畫一個元件；row = 要印的那筆資料，null = 設計畫面（欄位顯示成 {欄位名稱}）</summary>
    public static void DrawItem(Graphics g, PrintItem item, RectangleF rect, DataRow row)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var pen = new Pen(Color.Black, Math.Max(0.5f, item.LineWidth));
        switch (item.Kind)
        {
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

    /// <summary>畫整頁（預覽與列印）：g 的座標要已經換算成 1/96 吋</summary>
    public static void DrawPage(Graphics g, PrintTemplate template, DataRow row)
    {
        foreach (var item in template.Items)
            DrawItem(g, item, new RectangleF(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height)), row);
    }
}

/// <summary>執行時：依樣板讀資料、預覽列印、列印</summary>
public static class PrintRunner
{
    /// <summary>
    /// 預覽列印（預覽視窗上可以直接按列印）：records 每個元素提供一筆資料的主鍵值（name => value），每筆印一頁
    /// </summary>
    public static void Preview(PrintTemplate template, string title, string connectionString, IReadOnlyList<Func<string, object>> records, IWin32Window owner)
    {
        if (records.Count == 0)
        {
            MessageBox.Show("請先選一筆要列印的資料。", "列印");
            return;
        }
        List<DataRow> rows;
        try
        {
            rows = LoadRows(template, connectionString, records);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取列印資料失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (rows.Count == 0)
        {
            MessageBox.Show("讀不到要列印的資料（可能已被刪除）。", "列印");
            return;
        }
        Audit.Write(null, template.TableName, "列印", string.Join(" ; ", rows.Select(r => Audit.KeyText(template.KeyColumns.Select(k => new KeyValuePair<string, object>(k, r[k]))))), title);
        using var doc = CreateDocument(template, rows, title);
        ShowPreview(doc, owner, title);
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

    /// <summary>讀主資料表的第一筆（設計時預覽用）</summary>
    public static DataRow LoadSample(PrintTemplate t, string connectionString)
    {
        var d = DbDialect.Of(connectionString);
        using var conn = d.Open(connectionString);
        using var cmd = d.CreateCommand(conn, GridSql.Select(d, d.Table(t.TableSchema, t.TableName), t.UsedColumns(), t.Joins, 1) + d.Limit(1));
        var table = d.Fill(cmd);
        return table.Rows.Count > 0 ? table.Rows[0] : null;
    }

    /// <summary>建立列印文件：每筆資料一頁；紙張大小、方向照樣板</summary>
    public static PrintDocument CreateDocument(PrintTemplate template, IList<DataRow> rows, string name)
    {
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
            PrintRenderer.DrawPage(g, template, rows[index]);
            index++;
            e.HasMorePages = index < rows.Count;
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
