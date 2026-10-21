namespace FormBuilder;

/// <summary>
/// 列印樣板設計（按鈕動作 = 列印報表，在設計畫面雙擊按鈕開啟）：
/// 左邊「欄位」頁籤是主畫面資料表的欄位（可以 JOIN 其他資料表帶進更多欄位），「工具箱」有文字、圖片、線條、方框；
/// 拖到中間的紙張上排版，右邊屬性面板設定字型、格式、圖片檔。存成 *.print.json，執行時依樣板預覽列印
/// </summary>
public partial class PrintDesignerForm : Form
{
    private PrintTemplate template;
    private readonly string connectionString;
    /// <summary>樣板檔的完整路徑；null = 還沒存過</summary>
    private string templatePath;
    /// <summary>新樣板檔預設放的資料夾（畫面定義檔的資料夾）</summary>
    private readonly string defaultFolder;
    private readonly string defaultName;

    /// <summary>主資料表的欄位（從資料庫讀）</summary>
    private List<ColumnInfo> tableColumns = new();
    /// <summary>列印來源清單的欄位（按鈕依明細清單選取的資料列印時，選取那筆提供的欄位）；null = 沒有列印來源清單</summary>
    private readonly List<ColumnInfo> sourceColumns;
    /// <summary>編輯視窗上的明細清單（「設定單身」可以直接用它，不用再 JOIN）</summary>
    private readonly List<EditListDefinition> editLists;
    private readonly List<DesignBox> selection = new();
    private Dictionary<DesignBox, Point> dragStarts;
    private SelectionBand band;
    private bool bandAdditive;
    private readonly UndoHistory history;

    /// <summary>儲存後的樣板檔路徑（給按鈕的「列印樣板檔」用）；沒存過 = null</summary>
    public string SavedPath { get; private set; }

    /// <summary>從清單、工具箱拖到紙張上帶的資料</summary>
    private sealed class PrintDragData
    {
        public PrintItemKind Kind { get; init; }
        public string Column { get; init; }
        public string Header { get; init; }
        /// <summary>單身的欄位（放進明細表格）</summary>
        public bool IsDetail { get; init; }
    }

    /// <param name="path">既有的樣板檔（不存在就建立新的樣板）</param>
    /// <param name="keyColumns">主資料表的主鍵（列印時用來讀出要印的那筆）</param>
    /// <param name="recordColumns">列印來源清單的欄位（選取那筆提供的欄位）；null = 沒有列印來源清單</param>
    public PrintDesignerForm(string path, string connectionString, string tableSchema, string tableName, IEnumerable<string> keyColumns,
                             string defaultFolder, string defaultName, IEnumerable<ColumnInfo> recordColumns = null,
                             IEnumerable<EditListDefinition> editLists = null)
    {
        InitializeComponent();
        sourceColumns = recordColumns?.ToList();
        this.editLists = editLists?.Where(l => !string.IsNullOrWhiteSpace(l.TableName)).ToList() ?? new();
        this.connectionString = connectionString;
        this.defaultFolder = defaultFolder;
        this.defaultName = defaultName;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            template = PrintTemplate.Load(path);
            templatePath = path;
        }
        else
        {
            template = new PrintTemplate();
        }
        // 資料表與主鍵以目前畫面為準（畫面換了資料表時樣板跟著換）
        var keys = keyColumns.ToList();
        bool tableChanged = templatePath != null && !string.IsNullOrWhiteSpace(template.TableName)
                            && !template.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase);
        if (tableChanged && template.HasJoinTo(tableName))
        {
            // 例如按鈕改成印明細清單選取的資料：原本以廠商為主 JOIN 供料 → 以供料為主 JOIN 廠商，欄位都保留
            if (MessageBox.Show($"這個樣板原本以「{template.TableName}」為主資料表，現在要改印「{tableName}」的資料。{Environment.NewLine}{Environment.NewLine}"
                                + $"要自動轉換成以「{tableName}」為主、「{template.TableName}」改用 JOIN 帶入嗎？（樣板上的欄位都會保留；轉換後記得存檔）",
                                "列印樣板", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                List<string> columns = new();
                try { if (!string.IsNullOrWhiteSpace(connectionString)) columns = SchemaReader.GetColumns(connectionString, tableSchema, tableName).Select(c => c.Name).ToList(); }
                catch (Exception) { }
                var notes = template.RebaseTo(tableSchema, tableName, keys, new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase));
                MessageBox.Show("已轉換，請檢查後按「檔案 → 儲存」。" + (notes.Count > 0 ? Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, notes) : ""), "列印樣板");
            }
        }
        else if (tableChanged)
        {
            MessageBox.Show($"這個樣板原本以「{template.TableName}」為主資料表，現在改成「{tableName}」；原本的欄位如果「{tableName}」沒有，會印成空白，請重新拖欄位或用 JOIN 帶入。", "列印樣板");
        }
        template.TableSchema = tableSchema;
        template.TableName = tableName;
        template.KeyColumns = keys;
        UpdateTitle();

        // ---- 左邊「欄位」頁籤 ----
        lstColumns.MouseDown += (s, e) =>
        {
            int i = lstColumns.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && i >= 0 && lstColumns.Items[i] is ColumnItem c)
                lstColumns.DoDragDrop(new PrintDragData { Kind = PrintItemKind.欄位, Column = c.Name, Header = c.Header, IsDetail = c.IsDetail }, DragDropEffects.Copy);
        };
        lstColumns.DoubleClick += (s, e) =>
        {
            if (lstColumns.SelectedItem is not ColumnItem c) return;
            if (c.IsDetail) AddDetailColumn(null, NextFreePoint(), c.Name, c.Header);
            else AddItem(PrintItemKind.欄位, NextFreePoint(), c.Name, c.Header);
        };
        btnJoin.Click += (s, e) => AddJoin();
        btnDetail.Click += (s, e) => ChooseDetail();
        var columnMenu = new ContextMenuStrip();
        var miRemoveJoin = new ToolStripMenuItem("移除這個 JOIN");
        var miRemoveDetail = new ToolStripMenuItem("移除單身");
        columnMenu.Items.AddRange(new ToolStripItem[] { miRemoveJoin, miRemoveDetail });
        columnMenu.Opening += (s, e) =>
        {
            int i = lstColumns.IndexFromPoint(lstColumns.PointToClient(MousePosition));
            if (i >= 0) lstColumns.SelectedIndex = i;
            var c = lstColumns.SelectedItem as ColumnItem;
            miRemoveJoin.Visible = c is { IsDetail: false, JoinAlias: not null };
            miRemoveDetail.Visible = c is { IsDetail: true };
            e.Cancel = !miRemoveJoin.Visible && !miRemoveDetail.Visible;
        };
        miRemoveJoin.Click += (s, e) => { if (lstColumns.SelectedItem is ColumnItem { JoinAlias: { } alias }) RemoveJoin(alias); };
        miRemoveDetail.Click += (s, e) => RemoveDetail();
        lstColumns.ContextMenuStrip = columnMenu;

        // ---- 左邊「工具箱」頁籤 ----
        lstTools.Items.AddRange(new object[] { PrintItemKind.文字, PrintItemKind.圖片, PrintItemKind.線條, PrintItemKind.方框, PrintItemKind.明細表格 });
        lstTools.MouseDown += (s, e) =>
        {
            int i = lstTools.IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && i >= 0)
                lstTools.DoDragDrop(new PrintDragData { Kind = (PrintItemKind)lstTools.Items[i] }, DragDropEffects.Copy);
        };
        lstTools.DoubleClick += (s, e) =>
        {
            if (lstTools.SelectedItem is PrintItemKind kind) AddItem(kind, NextFreePoint());
        };

        // ---- 中間：紙張 ----
        canvas.DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(typeof(PrintDragData)) ? DragDropEffects.Copy : DragDropEffects.None;
        canvas.DragDrop += (s, e) => OnDrop(e);
        canvas.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            bandAdditive = ModifierKeys.HasFlag(Keys.Control);
            if (!bandAdditive) SetSelection(Array.Empty<DesignBox>());
            band ??= new SelectionBand(canvas);
            band.Begin(e.Location);
        };
        canvas.MouseMove += (s, e) => { if (band is { Active: true }) band.Move(e.Location); };
        canvas.MouseUp += (s, e) =>
        {
            if (band is not { Active: true }) return;
            Rectangle rect = band.End();
            if (rect.IsEmpty) return;
            var hit = canvas.Controls.OfType<DesignBox>().Where(b => b.Bounds.IntersectsWith(rect));
            SetSelection(bandAdditive ? selection.Concat(hit) : hit);
        };

        // ---- 右邊屬性面板 ----
        propertyGrid.PropertyValueChanged += (s, e) =>
        {
            foreach (var b in selection) b.RefreshFromModel();
            if (selection.Count == 0) ApplyPageSize();     // 紙張大小、方向
            UpdateStatus();
            MarkMissingColumns();                           // 改了欄位：重新檢查找不到資料來源的欄位
        };

        // ---- 選單 ----
        miSave.Click += (s, e) => Save(false);
        miSaveAs.Click += (s, e) => Save(true);
        miUndo.Click += (s, e) => history.Undo();
        miRedo.Click += (s, e) => history.Redo();
        miRemove.Click += (s, e) => RemoveSelected();
        // 複製 / 剪下 / 貼上 / 再製一份（放在「刪除」上面）
        var clipItems = new (string Text, string Keys, Action Action)[]
        {
            ("複製", "Ctrl+C", CopySelected), ("剪下", "Ctrl+X", CutSelected), ("貼上", "Ctrl+V", Paste), ("再製一份", "Ctrl+D", Duplicate),
        };
        int at = btnEditMenu.DropDownItems.IndexOf(miRemove);
        foreach (var (text, shortcut, action) in clipItems)
        {
            var mi = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut };
            mi.Click += (s, e) => action();
            btnEditMenu.DropDownItems.Insert(at++, mi);
        }
        // 上下層：列印時依元件順序畫（後面的蓋在前面的上面），設計畫面一樣
        var miFront = new ToolStripMenuItem("移到最上層");
        var miBack = new ToolStripMenuItem("移到最下層");
        miFront.Click += (s, e) => ChangeLayer(toFront: true);
        miBack.Click += (s, e) => ChangeLayer(toFront: false);
        btnEditMenu.DropDownItems.Insert(btnEditMenu.DropDownItems.IndexOf(sepEditAlign), miFront);
        btnEditMenu.DropDownItems.Insert(btnEditMenu.DropDownItems.IndexOf(sepEditAlign), miBack);
        AlignHelper.AddAlignMenu(btnEditMenu.DropDownItems, () => selection.ToList(), () => propertyGrid.Refresh());
        btnPreview.Click += (s, e) => PreviewSample();

        // 目前的樣板
        foreach (var item in template.Items) AttachBox(new PrintItemBox(item));
        SyncZOrder();
        ApplyPageSize();
        SetSelection(Array.Empty<DesignBox>());
        Load += (s, e) => LoadColumns();
        history = new UndoHistory(this, () => template.ToJson(), RestoreSnapshot);
    }

    private static int Snap(int v) => Math.Max(0, (int)Math.Round(v / 5.0) * 5);

    /// <summary>Delete 刪除、Ctrl+A 全選、Ctrl+Z 復原、Ctrl+Y 取消復原（正在打字時不攔截）</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = UndoHistory.IsEditingText() || propertyGrid.ContainsFocus;
        if (!typing && keyData == Keys.Delete && selection.Count > 0) { RemoveSelected(); return true; }
        if (!typing && keyData == (Keys.Control | Keys.A)) { SetSelection(canvas.Controls.OfType<DesignBox>()); return true; }
        if (!typing && keyData == (Keys.Control | Keys.C)) { CopySelected(); return true; }
        if (!typing && keyData == (Keys.Control | Keys.X)) { CutSelected(); return true; }
        if (!typing && keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
        if (!typing && keyData == (Keys.Control | Keys.D)) { Duplicate(); return true; }
        if (!UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.Z)) { history.Undo(); return true; }
        if (!UndoHistory.IsEditingText() && (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z))) { history.Redo(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 欄位清單 =================

    /// <summary>左邊欄位清單的一項：主資料表的欄位，或 JOIN 帶進來的欄位</summary>
    private sealed record ColumnItem(string Name, string Header, string Type, string JoinAlias, bool IsDetail = false, bool IsRecord = false)
    {
        public override string ToString() => IsDetail ? $"{Header}（單身，{Type}）"
                                           : IsRecord ? $"{Header}（選取資料，{Type}）"
                                           : JoinAlias == null ? $"{Header}（{Type}）" : $"{Header}（{JoinAlias}，{Type}）";
    }

    private void LoadColumns()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            if (!string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(template.TableName))
                tableColumns = SchemaReader.GetColumns(connectionString, template.TableSchema, template.TableName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取資料表欄位失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        // 之前設定的單身沒有記主鍵（「只印清單上選取的資料」要用）
        if (template.Detail != null && template.DetailKeyColumns.Count == 0) template.DetailKeyColumns = DetailKeysFromDatabase();
        FillColumnList();
    }

    private void FillColumnList()
    {
        lstColumns.BeginUpdate();
        lstColumns.Items.Clear();
        foreach (var c in tableColumns)
            lstColumns.Items.Add(new ColumnItem(c.Name, string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description, c.DataType, null));
        foreach (var c in template.JoinColumns)
            lstColumns.Items.Add(new ColumnItem(c.Column, c.Header ?? c.Column, c.DataType, c.JoinAlias));
        // 列印來源清單選取那筆提供的欄位：單頭資料表、JOIN 都沒有的才列（例如詢價日期）
        if (tableColumns.Count > 0)
        {
            var known = tableColumns.Select(c => c.Name).Concat(template.JoinColumns.Select(c => c.Column)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            template.RecordColumns = (sourceColumns ?? new()).Select(c => c.Name).Where(n => !known.Contains(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        foreach (var name in template.RecordColumns)
        {
            var c = sourceColumns?.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            lstColumns.Items.Add(new ColumnItem(name, string.IsNullOrWhiteSpace(c?.Description) ? name : c.Description, c?.DataType, null, IsRecord: true));
        }
        // 單身的欄位排在最後（拖到明細表格）
        foreach (var c in template.DetailColumns)
            lstColumns.Items.Add(new ColumnItem(c.Column, c.Header ?? c.Column, c.DataType, null, IsDetail: true));
        lstColumns.EndUpdate();
        // 屬性面板「欄位」的下拉清單
        PrintColumnConverter.Available = tableColumns.Select(c => c.Name).Concat(template.JoinColumns.Select(c => c.Column)).Concat(template.RecordColumns).ToList();
        PrintDetailColumnConverter.Available = template.DetailColumns.Select(c => c.Column).ToList();
        btnDetail.Text = template.Detail == null ? "設定單身（單頭與單身的對應）…" : $"單身：{template.Detail.TableName}（重新設定…）";
        MarkMissingColumns();
    }

    /// <summary>
    /// 樣板上找不到資料來源的欄位：不是主資料表的欄位、也不是 JOIN 帶進來的（例如換了主資料表，原本的欄位沒有 JOIN 進來）。
    /// 還沒讀到主資料表的欄位時無法判斷，回傳空的
    /// </summary>
    private List<string> MissingColumns()
    {
        if (tableColumns.Count == 0) return new();
        var known = PrintColumnConverter.Available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var detail = PrintDetailColumnConverter.Available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return template.Items.Where(i => i.Kind == PrintItemKind.欄位 && !string.IsNullOrWhiteSpace(i.Column) && !known.Contains(i.Column)).Select(i => i.Column)
            .Concat(template.Items.Where(i => i.Kind == PrintItemKind.明細表格)
                                  .SelectMany(i => i.TableColumns).Select(c => c.Column)
                                  .Where(c => !string.IsNullOrWhiteSpace(c) && !detail.Contains(c)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>找不到資料來源的欄位在紙張上用紅框標出來，狀態列說明怎麼補</summary>
    private void MarkMissingColumns()
    {
        var missing = MissingColumns().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var box in canvas.Controls.OfType<PrintItemBox>())
        {
            bool m = box.Item.Kind == PrintItemKind.欄位 ? missing.Contains(box.Item.Column ?? "")
                   : box.Item.Kind == PrintItemKind.明細表格 && box.Item.TableColumns.Any(c => missing.Contains(c.Column ?? ""));
            if (box.Missing == m) continue;
            box.Missing = m;
            box.Invalidate();
        }
        if (missing.Count > 0)
            UpdateStatus($"紅框的欄位在「{template.TableName}」找不到：{string.Join("、", missing)}，請按「JOIN 其他資料表」把它們帶進來");
    }

    /// <summary>預覽、列印前的檢查說明；沒問題回傳 null</summary>
    private string MissingColumnsMessage()
    {
        var missing = MissingColumns();
        if (missing.Count == 0) return null;
        var selfJoins = template.Joins.Where(j => j.TableName.Equals(template.TableName, StringComparison.OrdinalIgnoreCase)).Select(j => j.Alias).ToList();
        return $"樣板上有欄位在主資料表「{template.TableName}」找不到，也沒有用 JOIN 帶進來：{Environment.NewLine}　{string.Join("、", missing)}"
             + $"{Environment.NewLine}{Environment.NewLine}請按左下「JOIN 其他資料表…」，選這些欄位所在的資料表（例如廠商資料表，對應 廠商編號=廠商編號），"
             + "勾選這些欄位；名稱一樣就會自動接上，紙張上的紅框會消失。"
             + (selfJoins.Count == 0 ? "" : $"{Environment.NewLine}{Environment.NewLine}另外，JOIN {string.Join("、", selfJoins)} 是 JOIN「{template.TableName}」自己"
                                          + "（換主資料表前留下的），可能對到別筆資料，建議在左邊欄位清單按右鍵移除，改用主資料表本身的欄位。");
    }

    /// <summary>單頭可以拿來對應的欄位：主資料表的欄位 + JOIN 帶進來的欄位 + 列印來源清單選取那筆的欄位</summary>
    private List<ColumnInfo> HeaderColumns() =>
        tableColumns.Concat(template.JoinColumns.Select(c => new ColumnInfo { Name = c.Column, DataType = c.DataType, MaxLength = c.MaxLength, Description = c.Header }))
                    .Concat(template.RecordColumns.Select(n => sourceColumns?.FirstOrDefault(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) ?? new ColumnInfo { Name = n }))
                    .ToList();

    /// <summary>
    /// 設定單身：選單身資料表、單頭欄位=單身欄位的對應（例如 廠商編號=廠商編號, 詢價日期=詢價日期）、要印的單身欄位。
    /// 列印時對應的值都相同的單身資料印在同一張單據的明細表格
    /// </summary>
    /// <summary>設定單身：編輯視窗上有明細清單時，可以直接用清單（資料表、關聯、欄位、清單的 JOIN 都照清單），或另外選資料表</summary>
    private void ChooseDetail()
    {
        if (editLists.Count == 0)
        {
            SetDetail();
            return;
        }
        var menu = new ContextMenuStrip();
        foreach (var list in editLists)
            menu.Items.Add($"使用編輯視窗的明細清單：{list.Title ?? list.TableName}（關聯：{list.LinkText}）", null, (s, e) => UseListAsDetail(list));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("選其他資料表…", null, (s, e) => SetDetail());
        menu.Show(btnDetail, new Point(0, btnDetail.Height));
    }

    /// <summary>單身直接用編輯視窗的明細清單：資料表、關聯欄位、顯示的欄位、清單的 JOIN（例如品名）都照清單</summary>
    private void UseListAsDetail(EditListDefinition list)
    {
        if (template.Detail != null &&
            MessageBox.Show($"已經設定單身「{template.Detail.TableName}」（對應：{template.Detail.LinkText}），要換成明細清單「{list.Title ?? list.TableName}」嗎？",
                            "設定單身", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var copy = list.Clone();
        template.Detail = new GridJoinDefinition { Alias = "D", TableSchema = copy.TableSchema, TableName = copy.TableName, LinkText = copy.LinkText };
        template.DetailJoins = copy.Joins;
        template.DetailColumns = copy.Columns.Where(c => c.Visible).ToList();
        template.DetailKeyColumns = copy.Columns.Where(c => c.IsKey && !c.IsJoin).Select(c => c.Column).ToList();
        FillColumnList();
        if (!template.Items.Any(i => i.Kind == PrintItemKind.明細表格)) AddItem(PrintItemKind.明細表格, NextFreePoint());
        propertyGrid.Refresh();
        UpdateStatus($"單身用明細清單 {copy.TableName}（對應：{copy.LinkText}，{template.DetailColumns.Count} 個欄位）");
        MessageBox.Show($"已帶入明細清單「{list.Title ?? list.TableName}」的 {template.DetailColumns.Count} 個欄位，對應：{copy.LinkText}。{Environment.NewLine}{Environment.NewLine}"
                        + "如果還要其他欄位也相同才印在同一張（例如同一詢價日期），點紙張空白處，在屬性面板「單頭與單身的對應」後面加上，例如：, 詢價日期=詢價日期",
                        "設定單身");
    }

    private void SetDetail()
    {
        if (template.Detail != null &&
            MessageBox.Show($"已經設定單身「{template.Detail.TableName}」（對應：{template.Detail.LinkText}）。{Environment.NewLine}"
                            + "要重新設定嗎？（只改對應條件的話，點紙張空白處，在屬性面板「單頭與單身的對應」直接改）",
                            "設定單身", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        using var dlg = new JoinDialog(connectionString, "D", HeaderColumns(), Array.Empty<string>(), forDetail: true);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        template.Detail = dlg.Join;
        template.DetailJoins = new();
        template.DetailKeyColumns = DetailKeysFromDatabase();
        // 單身資料表本身的欄位（JOIN 對話框沒有其他欄位，名稱就是原本的欄位名稱）
        template.DetailColumns = dlg.NewColumns.Select(c => new GridColumnDefinition
            { Column = c.SourceColumn, Header = c.Header, DataType = c.DataType, MaxLength = c.MaxLength, Width = c.Width }).ToList();
        FillColumnList();
        // 還沒有明細表格就放一個，欄位先全部放進去
        if (!template.Items.Any(i => i.Kind == PrintItemKind.明細表格)) AddItem(PrintItemKind.明細表格, NextFreePoint());
        propertyGrid.Refresh();
        UpdateStatus($"已設定單身 {dlg.Join.TableName}（對應：{dlg.Join.LinkText}，{dlg.NewColumns.Count} 個欄位）");
    }

    /// <summary>單身資料表的主鍵：編輯視窗上同一個資料表的明細清單有設定主鍵就用它，否則用資料庫的主鍵</summary>
    private List<string> DetailKeysFromDatabase()
    {
        if (template.Detail == null) return new();
        var list = editLists.FirstOrDefault(l => l.TableName.Equals(template.Detail.TableName, StringComparison.OrdinalIgnoreCase));
        var keys = list?.Columns.Where(c => c.IsKey && !c.IsJoin).Select(c => c.Column).ToList() ?? new();
        if (keys.Count > 0) return keys;
        try { return SchemaReader.GetColumns(connectionString, template.Detail.TableSchema, template.Detail.TableName).Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList(); }
        catch (Exception) { return new(); }
    }

    private void RemoveDetail()
    {
        if (template.Detail == null) return;
        if (MessageBox.Show($"確定要移除單身「{template.Detail.TableName}」嗎？明細表格會印成空白。", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        template.Detail = null;
        template.DetailColumns = new();
        template.DetailJoins = new();
        template.DetailKeyColumns = new();
        FillColumnList();
        propertyGrid.Refresh();
    }

    /// <summary>JOIN 其他資料表：選資料表、關聯條件、要帶進來的欄位（跟單身 Grid 的 JOIN 一樣）</summary>
    private void AddJoin()
    {
        int n = 1;
        while (template.Joins.Any(j => j.Alias == $"J{n}")) n++;
        var existing = tableColumns.Select(c => c.Name).Concat(template.JoinColumns.Select(c => c.Column));
        using var dlg = new JoinDialog(connectionString, $"J{n}", tableColumns, existing);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        template.Joins.Add(dlg.Join);
        template.JoinColumns.AddRange(dlg.NewColumns);
        FillColumnList();
        UpdateStatus($"已加入 JOIN {dlg.Join.Alias}：{dlg.Join.TableName}（{dlg.NewColumns.Count} 個欄位）");
    }

    private void RemoveJoin(string alias)
    {
        var columns = template.JoinColumns.Where(c => c.JoinAlias == alias).Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int used = template.Items.Count(i => i.Kind == PrintItemKind.欄位 && columns.Contains(i.Column ?? ""));
        string note = used > 0 ? $"樣板上有 {used} 個元件用到這些欄位，移除後會印成空白。" : "";
        if (MessageBox.Show($"確定要移除 JOIN {alias}（{columns.Count} 個欄位）嗎？{note}", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        template.JoinColumns.RemoveAll(c => c.JoinAlias == alias);
        template.Joins.RemoveAll(j => j.Alias == alias);
        FillColumnList();
    }

    // ================= 元件 =================

    private void AddItem(PrintItemKind kind, Point location, string column = null, string header = null)
    {
        var item = new PrintItem { Kind = kind, X = location.X, Y = location.Y };
        switch (kind)
        {
            case PrintItemKind.欄位:
                item.Column = column;
                item.Text = string.IsNullOrWhiteSpace(header) ? null : header + "：";
                item.Width = 260;
                item.Height = 26;
                break;
            case PrintItemKind.文字:
                item.Text = "標題文字";
                item.FontSize = 16;
                item.Bold = true;
                item.Width = 260;
                item.Height = 36;
                break;
            case PrintItemKind.圖片:
                item.Width = 160;
                item.Height = 100;
                break;
            case PrintItemKind.線條:
                item.Width = 600;
                item.Height = 10;
                break;
            case PrintItemKind.方框:
                item.Width = 300;
                item.Height = 150;
                break;
            case PrintItemKind.明細表格:
                item.Width = Math.Max(200, template.PageSize.Width - 80 - location.X);
                item.Height = 300;
                item.TableColumns = template.DetailColumns
                    .Select(c => new PrintTableColumn { Column = c.Column, Header = c.Header, Width = Math.Clamp(c.Width, 60, 200) }).ToList();
                break;
        }
        template.Items.Add(item);
        var box = new PrintItemBox(item);
        AttachBox(box);
        SyncZOrder();
        SetSelection(new[] { box });
    }

    /// <summary>拖放到紙張或元件上：單身的欄位放進明細表格，其他照種類新增元件</summary>
    private void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PrintDragData)) is not PrintDragData data) return;
        Point p = canvas.PointToClient(new Point(e.X, e.Y));
        if (data.IsDetail)
        {
            var target = canvas.Controls.OfType<PrintItemBox>().LastOrDefault(b => b.Item.Kind == PrintItemKind.明細表格 && b.Bounds.Contains(p));
            AddDetailColumn(target, new Point(Snap(p.X), Snap(p.Y)), data.Column, data.Header);
            return;
        }
        AddItem(data.Kind, new Point(Snap(p.X), Snap(p.Y)), data.Column, data.Header);
    }

    /// <summary>單身的欄位加到明細表格：target 空白時用樣板上第一個明細表格，沒有就在 location 新增一個</summary>
    private void AddDetailColumn(PrintItemBox target, Point location, string column, string header)
    {
        target ??= canvas.Controls.OfType<PrintItemBox>().FirstOrDefault(b => b.Item.Kind == PrintItemKind.明細表格);
        if (target == null)
        {
            AddItem(PrintItemKind.明細表格, location);
            target = canvas.Controls.OfType<PrintItemBox>().First(b => b.Item.Kind == PrintItemKind.明細表格);
            target.Item.TableColumns.Clear();
        }
        if (target.Item.TableColumns.Any(c => string.Equals(c.Column, column, StringComparison.OrdinalIgnoreCase)))
        {
            UpdateStatus($"明細表格已經有「{column}」");
            return;
        }
        target.Item.TableColumns.Add(new PrintTableColumn { Column = column, Header = header });
        target.RefreshFromModel();
        SetSelection(new[] { target });
        UpdateStatus($"已把「{header ?? column}」加到明細表格");
    }

    /// <summary>明細表格要印哪些單身欄位、順序（勾選清單）</summary>
    private void ChooseTableColumns(PrintItemBox box)
    {
        if (template.DetailColumns.Count == 0)
        {
            MessageBox.Show("還沒有設定單身：請先按左下「設定單身」。", "明細表格");
            return;
        }
        using var dlg = new TableColumnsDialog(template.DetailColumns, box.Item.TableColumns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        box.Item.TableColumns = dlg.Columns;
        box.RefreshFromModel();
        SetSelection(new[] { box });
        MarkMissingColumns();
        UpdateStatus($"明細表格 {dlg.Columns.Count} 個欄位");
    }

    private void AttachBox(PrintItemBox box)
    {
        // 單身的欄位可以直接拖到明細表格上
        box.AllowDrop = true;
        box.DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(typeof(PrintDragData)) ? DragDropEffects.Copy : DragDropEffects.None;
        box.DragDrop += (s, e) => OnDrop(e);
        // 明細表格：雙擊或右鍵挑要印的單身欄位
        if (box.Item.Kind == PrintItemKind.明細表格)
        {
            box.DoubleClick += (s, e) => ChooseTableColumns(box);
            var menu = new ContextMenuStrip();
            menu.Items.Add("選擇表格欄位…", null, (s, e) => ChooseTableColumns(box));
            box.ContextMenuStrip = menu;
        }
        box.Pressed += (s, e) => OnBoxPressed(box);
        box.Dragging += (s, delta) => MoveSelection(box, delta);
        box.DragFinished += (s, e) => { dragStarts = null; propertyGrid.Refresh(); };
        box.Resized += (s, e) => propertyGrid.Refresh();
        canvas.Controls.Add(box);
        box.BringToFront();
    }

    /// <summary>設計畫面的上下層跟列印一樣：元件清單越後面的越上層</summary>
    private void SyncZOrder()
    {
        var boxes = canvas.Controls.OfType<PrintItemBox>().ToDictionary(b => b.Item);
        foreach (var item in template.Items)
            if (boxes.TryGetValue(item, out var box)) box.BringToFront();
    }

    /// <summary>選取的元件移到最上層 / 最下層（調整元件順序）</summary>
    private void ChangeLayer(bool toFront)
    {
        var items = selection.OfType<PrintItemBox>().Select(b => b.Item).ToList();
        if (items.Count == 0) return;
        template.Items.RemoveAll(items.Contains);
        if (toFront) template.Items.AddRange(items);
        else template.Items.InsertRange(0, items);
        SyncZOrder();
    }

    private Point NextFreePoint()
    {
        var boxes = canvas.Controls.OfType<PrintItemBox>().ToList();
        return boxes.Count == 0 ? new Point(40, 40) : new Point(40, boxes.Max(b => b.Bottom) + 10);
    }

    // ================= 複製 / 剪下 / 貼上 =================

    /// <summary>複製的元件（所有列印樣板設計器共用，可以從一個樣板貼到另一個）</summary>
    private static List<PrintItem> clipboard = new();
    /// <summary>同一份複製內容貼了幾次：每次多錯開一點，才不會疊在一起</summary>
    private int pasteCount;

    private void CopySelected()
    {
        // 依樣板上原本的順序（上下層）複製，貼上後誰蓋住誰不變
        var items = selection.OfType<PrintItemBox>().Select(b => b.Item).OrderBy(i => template.Items.IndexOf(i)).Select(i => i.Clone()).ToList();
        if (items.Count == 0) return;
        clipboard = items;
        pasteCount = 0;
        UpdateStatus($"已複製 {items.Count} 個元件（Ctrl+V 貼上）");
    }

    private void CutSelected()
    {
        CopySelected();
        RemoveSelected();
    }

    /// <summary>貼上：往右下錯開（連續貼上會繼續往下錯開），貼上的元件變成選取狀態，可以直接拖到想要的位置</summary>
    private void Paste()
    {
        if (clipboard.Count == 0) return;
        pasteCount++;
        int offset = 10 * pasteCount;
        var page = template.PageSize;
        var boxes = new List<DesignBox>();
        foreach (var source in clipboard)
        {
            var item = source.Clone();
            item.X = Math.Max(0, Math.Min(source.X + offset, page.Width - Math.Min(item.Width, page.Width)));
            item.Y = Math.Max(0, Math.Min(source.Y + offset, page.Height - Math.Min(item.Height, page.Height)));
            template.Items.Add(item);
            var box = new PrintItemBox(item);
            AttachBox(box);
            boxes.Add(box);
        }
        SyncZOrder();
        SetSelection(boxes);
        UpdateStatus($"已貼上 {boxes.Count} 個元件");
    }

    /// <summary>再製一份：複製 + 貼上</summary>
    private void Duplicate()
    {
        CopySelected();
        Paste();
    }

    private void RemoveSelected()
    {
        foreach (var box in selection.OfType<PrintItemBox>().ToList())
        {
            template.Items.Remove(box.Item);
            canvas.Controls.Remove(box);
            box.Dispose();
        }
        SetSelection(Array.Empty<DesignBox>());
    }

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

    /// <summary>選取的元件一起移動（每 5 像素一格），靠近其他元件時自動貼齊</summary>
    private void MoveSelection(DesignBox lead, Size delta)
    {
        if (dragStarts == null || dragStarts.Count == 0) return;
        int dx = (int)Math.Round(delta.Width / 5.0) * 5;
        int dy = (int)Math.Round(delta.Height / 5.0) * 5;
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
        // 沒選元件時，屬性面板顯示紙張與資料設定
        propertyGrid.SelectedObjects = selection.Count == 0 ? new object[] { template } : selection.Select(b => b.Model).ToArray();
        UpdateStatus();
    }

    private void ApplyPageSize()
    {
        canvas.Size = template.PageSize;
    }

    /// <summary>復原 / 取消復原：換成快照的樣板，重建紙張上的元件</summary>
    private void RestoreSnapshot(string json)
    {
        foreach (var box in canvas.Controls.OfType<PrintItemBox>().ToList()) box.Dispose();
        template = PrintTemplate.FromJson(json);
        foreach (var item in template.Items) AttachBox(new PrintItemBox(item));
        SyncZOrder();
        ApplyPageSize();
        FillColumnList();
        SetSelection(Array.Empty<DesignBox>());
    }

    // ================= 檔案 / 預覽 =================

    private void Save(bool saveAs)
    {
        if (saveAs || templatePath == null)
        {
            Directory.CreateDirectory(defaultFolder);
            using var dlg = new SaveFileDialog
            {
                Filter = "列印樣板 (*.print.json)|*.print.json",
                InitialDirectory = templatePath != null ? Path.GetDirectoryName(templatePath) : defaultFolder,
                FileName = templatePath != null ? Path.GetFileName(templatePath) : defaultName + ".print.json",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            templatePath = dlg.FileName;
        }
        try
        {
            template.Save(templatePath);
            SavedPath = templatePath;
            UpdateTitle();
            UpdateStatus("已儲存 " + Path.GetFileName(templatePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show("儲存失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>用主資料表的第一筆資料預覽列印（看排版用）</summary>
    private void PreviewSample()
    {
        if (MissingColumnsMessage() is { } message)
        {
            MessageBox.Show(message, "預覽列印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            var sheet = PrintRunner.LoadSample(template, connectionString);
            if (sheet == null)
            {
                MessageBox.Show("資料表裡沒有資料可以預覽。", "預覽列印");
                return;
            }
            using var doc = PrintRunner.CreateDocument(template, new[] { sheet }, Text);
            PrintRunner.ShowPreview(doc, this, Path.GetFileNameWithoutExtension(templatePath ?? defaultName));
        }
        catch (Exception ex)
        {
            MessageBox.Show("預覽失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateTitle() =>
        Text = $"列印樣板設計：{template.TableName}　{(templatePath == null ? "（尚未儲存）" : Path.GetFileName(templatePath))}";

    private void UpdateStatus(string message = null)
    {
        string info = $"紙張 {template.Paper}{(template.Landscape ? " 橫印" : "")}，{template.Items.Count} 個元件、{template.Joins.Count} 個 JOIN；"
                    + "點空白處可以設定紙張大小與方向";
        lblStatus.Text = message == null ? info : message + "　｜　" + info;
    }

    // ================= 給按鈕用：開啟設計器 =================

    /// <summary>
    /// 設計畫面雙擊「列印報表」按鈕時開啟；存過檔就把樣板檔寫回按鈕的「列印樣板檔」（跟畫面同資料夾時只存檔名）。
    /// 回傳 true = 按鈕的設定有改
    /// </summary>
    /// <param name="recordColumns">按鈕有設定列印來源清單時，那個清單的欄位（選取那筆提供的欄位）</param>
    public static bool OpenFor(ButtonDefinition b, IWin32Window owner, string formFolder, string connectionString,
                               string tableSchema, string tableName, IEnumerable<string> keyColumns, IEnumerable<ColumnInfo> recordColumns = null,
                               IEnumerable<EditListDefinition> editLists = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            MessageBox.Show("畫面還沒有資料表，請先從左邊拖欄位到畫面上。", "列印樣板");
            return false;
        }
        var keys = keyColumns.ToList();
        if (keys.Count == 0)
        {
            MessageBox.Show("這個資料表沒有設定主鍵，列印時不知道要印哪一筆。請先在欄位（或單頭清單的欄位設定）勾選主鍵。", "列印樣板");
            return false;
        }
        string folder = string.IsNullOrWhiteSpace(formFolder) ? FormDefinition.DefaultFolder : formFolder;
        string path = ButtonRunner.ResolvePath(b.PrintTemplate, folder);
        // 新樣板檔的預設檔名：資料表_按鈕文字（去掉檔名不能用的字元）
        string name = string.IsNullOrWhiteSpace(b.Text) ? tableName : $"{tableName}_{b.Text}";
        name = string.Concat(name.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
        using var dlg = new PrintDesignerForm(path, connectionString, tableSchema, tableName, keys, folder, name == "" ? "列印樣板" : name, recordColumns, editLists);
        dlg.ShowDialog(owner);
        if (dlg.SavedPath == null) return false;
        string saved = string.Equals(Path.GetDirectoryName(dlg.SavedPath), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(dlg.SavedPath) : dlg.SavedPath;
        if (saved == b.PrintTemplate) return false;
        b.PrintTemplate = saved;
        return true;
    }
}
