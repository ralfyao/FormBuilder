using System.ComponentModel;
using System.Data;

namespace FormBuilder;

/// <summary>
/// 編輯視窗版面設計：設計使用者雙擊 Grid 時彈出的編輯視窗。
/// 左邊「欄位」頁籤拖欄位、「工具箱」頁籤拖按鈕或下拉選單到版面上；
/// 版面上可拖曳移動、拖邊緣調整大小、圈選或 Ctrl 點選後按 Delete 刪除。按「確定」才寫回 GridDefinition
/// </summary>
public partial class EditLayoutDialog : Form
{
    private readonly GridDefinition grid;
    /// <summary>可以放上版面的欄位：Grid 的欄位，加上這裡「JOIN 其他資料表」新帶進來的欄位（接在後面，確定時才加到 Grid）</summary>
    private readonly List<GridColumnDefinition> columns;
    /// <summary>這裡新加的 JOIN（確定時才寫進 joinTarget）</summary>
    private readonly List<GridJoinDefinition> newJoins = new();
    /// <summary>JOIN 要寫到哪裡：單頭清單是畫面的單頭 JOIN，一般 Grid 是 Grid 自己的 Joins</summary>
    private readonly List<GridJoinDefinition> joinTarget;
    /// <summary>JOIN 帶進來的欄位不能用的名稱與別名（單頭已經用掉的欄位、設定視窗裡還沒存的 JOIN）</summary>
    private readonly List<string> reservedNames;
    private readonly List<string> reservedAliases;
    /// <summary>版面上的欄位（用 FieldDefinition 暫存位置，確定時再寫回 Grid 欄位）</summary>
    private readonly Dictionary<GridColumnDefinition, FieldBox> placed = new();
    /// <summary>版面上的按鈕（編輯的是複本，確定時才寫回）</summary>
    private readonly List<ButtonBox> buttonBoxes = new();
    /// <summary>版面上的明細清單（編輯的是複本，確定時才寫回）</summary>
    private readonly List<EditListBox> listBoxes = new();
    /// <summary>版面上的文字標籤（編輯的是複本，確定時才寫回）</summary>
    private readonly List<FieldBox> labelBoxes = new();
    private readonly List<DesignBox> selection = new();
    /// <summary>「資料表」頁籤要用的連線字串；沒有連線時不能加明細清單</summary>
    private readonly string connectionString;
    private List<(string Schema, string Name)> allTables;
    private Dictionary<DesignBox, Point> dragStarts;

    /// <summary>編輯視窗欄位區的大小（拖版面的右邊、下邊、右下角調整）</summary>
    private Size formSize;
    private readonly CanvasResizer canvasResizer;

    // 圈選
    private SelectionBand band;
    private bool bandAdditive;

    /// <summary>生效欄位（按「確定」才寫回 grid）</summary>
    private string effectiveColumn;

    /// <summary>編輯視窗底色（Color.Empty = 預設灰色；按「確定」才寫回 grid）</summary>
    private Color backColor;

    private void SetBackColor(Color color)
    {
        backColor = color;
        canvas.BackColor = ColorText.BackOrDefault(color);
        foreach (var box in canvas.Controls.OfType<FieldBox>()) box.RefreshFromModel();       // 標籤顏色跟著底色
    }

    /// <summary>「檔案 → 儲存」：存檔整個畫面定義檔（由設計畫面提供）；null = 不能存檔</summary>
    private readonly Action saveForm;
    /// <summary>畫面的字型（編輯視窗沒設定字型時跟它一樣）</summary>
    private readonly Font screenFont;
    /// <summary>編輯視窗的字型設定（按「確定」或「儲存」才寫回 grid）</summary>
    private string editFontName;
    private float editFontSize;

    /// <param name="masterJoins">單頭清單才要傳：畫面的單頭 JOIN（這裡 JOIN 的資料表加在單頭主查詢上）</param>
    /// <param name="reservedNames">JOIN 帶進來的欄位不能用的名稱（單頭已經用掉的欄位等）</param>
    /// <param name="reservedAliases">JOIN 不能用的別名（設定視窗裡還沒存的 JOIN）</param>
    public EditLayoutDialog(GridDefinition gridDefinition, string connectionString = null, Action saveForm = null, Font screenFont = null,
                            List<GridJoinDefinition> masterJoins = null, IEnumerable<string> reservedNames = null, IEnumerable<string> reservedAliases = null)
    {
        InitializeComponent();
        grid = gridDefinition;
        columns = grid.Columns.ToList();
        joinTarget = masterJoins ?? grid.Joins;
        this.reservedNames = reservedNames?.ToList() ?? new List<string>();
        this.reservedAliases = reservedAliases?.ToList() ?? new List<string>();
        this.connectionString = connectionString;
        this.saveForm = saveForm;
        this.screenFont = screenFont ?? Font;
        Text = $"編輯視窗版面：{grid.Title ?? grid.TableName}";

        // ---- 版面上方：編輯視窗工具列的「生效 / 取消生效」要用哪個欄位 ----
        effectiveColumn = grid.EffectiveColumn;
        var noneItem = new ToolStripMenuItem("（不使用）") { Tag = null };
        btnEffectiveColumn.DropDownItems.Add(noneItem);
        btnEffectiveColumn.DropDownItems.Add(new ToolStripSeparator());
        foreach (var c in grid.Columns)
            btnEffectiveColumn.DropDownItems.Add(new ToolStripMenuItem(ColumnText(c)) { Tag = c.Column });
        btnEffectiveColumn.DropDownItemClicked += (s, e) =>
        {
            if (e.ClickedItem is not ToolStripMenuItem item) return;
            effectiveColumn = item.Tag as string;
            UpdateEffectiveButton();
        };
        UpdateEffectiveButton();

        // ---- 版面上方：編輯視窗的底色 ----
        miPickBackColor.Click += (s, e) =>
        {
            using var dlg = new ColorDialog { Color = ColorText.BackOrDefault(backColor), FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK) SetBackColor(dlg.Color);
        };
        miResetBackColor.Click += (s, e) => SetBackColor(Color.Empty);

        // ---- 左邊「欄位」頁籤：拖到版面或雙擊加入 ----
        lstAvailable.DisplayMember = nameof(GridColumnDefinition.Header);
        lstAvailable.MouseDown += (s, e) =>
        {
            int i = lstAvailable.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && i >= 0)
                lstAvailable.DoDragDrop(lstAvailable.Items[i], DragDropEffects.Copy);
        };
        lstAvailable.DoubleClick += (s, e) =>
        {
            if (lstAvailable.SelectedItem is GridColumnDefinition c) PlaceColumn(c, NextFreePoint());
        };
        // JOIN 其他資料表：帶出關聯資料表的欄位放到編輯視窗上（只顯示，例如用客戶編號帶出客戶名稱）
        var btnJoin = new Button { Text = "JOIN 其他資料表…", Dock = DockStyle.Bottom, Height = 28 };
        btnJoin.Click += (s, e) => AddJoin();
        if (!grid.IsSqlList) tabColumns.Controls.Add(btnJoin);         // SQL 清單的欄位來自 SQL，要帶其他資料表就寫在 SQL 裡
        lstAvailable.BringToFront();                       // Fill 最後排版，才不會被按鈕蓋住
        // ---- 左邊「工具箱」頁籤 ----
        toolbox.AddEntry("Label", "文字標籤 (Label)");
        toolbox.AddEntry("SqlList", "SQL 清單（Grid，唯讀）");
        toolbox.ItemActivated += kind => AddToolboxItem(kind, NextFreePoint());

        // ---- 左邊「資料表」頁籤：拖到版面或雙擊，加入依關聯欄位列出明細的清單 ----
        tabs.SelectedIndexChanged += (s, e) =>
        {
            if (tabs.SelectedTab == tabTables) LoadTables();
        };
        txtTableFilter.TextChanged += (s, e) => FillTableList();
        lstTables.MouseDown += (s, e) =>
        {
            int i = lstTables.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && i >= 0 && lstTables.Items[i] is TableItem t)
                lstTables.DoDragDrop(new TableDragData { Schema = t.Schema, Table = t.Name }, DragDropEffects.Copy);
        };
        lstTables.DoubleClick += (s, e) =>
        {
            if (lstTables.SelectedItem is TableItem t) AddListFromTable(t.Schema, t.Name, NextFreePoint());
        };

        // ---- 中間：版面 ----
        canvas.DragEnter += (s, e) =>
            e.Effect = e.Data.GetDataPresent(typeof(GridColumnDefinition)) || e.Data.GetDataPresent(typeof(ToolboxDragData))
                    || e.Data.GetDataPresent(typeof(TableDragData))
                ? DragDropEffects.Copy : DragDropEffects.None;
        canvas.DragDrop += (s, e) =>
        {
            Point p = canvas.PointToClient(new Point(e.X, e.Y));
            var location = new Point(Snap(p.X), Snap(p.Y));
            if (e.Data.GetData(typeof(GridColumnDefinition)) is GridColumnDefinition c) PlaceColumn(c, location);
            else if (e.Data.GetData(typeof(ToolboxDragData)) is ToolboxDragData tool) AddToolboxItem(tool.Kind, location);
            else if (e.Data.GetData(typeof(TableDragData)) is TableDragData table) AddListFromTable(table.Schema, table.Table, location);
        };
        // 拖版面邊緣調整大小；拖出外框會自動捲動，按住不放就持續延伸
        canvasResizer = new CanvasResizer(canvas, canvasHost, () => formSize, size => { formSize = size; UpdateCanvasSize(); });
        canvas.MouseDown += (s, e) => OnCanvasMouseDown(e);
        canvas.MouseMove += (s, e) => OnCanvasMouseMove(e);
        canvas.MouseUp += (s, e) => OnCanvasMouseUp();
        canvas.Paint += (s, e) =>                          // 右下角畫出可拖曳的格紋
            ControlPaint.DrawSizeGrip(e.Graphics, canvas.BackColor, canvas.ClientSize.Width - 16, canvas.ClientSize.Height - 16, 16, 16);

        // ---- 檔案選單：儲存（寫回版面並存檔整個畫面定義檔）----
        miSave.Enabled = miSaveClose.Enabled = saveForm != null;
        if (saveForm == null) miSave.ToolTipText = miSaveClose.ToolTipText = "從設計畫面開啟時才能存檔";
        miSave.Click += (s, e) => SaveLayout(close: false);
        miSaveClose.Click += (s, e) => SaveLayout(close: true);
        // 匯入現有畫面：用已經設計好的畫面（例如單頭畫面）當編輯視窗的版面
        btnFileMenu.DropDownItems.Insert(0, new ToolStripSeparator());
        btnFileMenu.DropDownItems.Insert(0, new ToolStripMenuItem("匯入現有畫面…", null, (s, e) => ImportForm())
        {
            ToolTipText = "選一個畫面定義檔，同名的欄位照那個畫面的位置、大小、標籤、輸入方式放上來，文字標籤、按鈕、版面大小、底色、字型也一起帶入",
        });
        btnFileMenu.DropDownItems.Insert(1, new ToolStripMenuItem("匯出成獨立畫面…", null, (s, e) => ExportForm())
        {
            ToolTipText = "把這個編輯視窗存成一個獨立的畫面定義檔：欄位（含下拉選單、開窗選取、執行後動作、預設值、編號規則的 SQL）、JOIN、按鈕、文字標籤、明細清單都一起帶過去",
        });

        // ---- 預覽：用目前的版面開一個編輯視窗看看（不用先存檔）----
        effectBar.Items.Insert(effectBar.Items.IndexOf(sepMenus), new ToolStripButton("預覽", null, (s, e) => Preview())
        {
            ToolTipText = "用目前的版面開編輯視窗，資料是資料表的前 50 筆；可以按新增、修改試著輸入，按確定不會存進資料庫",
        });

        // ---- 編輯選單：復原、刪除、排列，以及水平 / 垂直 / 大小的對齊 ----
        miUndo.Click += (s, e) => history.Undo();
        miRedo.Click += (s, e) => history.Redo();
        miRemove.Click += (s, e) => RemoveSelected();
        miArrange.Click += (s, e) => ArrangeAll();
        AlignHelper.AddAlignMenu(btnEditMenu.DropDownItems, () => selection.ToList(), UpdateCanvasSize);

        // ---- 字型：編輯視窗的字型與大小（沒設定 = 跟畫面一樣）----
        editFontName = grid.EditFontName;
        editFontSize = grid.EditFontSize;
        miPickFont.Click += (s, e) =>
        {
            using var dlg = new FontDialog { Font = canvas.Font, ShowEffects = false, FontMustExist = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            editFontName = dlg.Font.FontFamily.Name;
            editFontSize = (float)Math.Round(dlg.Font.SizeInPoints, 1);
            ApplyEditFont();
        };
        miResetFont.Click += (s, e) =>
        {
            editFontName = null;
            editFontSize = 0;
            ApplyEditFont();
        };

        // 右邊屬性面板：點選欄位後修改標籤、提示文字（placeholder）、寬度、位置
        propertyGrid.PropertyValueChanged += (s, e) =>
        {
            // 明細清單改了上層清單：用上層清單的欄位重新推測關聯欄位
            if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(EditListDefinition.ParentList))
                foreach (var l in selection.OfType<EditListBox>().Select(b => b.ListDef).Where(l => !l.IsSqlList)) GuessListLinks(l);
            foreach (var b in selection) b.RefreshFromModel();
            UpdateCanvasSize();
        };

        // ---- 下方按鈕 ----
        btnArrange.Click += (s, e) => ArrangeAll();
        btnRemove.Click += (s, e) => RemoveSelected();
        btnOk.Click += (s, e) => Apply();

        // 目前的版面（還沒設計過就先排成一欄）
        canvas.Font = CurrentEditFont();                                       // 先設字型，欄位示意才會照字型排
        UpdateFontButton();
        backColor = grid.EditBackColor;
        canvas.BackColor = ColorText.BackOrDefault(backColor);                 // 先設底色，欄位標籤才會依底色決定文字顏色
        grid.EnsureEditLayout();
        foreach (var c in grid.EditColumns) PlaceColumn(c, new Point(c.EditX, c.EditY), select: false);
        foreach (var b in grid.EditButtons) AddButtonBox(b.Clone(), select: false);
        foreach (var l in grid.EditLists) AddListBox(l.Clone(), select: false);
        foreach (var f in grid.EditTextLabels) AddLabelBox(f.Clone(), select: false);
        // 設計過大小就用設計的，沒有就依欄位自動決定
        formSize = grid.EditFormSize ?? InputHelper.ContentSize(placed.Values.Select(b => b.Field), 500);
        RefreshAvailable();
        SetSelection(Array.Empty<DesignBox>());
        UpdateCanvasSize();

        // Ctrl+Z 復原、Ctrl+Y 取消復原：記錄版面（欄位、按鈕、大小）的快照
        history = new UndoHistory(this, CaptureLayout, RestoreLayout);
    }

    private readonly UndoHistory history;

    /// <summary>復原用的版面快照</summary>
    private sealed class LayoutState
    {
        public List<PlacedField> Fields { get; set; } = new();
        public List<ButtonDefinition> Buttons { get; set; } = new();
        public List<EditListDefinition> Lists { get; set; } = new();
        public List<FieldDefinition> Labels { get; set; } = new();
        public int Width { get; set; }
        public int Height { get; set; }
        public string EffectiveColumn { get; set; }
        public string BackColorHtml { get; set; }
    }

    private sealed class PlacedField
    {
        public string Column { get; set; }
        public FieldDefinition Field { get; set; }
    }

    private string CaptureLayout() => System.Text.Json.JsonSerializer.Serialize(new LayoutState
    {
        Fields = placed.Select(p => new PlacedField { Column = p.Key.Column, Field = p.Value.Field }).ToList(),
        Buttons = buttonBoxes.Select(b => b.ButtonDef).ToList(),
        Lists = listBoxes.Select(b => b.ListDef).ToList(),
        Labels = labelBoxes.Select(b => b.Field).ToList(),
        Width = formSize.Width,
        Height = formSize.Height,
        EffectiveColumn = effectiveColumn,
        BackColorHtml = ColorText.Format(backColor),
    });

    private void RestoreLayout(string json)
    {
        var state = System.Text.Json.JsonSerializer.Deserialize<LayoutState>(json);
        foreach (var box in canvas.Controls.OfType<DesignBox>().ToList()) box.Dispose();
        canvas.Controls.Clear();
        placed.Clear();
        buttonBoxes.Clear();
        listBoxes.Clear();
        labelBoxes.Clear();
        foreach (var p in state.Fields)
        {
            var column = columns.FirstOrDefault(c => c.Column == p.Column);
            if (column == null) continue;
            var box = new FieldBox(p.Field);
            placed[column] = box;
            AttachBox(box);
        }
        foreach (var b in state.Buttons) AddButtonBox(b, select: false);
        foreach (var l in state.Lists ?? new List<EditListDefinition>()) AddListBox(l, select: false);
        foreach (var f in state.Labels ?? new List<FieldDefinition>()) AddLabelBox(f, select: false);
        formSize = new Size(state.Width, state.Height);
        effectiveColumn = state.EffectiveColumn;
        UpdateEffectiveButton();
        SetBackColor(ColorText.Parse(state.BackColorHtml));
        RefreshAvailable();
        SetSelection(Array.Empty<DesignBox>());
        UpdateCanvasSize();
    }

    /// <summary>
    /// 檔案 → 匯入現有畫面：讀一個畫面定義檔，跟這個 Grid 同名的欄位照那個畫面的位置、大小、標籤與輸入方式放上版面，
    /// 對不上的欄位加成「畫面欄位」（不查詢、不存檔，可以輸入，按鈕的 SQL 用 @欄位 取得）；
    /// 文字標籤、按鈕、版面大小、底色、字型也一起帶入（取代目前的版面，可以按 Ctrl+Z 復原）；Grid 不帶入
    /// </summary>
    private void ImportForm()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "匯入現有畫面",
            Filter = "畫面定義檔 (*.json)|*.json",
            InitialDirectory = Directory.Exists(FormDefinition.DefaultFolder) ? FormDefinition.DefaultFolder : null,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) ImportForm(dlg.FileName);
    }

    private void ImportForm(string path)
    {
        FormDefinition source;
        try
        {
            source = FormDefinition.Load(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取畫面定義檔失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var fields = new List<PlacedField>();
        var screenOnly = new List<GridColumnDefinition>();
        foreach (var f in source.Fields.Where(f => !f.IsTextLabel))
        {
            var column = columns.Concat(screenOnly).FirstOrDefault(c => c.Column.Equals(f.Column, StringComparison.OrdinalIgnoreCase));
            if (column != null && fields.Any(p => p.Column == column.Column)) continue;     // 來源畫面同一個欄位放了兩次
            if (column == null)
            {
                // 對不上的欄位：加成畫面欄位（Grid 上不顯示）
                column = new GridColumnDefinition
                {
                    Column = f.Column,
                    Header = string.IsNullOrWhiteSpace(f.Label) ? f.Column : f.Label,
                    DataType = f.DataType,
                    MaxLength = f.MaxLength,
                    IsNullable = true,
                    Width = Math.Clamp(f.Width, 60, 200),
                    Visible = false,
                    ScreenOnly = true,
                };
                screenOnly.Add(column);
            }
            // 複製一份（不動到來源畫面），主鍵、不能輸入照這個 Grid 的欄位
            var copy = System.Text.Json.JsonSerializer.Deserialize<FieldDefinition>(System.Text.Json.JsonSerializer.Serialize(f));
            copy.Column = column.Column;
            copy.IsKey = column.IsKey;
            copy.NoEntry = column.ScreenOnly ? f.NoEntry : f.NoEntry || column.NoEntry || column.IsIdentity;
            copy.QueryOnly = column.ScreenOnly;
            fields.Add(new PlacedField { Column = column.Column, Field = copy });
        }
        string name = string.IsNullOrWhiteSpace(source.Title) ? Path.GetFileNameWithoutExtension(path) : source.Title;
        if (fields.Count == 0)
        {
            MessageBox.Show($"「{name}」沒有可以匯入的欄位。", "匯入現有畫面");
            return;
        }
        var labels = source.Fields.Where(f => f.IsTextLabel).Select(f => f.Clone()).ToList();
        if (placed.Count + buttonBoxes.Count + listBoxes.Count + labelBoxes.Count > 0 &&
            MessageBox.Show("匯入會取代目前的版面（之後可以按 Ctrl+Z 復原），確定要匯入嗎？", "匯入現有畫面",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        columns.AddRange(screenOnly);                      // 按「確定」時接在 Grid 欄位後面
        RestoreLayout(System.Text.Json.JsonSerializer.Serialize(new LayoutState
        {
            Fields = fields,
            Buttons = source.Buttons.Select(b => b.Clone()).ToList(),
            Labels = labels,
            Width = source.Width,
            Height = source.Height,
            EffectiveColumn = effectiveColumn,
            BackColorHtml = source.BackColorHtml ?? ColorText.Format(backColor),
        }));
        if (!string.IsNullOrWhiteSpace(source.FontName) || source.FontSize > 0)
        {
            editFontName = string.IsNullOrWhiteSpace(source.FontName) ? null : source.FontName;
            editFontSize = source.FontSize;
            ApplyEditFont();
        }
        history.Check();
        lblHint.Text = $"  已匯入「{name}」：{fields.Count} 個欄位、{labels.Count} 個文字標籤、{source.Buttons.Count} 個按鈕";
        if (screenOnly.Count > 0)
            MessageBox.Show($"這些欄位在{(grid.IsSqlList ? " SQL 清單" : $" {grid.TableName} ")}沒有同名的欄位，匯入成「畫面欄位」"
                            + "（不讀資料、不存檔，可以輸入，按鈕的 SQL 可以用 @欄位 取得）：" + Environment.NewLine
                            + string.Join("、", screenOnly.Select(c => c.Header == c.Column ? c.Column : $"{c.Header}（{c.Column}）")), "匯入現有畫面");
    }

    private static int Snap(int v) => Math.Max(0, (int)Math.Round(v / (double)DesignBox.Grid) * DesignBox.Grid);

    /// <summary>
    /// 檔案 → 匯出成獨立畫面：用目前的版面產生一個畫面定義檔（單頭 = 這個 Grid 的資料表）。
    /// 欄位的設定（下拉選單選項 SQL、開窗選取、執行後動作、預設值、編號規則…）、JOIN、按鈕（執行SQL、開啟畫面、列印…）、
    /// 文字標籤、明細清單（資料表清單變成單身 Grid，SQL 清單變成 SQL 清單）都一起帶過去；原本的畫面不受影響
    /// </summary>
    private void ExportForm()
    {
        if (grid.IsSqlList)
        {
            MessageBox.Show("SQL 清單的編輯視窗沒有對應的資料表，不能匯出成獨立畫面。", "匯出成獨立畫面");
            return;
        }
        // 複本：不動到正在設計的 Grid
        var copy = FormDefinition.FromJson(new FormDefinition { Grids = { grid } }.ToJson()).Grids[0];
        if (!WriteBack(copy)) return;
        var notes = new List<string>();

        // 欄位：照編輯視窗的版面；JOIN 帶進來的欄位用單頭 JOIN（只顯示）
        var columnsOnLayout = copy.EditColumns;
        var fields = columnsOnLayout.Select(c =>
        {
            var f = InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity || c.IsJoin);
            f.JoinAlias = c.JoinAlias;
            f.SourceColumn = c.SourceColumn;
            return f;
        }).ToList();
        var usedAliases = columnsOnLayout.Where(c => c.IsJoin).Select(c => c.JoinAlias).ToHashSet();
        var joins = joinTarget.Concat(newJoins).Where(j => usedAliases.Contains(j.Alias))
            .GroupBy(j => j.Alias).Select(g => g.First().Clone()).ToList();
        fields.AddRange(copy.EditTextLabels.Select(f => f.Clone()));

        // 明細清單 → 單身 Grid（關聯欄位「編輯視窗欄位=明細欄位」就是單身的「單頭欄位=明細欄位」）
        var grids = new List<GridDefinition>();
        foreach (var l in copy.EditLists)
        {
            if (!string.IsNullOrWhiteSpace(l.ParentList))
                notes.Add($"「{l.Title ?? l.TableName}」設了上層清單「{l.ParentList}」：獨立畫面的單身沒有上層清單，改成依單頭列出，請到畫面設計確認關聯欄位");
            grids.Add(new GridDefinition
            {
                Title = l.Title,
                TableSchema = l.IsSqlList ? null : l.TableSchema,
                TableName = l.IsSqlList ? null : l.TableName,
                LinkText = l.IsSqlList ? "" : l.LinkText,
                Sql = l.Sql,
                X = l.X,
                Y = l.Y,
                Width = l.Width,
                Height = l.Height,
                Columns = l.Columns,
                Joins = l.Joins,
                OpenForm = l.OpenForm,
                OpenParams = l.OpenParams,
            });
        }

        var size = copy.EditFormSize;
        int right = fields.Select(f => f.X + f.LabelWidth + f.Width).Concat(copy.EditButtons.Select(b => b.X + b.Width)).Concat(grids.Select(g => g.X + g.Width)).DefaultIfEmpty(400).Max();
        int bottom = fields.Select(f => f.Y + f.Height).Concat(copy.EditButtons.Select(b => b.Y + b.Height)).Concat(grids.Select(g => g.Y + g.Height)).DefaultIfEmpty(300).Max();
        var form = new FormDefinition
        {
            Title = copy.Title ?? copy.TableName,
            TableSchema = copy.TableSchema,
            TableName = copy.TableName,
            Width = size?.Width ?? right + 20,
            Height = size?.Height ?? bottom + 20,
            BackColorHtml = copy.EditBackColorHtml,
            FontName = copy.EditFontName,
            FontSize = copy.EditFontSize,
            Fields = fields,
            Joins = joins,
            Buttons = copy.EditButtons.Select(b => b.Clone()).ToList(),
            Grids = grids,
        };
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            form.DatabaseType = DbDialect.Of(connectionString).Kind;
            form.ConnectionString = connectionString;
        }
        var missingKeys = copy.Columns.Where(c => c.IsKey && !c.IsJoin && !fields.Any(f => f.Column.Equals(c.Column, StringComparison.OrdinalIgnoreCase))).Select(c => c.Column).ToList();
        if (missingKeys.Count > 0)
            notes.Add($"主鍵（{string.Join("、", missingKeys)}）沒有放在版面上：獨立畫面沒有主鍵就只能查詢、新增，請到畫面設計把主鍵欄位拖上去");
        if (!string.IsNullOrWhiteSpace(copy.EffectiveColumn))
            notes.Add($"編輯視窗的「生效 / 取消生效」（{copy.EffectiveColumn}）獨立畫面沒有，請改用按鈕「執行SQL」");

        string folder = FormDefinition.DefaultFolder;
        using var dlg = new SaveFileDialog
        {
            Title = "匯出成獨立畫面",
            Filter = "畫面定義檔 (*.json)|*.json",
            InitialDirectory = Directory.Exists(folder) ? folder : null,
            FileName = string.Join("_", (form.Title + "-編輯視窗").Split(Path.GetInvalidFileNameChars())) + ".json",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            form.Save(dlg.FileName);       // 開啟畫面、列印樣板等路徑改成相對於新檔案的資料夾
        }
        catch (Exception ex)
        {
            MessageBox.Show("匯出失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        MessageBox.Show($"已匯出成 {Path.GetFileName(dlg.FileName)}：{fields.Count} 個欄位（含文字標籤）、{form.Buttons.Count} 個按鈕、{grids.Count} 個清單、{joins.Count} 個 JOIN。"
                        + Environment.NewLine + "用畫面設計開啟這個檔案就可以繼續修改，要放進 ERP 選單請發布。"
                        + (notes.Count == 0 ? "" : Environment.NewLine + Environment.NewLine + "注意：" + Environment.NewLine + string.Join(Environment.NewLine, notes.Select(n => "　" + n))),
                        "匯出成獨立畫面");
    }

    /// <summary>
    /// Ctrl+C：複製選取的按鈕、文字標籤、SQL 清單；
    /// Grid 的欄位、資料表的明細清單一個只能放一次，不複製
    /// </summary>
    private void CopySelection()
    {
        var labels = selection.OfType<FieldBox>().Where(labelBoxes.Contains).Select(b => b.Field).ToList();
        var buttons = selection.OfType<ButtonBox>().Select(b => b.ButtonDef).ToList();
        var lists = selection.OfType<EditListBox>().Select(b => b.ListDef).Where(l => l.IsSqlList).ToList();
        int copied = labels.Count + buttons.Count + lists.Count;
        if (copied == 0)
        {
            lblHint.Text = "  選取的是欄位或資料表的明細清單：同一個只能放一次，不能複製（按鈕、文字標籤、SQL 清單可以複製）";
            return;
        }
        DesignClipboard.Copy(labels, buttons, Enumerable.Empty<GridDefinition>(), lists);
        lblHint.Text = $"  已複製 {copied} 個元件，按 Ctrl+V 貼上" + (copied < selection.Count ? $"（{selection.Count - copied} 個欄位 / 明細清單不能複製，略過）" : "");
    }

    /// <summary>Ctrl+V：貼上複製的元件（新的一份，位置往右下錯開）；文字標籤、SQL 清單取新的名稱</summary>
    private void PasteClipboard()
    {
        var (fields, buttons, _, lists) = DesignClipboard.Paste();
        var labels = fields.Where(f => f.IsTextLabel).ToList();         // 計算欄位只能放在主畫面
        if (labels.Count + buttons.Count + lists.Count == 0)
        {
            lblHint.Text = "  複製的元件不能貼在編輯視窗（計算欄位、主畫面的 SQL 清單只能貼在畫面設計）";
            return;
        }
        var usedNames = new HashSet<string>(columns.Select(c => c.Column).Concat(labelBoxes.Select(b => b.Field.Column)), StringComparer.OrdinalIgnoreCase);
        var usedLists = new HashSet<string>(listBoxes.Select(b => b.ListDef.TableName), StringComparer.OrdinalIgnoreCase);
        foreach (var f in labels)
        {
            f.Column = DesignClipboard.UniqueName("標籤", usedNames);
            AddLabelBox(f, select: false);
        }
        foreach (var b in buttons) AddButtonBox(b, select: false);
        foreach (var l in lists)
        {
            l.TableName = DesignClipboard.UniqueName("SQL清單", usedLists);      // 識別用的名稱（上層清單用）不能重複
            AddListBox(l, select: false);
        }
        SetSelection(labelBoxes.Where(b => labels.Contains(b.Field)).Cast<DesignBox>()
            .Concat(buttonBoxes.Where(b => buttons.Contains(b.ButtonDef)))
            .Concat(listBoxes.Where(b => lists.Contains(b.ListDef))));
        lblHint.Text = $"  已貼上 {labels.Count + buttons.Count + lists.Count} 個元件（已選取，可以直接拖曳到要放的位置）";
    }

    /// <summary>Delete：刪除選取的欄位；Ctrl+A：全選</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = propertyGrid.ContainsFocus;            // 在屬性面板打字時，Delete / Ctrl+A 照常作用在文字上
        if (!typing && keyData == Keys.Delete && selection.Count > 0) { RemoveSelected(); return true; }
        if (!typing && !UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.C) && selection.Count > 0) { CopySelection(); return true; }
        if (!typing && !UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.V) && !DesignClipboard.IsEmpty) { PasteClipboard(); return true; }
        if (!UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.Z)) { history.Undo(); return true; }
        if (!UndoHistory.IsEditingText() && (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z))) { history.Redo(); return true; }
        if (!typing && keyData == (Keys.Control | Keys.A)) { SetSelection(canvas.Controls.OfType<DesignBox>()); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 放置 / 刪除欄位 =================

    private void PlaceColumn(GridColumnDefinition c, Point location, bool select = true)
    {
        if (placed.TryGetValue(c, out var existing))
        {
            SetSelection(new[] { existing });
            return;
        }
        var field = new FieldDefinition
        {
            Column = c.Column,
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
            DateFormat = c.EditDateFormat,
            NumberRule = c.EditNumberRule,
            DefaultValue = c.EditDefaultValue,
            IsReadOnly = c.EditReadOnly,
            CheckedValue = string.IsNullOrEmpty(c.EditCheckedValue) ? "1" : c.EditCheckedValue,
            UncheckedValue = c.EditUncheckedValue ?? "0",
            IsKey = c.IsKey,
            NoEntry = c.NoEntry || c.IsIdentity,
            X = location.X,
            Y = location.Y,
            Width = c.EditWidth > 0 ? c.EditWidth : Math.Clamp(c.Width, 120, 400),
            Height = c.EditHeight > 0 ? c.EditHeight : FieldDefinition.DefaultHeight,
        };
        var box = new FieldBox(field);
        placed[c] = box;
        AttachBox(box);
        RefreshAvailable();
        UpdateCanvasSize();
        if (select) SetSelection(new[] { box });
    }

    private void AttachBox(DesignBox box)
    {
        box.Pressed += (s, e) => OnBoxPressed(box);
        box.Dragging += (s, delta) => MoveSelection(box, delta);
        box.DragFinished += (s, e) => { dragStarts = null; UpdateCanvasSize(); };
        box.Resized += (s, e) => UpdateCanvasSize();
        canvas.Controls.Add(box);
        if (box is FieldBox) box.RefreshFromModel();       // 加進版面後才知道底色，標籤文字顏色要重算
        // 雙擊「列印報表」按鈕：開啟列印樣板設計（資料表、主鍵用這個 Grid 的）
        if (box is ButtonBox bb)
            bb.DoubleClick += (s, e) =>
            {
                if (bb.ButtonDef.Action != ButtonAction.列印報表) return;
                // 樣板一律以單頭（這個 Grid 的資料表）為主；有設定「列印來源清單」時，清單選取那筆的欄位（例如詢價日期）
                // 也可以放到樣板上、拿來對應單身
                string listTable = bb.ButtonDef.PrintListTable?.Trim();
                var list = string.IsNullOrEmpty(listTable) ? null
                    : listBoxes.Select(x => x.ListDef).FirstOrDefault(l => l.TableName.Equals(listTable, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(listTable) && list == null)
                {
                    MessageBox.Show($"版面上沒有「{listTable}」這個明細清單，請檢查按鈕的「列印來源清單」。", "列印樣板");
                    return;
                }
                var recordColumns = list?.Columns.Where(c => !c.IsJoin)
                    .Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, MaxLength = c.MaxLength, Description = c.Header }).ToList();
                if (PrintDesignerForm.OpenFor(bb.ButtonDef, this, FormDefinition.DefaultFolder, connectionString, grid.TableSchema, grid.TableName,
                                              grid.KeyColumns.Select(c => c.Column), recordColumns, listBoxes.Select(x => x.ListDef)))
                    propertyGrid.Refresh();
            };
    }

    // ================= 工具箱 =================

    private void AddToolboxItem(string kind, Point location)
    {
        if (kind == "Button") AddButtonBox(new ButtonDefinition { X = location.X, Y = location.Y });
        else if (kind == "ComboBox") AddKindField(location, InputKind.下拉選單);
        else if (kind == "DatePicker") AddKindField(location, InputKind.日期選擇);
        else if (kind == "CheckBox") AddKindField(location, InputKind.勾選框);
        else if (kind == "RadioButton") AddKindField(location, InputKind.選項按鈕);
        else if (kind == "Label") AddTextLabel(location);
        else if (kind == "SqlList") AddSqlList(location);
    }

    /// <summary>
    /// 加入文字標籤：只顯示文字（屬性面板的「標籤」），不對應資料表；欄位「執行後動作」的 SQL 寫「AS 變數名稱」
    /// （或回傳對應「變數名稱=SQL 欄位」）就能把查出來的值顯示在這裡，打開、切換資料時也會重新帶出
    /// </summary>
    private void AddTextLabel(Point location)
    {
        var used = new HashSet<string>(columns.Select(c => c.Column).Concat(labelBoxes.Select(b => b.Field.Column)), StringComparer.OrdinalIgnoreCase);
        int n = 1;
        while (used.Contains($"標籤{n}")) n++;
        AddLabelBox(new FieldDefinition
        {
            Column = $"標籤{n}",
            DataType = "nvarchar",
            IsNullable = true,
            NoEntry = true,
            IsCalculated = true,
            InputType = InputKind.文字標籤,
            Label = "文字",
            LabelWidth = 0,
            X = location.X,
            Y = location.Y,
            Width = 120,
        });
        lblHint.Text = $"  已加入文字標籤「標籤{n}」：在屬性面板改「標籤」（顯示的文字）與「變數名稱」";
    }

    private void AddLabelBox(FieldDefinition field, bool select = true)
    {
        var box = new FieldBox(field);
        labelBoxes.Add(box);
        AttachBox(box);
        UpdateCanvasSize();
        if (select) SetSelection(new[] { box });
    }

    private void AddButtonBox(ButtonDefinition button, bool select = true)
    {
        var box = new ButtonBox(button);
        buttonBoxes.Add(box);
        AttachBox(box);
        UpdateCanvasSize();
        if (select) SetSelection(new[] { box });
    }

    // ================= 明細清單（資料表頁籤） =================

    private sealed record TableItem(string Schema, string Name)
    {
        public override string ToString() => $"{Schema}.{Name}";
    }

    /// <summary>第一次切到「資料表」頁籤時才讀資料庫的資料表清單</summary>
    private void LoadTables()
    {
        if (allTables != null) return;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            lblTablesHint.Text = "還沒有資料庫連線，請回主畫面先連線，再開啟編輯視窗版面。";
            allTables = new List<(string, string)>();
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            allTables = SchemaReader.GetTables(connectionString);
        }
        catch (Exception ex)
        {
            allTables = null;                                // 下次切過來再試一次
            MessageBox.Show("讀取資料表清單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        FillTableList();
    }

    private void FillTableList()
    {
        if (allTables == null) return;
        string keyword = txtTableFilter.Text.Trim();
        lstTables.BeginUpdate();
        lstTables.Items.Clear();
        foreach (var t in allTables.Where(t => keyword == "" || t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            lstTables.Items.Add(new TableItem(t.Schema, t.Name));
        lstTables.EndUpdate();
    }

    /// <summary>把整個資料表放到版面上變成明細清單：預設列出全部欄位，並自動推測關聯欄位</summary>
    private void AddListFromTable(string schema, string table, Point location)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("沒有資料庫連線，不能加入明細清單。請先在主畫面連線。", "提醒");
            return;
        }
        List<ColumnInfo> columns;
        try
        {
            Cursor = Cursors.WaitCursor;
            columns = SchemaReader.GetColumns(connectionString, schema, table);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取欄位失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        var masterColumns = grid.Columns.Select(c => new ColumnInfo { Name = c.Column, IsPrimaryKey = c.IsKey }).ToList();
        var list = new EditListDefinition
        {
            TableSchema = schema,
            TableName = table,
            Title = table,
            LinkText = DesignerForm.GuessLinks(masterColumns, columns),
            X = location.X,
            Y = location.Y,
            Width = Math.Max(300, formSize.Width - location.X - 10),
            Height = 150,
            Columns = columns.Select(c => new GridColumnDefinition
            {
                Column = c.Name,
                DataType = c.DataType,
                MaxLength = c.MaxLength,
                IsNullable = c.IsNullable,
                IsIdentity = c.IsIdentity,
                IsKey = c.IsPrimaryKey,
                Header = string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description,
                Width = Math.Clamp(TypeHelper.SuggestWidth(c), 60, 200),
            }).ToList(),
        };
        AddListBox(list);
        if (list.Links.Count == 0) MessageBox.Show("推測不出關聯欄位，請點一下清單設定（編輯視窗欄位=明細欄位）。", "提醒");
    }

    /// <summary>
    /// 工具箱的「SQL 清單」：資料來自自己寫的 SELECT（唯讀），@名稱 取編輯視窗目前這筆的欄位值，切換資料時重查；
    /// 設了上層清單時取上層清單選取那一列的值
    /// </summary>
    private void AddSqlList(Point location)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("沒有資料庫連線，不能加入 SQL 清單。請先在主畫面連線。", "提醒");
            return;
        }
        // 識別用的名稱（上層清單、列印來源清單選的就是它），不能跟其他清單重複
        var names = new HashSet<string>(listBoxes.Select(b => b.ListDef.TableName), StringComparer.OrdinalIgnoreCase);
        int n = 1;
        while (names.Contains($"SQL清單{n}")) n++;
        var list = new EditListDefinition
        {
            TableName = $"SQL清單{n}",
            Title = $"SQL清單{n}",
            X = location.X,
            Y = location.Y,
            Width = Math.Max(300, formSize.Width - location.X - 10),
            Height = 150,
        };
        if (!EditSqlList(list)) return;
        AddListBox(list);
        lblHint.Text = $"  已加入 SQL 清單「{list.TableName}」：點一下可以修改 SQL 與欄位";
    }

    /// <summary>設定 SQL 清單的 SQL 與欄位（沿用畫面設計的 SQL 清單設定視窗）；按確定回傳 true</summary>
    private bool EditSqlList(EditListDefinition l)
    {
        var temp = new GridDefinition { Title = l.Title, Sql = l.Sql, Columns = l.Columns, Joins = l.Joins };
        // 可以用的 @參數：編輯視窗的欄位；有上層清單時是上層清單的欄位
        var parent = ParentListOf(l);
        var names = parent != null ? parent.Columns.Select(c => c.Column) : columns.Select(c => c.Column);
        using var dlg = new SqlListDialog(temp, connectionString, names);
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        l.Title = temp.Title;
        l.Sql = temp.Sql;
        l.Columns = temp.Columns;
        l.Joins = temp.Joins;
        return true;
    }

    private void AddListBox(EditListDefinition list, bool select = true)
    {
        var box = new EditListBox(list);
        listBoxes.Add(box);
        AttachBox(box);
        box.Clicked += (s, e) =>
        {
            if (ModifierKeys.HasFlag(Keys.Control) || selection.Count != 1 || selection[0] != box) return;
            EditListColumns(box);
        };
        UpdateCanvasSize();
        if (select) SetSelection(new[] { box });
    }

    /// <summary>點一下清單：設定關聯欄位、要顯示的欄位、標題與寬度（沿用單身欄位設定視窗）</summary>
    private void EditListColumns(EditListBox box)
    {
        var l = box.ListDef;
        if (l.IsSqlList)
        {
            if (!EditSqlList(l)) return;
            box.RefreshFromModel();
            propertyGrid.Refresh();
            return;
        }
        var temp = new GridDefinition
        {
            TableSchema = l.TableSchema,
            TableName = l.TableName,
            LinkText = l.LinkText,
            Columns = l.Columns,
            Joins = l.Joins,
        };
        // 關聯欄位的左邊：有上層清單時是上層清單的欄位，沒有就是編輯視窗的欄位
        var parent = ParentListOf(l);
        var masterColumns = parent != null ? parent.Columns.Select(c => c.Column).ToList() : columns.Where(c => !c.IsJoin).Select(c => c.Column).ToList();
        using var dlg = new GridColumnsDialog(temp, masterColumns, connectionString, forEditList: true);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        l.LinkText = temp.LinkText;
        l.Joins = temp.Joins;
        box.RefreshFromModel();
        propertyGrid.Refresh();
    }

    /// <summary>明細清單設定的上層清單（版面上同資料表名稱的另一個清單）；沒設或找不到 = null</summary>
    private EditListDefinition ParentListOf(EditListDefinition l)
    {
        string name = l.ParentList?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        return listBoxes.Select(b => b.ListDef).FirstOrDefault(x => x != l && x.TableName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>上層清單改了：用上層清單（沒有就用編輯視窗）的欄位推測關聯欄位；推測不出來就保留原本的，並提醒</summary>
    private void GuessListLinks(EditListDefinition l)
    {
        var parent = ParentListOf(l);
        if (!string.IsNullOrWhiteSpace(l.ParentList) && parent == null)
        {
            MessageBox.Show($"版面上沒有「{l.ParentList}」這個明細清單。", "提醒");
            return;
        }
        var masterColumns = parent != null
            ? parent.Columns.Where(c => !c.IsJoin).Select(c => new ColumnInfo { Name = c.Column, IsPrimaryKey = c.IsKey }).ToList()
            : columns.Where(c => !c.IsJoin).Select(c => new ColumnInfo { Name = c.Column, IsPrimaryKey = c.IsKey }).ToList();
        var detailColumns = l.Columns.Where(c => !c.IsJoin).Select(c => new ColumnInfo { Name = c.Column }).ToList();
        string links = DesignerForm.GuessLinks(masterColumns, detailColumns);
        if (links != "") l.LinkText = links;
        else MessageBox.Show("推測不出關聯欄位，請點一下清單設定（上層清單欄位=明細欄位）。", "提醒");
    }

    /// <summary>下拉選單、日期選擇、勾選框、選項按鈕：從還沒放上版面的欄位選一個，放上去並設成對應的輸入方式</summary>
    private void AddKindField(Point location, InputKind kind)
    {
        var (name, tip, minWidth) = DesignerForm.KindInfo(kind);
        var available = columns.Where(c => !placed.ContainsKey(c)).ToList();
        if (available.Count == 0)
        {
            MessageBox.Show($"欄位都已經放在版面上了。要改成{name}，請點選該欄位，在屬性面板把「輸入方式」改成「{name}」。", "提醒");
            return;
        }
        var infos = available.Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, Description = c.Header }).ToList();
        using var dlg = new ColumnPickDialog($"{name}：{grid.TableName}", infos);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var column = available.First(c => c.Column == dlg.SelectedColumn.Name);
        PlaceColumn(column, location);
        var box = placed[column];
        box.Field.InputType = kind;
        box.Field.Width = Math.Max(box.Field.Width, minWidth);
        box.RefreshFromModel();
        SetSelection(new[] { box });                      // 屬性面板接著設定選項、日期格式或勾選值
        lblHint.Text = $"  已加入{name}：{tip}";
    }

    private void RemoveSelected()
    {
        foreach (var box in selection.ToList())
        {
            if (box is FieldBox fb && !labelBoxes.Remove(fb)) placed.Remove(placed.First(p => p.Value == fb).Key);   // 文字標籤不在 placed 裡
            if (box is ButtonBox bb) buttonBoxes.Remove(bb);
            if (box is EditListBox lb) listBoxes.Remove(lb);
            canvas.Controls.Remove(box);
            box.Dispose();
        }
        SetSelection(Array.Empty<DesignBox>());
        RefreshAvailable();
        UpdateCanvasSize();
    }

    /// <summary>
    /// 「欄位」頁籤的「JOIN 其他資料表」：選資料表、關聯條件（Grid 欄位 = 關聯資料表欄位）、要帶進來的欄位，
    /// 帶進來的欄位放到版面上（只顯示、不寫回資料庫）。按「確定」才加到 Grid；Grid 上預設不顯示，要顯示請到單身欄位設定勾選
    /// </summary>
    private void AddJoin()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("沒有資料庫連線，不能 JOIN 其他資料表。請先在主畫面連線。", "提醒");
            return;
        }
        var aliases = new HashSet<string>(joinTarget.Concat(newJoins).Select(j => j.Alias).Concat(reservedAliases), StringComparer.OrdinalIgnoreCase);
        int n = 1;
        while (aliases.Contains($"J{n}")) n++;
        var localColumns = columns.Where(c => !c.IsJoin)
            .Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, IsPrimaryKey = c.IsKey }).ToList();
        var used = columns.Select(c => c.Column).Concat(labelBoxes.Select(b => b.Field.Column)).Concat(reservedNames);
        using var dlg = new JoinDialog(connectionString, $"J{n}", localColumns, used);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        newJoins.Add(dlg.Join);
        var location = NextFreePoint();
        var boxes = new List<DesignBox>();
        foreach (var c in dlg.NewColumns)
        {
            c.Visible = false;                             // 加在編輯視窗用的；Grid 上要顯示到單身欄位設定勾選
            columns.Add(c);
            PlaceColumn(c, location, select: false);
            boxes.Add(placed[c]);
            location = NextFreePoint();
        }
        SetSelection(boxes);
        history.Check();
        lblHint.Text = $"  已 JOIN {dlg.Join.TableName}（{dlg.Join.LinkText}），帶進來 {dlg.NewColumns.Count} 個欄位，只顯示、不會存檔；按「確定」才會加到 Grid";
    }

    /// <summary>左邊清單 = 還沒放上版面的欄位，依原本欄位順序</summary>
    private void RefreshAvailable()
    {
        lstAvailable.BeginUpdate();
        lstAvailable.Items.Clear();
        foreach (var c in columns.Where(c => !placed.ContainsKey(c))) lstAvailable.Items.Add(c);
        lstAvailable.EndUpdate();
    }

    private Point NextFreePoint()
    {
        if (placed.Count == 0) return new Point(10, 12);
        return new Point(10, placed.Values.Max(b => b.Field.Y + Math.Max(32, b.Field.Height + 6)));
    }

    /// <summary>版面上的欄位依目前上下、左右順序重新排成一欄</summary>
    private void ArrangeAll()
    {
        int y = 12;
        foreach (var box in placed.Values.OrderBy(b => b.Field.Y).ThenBy(b => b.Field.X))
        {
            box.Field.X = 10;
            box.Field.Y = y;
            box.RefreshFromModel();
            y += Math.Max(32, box.Field.Height + 6);
        }
        UpdateCanvasSize();
    }

    /// <summary>版面大小 = 使用者看到的編輯視窗欄位區大小；至少要容納所有欄位</summary>
    private void UpdateCanvasSize()
    {
        Size min = MinCanvasSize();
        formSize = new Size(Math.Max(formSize.Width, min.Width), Math.Max(formSize.Height, min.Height));
        canvas.Size = formSize;
        canvas.Invalidate();
        lblHint.Text = $"  版面 {formSize.Width} × {formSize.Height}，{placed.Count} 個欄位、{buttonBoxes.Count} 個按鈕、{listBoxes.Count} 個明細清單；拖版面邊緣調整大小，圈選或 Ctrl 點選後按 Delete 刪除";
    }

    /// <summary>版面最小要容納所有欄位，且不小於 300 × 150</summary>
    private Size MinCanvasSize()
    {
        int w = 300, h = 150;
        foreach (var box in canvas.Controls.OfType<DesignBox>())
        {
            w = Math.Max(w, box.Right + 20);
            h = Math.Max(h, box.Bottom + 20);
        }
        return new Size(w, h);
    }



    // ================= 選取與移動 =================

    private void OnBoxPressed(DesignBox box)
    {
        if (ModifierKeys.HasFlag(Keys.Control))
        {
            SetSelection(selection.Contains(box) ? selection.Where(b => b != box).ToList() : selection.Append(box).ToList());
            dragStarts = null;
            return;
        }
        if (!selection.Contains(box)) SetSelection(new[] { box });
        dragStarts = selection.ToDictionary(b => b, b => b.ModelLocation);
    }

    /// <summary>選取的欄位一起移動：先對齊格線，靠近其他欄位時再自動貼齊（以滑鼠拖的那個欄位為準）</summary>
    private void MoveSelection(DesignBox lead, Size delta)
    {
        if (dragStarts == null || dragStarts.Count == 0) return;
        int dx = (int)Math.Round(delta.Width / (double)DesignBox.Grid) * DesignBox.Grid;
        int dy = (int)Math.Round(delta.Height / (double)DesignBox.Grid) * DesignBox.Grid;
        if (dragStarts.TryGetValue(lead, out var leadStart))
            (dx, dy) = AlignHelper.Magnetize(lead, leadStart, dx, dy, canvas.Controls.OfType<DesignBox>().Where(b => !dragStarts.ContainsKey(b)));
        dx = Math.Max(dx, -dragStarts.Values.Min(p => p.X));
        dy = Math.Max(dy, -dragStarts.Values.Min(p => p.Y));
        foreach (var (box, start) in dragStarts) box.SetModelLocation(start.X + dx, start.Y + dy);
    }

    private void SetSelection(IEnumerable<DesignBox> boxes)
    {
        var list = boxes.Distinct().ToList();
        foreach (var b in selection) b.IsSelected = false;
        selection.Clear();
        selection.AddRange(list);
        foreach (var b in selection) b.IsSelected = true;
        if (selection.Count > 0) ActiveControl = null;
        // 屬性面板：選一個顯示它，選多個只列共同屬性（改了套用到全部）
        PrintListConverter.Available = listBoxes.Select(l => l.ListDef.TableName).Distinct().ToList();    // 按鈕「列印來源清單」的選項
        // 文字標籤用畫面設計的屬性（標籤、變數名稱、字型、底色）
        propertyGrid.SelectedObjects = selection.Select(b => b is FieldBox { Field.IsTextLabel: false } fb ? new EditFieldView(fb.Field) : b.Model).ToArray();
    }

    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        // 抓到版面邊緣 = 調整大小；其他地方 = 拉框圈選
        if (canvasResizer.TryStart(e)) return;
        bandAdditive = ModifierKeys.HasFlag(Keys.Control);
        if (!bandAdditive) SetSelection(Array.Empty<DesignBox>());
        band ??= new SelectionBand(canvas);
        band.Begin(e.Location);
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (canvasResizer.Active || band is not { Active: true })
        {
            canvasResizer.Move(e);                         // 拖曳中調整大小；沒在拖曳時切換游標
            return;
        }
        band.Move(e.Location);
    }

    private void OnCanvasMouseUp()
    {
        if (canvasResizer.End()) return;
        if (band is not { Active: true }) return;
        Rectangle rect = band.End();
        if (rect.IsEmpty) return;                          // 只是點一下，沒有拉框
        var hit = canvas.Controls.OfType<DesignBox>().Where(b => b.Bounds.IntersectsWith(rect));
        SetSelection(bandAdditive ? selection.Concat(hit) : hit);
    }

    // ================= 確定 =================

    private static string ColumnText(GridColumnDefinition c) =>
        c.Header == c.Column ? $"{c.Column}（{c.DataType}）" : $"{c.Header}：{c.Column}（{c.DataType}）";

    private void UpdateEffectiveButton()
    {
        var column = grid.Columns.FirstOrDefault(c => c.Column.Equals(effectiveColumn ?? "", StringComparison.OrdinalIgnoreCase));
        btnEffectiveColumn.Text = column != null ? ColumnText(column) : "（不使用）";
        foreach (var item in btnEffectiveColumn.DropDownItems.OfType<ToolStripMenuItem>())
            item.Checked = string.Equals(item.Tag as string, column?.Column, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>編輯視窗目前的字型（設定的字型；沒設定就跟畫面一樣）</summary>
    private Font CurrentEditFont() =>
        new GridDefinition { EditFontName = editFontName, EditFontSize = editFontSize }.EditFont(screenFont);

    private void ApplyEditFont()
    {
        canvas.Font = CurrentEditFont();
        foreach (var box in canvas.Controls.OfType<DesignBox>()) box.RefreshFromModel();   // 欄位示意的標籤高度跟著字型
        UpdateFontButton();
        UpdateCanvasSize();
    }

    /// <summary>「字型」按鈕顯示目前的設定（沒設定時只顯示「字型」）</summary>
    private void UpdateFontButton() =>
        btnFont.Text = string.IsNullOrWhiteSpace(editFontName) && editFontSize <= 0 ? "字型" : $"字型：{canvas.Font.Name} {canvas.Font.SizeInPoints:0.#}";

    /// <summary>檔案 → 儲存：版面寫回 Grid，再存檔整個畫面定義檔；close = 存完關閉視窗</summary>
    private void SaveLayout(bool close)
    {
        if (saveForm == null || !WriteBack()) return;
        saveForm();
        if (close) DialogResult = DialogResult.OK;
        else lblHint.Text = $"  已儲存（{DateTime.Now:HH:mm:ss}）";
    }

    private void Apply()
    {
        if (WriteBack()) DialogResult = DialogResult.OK;
    }

    /// <summary>把目前的版面寫回 GridDefinition；版面上沒有欄位時不寫，回傳 false</summary>
    private bool WriteBack() => WriteBack(grid);

    /// <param name="target">要寫入的 Grid：grid 本身，或預覽用的複本（欄位順序跟 grid 一樣）</param>
    private bool WriteBack(GridDefinition target)
    {
        if (placed.Count == 0)
        {
            MessageBox.Show("編輯視窗至少要有一個欄位。", "提醒");
            return false;
        }
        // 這裡 JOIN 新帶進來的欄位接在 Grid 欄位後面（預覽用的複本加的是複製品），JOIN 本身加到單頭 JOIN 或 Grid 的 Joins
        for (int i = target.Columns.Count; i < columns.Count; i++)
            target.Columns.Add(target == grid ? columns[i]
                : System.Text.Json.JsonSerializer.Deserialize<GridColumnDefinition>(System.Text.Json.JsonSerializer.Serialize(columns[i])));
        if (target == grid)
            joinTarget.AddRange(newJoins.Where(j => !joinTarget.Any(x => x.Alias == j.Alias)));
        for (int i = 0; i < columns.Count; i++)
        {
            var c = target.Columns[i];
            if (placed.TryGetValue(columns[i], out var box))
            {
                c.EditVisible = true;
                c.EditX = box.Field.X;
                c.EditY = box.Field.Y;
                c.EditWidth = box.Field.Width;
                c.EditHeight = box.Field.Height == FieldDefinition.DefaultHeight ? 0 : box.Field.Height;
                // 標籤跟 Grid 標題一樣就不另存，之後改 Grid 標題時編輯視窗會跟著變
                c.EditLabel = box.Field.Label == c.Header ? null : box.Field.Label;
                c.EditPlaceholder = string.IsNullOrEmpty(box.Field.Placeholder) ? null : box.Field.Placeholder;
                c.EditLabelWidth = box.Field.LabelWidth == FieldBox.DefaultLabelWidth ? 0 : box.Field.LabelWidth;
                c.EditInputType = box.Field.InputType;
                c.EditItems = box.Field.Items;
                c.EditItemsSql = box.Field.ItemsSql;
                c.EditAllowFreeText = box.Field.AllowFreeText;
                c.EditPickerForm = string.IsNullOrWhiteSpace(box.Field.PickerForm) ? null : box.Field.PickerForm.Trim();
                c.EditPickerParams = string.IsNullOrWhiteSpace(box.Field.PickerParams) ? null : box.Field.PickerParams;
                c.EditPickerReturn = string.IsNullOrWhiteSpace(box.Field.PickerReturn) ? null : box.Field.PickerReturn;
                c.EditAfterSelectSql = string.IsNullOrWhiteSpace(box.Field.AfterSelectSql) ? null : box.Field.AfterSelectSql;
                c.EditAfterSelectParams = string.IsNullOrWhiteSpace(box.Field.AfterSelectParams) ? null : box.Field.AfterSelectParams;
                c.EditAfterSelectReturn = string.IsNullOrWhiteSpace(box.Field.AfterSelectReturn) ? null : box.Field.AfterSelectReturn;
                c.EditDateFormat = string.IsNullOrWhiteSpace(box.Field.DateFormat) ? null : box.Field.DateFormat.Trim();
                c.EditNumberRule = string.IsNullOrWhiteSpace(box.Field.NumberRule) ? null : box.Field.NumberRule.Trim();
                c.EditDefaultValue = string.IsNullOrWhiteSpace(box.Field.DefaultValue) ? null : box.Field.DefaultValue.Trim();
                c.EditReadOnly = box.Field.IsReadOnly;
                c.EditCheckedValue = box.Field.CheckedValue == "1" ? null : box.Field.CheckedValue;
                c.EditUncheckedValue = box.Field.UncheckedValue == "0" ? null : box.Field.UncheckedValue;
                c.EditFontSize = box.Field.FontSize;
                c.EditLabelFontSize = box.Field.LabelFontSize;
                c.EditFontName = string.IsNullOrWhiteSpace(box.Field.FontName) ? null : box.Field.FontName.Trim();
                c.EditBackColorHtml = box.Field.BackColorHtml;
            }
            else
            {
                c.EditVisible = false;
            }
        }
        target.EditButtons = buttonBoxes.Select(b => b.ButtonDef).ToList();
        target.EditLists = listBoxes.Select(b => b.ListDef).ToList();
        target.EditTextLabels = labelBoxes.Select(b => b.Field).ToList();
        target.HasEditLayout = true;
        target.EditFormWidth = formSize.Width;
        target.EditFormHeight = formSize.Height;
        target.EffectiveColumn = string.IsNullOrEmpty(effectiveColumn) ? null : effectiveColumn;
        target.EditBackColor = backColor;
        target.EditFontName = string.IsNullOrWhiteSpace(editFontName) ? null : editFontName;
        target.EditFontSize = editFontSize;
        return true;
    }

    // ================= 預覽 =================

    /// <summary>
    /// 預覽：用目前的版面（還沒按確定、存檔也可以）開一個編輯視窗，資料是這個 Grid 資料表的前 50 筆，可以切換上一筆、下一筆；
    /// 可以按新增、修改試著輸入（按確定不會存檔），不能刪除，也不能執行 SQL 按鈕（不會改到資料）。JOIN 帶進來的欄位預覽時是空白
    /// </summary>
    private void Preview()
    {
        // 複本：不動到正在設計的 Grid
        var copy = FormDefinition.FromJson(new FormDefinition { Grids = { grid } }.ToJson()).Grids[0];
        if (!WriteBack(copy)) return;
        DataTable data = new();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var d = DbDialect.Of(connectionString);
                if (copy.IsSqlList)
                {
                    data = SqlList.Load(connectionString, SqlList.Compose(connectionString, copy.Sql, copy.Columns, copy.Joins), name => null);      // SQL 清單：@參數當空值
                }
                else
                {
                    using var conn = d.Open(connectionString);
                    using var cmd = d.CreateCommand(conn, $"SELECT {d.Top(50)}* FROM {copy.FullTableName(d)}{d.Limit(50)}");
                    data = d.Fill(cmd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取預覽資料失敗（只顯示空白的編輯視窗）：" + ex.Message, "預覽");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
        var fields = copy.EditColumns.Select(c => InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity)).ToList();
        var navigator = new PreviewNavigator(data, string.IsNullOrWhiteSpace(connectionString) ? copy.TableName : copy.FullTableName(DbDialect.Of(connectionString)));
        using var dlg = new RecordNavigatorDialog(navigator, "預覽：" + (copy.Title ?? copy.TableName), fields, copy.EditFormSize, connectionString,
                                                  copy.EditButtons, FormDefinition.DefaultFolder, copy.EffectiveColumn, copy.EditLists,
                                                  copy.EditBackColor, copy.EditFont(screenFont), copy.EditTextLabels);
        dlg.ShowDialog(this);
    }

    /// <summary>
    /// 預覽用的資料來源：可以按新增、修改試著輸入（下拉選單、開窗選取、執行後動作都照常動作），按確定只檢查、不存進資料庫；
    /// 不能刪除、生效，權限是唯讀的，「執行SQL」按鈕也不能按
    /// </summary>
    private sealed class PreviewNavigator : IRecordNavigator
    {
        private readonly DataTable data;
        private int current;

        public PreviewNavigator(DataTable data, string fullTableName)
        {
            this.data = data;
            FullTableName = fullTableName;
            current = data.Rows.Count > 0 ? 0 : -1;
        }

        public string FullTableName { get; }
        public int Count => data.Rows.Count;
        public int Current => current;
        public DataRow CurrentRow => current >= 0 ? data.Rows[current] : null;
        public bool CanModify => true;
        public bool CanInsert => true;
        public bool CanDelete => false;
        public bool CanPrint => true;
        public bool CanApprove => false;
        public FormPermission Permission { get; } = new(true, false, false, false, true);
        public void MoveTo(int index) { if (index >= 0 && index < data.Rows.Count) current = index; }
        public bool Refresh() => current >= 0;
        public bool Insert(Dictionary<string, object> values) => NotSaved(values);
        public bool Update(Dictionary<string, object> values) => NotSaved(values);

        /// <summary>按確定：必填已經檢查過了，列出輸入的值，不存進資料庫</summary>
        private static bool NotSaved(Dictionary<string, object> values)
        {
            MessageBox.Show("預覽不會存進資料庫。這次輸入的值：" + Environment.NewLine + Environment.NewLine
                            + string.Join(Environment.NewLine, values.Select(v => $"　{v.Key} = {Audit.Format(v.Value)}")), "預覽");
            return true;
        }
        public bool UpdateColumn(string column, object value, string doneMessage) => false;
        public bool Delete() => false;
    }
}

/// <summary>版面設計器屬性面板顯示的內容：只開放跟編輯視窗版面有關的屬性</summary>
public class EditFieldView
{
    private readonly FieldDefinition field;
    public EditFieldView(FieldDefinition f) => field = f;

    [DisplayName("欄位名稱"), ReadOnly(true)]
    public string Column => field.Column;

    [DisplayName("標籤"), Description("編輯視窗上顯示的欄位名稱")]
    public string Label { get => field.Label; set => field.Label = value; }

    [DisplayName("提示文字 (Placeholder)"), Description("輸入框空白時顯示的灰色提示文字（文字、數字欄位才有）")]
    public string Placeholder { get => field.Placeholder; set => field.Placeholder = value; }

    [DisplayName("標籤寬度"), Description("輸入框左邊標籤區的寬度；拖欄位的左邊緣也可以調整")]
    public int LabelWidth { get => field.LabelWidth; set => field.LabelWidth = Math.Clamp(value, 0, 400); }

    [DisplayName("輸入框寬度")]
    public int Width { get => field.Width; set => field.Width = Math.Max(30, value); }

    [DisplayName("輸入框高度"), Description("預設 26；拉高（超過 32）後文字框會變成多行輸入，適合備註；下拉選單、日期的高度固定。拖欄位的下邊緣也可以調整")]
    public int Height { get => field.Height; set => field.Height = Math.Max(FieldDefinition.DefaultHeight, value); }

    [DisplayName("輸入方式"), Description("自動：依資料型別用文字框、勾選框或日期；下拉選單、選項按鈕：從選項中挑選；日期選擇：日期挑選器；勾選框：打勾 / 不打勾")]
    public InputKind InputType { get => field.InputType; set => field.InputType = value; }

    [DisplayName("唯讀"), Description("新增、修改時使用者不能改這個欄位，但值照樣存進資料庫（例如自動編號、預設值）。想連存檔都不要，請在 Grid 欄位設定勾「只顯示」")]
    public bool IsReadOnly { get => field.IsReadOnly; set => field.IsReadOnly = value; }

    [DisplayName("編號規則"),
     Description("新增時自動產生編號（可再手動修改；設成唯讀就不能改）。用中括號組成：[BF] 固定文字；[YYYY][MM][DD] 新增當天的日期；"
               + "[XXX] 流水號，幾個 X 就是幾碼，到資料表查同樣前綴的最大流水號 + 1。例如 [BF][YYYY][MM][DD][XXX] → BF20240131001。不需要自動編號就留空白")]
    public string NumberRule { get => field.NumberRule; set => field.NumberRule = value; }

    [DisplayName("預設值"), Description("新增時自動帶入的值（使用者仍可以改）。可以寫固定值；日期用 NOW()（現在日期時間）、TODAY()（今天），可以加減天數，例如 TODAY()+7；USER() = 登入帳號；"
                                     + "也可以寫 SELECT 查資料庫，取第一筆的第一個欄位，@欄位名稱 取視窗上的值，例如：SELECT 姓名 FROM H員工清冊 WHERE 工號 = @員工編號。有設編號規則時以編號規則為準"),
     Editor(typeof(SqlEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public string DefaultValue { get => field.DefaultValue; set => field.DefaultValue = value; }

    [DisplayName("日期格式"), Description("日期選擇用在文字、數字欄位時，存進資料庫的格式（ERP 常見的是 yyyyMMdd）；沒填就用 yyyyMMdd。date、datetime 欄位不需要")]
    public string DateFormat { get => field.DateFormat; set => field.DateFormat = value; }

    [DisplayName("勾選時的值"), Description("非 bit 欄位用勾選框時，打勾存進資料庫的值，預設 1；例如 Y")]
    public string CheckedValue { get => field.CheckedValue; set => field.CheckedValue = value; }

    [DisplayName("取消時的值"), Description("非 bit 欄位用勾選框時，沒打勾存進資料庫的值，預設 0；例如 N")]
    public string UncheckedValue { get => field.UncheckedValue; set => field.UncheckedValue = value; }

    [DisplayName("固定選項"), Description("下拉選單、選項按鈕的選項，用逗號或換行分隔；值和顯示文字不同時寫「值=顯示」，例如：Y=是,N=否"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Items { get => field.Items; set => field.Items = value; }

    [DisplayName("選項 SQL"), Description("從資料庫取選項：第一欄是值，第二欄（可省略）是顯示文字，例如：SELECT 代碼, 名稱 FROM 幣別；可以用 @視窗欄位，那個欄位改了就重查選項（連動下拉），例如：SELECT 代碼, 分類 FROM H職務工作分類 WHERE 職務 = @職務；@登入帳號、@登入姓名 = 登入的使用者"),
     Editor(typeof(SqlEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public string ItemsSql { get => field.ItemsSql; set => field.ItemsSql = value; }

    [DisplayName("可自行輸入"), Description("True：選項以外的值也可以直接打字輸入")]
    public bool AllowFreeText { get => field.AllowFreeText; set => field.AllowFreeText = value; }

    [DisplayName("開窗畫面"), Description("有設定時，點這個欄位的下拉（或在文字框雙擊、按 F4）就開啟這個畫面當選取視窗，選一筆後把值帶回來"),
     Editor("System.Windows.Forms.Design.FileNameEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string PickerForm { get => field.PickerForm; set => field.PickerForm = value; }

    [DisplayName("開窗傳入參數"), Description("「對方欄位=@本視窗欄位」、「對方欄位=SQL 運算式」（含 @欄位 或括號，由資料庫算出）或「對方欄位=固定值」，例如：廠商編號=@廠商編號")]
    public string PickerParams { get => field.PickerParams; set => field.PickerParams = value; }

    [DisplayName("開窗回傳對應"), Description("「本視窗欄位=對方欄位」，可以帶回多個欄位；空白 = 只帶回這個欄位（對方同名的欄位）；只寫對方的欄位名稱 = 把它帶回這個欄位")]
    public string PickerReturn { get => field.PickerReturn; set => field.PickerReturn = value; }

    [DisplayName("執行後動作"), Description("下拉選單選了選項後執行的 SQL；@名稱 取「執行後動作傳入參數」或視窗上任何欄位的值。SQL 有 SELECT 時，第一筆依「執行後動作回傳對應」填進視窗上的欄位"),
     Editor(typeof(SqlEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public string AfterSelectSql { get => field.AfterSelectSql; set => field.AfterSelectSql = value; }

    [DisplayName("執行後動作傳入參數"), Description("「名稱=值」，多組用逗號或換行分隔，SQL 裡用 @名稱 取得；值可以是 @畫面欄位、SQL 運算式（含 @欄位 或括號，由資料庫算出）或固定值，例如：新編號=LEFT(@產品編號, 13) + '01'。沒列在這裡的 @名稱 直接取畫面上同名欄位的值"),
     Editor(typeof(SqlEditor), typeof(System.Drawing.Design.UITypeEditor))]
    public string AfterSelectParams { get => field.AfterSelectParams; set => field.AfterSelectParams = value; }

    [DisplayName("執行後動作回傳對應"), Description("SQL 查出資料時，第一筆要填進畫面上的哪些欄位：「畫面欄位=SQL 欄位」，多組用逗號分隔，例如：地址=ADDRESS, 電話=TEL。空白 = SQL 的每個欄位填進畫面上同名的欄位（畫面上沒有的略過）")]
    public string AfterSelectReturn { get => field.AfterSelectReturn; set => field.AfterSelectReturn = value; }

    [DisplayName("字體大小"), DefaultValue(0f), Description("輸入框文字的大小（點），0 = 預設（9）。字放大時請一起把「輸入框高度」拉高")]
    public float FontSize { get => field.FontSize; set => field.FontSize = Math.Clamp(value, 0, 72); }

    [DisplayName("標籤字體大小"), DefaultValue(0f), Description("標籤文字的大小（點），0 = 跟編輯視窗的字型大小一樣。字放大時請一起把「標籤寬度」拉寬")]
    public float LabelFontSize { get => field.LabelFontSize; set => field.LabelFontSize = Math.Clamp(value, 0, 72); }

    [DisplayName("字型"), TypeConverter(typeof(FontNameConverter)), Description("這個欄位（標籤與輸入框）用的字型；空白 = 跟編輯視窗的字型一樣")]
    public string FontName { get => field.FontName; set => field.FontName = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [DisplayName("底色"), DefaultValue(typeof(Color), ""), Description("輸入框的底色；沒設定（Empty）就是預設的白底。在屬性上按右鍵選「重設」可以還原")]
    public Color BackColor { get => field.BackColor; set => field.BackColor = value; }

    [DisplayName("X")]
    public int X { get => field.X; set => field.X = Math.Max(0, value); }

    [DisplayName("Y")]
    public int Y { get => field.Y; set => field.Y = Math.Max(0, value); }
}
