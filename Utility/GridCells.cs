using System.ComponentModel;
using System.Globalization;

namespace FormBuilder;

/// <summary>
/// Grid 的欄位：依「輸入方式」建立對應的 DataGridView 欄位（文字框、下拉選單、勾選框、日期選擇）。
/// 執行畫面的單身 / 單頭清單與編輯視窗的明細清單共用
/// </summary>
public static class GridCells
{
    /// <summary>「自動」實際會用哪一種：bit = 勾選框、date / datetime = 日期選擇，其他 = 文字框</summary>
    public static GridCellKind Resolve(GridColumnDefinition c) => c.CellType != GridCellKind.自動 ? c.CellType
        : TypeHelper.IsBool(c.DataType) ? GridCellKind.勾選框
        : TypeHelper.IsDate(c.DataType) ? GridCellKind.日期選擇
        : GridCellKind.文字框;

    /// <param name="connectionString">下拉選單的選項 SQL 用</param>
    public static DataGridViewColumn CreateColumn(GridColumnDefinition c, string connectionString)
    {
        DataGridViewColumn col = Resolve(c) switch
        {
            GridCellKind.下拉選單 => CreateCombo(c, connectionString),
            GridCellKind.勾選框 => CreateCheckBox(c),
            GridCellKind.日期選擇 => new CalendarColumn(c),
            _ => CreateTextBox(c),
        };
        col.DataPropertyName = c.Column;
        col.HeaderText = c.Header;
        col.Width = c.Width;
        col.Tag = c;
        if (TypeHelper.IsNumber(c.DataType) && col is DataGridViewTextBoxColumn)
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return col;
    }

    /// <summary>生效欄位顯示用的勾選框欄位名稱（HookEffectiveColumn 靠它找到要轉換的欄位）</summary>
    public const string EffectiveColumnName = "__effective";

    /// <summary>
    /// 生效欄位：顯示成唯讀勾選框（只看不改，生效 / 取消生效要用按鈕）。
    /// 欄位是文字或數字時，值是 1、Y、True 就打勾（跟生效按鈕的判斷一樣），要配合 HookEffectiveColumn
    /// </summary>
    public static DataGridViewColumn CreateEffectiveColumn(GridColumnDefinition c) => new DataGridViewCheckBoxColumn
    {
        Name = EffectiveColumnName,
        DataPropertyName = c.Column,
        HeaderText = c.Header,
        Width = Math.Min(c.Width, 70),
        ReadOnly = true,
        Tag = c,
    };

    /// <summary>把生效欄位的值（文字、數字）轉成打勾 / 不打勾顯示</summary>
    public static void HookEffectiveColumn(DataGridView grid)
    {
        if (grid.Columns[EffectiveColumnName] == null) return;
        grid.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != EffectiveColumnName) return;
            e.Value = GridDefinition.IsEffectiveValue(e.Value);
            e.FormattingApplied = true;
        };
    }

    private static DataGridViewTextBoxColumn CreateTextBox(GridColumnDefinition c)
    {
        var col = new DataGridViewTextBoxColumn();
        if (TypeHelper.IsDate(c.DataType)) col.DefaultCellStyle.Format = "yyyy/MM/dd";       // 日期欄位一律只顯示日期，不顯示時分秒
        if (TypeHelper.IsString(c.DataType) && c.MaxLength > 0) col.MaxInputLength = c.MaxLength;
        return col;
    }

    /// <summary>下拉選單的一個選項（Value 已轉成欄位的型別，才能跟資料表裡的值比對）</summary>
    public sealed class Option
    {
        public object Value { get; init; }
        public string Text { get; init; }
    }

    /// <summary>下拉選單：選項 = 「選項 / 格式」填的固定選項（值=顯示，逗號分隔），或 SELECT 開頭的選項 SQL</summary>
    private static DataGridViewComboBoxColumn CreateCombo(GridColumnDefinition c, string connectionString)
    {
        string options = c.CellOptions?.Trim() ?? "";
        bool isSql = options.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || options.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        var source = new FieldDefinition { Items = isSql ? null : options, ItemsSql = isSql ? options : null };
        var items = new List<Option>();
        if (c.IsNullable) items.Add(new Option { Value = DBNull.Value, Text = "" });
        foreach (var item in InputHelper.BuildItems(source, connectionString, withBlank: false))
        {
            object value = ToColumnValue(item.Value, c);
            if (value != null) items.Add(new Option { Value = value, Text = item.Text });
        }
        return new DataGridViewComboBoxColumn
        {
            DataSource = items,
            ValueMember = nameof(Option.Value),
            DisplayMember = nameof(Option.Text),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,   // 平常像文字，進入編輯才出現下拉箭頭
            FlatStyle = FlatStyle.Flat,
        };
    }

    /// <summary>勾選框：bit 欄位直接用 True / False；其他欄位用「選項 / 格式」填的「勾選值/取消值」（例如 Y/N，預設 1/0）</summary>
    private static DataGridViewCheckBoxColumn CreateCheckBox(GridColumnDefinition c)
    {
        var col = new DataGridViewCheckBoxColumn();
        if (TypeHelper.IsBool(c.DataType)) return col;
        string[] parts = (c.CellOptions ?? "").Split('/', StringSplitOptions.TrimEntries);
        string on = parts.Length > 0 && parts[0] != "" ? parts[0] : "1";
        string off = parts.Length > 1 && parts[1] != "" ? parts[1] : "0";
        col.TrueValue = ToColumnValue(on, c) ?? on;
        col.FalseValue = ToColumnValue(off, c) ?? off;
        col.IndeterminateValue = DBNull.Value;
        return col;
    }

    /// <summary>
    /// 把選項的文字值轉成欄位的型別：數字欄位轉數字；char / nchar 補空白到欄位長度（資料庫讀出來的值有補空白）。
    /// 轉不過去回傳 null（這個選項不用）
    /// </summary>
    private static object ToColumnValue(string text, GridColumnDefinition c)
    {
        if (TypeHelper.IsString(c.DataType))
            return c.DataType is "char" or "nchar" && c.MaxLength > 0 ? text.PadRight(c.MaxLength) : text;
        if (TypeHelper.IsBool(c.DataType))
            return text.Trim() is "1" or "Y" or "y" || text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        try
        {
            return TypeHelper.Parse(text, c.DataType);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 停用下拉選單、勾選框顯示時的格式錯誤訊息（資料表裡有選項以外的值時，格子顯示空白，不要一直跳訊息）；
    /// 回傳 true = 已處理
    /// </summary>
    public static bool IgnoreDisplayError(DataGridView grid, DataGridViewDataErrorEventArgs e)
    {
        bool display = e.Context.HasFlag(DataGridViewDataErrorContexts.Formatting) || e.Context.HasFlag(DataGridViewDataErrorContexts.Display)
                    || e.Context.HasFlag(DataGridViewDataErrorContexts.PreferredSize);
        if (!display || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex] is not (DataGridViewComboBoxColumn or DataGridViewCheckBoxColumn)) return false;
        e.ThrowException = false;
        return true;
    }

    private static readonly Color GridLineColor = Color.FromArgb(218, 218, 218);

    /// <summary>
    /// 設計畫面上的 Grid 示意（單身 / 單頭清單 / 編輯視窗的明細清單共用）：圓角外框、欄位標題、空白資料列，
    /// 跟執行時一樣有橫線和直線
    /// </summary>
    public static void PaintPreview(Graphics g, Control box, IEnumerable<GridColumnDefinition> columns, int headerHeight, int rowHeight, bool selected)
    {
        g.Clear(RoundedStyle.BackgroundOf(box.Parent));            // 圓角外面露出畫面的底色
        int w = box.Width, h = box.Height;
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = RoundedStyle.RoundRect(new RectangleF(0.5f, 0.5f, w - 1f, h - 1f), RoundedStyle.Radius);
        g.FillPath(SystemBrushes.Window, path);
        g.SmoothingMode = old;

        var clip = g.Clip;
        g.SetClip(path);
        g.FillRectangle(SystemBrushes.Control, 0, 0, w, headerHeight);
        using var linePen = new Pen(GridLineColor);
        // 空白資料列的橫線
        for (int y = headerHeight + rowHeight; y < h; y += rowHeight)
            g.DrawLine(linePen, 0, y, w, y);
        // 欄位標題 + 每個欄位右邊的直線（從標題一路到底）
        int x = 0;
        foreach (var c in columns.Where(c => c.Visible))
        {
            var cell = new Rectangle(x, 0, c.Width, headerHeight);
            TextRenderer.DrawText(g, c.Header, box.Font, Rectangle.Inflate(cell, -4, 0), SystemColors.ControlText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += c.Width;
            g.DrawLine(SystemPens.ControlDark, x - 1, 0, x - 1, headerHeight);
            g.DrawLine(linePen, x - 1, headerHeight, x - 1, h);
            if (x > w) break;
        }
        g.DrawLine(SystemPens.ControlDark, 0, headerHeight, w, headerHeight);
        g.Clip = clip;

        // 外框：選取時用較粗的藍框
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (selected)
        {
            using var pen = new Pen(Color.DodgerBlue, 3);
            using var inner = RoundedStyle.RoundRect(new RectangleF(1.5f, 1.5f, w - 3f, h - 3f), RoundedStyle.Radius - 1);
            g.DrawPath(pen, inner);
        }
        else
        {
            using var pen = new Pen(RoundedStyle.BorderColor);
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = old;
    }

    /// <summary>Grid 一律有橫線和直線（格線）</summary>
    public static void ApplyGridLines(DataGridView grid)
    {
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = GridLineColor;
    }
}

/// <summary>Grid 儲存格的輸入方式</summary>
public enum GridCellKind
{
    /// <summary>依資料型別：bit = 勾選框、日期 = 日期選擇，其他 = 文字框</summary>
    自動,
    文字框,
    下拉選單,
    勾選框,
    日期選擇,
}

// ================= 日期選擇欄位（DataGridView 沒有內建，照 MSDN 的 CalendarColumn 做法） =================

/// <summary>
/// 日期選擇欄位：date / datetime 欄位直接存日期；文字、數字欄位依「選項 / 格式」填的日期格式（預設 yyyyMMdd）存成文字
/// </summary>
public class CalendarColumn : DataGridViewColumn
{
    /// <summary>文字、數字欄位存進資料庫的日期格式；null = date / datetime 欄位</summary>
    public string StorageFormat { get; set; }
    /// <summary>畫面上顯示的格式</summary>
    public string DisplayFormat { get; set; } = "yyyy/MM/dd";

    public CalendarColumn() : base(new CalendarCell()) { }

    public CalendarColumn(GridColumnDefinition c) : this()
    {
        bool nativeDate = TypeHelper.IsDate(c.DataType);
        StorageFormat = nativeDate ? null : string.IsNullOrWhiteSpace(c.CellOptions) ? "yyyyMMdd" : c.CellOptions.Trim();
        // 一律只顯示日期（DisplayFormat 預設值），不顯示時分秒
    }

    public override DataGridViewCell CellTemplate
    {
        get => base.CellTemplate;
        set
        {
            if (value != null && value is not CalendarCell) throw new InvalidCastException("必須是 CalendarCell");
            base.CellTemplate = value;
        }
    }

    public override object Clone()
    {
        var clone = (CalendarColumn)base.Clone();
        clone.StorageFormat = StorageFormat;
        clone.DisplayFormat = DisplayFormat;
        return clone;
    }
}

public class CalendarCell : DataGridViewTextBoxCell
{
    private CalendarColumn Column => OwningColumn as CalendarColumn;
    private string DisplayFormat => Column?.DisplayFormat ?? "yyyy/MM/dd";

    public override Type EditType => typeof(CalendarEditingControl);
    public override Type FormattedValueType => typeof(string);

    /// <summary>格子裡的值 → 日期（文字欄位依存檔格式解析；解析不了 = 沒有日期）</summary>
    public DateTime? ToDate(object value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime d) return d;
        string format = Column?.StorageFormat;
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (format != null && DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        return DateTime.TryParse(text, out var parsed) ? parsed : null;
    }

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
                                                TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter,
                                                DataGridViewDataErrorContexts context) =>
        ToDate(value)?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "";

    /// <summary>畫面上的文字 → 存進資料表的值（date 欄位 = 日期；文字、數字欄位 = 依存檔格式的文字 / 數字）</summary>
    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
                                               TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        string text = Convert.ToString(formattedValue)?.Trim();
        if (string.IsNullOrEmpty(text)) return DBNull.Value;
        if (!DateTime.TryParseExact(text, DisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && !DateTime.TryParse(text, out date))
            throw new FormatException("日期格式不正確：" + text);
        // 畫面只顯示日期：原本有時間的話保留原本的時間，改日期不會把時間變成 00:00
        if (RowIndex >= 0 && ToDate(GetValue(RowIndex)) is { } old) date = date.Date + old.TimeOfDay;
        string format = Column?.StorageFormat;
        if (format == null) return date;
        string stored = date.ToString(format, CultureInfo.InvariantCulture);
        // 數字欄位（例如 int 存 20261021）轉回數字型別
        return ValueType != null && ValueType != typeof(string) ? Convert.ChangeType(stored, ValueType, CultureInfo.InvariantCulture) : stored;
    }

    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
        if (DataGridView.EditingControl is CalendarEditingControl picker)
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = DisplayFormat;
            picker.Value = ToDate(GetValue(rowIndex)) ?? DateTime.Today;
        }
    }
}

/// <summary>日期欄位編輯時出現的日期選擇器</summary>
public class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
{
    private bool valueChanged;

    public DataGridView EditingControlDataGridView { get; set; }
    public int EditingControlRowIndex { get; set; }
    public bool EditingControlValueChanged { get => valueChanged; set => valueChanged = value; }
    public Cursor EditingPanelCursor => base.Cursor;
    public bool RepositionEditingControlOnValueChange => false;

    public object EditingControlFormattedValue
    {
        get => Value.ToString(CustomFormat, CultureInfo.InvariantCulture);
        set
        {
            if (value is string s && DateTime.TryParseExact(s, CustomFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) Value = d;
        }
    }

    public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

    public void ApplyCellStyleToEditingControl(DataGridViewCellStyle style)
    {
        Font = style.Font;
        CalendarForeColor = style.ForeColor;
        CalendarMonthBackground = style.BackColor;
    }

    /// <summary>方向鍵、數字鍵等留給日期選擇器自己處理（換日、換月）</summary>
    public bool EditingControlWantsInputKey(Keys key, bool dataGridViewWantsInputKey) => (key & Keys.KeyCode) switch
    {
        Keys.Left or Keys.Up or Keys.Down or Keys.Right or Keys.Home or Keys.End or Keys.PageDown or Keys.PageUp => true,
        _ => !dataGridViewWantsInputKey,
    };

    public void PrepareEditingControlForEdit(bool selectAll) { }

    protected override void OnValueChanged(EventArgs eventargs)
    {
        valueChanged = true;
        EditingControlDataGridView?.NotifyCurrentCellDirty(true);
        base.OnValueChanged(eventargs);
    }
}

// ================= 圓角 Grid =================

/// <summary>
/// 圓角 Grid 的外框：DataGridView 本身不能畫圓角，所以放進這個面板，
/// 面板畫圓角框線，Grid 縮在框內並用 Region 裁成圓角（捲軸也一起裁掉角落）
/// </summary>
public class RoundedGridHost : Panel
{
    public DataGridView Grid { get; }

    public RoundedGridHost(DataGridView grid)
    {
        Grid = grid;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // 位置、大小、停靠方式交給外框，Grid 填滿框內
        Bounds = grid.Bounds;
        Dock = grid.Dock;
        Anchor = grid.Anchor;
        Padding = new Padding(1);
        grid.BorderStyle = BorderStyle.None;
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);
        grid.SizeChanged += (s, e) => ClipGrid();
        grid.Enter += (s, e) => Invalidate();
        grid.Leave += (s, e) => Invalidate();
        grid.EnabledChanged += (s, e) => Invalidate();
        grid.BackgroundColorChanged += (s, e) => Invalidate();   // 單身輸入中 / 瀏覽時底色不同
        grid.VisibleChanged += (s, e) => Visible = grid.Visible;
        ClipGrid();
    }

    private void ClipGrid()
    {
        if (Grid.Width <= 0 || Grid.Height <= 0) return;
        using var path = RoundedStyle.RoundRect(new RectangleF(0, 0, Grid.Width, Grid.Height), RoundedStyle.Radius - 1);
        Grid.Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(RoundedStyle.BackgroundOf(Parent));     // 圓角外面露出父控制項的底色
        RoundedStyle.DrawBox(e.Graphics, ClientRectangle, Grid.BackgroundColor,
                             Grid.ContainsFocus ? RoundedStyle.FocusColor : RoundedStyle.BorderColor);
    }

    /// <summary>把 Grid 包進圓角外框，回傳要加到畫面上的外框</summary>
    public static RoundedGridHost Wrap(DataGridView grid) => new(grid);
}
