using System.Globalization;

namespace FormBuilder;

/// <summary>選項按鈕群組：每個選項一個 RadioButton，一次只能選一個；值是選項的「值」</summary>
public class RadioGroup : FlowLayoutPanel
{
    private readonly List<(RadioButton Button, string Value)> options = new();

    public RadioGroup(IEnumerable<ComboItem> items)
    {
        AutoScroll = true;
        WrapContents = true;
        BackColor = Color.Transparent;
        foreach (var item in items)
        {
            var radio = new RadioButton { Text = item.Text, AutoSize = true, Margin = new Padding(0, 3, 14, 3) };
            options.Add((radio, item.Value.Trim()));
            Controls.Add(radio);
        }
    }

    /// <summary>目前選的選項的值；沒選 = null。設定時找不到對應的值就全部不選</summary>
    public string SelectedValue
    {
        get => options.FirstOrDefault(o => o.Button.Checked).Value;
        set
        {
            foreach (var (button, optionValue) in options)
                button.Checked = value != null && optionValue == value;
        }
    }
}

/// <summary>下拉選單的一個選項：存進資料庫的值，以及顯示的文字</summary>
public record ComboItem(string Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>依欄位型別建立輸入控制項、取值、設值（執行畫面與單筆編輯視窗共用）</summary>
public static class InputHelper
{
    /// <param name="connectionString">下拉選單有「選項 SQL」時用來讀選項</param>
    public static Control CreateInput(FieldDefinition f, string connectionString = null)
    {
        switch (f.InputType)
        {
            case InputKind.下拉選單:
                return CreateCombo(f, connectionString);
            case InputKind.選項按鈕:
                return new RadioGroup(BuildItems(f, connectionString, withBlank: false));
            case InputKind.勾選框:
                return CreateCheckBox(f);
            case InputKind.日期選擇:
                return CreateDatePicker(f);
            case InputKind.文字標籤:
                // 文字標籤：顯示「標籤」的文字，SQL 的回傳對應可以把值填進來（SetValue 改 Text）
                return new Label { Text = f.Label ?? "", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
        }
        if (TypeHelper.IsBool(f.DataType))
            return new CheckBox();
        if (TypeHelper.IsDate(f.DataType))
            return CreateDatePicker(f);
        var tb = new TextBox { PlaceholderText = f.Placeholder ?? "" };
        if (TypeHelper.IsString(f.DataType) && f.MaxLength > 0) tb.MaxLength = f.MaxLength;
        if (TypeHelper.IsNumber(f.DataType)) tb.TextAlign = HorizontalAlignment.Right;
        return tb;
    }

    /// <summary>
    /// 套用後台設定的字型、字體大小與底色（要在 RoundedStyle.Place 之前，文字框的高度才會依字體算）；
    /// baseFont = 畫面的字型，放大時沿用同一個字型
    /// </summary>
    public static void ApplyLook(FieldDefinition f, Control input, Font baseFont)
    {
        if (FontPick.IsSet(f.FontName, f.FontSize)) input.Font = FontPick.With(baseFont, f.FontName, f.FontSize);
        if (!f.BackColor.IsEmpty)
        {
            input.BackColor = f.BackColor;
            RoundedStyle.SetFill(input, f.BackColor);     // 圓角外框也用同一個底色
        }
    }

    /// <summary>勾選框打勾 / 沒打勾要存進非 bit 欄位的值</summary>
    private sealed record CheckValues(string Checked, string Unchecked);

    private static CheckBox CreateCheckBox(FieldDefinition f) => new()
    {
        Tag = new CheckValues(string.IsNullOrEmpty(f.CheckedValue) ? "1" : f.CheckedValue, f.UncheckedValue ?? "0"),
    };

    private const string DefaultDateFormat = "yyyyMMdd";

    /// <summary>
    /// 文字框裡的日期只顯示日期；記住原本的值，使用者沒改文字時取回原本的值（datetime 欄位的時間不會被存成 00:00）
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Tuple<string, DateTime>> shownDates = new();

    /// <summary>
    /// 日期選擇。date、datetime 欄位直接用日期；文字、數字欄位依「日期格式」轉成文字存取（Tag 放格式），
    /// 文字、數字欄位可以是空的，所以一定有「沒有日期」的勾選框
    /// </summary>
    private static DateTimePicker CreateDatePicker(FieldDefinition f)
    {
        bool nativeDate = TypeHelper.IsDate(f.DataType);
        string textFormat = string.IsNullOrWhiteSpace(f.DateFormat) ? DefaultDateFormat : f.DateFormat.Trim();
        return new DateTimePicker
        {
            // 一律只顯示日期；datetime 欄位原本的時間還留在 Value 裡，存檔時不會變成 00:00
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy/MM/dd",
            ShowCheckBox = nativeDate ? f.IsNullable : true,       // 取消勾選代表沒有日期
            Tag = nativeDate ? null : textFormat,
        };
    }

    /// <summary>
    /// 下拉選單：選項 = 固定選項 + 選項 SQL 查出來的。
    /// 允許 NULL（或可自行輸入）時，最上面多一個空白選項代表「不選」
    /// </summary>
    private static ComboBox CreateCombo(FieldDefinition f, string connectionString)
    {
        var cb = new ComboBox { DropDownStyle = f.AllowFreeText ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList };
        cb.Items.AddRange(BuildItems(f, connectionString, withBlank: f.IsNullable || f.AllowFreeText).ToArray());
        return cb;
    }

    /// <summary>選項 = 固定選項 + 選項 SQL 查出來的（下拉選單、選項按鈕共用）；withBlank = 最上面加空白選項</summary>
    public static List<ComboItem> BuildItems(FieldDefinition f, string connectionString, bool withBlank)
    {
        var items = new List<ComboItem>();
        if (withBlank) items.Add(new ComboItem("", ""));
        items.AddRange(ParseItems(f.Items));
        if (!string.IsNullOrWhiteSpace(f.ItemsSql) && !string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var d = DbDialect.Of(connectionString);
                using var conn = d.Open(connectionString);
                using var cmd = d.CreateCommand(conn, d.PrepareUserSql(f.ItemsSql).Sql);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string value = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)).Trim();
                    string text = reader.FieldCount > 1 && !reader.IsDBNull(1) ? Convert.ToString(reader.GetValue(1)) : value;
                    items.Add(new ComboItem(value, text));
                }
            }
            catch (Exception ex)
            {
                // 選項 SQL 寫錯時不擋住整個畫面，只在選單裡顯示錯誤
                items.Add(new ComboItem("", "（選項 SQL 錯誤：" + ex.Message + "）"));
            }
        }
        return items;
    }

    /// <summary>解析固定選項：逗號或換行分隔，「值=顯示」或只有值</summary>
    public static IEnumerable<ComboItem> ParseItems(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (string part in text.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            yield return eq > 0 ? new ComboItem(part[..eq].Trim(), part[(eq + 1)..].Trim()) : new ComboItem(part, part);
        }
    }

    /// <summary>從控制項取值並轉成資料庫型別；格式不對時丟出含欄位名稱的訊息</summary>
    public static object GetValue(FieldDefinition f, Control c)
    {
        string text;
        switch (c)
        {
            case CheckBox cb:
                // bit 欄位直接存 true / false；其他欄位存「勾選時的值」「取消時的值」，再依欄位型別轉換
                if (TypeHelper.IsBool(f.DataType) || cb.Tag is not CheckValues values) return cb.Checked;
                text = cb.Checked ? values.Checked : values.Unchecked;
                break;
            case DateTimePicker dtp:
                if (TypeHelper.IsDate(f.DataType))
                {
                    if (dtp.ShowCheckBox && !dtp.Checked) return DBNull.Value;
                    return f.DataType == "date" ? dtp.Value.Date : dtp.Value;
                }
                // 文字、數字欄位：依日期格式轉成文字；沒有日期 = 空白（可 NULL 的文字欄位存 NULL）
                text = dtp.ShowCheckBox && !dtp.Checked ? "" : dtp.Value.ToString(dtp.Tag as string ?? DefaultDateFormat, CultureInfo.InvariantCulture);
                break;
            case RadioGroup radios:
                text = radios.SelectedValue ?? "";
                break;
            case ComboBox combo:
                // 選了選項就用選項的值；可自行輸入時沒選選項就用打的文字
                text = combo.SelectedItem is ComboItem item ? item.Value
                     : combo.DropDownStyle == ComboBoxStyle.DropDown ? combo.Text : "";
                break;
            default:
                if (shownDates.TryGetValue(c, out var shown) && shown.Item1 == c.Text && TypeHelper.IsDate(f.DataType)) return shown.Item2;
                text = c.Text;
                break;
        }
        if (TypeHelper.IsString(f.DataType))
            return text == "" && f.IsNullable ? DBNull.Value : text;
        if (text.Trim() == "") return DBNull.Value;
        if (TypeHelper.IsBool(f.DataType))
            return text.Trim() is "1" or "Y" or "y" || text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        try
        {
            return TypeHelper.Parse(text, f.DataType);
        }
        catch (Exception)
        {
            throw new FormatException($"「{f.Label}」的格式不正確：{text}");
        }
    }

    public static void SetValue(Control c, object value)
    {
        bool isNull = value == null || value == DBNull.Value;
        switch (c)
        {
            case CheckBox cb:
                if (isNull) cb.Checked = false;
                else if (value is bool flag) cb.Checked = flag;
                else if (cb.Tag is CheckValues values)
                    cb.Checked = Convert.ToString(value).Trim().Equals(values.Checked.Trim(), StringComparison.OrdinalIgnoreCase);
                else cb.Checked = Convert.ToBoolean(value);
                break;
            case DateTimePicker dtp:
            {
                // 文字、數字欄位存的是格式化的日期文字（Tag 放格式）：解析失敗或空白都當成沒有日期
                DateTime? date = null;
                if (!isNull && dtp.Tag is string format)
                {
                    if (DateTime.TryParseExact(Convert.ToString(value).Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        date = parsed;
                }
                else if (!isNull)
                {
                    date = Convert.ToDateTime(value);
                }
                if (date == null)
                {
                    if (dtp.ShowCheckBox) dtp.Checked = false;
                    dtp.Value = DateTime.Today;
                }
                else
                {
                    dtp.Value = date.Value;
                    if (dtp.ShowCheckBox) dtp.Checked = true;
                }
                break;
            }
            case RadioGroup radios:
                radios.SelectedValue = isNull ? null : (value is bool b2 ? (b2 ? "1" : "0") : Convert.ToString(value)).Trim();
                break;
            case ComboBox combo:
            {
                // 用值找選項（去掉 nchar 補的空白再比對）；找不到時，可自行輸入就直接顯示值
                string s = isNull ? "" : (value is bool b ? (b ? "1" : "0") : Convert.ToString(value)).Trim();
                int index = -1;
                for (int i = 0; i < combo.Items.Count; i++)
                    if (combo.Items[i] is ComboItem item && item.Value.Trim() == s) { index = i; break; }
                // 開窗選取帶回來的值不在選項裡：加進選項，才顯示得出來
                if (index < 0 && s != "" && combo.DropDownStyle == ComboBoxStyle.DropDownList && ButtonRunner.HasPicker(combo))
                    index = combo.Items.Add(new ComboItem(s, s));
                combo.SelectedIndex = index;
                if (index < 0 && combo.DropDownStyle == ComboBoxStyle.DropDown) combo.Text = s;
                break;
            }
            default:
                shownDates.Remove(c);
                if (value is DateTime dateValue)
                {
                    c.Text = dateValue.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                    shownDates.Add(c, Tuple.Create(c.Text, dateValue));
                }
                else c.Text = isNull ? "" : Convert.ToString(value);
                break;
        }
    }

    /// <summary>把 Grid 欄位轉成單筆編輯用的欄位定義（位置、寬度用編輯視窗版面）；readOnly = 只顯示不能改</summary>
    public static FieldDefinition FromGridColumn(GridColumnDefinition c, bool readOnly) => new()
    {
        Column = c.Column,
        DataType = c.DataType,
        MaxLength = c.MaxLength,
        IsNullable = c.IsNullable,
        IsIdentity = c.IsIdentity,
        IsKey = c.IsKey,
        Label = string.IsNullOrWhiteSpace(c.EditLabel) ? c.Header : c.EditLabel,
        Placeholder = c.EditPlaceholder,
        FontSize = c.EditFontSize,
        LabelFontSize = c.EditLabelFontSize,
        FontName = c.EditFontName,
        BackColorHtml = c.EditBackColorHtml,
        LabelWidth = c.EditLabelWidth > 0 ? c.EditLabelWidth : FieldBox.DefaultLabelWidth,
        InputType = c.EditInputType,
        Items = c.EditItems,
        ItemsSql = c.EditItemsSql,
        AllowFreeText = c.EditAllowFreeText,
        PickerForm = c.EditPickerForm,
        PickerParams = c.EditPickerParams,
        PickerReturn = c.EditPickerReturn,
        AfterSelectSql = c.EditAfterSelectSql,
        AfterSelectParams = c.EditAfterSelectParams,
        AfterSelectReturn = c.EditAfterSelectReturn,
        X = c.EditX,
        Y = c.EditY,
        DateFormat = c.EditDateFormat,
        NumberRule = c.EditNumberRule,
        DefaultValue = c.EditDefaultValue,
        IsReadOnly = c.EditReadOnly,
        CheckedValue = string.IsNullOrEmpty(c.EditCheckedValue) ? "1" : c.EditCheckedValue,
        UncheckedValue = c.EditUncheckedValue ?? "0",
        Width = c.EditWidth > 0 ? c.EditWidth : Math.Clamp(c.Width, 120, 400),
        Height = c.EditHeight > 0 ? c.EditHeight : FieldDefinition.DefaultHeight,
        NoEntry = readOnly,
    };

    /// <summary>
    /// 依欄位的 X、Y 擺放「標籤 + 輸入框」（跟設計畫面上的欄位方塊同樣的幾何），
    /// 回傳每個欄位的輸入控制項；body 會加入這些控制項
    /// </summary>
    public static Dictionary<FieldDefinition, Control> LayoutFields(Control body, IEnumerable<FieldDefinition> fields, string connectionString = null)
    {
        var inputs = new Dictionary<FieldDefinition, Control>();
        Color labelColor = ColorText.LabelColor(body.BackColor);       // 底色深時標籤用白字
        foreach (var f in fields)
        {
            if (!f.IsTextLabel)                              // 文字標籤本身就是文字，不另外放標籤
            body.Controls.Add(new Label
            {
                Text = f.Label,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                ForeColor = labelColor,
                Font = LabelGeometry.Font(f, body.Font),                                              // 標籤字體大小
                Bounds = new Rectangle(f.X, f.Y, f.LabelWidth, LabelGeometry.Height(LabelGeometry.Font(f, body.Font))),   // 字放大時標籤跟著變高
            });
            Control input = CreateInput(f, connectionString);
            if (input is RadioGroup || f.IsTextLabel) input.ForeColor = labelColor;
            ApplyLook(f, input, body.Font);                  // 字體大小、底色
            RoundedStyle.Place(body, input, new Rectangle(f.X + f.LabelWidth + 4, f.Y, Math.Max(f.Width, 30), Math.Max(f.Height, FieldDefinition.DefaultHeight)));   // 圓角外觀
            inputs[f] = input;
        }
        return inputs;
    }

    /// <summary>欄位排版後需要的內容大小（右下角再留邊）</summary>
    public static Size ContentSize(IEnumerable<FieldDefinition> fields, int minWidth)
    {
        int w = minWidth, h = 60;
        foreach (var f in fields)
        {
            w = Math.Max(w, f.X + f.LabelWidth + 4 + Math.Max(f.Width, 30) + 30);
            h = Math.Max(h, f.Y + Math.Max(f.Height, FieldDefinition.DefaultHeight) + 20);
        }
        return new Size(w, h);
    }
}
