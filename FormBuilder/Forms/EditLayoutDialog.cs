using System.ComponentModel;

namespace FormBuilder;

/// <summary>
/// 編輯視窗版面設計：設計使用者雙擊 Grid 時彈出的編輯視窗。
/// 左邊「欄位」頁籤拖欄位、「工具箱」頁籤拖按鈕或下拉選單到版面上；
/// 版面上可拖曳移動、拖邊緣調整大小、圈選或 Ctrl 點選後按 Delete 刪除。按「確定」才寫回 GridDefinition
/// </summary>
public partial class EditLayoutDialog : Form
{
    private readonly GridDefinition grid;
    /// <summary>版面上的欄位（用 FieldDefinition 暫存位置，確定時再寫回 Grid 欄位）</summary>
    private readonly Dictionary<GridColumnDefinition, FieldBox> placed = new();
    /// <summary>版面上的按鈕（編輯的是複本，確定時才寫回）</summary>
    private readonly List<ButtonBox> buttonBoxes = new();
    /// <summary>版面上的明細清單（編輯的是複本，確定時才寫回）</summary>
    private readonly List<EditListBox> listBoxes = new();
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

    public EditLayoutDialog(GridDefinition gridDefinition, string connectionString = null, Action saveForm = null, Font screenFont = null)
    {
        InitializeComponent();
        grid = gridDefinition;
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
        // ---- 左邊「工具箱」頁籤 ----
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
        foreach (var p in state.Fields)
        {
            var column = grid.Columns.FirstOrDefault(c => c.Column == p.Column);
            if (column == null) continue;
            var box = new FieldBox(p.Field);
            placed[column] = box;
            AttachBox(box);
        }
        foreach (var b in state.Buttons) AddButtonBox(b, select: false);
        foreach (var l in state.Lists ?? new List<EditListDefinition>()) AddListBox(l, select: false);
        formSize = new Size(state.Width, state.Height);
        effectiveColumn = state.EffectiveColumn;
        UpdateEffectiveButton();
        SetBackColor(ColorText.Parse(state.BackColorHtml));
        RefreshAvailable();
        SetSelection(Array.Empty<DesignBox>());
        UpdateCanvasSize();
    }

    private static int Snap(int v) => Math.Max(0, (int)Math.Round(v / (double)DesignBox.Grid) * DesignBox.Grid);

    /// <summary>Delete：刪除選取的欄位；Ctrl+A：全選</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = propertyGrid.ContainsFocus;            // 在屬性面板打字時，Delete / Ctrl+A 照常作用在文字上
        if (!typing && keyData == Keys.Delete && selection.Count > 0) { RemoveSelected(); return true; }
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
        var temp = new GridDefinition
        {
            TableSchema = l.TableSchema,
            TableName = l.TableName,
            LinkText = l.LinkText,
            Columns = l.Columns,
            Joins = l.Joins,
        };
        using var dlg = new GridColumnsDialog(temp, grid.Columns.Where(c => !c.IsJoin).Select(c => c.Column).ToList(), connectionString, forEditList: true);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        l.LinkText = temp.LinkText;
        l.Joins = temp.Joins;
        box.RefreshFromModel();
        propertyGrid.Refresh();
    }

    /// <summary>下拉選單、日期選擇、勾選框、選項按鈕：從還沒放上版面的欄位選一個，放上去並設成對應的輸入方式</summary>
    private void AddKindField(Point location, InputKind kind)
    {
        var (name, tip, minWidth) = DesignerForm.KindInfo(kind);
        var available = grid.Columns.Where(c => !placed.ContainsKey(c)).ToList();
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
            if (box is FieldBox fb) placed.Remove(placed.First(p => p.Value == fb).Key);
            if (box is ButtonBox bb) buttonBoxes.Remove(bb);
            if (box is EditListBox lb) listBoxes.Remove(lb);
            canvas.Controls.Remove(box);
            box.Dispose();
        }
        SetSelection(Array.Empty<DesignBox>());
        RefreshAvailable();
        UpdateCanvasSize();
    }

    /// <summary>左邊清單 = 還沒放上版面的欄位，依原本欄位順序</summary>
    private void RefreshAvailable()
    {
        lstAvailable.BeginUpdate();
        lstAvailable.Items.Clear();
        foreach (var c in grid.Columns.Where(c => !placed.ContainsKey(c))) lstAvailable.Items.Add(c);
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
        propertyGrid.SelectedObjects = selection.Select(b => b is FieldBox fb ? new EditFieldView(fb.Field) : b.Model).ToArray();
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
    private bool WriteBack()
    {
        if (placed.Count == 0)
        {
            MessageBox.Show("編輯視窗至少要有一個欄位。", "提醒");
            return false;
        }
        foreach (var c in grid.Columns)
        {
            if (placed.TryGetValue(c, out var box))
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
        grid.EditButtons = buttonBoxes.Select(b => b.ButtonDef).ToList();
        grid.EditLists = listBoxes.Select(b => b.ListDef).ToList();
        grid.HasEditLayout = true;
        grid.EditFormWidth = formSize.Width;
        grid.EditFormHeight = formSize.Height;
        grid.EffectiveColumn = string.IsNullOrEmpty(effectiveColumn) ? null : effectiveColumn;
        grid.EditBackColor = backColor;
        grid.EditFontName = string.IsNullOrWhiteSpace(editFontName) ? null : editFontName;
        grid.EditFontSize = editFontSize;
        return true;
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

    [DisplayName("預設值"), Description("新增時自動帶入的值（使用者仍可以改）。可以寫固定值；日期用 NOW()（現在日期時間）、TODAY()（今天），可以加減天數，例如 TODAY()+7；USER() = 登入帳號；有設編號規則時以編號規則為準")]
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

    [DisplayName("選項 SQL"), Description("從資料庫取選項：第一欄是值，第二欄（可省略）是顯示文字，例如：SELECT 代碼, 名稱 FROM 幣別"),
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
