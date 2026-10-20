using Microsoft.Data.SqlClient;

namespace FormBuilder;

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
        if (f.InputType == InputKind.下拉選單)
            return CreateCombo(f, connectionString);
        if (TypeHelper.IsBool(f.DataType))
            return new CheckBox();
        if (TypeHelper.IsDate(f.DataType))
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = f.DataType == "date" ? "yyyy/MM/dd" : "yyyy/MM/dd HH:mm:ss",
                ShowCheckBox = f.IsNullable,              // 允許 NULL 時，取消勾選代表沒有日期
            };
        var tb = new TextBox { PlaceholderText = f.Placeholder ?? "" };
        if (TypeHelper.IsString(f.DataType) && f.MaxLength > 0) tb.MaxLength = f.MaxLength;
        if (TypeHelper.IsNumber(f.DataType)) tb.TextAlign = HorizontalAlignment.Right;
        return tb;
    }

    /// <summary>
    /// 下拉選單：選項 = 固定選項 + 選項 SQL 查出來的。
    /// 允許 NULL（或可自行輸入）時，最上面多一個空白選項代表「不選」
    /// </summary>
    private static ComboBox CreateCombo(FieldDefinition f, string connectionString)
    {
        var cb = new ComboBox { DropDownStyle = f.AllowFreeText ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList };
        var items = new List<ComboItem>();
        if (f.IsNullable || f.AllowFreeText) items.Add(new ComboItem("", ""));
        items.AddRange(ParseItems(f.Items));
        if (!string.IsNullOrWhiteSpace(f.ItemsSql) && !string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(f.ItemsSql, conn);
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
        cb.Items.AddRange(items.ToArray());
        return cb;
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
                return cb.Checked;
            case DateTimePicker dtp:
                if (dtp.ShowCheckBox && !dtp.Checked) return DBNull.Value;
                return f.DataType == "date" ? dtp.Value.Date : dtp.Value;
            case ComboBox combo:
                // 選了選項就用選項的值；可自行輸入時沒選選項就用打的文字
                text = combo.SelectedItem is ComboItem item ? item.Value
                     : combo.DropDownStyle == ComboBoxStyle.DropDown ? combo.Text : "";
                break;
            default:
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
                cb.Checked = !isNull && Convert.ToBoolean(value);
                break;
            case DateTimePicker dtp:
                if (isNull)
                {
                    if (dtp.ShowCheckBox) dtp.Checked = false;
                    dtp.Value = DateTime.Today;
                }
                else
                {
                    dtp.Value = Convert.ToDateTime(value);
                    if (dtp.ShowCheckBox) dtp.Checked = true;
                }
                break;
            case ComboBox combo:
            {
                // 用值找選項（去掉 nchar 補的空白再比對）；找不到時，可自行輸入就直接顯示值
                string s = isNull ? "" : (value is bool b ? (b ? "1" : "0") : Convert.ToString(value)).Trim();
                int index = -1;
                for (int i = 0; i < combo.Items.Count; i++)
                    if (combo.Items[i] is ComboItem item && item.Value.Trim() == s) { index = i; break; }
                combo.SelectedIndex = index;
                if (index < 0 && combo.DropDownStyle == ComboBoxStyle.DropDown) combo.Text = s;
                break;
            }
            default:
                c.Text = isNull ? "" : Convert.ToString(value);
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
        LabelWidth = c.EditLabelWidth > 0 ? c.EditLabelWidth : FieldBox.DefaultLabelWidth,
        InputType = c.EditInputType,
        Items = c.EditItems,
        ItemsSql = c.EditItemsSql,
        AllowFreeText = c.EditAllowFreeText,
        X = c.EditX,
        Y = c.EditY,
        Width = c.EditWidth > 0 ? c.EditWidth : Math.Clamp(c.Width, 120, 400),
        NoEntry = readOnly,
    };

    /// <summary>
    /// 依欄位的 X、Y 擺放「標籤 + 輸入框」（跟設計畫面上的欄位方塊同樣的幾何），
    /// 回傳每個欄位的輸入控制項；body 會加入這些控制項
    /// </summary>
    public static Dictionary<FieldDefinition, Control> LayoutFields(Control body, IEnumerable<FieldDefinition> fields, string connectionString = null)
    {
        var inputs = new Dictionary<FieldDefinition, Control>();
        foreach (var f in fields)
        {
            body.Controls.Add(new Label
            {
                Text = f.Label,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Bounds = new Rectangle(f.X, f.Y + 2, f.LabelWidth, 22),
            });
            Control input = CreateInput(f, connectionString);
            RoundedStyle.Place(body, input, new Rectangle(f.X + f.LabelWidth + 4, f.Y, Math.Max(f.Width, 30), 26));   // 圓角外觀
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
            h = Math.Max(h, f.Y + 26 + 20);
        }
        return new Size(w, h);
    }
}
