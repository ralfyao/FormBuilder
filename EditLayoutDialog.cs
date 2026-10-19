using System.ComponentModel;

namespace FormBuilder;

/// <summary>
/// 編輯視窗版面設計：設計使用者雙擊 Grid 時彈出的編輯視窗。
/// 左邊「欄位」頁籤拖欄位、「工具箱」頁籤拖按鈕或下拉選單到版面上；
/// 版面上可拖曳移動、拖邊緣調整大小、圈選或 Ctrl 點選後按 Delete 刪除。按「確定」才寫回 GridDefinition
/// </summary>
public class EditLayoutDialog : Form
{
    private readonly GridDefinition grid;
    /// <summary>版面上的欄位（用 FieldDefinition 暫存位置，確定時再寫回 Grid 欄位）</summary>
    private readonly Dictionary<GridColumnDefinition, FieldBox> placed = new();
    /// <summary>版面上的按鈕（編輯的是複本，確定時才寫回）</summary>
    private readonly List<ButtonBox> buttonBoxes = new();
    private readonly List<DesignBox> selection = new();
    private Dictionary<DesignBox, Point> dragStarts;

    private readonly ListBox lstAvailable = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Panel canvasHost = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SystemColors.AppWorkspace };
    private readonly Panel canvas = new() { BackColor = SystemColors.Control, AllowDrop = true, Location = new Point(10, 10), BorderStyle = BorderStyle.FixedSingle };
    private readonly Label lblHint = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly PropertyGrid propertyGrid = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true, PropertySort = PropertySort.NoSort };

    /// <summary>編輯視窗欄位區的大小（拖版面的右邊、下邊、右下角調整）</summary>
    private Size formSize;
    private readonly CanvasResizer canvasResizer;

    // 圈選
    private bool banding, bandAdditive;
    private Point bandStart;
    private Rectangle bandScreenRect;

    public EditLayoutDialog(GridDefinition gridDefinition)
    {
        grid = gridDefinition;
        Text = $"編輯視窗版面：{grid.Title ?? grid.TableName}";
        Font = new Font("Microsoft JhengHei UI", 9F);
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        // ---- 左邊：「欄位」頁籤（還沒放上版面的欄位）、「工具箱」頁籤（按鈕、下拉選單）----
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var tabColumns = new TabPage("欄位") { Padding = new Padding(4) };
        tabColumns.Controls.Add(lstAvailable);
        tabColumns.Controls.Add(new Label { Text = "拖到右邊版面，或雙擊加入", Dock = DockStyle.Top, Height = 22 });
        var toolbox = new ToolboxList();
        toolbox.ItemActivated += kind => AddToolboxItem(kind, NextFreePoint());
        var tabToolbox = new TabPage("工具箱") { Padding = new Padding(4) };
        tabToolbox.Controls.Add(toolbox);
        tabToolbox.Controls.Add(new Label { Text = "拖到右邊版面，或雙擊加入", Dock = DockStyle.Top, Height = 22 });
        tabs.TabPages.Add(tabColumns);
        tabs.TabPages.Add(tabToolbox);
        var left = new Panel { Dock = DockStyle.Left, Width = 220, Padding = new Padding(6) };
        left.Controls.Add(tabs);
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

        // ---- 中間：版面 ----
        canvasHost.Controls.Add(canvas);
        canvas.DragEnter += (s, e) =>
            e.Effect = e.Data.GetDataPresent(typeof(GridColumnDefinition)) || e.Data.GetDataPresent(typeof(ToolboxDragData))
                ? DragDropEffects.Copy : DragDropEffects.None;
        canvas.DragDrop += (s, e) =>
        {
            Point p = canvas.PointToClient(new Point(e.X, e.Y));
            var location = new Point(Snap(p.X), Snap(p.Y));
            if (e.Data.GetData(typeof(GridColumnDefinition)) is GridColumnDefinition c) PlaceColumn(c, location);
            else if (e.Data.GetData(typeof(ToolboxDragData)) is ToolboxDragData tool) AddToolboxItem(tool.Kind, location);
        };
        // 拖版面邊緣調整大小；拖出外框會自動捲動，按住不放就持續延伸
        canvasResizer = new CanvasResizer(canvas, canvasHost, () => formSize, size => { formSize = size; UpdateCanvasSize(); });
        canvas.MouseDown += (s, e) => OnCanvasMouseDown(e);
        canvas.MouseMove += (s, e) => OnCanvasMouseMove(e);
        canvas.MouseUp += (s, e) => OnCanvasMouseUp();
        canvas.Paint += (s, e) =>                          // 右下角畫出可拖曳的格紋
            ControlPaint.DrawSizeGrip(e.Graphics, canvas.BackColor, canvas.ClientSize.Width - 16, canvas.ClientSize.Height - 16, 16, 16);

        // ---- 下方按鈕 ----
        var btnArrange = new Button { Text = "重新排列成一欄", Width = 130, Dock = DockStyle.Left };
        btnArrange.Click += (s, e) => ArrangeAll();
        var btnRemove = new Button { Text = "刪除選取欄位", Width = 110, Dock = DockStyle.Left };
        btnRemove.Click += (s, e) => RemoveSelected();
        var btnOk = new Button { Text = "確定", Width = 90, Dock = DockStyle.Right };
        btnOk.Click += (s, e) => Apply();
        var btnCancel = new Button { Text = "取消", Width = 90, Dock = DockStyle.Right, DialogResult = DialogResult.Cancel };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
        bottom.Controls.Add(lblHint);
        bottom.Controls.Add(btnRemove);
        bottom.Controls.Add(btnArrange);
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);
        CancelButton = btnCancel;

        // 版面上方的對齊工具列（只蓋在版面區，不蓋左邊清單）
        var alignBar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        AlignHelper.AddAlignButtons(alignBar, () => selection.ToList(), UpdateCanvasSize);
        var center = new Panel { Dock = DockStyle.Fill };
        center.Controls.Add(canvasHost);
        center.Controls.Add(alignBar);

        // 右邊：點選欄位後修改標籤、提示文字（placeholder）、寬度、位置
        var right = new Panel { Dock = DockStyle.Right, Width = 280, Padding = new Padding(6) };
        right.Controls.Add(propertyGrid);
        right.Controls.Add(new Label { Text = "屬性（點選欄位或按鈕後修改；多選時一次改全部）", Dock = DockStyle.Top, Height = 36 });
        propertyGrid.PropertyValueChanged += (s, e) =>
        {
            foreach (var b in selection) b.RefreshFromModel();
            UpdateCanvasSize();
        };

        Controls.Add(center);
        Controls.Add(left);
        Controls.Add(right);
        Controls.Add(bottom);

        // 目前的版面（還沒設計過就先排成一欄）
        grid.EnsureEditLayout();
        foreach (var c in grid.EditColumns) PlaceColumn(c, new Point(c.EditX, c.EditY), select: false);
        foreach (var b in grid.EditButtons) AddButtonBox(b.Clone(), select: false);
        // 設計過大小就用設計的，沒有就依欄位自動決定
        formSize = grid.EditFormSize ?? InputHelper.ContentSize(placed.Values.Select(b => b.Field), 500);
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
            LabelWidth = c.EditLabelWidth > 0 ? c.EditLabelWidth : FieldBox.DefaultLabelWidth,
            InputType = c.EditInputType,
            Items = c.EditItems,
            ItemsSql = c.EditItemsSql,
            AllowFreeText = c.EditAllowFreeText,
            IsKey = c.IsKey,
            NoEntry = c.NoEntry || c.IsIdentity,
            X = location.X,
            Y = location.Y,
            Width = c.EditWidth > 0 ? c.EditWidth : Math.Clamp(c.Width, 120, 400),
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
    }

    // ================= 工具箱 =================

    private void AddToolboxItem(string kind, Point location)
    {
        if (kind == "Button") AddButtonBox(new ButtonDefinition { X = location.X, Y = location.Y });
        else if (kind == "ComboBox") AddComboField(location);
    }

    private void AddButtonBox(ButtonDefinition button, bool select = true)
    {
        var box = new ButtonBox(button);
        buttonBoxes.Add(box);
        AttachBox(box);
        UpdateCanvasSize();
        if (select) SetSelection(new[] { box });
    }

    /// <summary>下拉選單：從還沒放上版面的欄位選一個，放上去並設成下拉選單</summary>
    private void AddComboField(Point location)
    {
        var available = grid.Columns.Where(c => !placed.ContainsKey(c)).ToList();
        if (available.Count == 0)
        {
            MessageBox.Show("欄位都已經放在版面上了。要改成下拉選單，請點選該欄位，在屬性面板把「輸入方式」改成「下拉選單」。", "提醒");
            return;
        }
        var infos = available.Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, Description = c.Header }).ToList();
        using var dlg = new ColumnPickDialog($"下拉選單：{grid.TableName}", infos);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var column = available.First(c => c.Column == dlg.SelectedColumn.Name);
        PlaceColumn(column, location);
        var box = placed[column];
        box.Field.InputType = InputKind.下拉選單;
        box.RefreshFromModel();
        SetSelection(new[] { box });                      // 屬性面板接著設定固定選項或選項 SQL
    }

    private void RemoveSelected()
    {
        foreach (var box in selection.ToList())
        {
            if (box is FieldBox fb) placed.Remove(placed.First(p => p.Value == fb).Key);
            if (box is ButtonBox bb) buttonBoxes.Remove(bb);
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
        return new Point(10, placed.Values.Max(b => b.Field.Y) + 32);
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
            y += 32;
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
        lblHint.Text = $"  版面 {formSize.Width} × {formSize.Height}，{placed.Count} 個欄位、{buttonBoxes.Count} 個按鈕；拖版面邊緣調整大小，圈選或 Ctrl 點選後按 Delete 刪除";
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
            (dx, dy) = AlignHelper.Magnetize(leadStart, dx, dy, canvas.Controls.OfType<DesignBox>().Where(b => !dragStarts.ContainsKey(b)));
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
        propertyGrid.SelectedObjects = selection.Select(b => b is FieldBox fb ? new EditFieldView(fb.Field) : b.Model).ToArray();
    }

    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        // 抓到版面邊緣 = 調整大小；其他地方 = 拉框圈選
        if (canvasResizer.TryStart(e)) return;
        bandAdditive = ModifierKeys.HasFlag(Keys.Control);
        if (!bandAdditive) SetSelection(Array.Empty<DesignBox>());
        banding = true;
        bandStart = e.Location;
        bandScreenRect = Rectangle.Empty;
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (canvasResizer.Active || !banding)
        {
            canvasResizer.Move(e);                         // 拖曳中調整大小；沒在拖曳時切換游標
            return;
        }
        if (!bandScreenRect.IsEmpty) ControlPaint.DrawReversibleFrame(bandScreenRect, canvas.BackColor, FrameStyle.Dashed);
        var rect = Rectangle.FromLTRB(Math.Min(bandStart.X, e.X), Math.Min(bandStart.Y, e.Y), Math.Max(bandStart.X, e.X), Math.Max(bandStart.Y, e.Y));
        bandScreenRect = canvas.RectangleToScreen(rect);
        ControlPaint.DrawReversibleFrame(bandScreenRect, canvas.BackColor, FrameStyle.Dashed);
    }

    private void OnCanvasMouseUp()
    {
        if (canvasResizer.End()) return;
        if (!banding) return;
        banding = false;
        if (bandScreenRect.IsEmpty) return;
        ControlPaint.DrawReversibleFrame(bandScreenRect, canvas.BackColor, FrameStyle.Dashed);
        Rectangle band = canvas.RectangleToClient(bandScreenRect);
        bandScreenRect = Rectangle.Empty;
        var hit = canvas.Controls.OfType<DesignBox>().Where(b => b.Bounds.IntersectsWith(band));
        SetSelection(bandAdditive ? selection.Concat(hit) : hit);
    }

    // ================= 確定 =================

    private void Apply()
    {
        if (placed.Count == 0)
        {
            MessageBox.Show("編輯視窗至少要有一個欄位。", "提醒");
            return;
        }
        foreach (var c in grid.Columns)
        {
            if (placed.TryGetValue(c, out var box))
            {
                c.EditVisible = true;
                c.EditX = box.Field.X;
                c.EditY = box.Field.Y;
                c.EditWidth = box.Field.Width;
                // 標籤跟 Grid 標題一樣就不另存，之後改 Grid 標題時編輯視窗會跟著變
                c.EditLabel = box.Field.Label == c.Header ? null : box.Field.Label;
                c.EditPlaceholder = string.IsNullOrEmpty(box.Field.Placeholder) ? null : box.Field.Placeholder;
                c.EditLabelWidth = box.Field.LabelWidth == FieldBox.DefaultLabelWidth ? 0 : box.Field.LabelWidth;
                c.EditInputType = box.Field.InputType;
                c.EditItems = box.Field.Items;
                c.EditItemsSql = box.Field.ItemsSql;
                c.EditAllowFreeText = box.Field.AllowFreeText;
            }
            else
            {
                c.EditVisible = false;
            }
        }
        grid.EditButtons = buttonBoxes.Select(b => b.ButtonDef).ToList();
        grid.HasEditLayout = true;
        grid.EditFormWidth = formSize.Width;
        grid.EditFormHeight = formSize.Height;
        DialogResult = DialogResult.OK;
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

    [DisplayName("輸入方式"), Description("自動：依資料型別用文字框、勾選框或日期；下拉選單：從選項中挑選")]
    public InputKind InputType { get => field.InputType; set => field.InputType = value; }

    [DisplayName("固定選項"), Description("下拉選單的選項，用逗號或換行分隔；值和顯示文字不同時寫「值=顯示」，例如：Y=是,N=否"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string Items { get => field.Items; set => field.Items = value; }

    [DisplayName("選項 SQL"), Description("從資料庫取選項：第一欄是值，第二欄（可省略）是顯示文字，例如：SELECT 代碼, 名稱 FROM 幣別"),
     Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string ItemsSql { get => field.ItemsSql; set => field.ItemsSql = value; }

    [DisplayName("可自行輸入"), Description("True：選項以外的值也可以直接打字輸入")]
    public bool AllowFreeText { get => field.AllowFreeText; set => field.AllowFreeText = value; }

    [DisplayName("X")]
    public int X { get => field.X; set => field.X = Math.Max(0, value); }

    [DisplayName("Y")]
    public int Y { get => field.Y; set => field.Y = Math.Max(0, value); }
}
