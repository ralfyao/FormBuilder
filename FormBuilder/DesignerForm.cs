namespace FormBuilder;

/// <summary>
/// 後台設計視窗：連線資料庫 → 選資料表 → 把欄位拖到畫面上 → 存成畫面定義檔（JSON）給使用者使用
/// </summary>
public partial class DesignerForm : Form
{
    private FormDefinition def = new();
    private List<ColumnInfo> defTableColumns = new();      // 畫面所屬資料表的全部欄位（用來提醒漏放的必填欄位）
    private string currentFilePath;
    /// <summary>目前選取的元件（欄位或單身 Grid；可多選：滑鼠圈選，或按住 Ctrl 點選）</summary>
    private readonly List<DesignBox> selection = new();
    /// <summary>拖曳移動時，每個被選元件按下滑鼠當下的位置；null = 沒有在移動</summary>
    private Dictionary<DesignBox, Point> dragStarts;

    // 左邊樹狀清單目前選到的資料表
    private string browsingSchema;
    private string browsingTable;
    private List<ColumnInfo> browsingColumns = new();
    /// <summary>已讀過的資料表欄位，key = schema.table，避免每次展開都重查資料庫</summary>
    private readonly Dictionary<string, List<ColumnInfo>> columnCache = new();
    private const string LoadingNodeText = "載入中…";

    private string connectionString;                       // 由「資料庫連線設定」視窗組成
    private List<(string Schema, string Name)> allTables = new();

    public DesignerForm()
    {
        InitializeComponent();

        // ---- 工具列（按鈕在 Designer.cs；對齊按鈕由 AlignHelper 加在後面）----
        btnNewForm.Click += (s, e) => NewForm();
        btnOpen.Click += (s, e) => OpenForm();
        btnSave.Click += (s, e) => SaveForm(false);
        btnSaveAs.Click += (s, e) => SaveForm(true);
        btnRemove.Click += (s, e) => RemoveSelectedField();
        btnAddAll.Click += (s, e) => AddAllColumns();
        btnGridColumns.Click += (s, e) => EditGridColumns(canvas.Controls.OfType<GridBox>().FirstOrDefault());
        btnPreview.Click += (s, e) => Preview();
        AlignHelper.AddAlignButtons(toolStrip, () => selection.ToList(), () => { propertyGrid.Refresh(); ApplyCanvasSize(); });

        // ---- 連線列 ----
        btnConnect.Click += (s, e) => ChooseConnection();
        btnReconnect.Click += (s, e) => Connect();

        // ---- 左上「工具箱」頁籤 ----
        toolbox.ItemActivated += kind => AddToolboxItem(kind, NextFreePoint());

        // 分隔線位置要等視窗有實際大小後才能設定
        Load += (s, e) =>
        {
            mainSplit.SplitterDistance = 360;
            rightSplit.SplitterDistance = Math.Max(200, rightSplit.Width - 300);
            leftSplit.SplitterDistance = (int)(leftSplit.Height * 0.6);
        };

        // ---- 事件 ----
        txtTableFilter.TextChanged += (s, e) => FillTableList();
        treeTables.BeforeExpand += (s, e) => LoadColumnNodes(e.Node);
        treeTables.AfterSelect += (s, e) => OnTreeSelect(e.Node);
        treeTables.ItemDrag += (s, e) =>
        {
            if (e.Item is TreeNode { Tag: ColumnDragData data })
                DoDragDrop(data, DragDropEffects.Copy);
            else if (e.Item is TreeNode { Tag: ValueTuple<string, string> t })
                DoDragDrop(new TableDragData { Schema = t.Item1, Table = t.Item2 }, DragDropEffects.Copy);
        };
        treeTables.NodeMouseDoubleClick += (s, e) =>
        {
            if (e.Node.Tag is ColumnDragData data)
                AddField(data, NextFreePoint());
        };
        canvas.DragEnter += (s, e) =>
            e.Effect = e.Data.GetDataPresent(typeof(ColumnDragData)) || e.Data.GetDataPresent(typeof(TableDragData))
                    || e.Data.GetDataPresent(typeof(ToolboxDragData))
                ? DragDropEffects.Copy : DragDropEffects.None;
        canvas.DragDrop += (s, e) =>
        {
            Point p = canvas.PointToClient(new Point(e.X, e.Y));
            var location = new Point(Snap(p.X), Snap(p.Y));
            if (e.Data.GetData(typeof(ColumnDragData)) is ColumnDragData column)
                AddField(column, location);
            else if (e.Data.GetData(typeof(TableDragData)) is TableDragData table)
                AddGrid(table, location);
            else if (e.Data.GetData(typeof(ToolboxDragData)) is ToolboxDragData tool)
                AddToolboxItem(tool.Kind, location);
        };
        // 畫面空白處：抓邊緣調整畫面大小（拖出外框會自動捲動延伸），其他地方拉框圈選欄位
        canvasResizer = new CanvasResizer(canvas, canvasHost, () => new Size(def.Width, def.Height), size =>
        {
            Size min = MinCanvasSize();
            def.Width = Math.Max(min.Width, size.Width);
            def.Height = Math.Max(min.Height, size.Height);
            ApplyCanvasSize();
        });
        canvas.MouseDown += (s, e) => OnCanvasMouseDown(e);
        canvas.MouseMove += (s, e) => OnCanvasMouseMove(e);
        canvas.MouseUp += (s, e) => OnCanvasMouseUp();
        canvas.Paint += (s, e) =>                          // 右下角畫出可拖曳的格紋
            ControlPaint.DrawSizeGrip(e.Graphics, canvas.BackColor, canvas.ClientSize.Width - 16, canvas.ClientSize.Height - 16, 16, 16);
        propertyGrid.PropertyValueChanged += (s, e) =>
        {
            foreach (var box in selection) box.RefreshFromModel();     // 多選時屬性面板會一次改全部
            ApplyCanvasSize();
            UpdateStatus();
        };

        SetConnection(null);
        NewForm();

        // Ctrl+Z 復原、Ctrl+Y 取消復原：記錄整個畫面定義的快照
        history = new UndoHistory(this, () => def.ToJson(), RestoreSnapshot);
    }

    private UndoHistory history;

    /// <summary>
    /// Delete：刪除選取的欄位；Ctrl+A：全選畫面上的欄位；Ctrl+Z：復原；Ctrl+Y（或 Ctrl+Shift+Z）：取消復原。
    /// 正在篩選框或屬性面板打字時不攔截，讓按鍵照常作用在文字上
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = txtTableFilter.Focused || propertyGrid.ContainsFocus || columnGrid.ContainsFocus;
        if (!UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.Z))
        {
            UpdateStatus(history.Undo() ? "已復原上一個動作（Ctrl+Y 取消復原）" : "沒有可以復原的動作");
            return true;
        }
        if (!UndoHistory.IsEditingText() && (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)))
        {
            UpdateStatus(history.Redo() ? "已取消復原（重做）" : "沒有可以重做的動作");
            return true;
        }
        if (!typing && keyData == Keys.Delete && selection.Count > 0)
        {
            RemoveSelectedField();
            return true;
        }
        if (!typing && keyData == (Keys.Control | Keys.A))
        {
            SetSelection(canvas.Controls.OfType<DesignBox>());
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static int Snap(int v) => Math.Max(0, (int)Math.Round(v / (double)DesignBox.Grid) * DesignBox.Grid);

    // ================= 連線與資料表清單 =================

    /// <summary>開啟連線設定視窗，輸入伺服器、資料庫、帳號、密碼後連線</summary>
    private void ChooseConnection()
    {
        using var dlg = new ConnectionDialog(connectionString);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        SetConnection(dlg.ConnectionString);
        Connect();
    }

    private void SetConnection(string value)
    {
        connectionString = value;
        lblConn.Text = ConnectionDialog.Mask(value);      // 畫面上不顯示密碼
    }

    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ChooseConnection();
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            allTables = SchemaReader.GetTables(connectionString);
            columnCache.Clear();                               // 可能換了資料庫，欄位要重讀
            columnGrid.SelectedObject = null;
            def.ConnectionString = connectionString;
            FillTableList();
            ReloadDefTableColumns();
            UpdateStatus($"已連線，共 {allTables.Count} 個資料表");
        }
        catch (Exception ex)
        {
            MessageBox.Show("連線失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>
    /// 樹狀清單：第一層是資料表，展開後才去資料庫讀欄位（資料表很多，一次全讀太慢）
    /// </summary>
    private void FillTableList()
    {
        string keyword = txtTableFilter.Text.Trim();
        treeTables.BeginUpdate();
        treeTables.Nodes.Clear();
        foreach (var t in allTables.Where(t => keyword == "" || t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            var tableNode = new TreeNode($"{t.Schema}.{t.Name}") { Tag = t };
            tableNode.Nodes.Add(LoadingNodeText);      // 先放一個假節點，才會出現可展開的「+」
            treeTables.Nodes.Add(tableNode);
        }
        treeTables.EndUpdate();
    }

    /// <summary>展開資料表時，把假節點換成實際的欄位節點</summary>
    private void LoadColumnNodes(TreeNode tableNode)
    {
        if (tableNode.Tag is not ValueTuple<string, string> t) return;
        if (tableNode.Nodes.Count != 1 || tableNode.Nodes[0].Text != LoadingNodeText) return;
        var columns = GetColumnsCached(t.Item1, t.Item2);
        tableNode.Nodes.Clear();
        if (columns == null) return;
        foreach (var c in columns)
        {
            string type = c.MaxLength > 0 ? $"{c.DataType}({c.MaxLength})" : c.MaxLength == -1 ? $"{c.DataType}(max)" : c.DataType;
            string text = $"{c.Name}  {type}"
                        + (c.IsPrimaryKey ? "  [鍵]" : "")
                        + (c.IsRequiredOnInsert ? "  [必填]" : "")
                        + (string.IsNullOrWhiteSpace(c.Description) ? "" : $"  {c.Description}");
            tableNode.Nodes.Add(new TreeNode(text)
            {
                Tag = new ColumnDragData { Schema = t.Item1, Table = t.Item2, Column = c },
                ForeColor = c.IsPrimaryKey ? Color.DarkBlue : treeTables.ForeColor,
            });
        }
    }

    /// <summary>點選資料表：記住它（給「加入全部欄位」用）；點選欄位：下方顯示該欄位屬性</summary>
    private void OnTreeSelect(TreeNode node)
    {
        switch (node.Tag)
        {
            case ValueTuple<string, string> t:
                SetBrowsingTable(t.Item1, t.Item2);
                columnGrid.SelectedObject = null;
                break;
            case ColumnDragData data:
                SetBrowsingTable(data.Schema, data.Table);
                columnGrid.SelectedObject = data.Column;
                break;
        }
    }

    private void SetBrowsingTable(string schema, string table)
    {
        browsingSchema = schema;
        browsingTable = table;
        browsingColumns = GetColumnsCached(schema, table) ?? new List<ColumnInfo>();
    }

    /// <summary>讀取資料表欄位（讀過的直接用快取）；失敗時顯示訊息並回傳 null</summary>
    private List<ColumnInfo> GetColumnsCached(string schema, string table)
    {
        string key = $"{schema}.{table}";
        if (columnCache.TryGetValue(key, out var cached)) return cached;
        try
        {
            Cursor = Cursors.WaitCursor;
            var columns = SchemaReader.GetColumns(connectionString, schema, table);
            columnCache[key] = columns;
            return columns;
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取欄位失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>畫面已綁定資料表時，重讀該資料表全部欄位，用來檢查漏放的必填欄位</summary>
    private void ReloadDefTableColumns()
    {
        defTableColumns = new List<ColumnInfo>();
        if (def.TableName == null || string.IsNullOrWhiteSpace(def.ConnectionString)) return;
        try
        {
            defTableColumns = SchemaReader.GetColumns(def.ConnectionString, def.TableSchema, def.TableName);
        }
        catch
        {
            // 連不上時只是少了提醒，不影響設計
        }
    }

    // ================= 欄位放置 =================

    private void AddField(ColumnDragData data, Point location)
    {
        if (data?.Column == null) return;
        if (def.TableName == null)
        {
            // 第一個放上來的欄位決定這個畫面的單頭資料表
            def.TableSchema = data.Schema;
            def.TableName = data.Table;
            def.ConnectionString = connectionString;
            if (def.Title == "新畫面") def.Title = data.Table;
            defTableColumns = (GetColumnsCached(data.Schema, data.Table) ?? new List<ColumnInfo>()).ToList();
        }
        else if (def.TableSchema != data.Schema || def.TableName != data.Table)
        {
            MessageBox.Show($"單頭欄位只能來自同一個資料表，這個畫面的單頭是 {def.TableSchema}.{def.TableName}。\n要放其他資料表當單身，請把整個資料表（不是欄位）拖到畫面上。", "提醒");
            return;
        }

        var existing = def.Fields.FirstOrDefault(f => f.Column == data.Column.Name);
        if (existing != null)
        {
            SelectBox(canvas.Controls.OfType<FieldBox>().First(b => b.Field == existing));
            UpdateStatus($"欄位 {existing.Column} 已經在畫面上了");
            return;
        }

        var c = data.Column;
        var field = new FieldDefinition
        {
            Column = c.Name,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            IsNullable = c.IsNullable,
            IsIdentity = c.IsIdentity,
            IsKey = c.IsPrimaryKey,
            NoEntry = c.IsIdentity,                                  // 自動編號由資料庫產生，預設只顯示
            Label = string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description,
            X = location.X,
            Y = location.Y,
            Width = TypeHelper.SuggestWidth(c),
        };
        def.Fields.Add(field);
        SelectBox(AddBox(field));
        ApplyCanvasSize();
        UpdateStatus();
    }

    private void AddAllColumns()
    {
        if (browsingTable == null)
        {
            MessageBox.Show("請先在左邊選一個資料表。", "提醒");
            return;
        }
        foreach (var c in browsingColumns.Where(c => def.Fields.All(f => f.Column != c.Name)))
            AddField(new ColumnDragData { Schema = browsingSchema, Table = browsingTable, Column = c }, NextFreePoint());
    }

    /// <summary>雙擊或「加入全部欄位」時，自動排在最後一個欄位下方</summary>
    private Point NextFreePoint()
    {
        if (def.Fields.Count == 0) return new Point(20, 20);
        var last = def.Fields.OrderBy(f => f.Y).Last();
        return new Point(20, last.Y + 30);
    }

    private FieldBox AddBox(FieldDefinition field) => (FieldBox)AttachBox(new FieldBox(field));

    private GridBox AddGridBox(GridDefinition grid)
    {
        var box = (GridBox)AttachBox(new GridBox(grid));
        // 點一下就開欄位設定；按住 Ctrl 點是多選，不開
        box.Clicked += (s, e) =>
        {
            if (!ModifierKeys.HasFlag(Keys.Control)) EditGridColumns(box);
        };
        return box;
    }

    private DesignBox AttachBox(DesignBox box)
    {
        box.Pressed += (s, e) => OnBoxPressed((DesignBox)s);
        box.Dragging += (s, delta) => MoveSelection((DesignBox)s, delta);
        box.DragFinished += (s, e) => { dragStarts = null; propertyGrid.Refresh(); ApplyCanvasSize(); };
        box.Resized += (s, e) => { propertyGrid.Refresh(); ApplyCanvasSize(); };
        canvas.Controls.Add(box);
        return box;
    }

    // ================= 工具箱 =================

    private void AddToolboxItem(string kind, Point location)
    {
        if (kind == "Button") AddButton(location);
        else if (kind == "ComboBox") AddComboField(location);
    }

    /// <summary>加入按鈕；文字、動作在右邊屬性面板設定</summary>
    private void AddButton(Point location)
    {
        var button = new ButtonDefinition { X = location.X, Y = location.Y };
        def.Buttons.Add(button);
        SelectBox(AttachBox(new ButtonBox(button)));
        ApplyCanvasSize();
        UpdateStatus("已加入按鈕：在右邊屬性面板設定「文字」和「動作」");
    }

    /// <summary>加入下拉選單：先選要對應單頭的哪個欄位，再把那個欄位設成下拉選單</summary>
    private void AddComboField(Point location)
    {
        if (def.TableName == null)
        {
            MessageBox.Show("請先把單頭資料表的欄位拖到畫面上（決定單頭），再加入下拉選單。", "提醒");
            return;
        }
        var columns = (defTableColumns.Count > 0 ? defTableColumns : GetColumnsCached(def.TableSchema, def.TableName) ?? new List<ColumnInfo>())
            .Where(c => def.Fields.All(f => !f.Column.Equals(c.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (columns.Count == 0)
        {
            MessageBox.Show("單頭資料表的欄位都已經放在畫面上了。要改成下拉選單，請點選該欄位，在屬性面板把「輸入方式」改成「下拉選單」。", "提醒");
            return;
        }
        using var dlg = new ColumnPickDialog($"下拉選單：{def.TableName}", columns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        AddField(new ColumnDragData { Schema = def.TableSchema, Table = def.TableName, Column = dlg.SelectedColumn }, location);
        var field = def.Fields.LastOrDefault(f => f.Column == dlg.SelectedColumn.Name);
        if (field == null) return;
        field.InputType = InputKind.下拉選單;
        canvas.Controls.OfType<FieldBox>().First(b => b.Field == field).RefreshFromModel();
        propertyGrid.Refresh();
        UpdateStatus("已加入下拉選單：在右邊屬性面板設定「固定選項」或「選項 SQL」");
    }

    // ================= 單身 Grid =================

    /// <summary>
    /// 把整個資料表拖到畫面上產生 Grid，預設放入全部欄位：
    /// 拖的是單頭自己的資料表 → 單頭清單（列出查詢結果，點選切換單頭）；
    /// 拖的是其他資料表 → 單身，並自動推測關聯欄位
    /// </summary>
    private void AddGrid(TableDragData data, Point location)
    {
        if (def.TableName == null)
        {
            MessageBox.Show("請先把單頭資料表的欄位拖到畫面上（決定單頭），再把資料表整個拖過來產生 Grid。", "提醒");
            return;
        }
        bool isMasterList = def.TableSchema == data.Schema && def.TableName == data.Table;
        if (def.Grids.Any(g => g.IsMasterList == isMasterList))
        {
            MessageBox.Show(isMasterList
                ? "畫面上已經有單頭清單 Grid 了。"
                : "一個畫面目前只能放一個單身 Grid，要換資料表請先移除現有的單身。", "提醒");
            return;
        }
        var gridColumns = GetColumnsCached(data.Schema, data.Table);
        if (gridColumns == null) return;
        var masterColumns = defTableColumns.Count > 0 ? defTableColumns : GetColumnsCached(def.TableSchema, def.TableName) ?? new List<ColumnInfo>();

        var grid = new GridDefinition
        {
            IsMasterList = isMasterList,
            TableSchema = data.Schema,
            TableName = data.Table,
            Title = data.Table,
            LinkText = isMasterList ? "" : GuessLinks(masterColumns, gridColumns),
            X = location.X,
            Y = location.Y,
            Width = Math.Max(400, def.Width - location.X - 20),
            Height = 200,
            Columns = gridColumns.Select(c => new GridColumnDefinition
            {
                Column = c.Name,
                DataType = c.DataType,
                MaxLength = c.MaxLength,
                IsNullable = c.IsNullable,
                IsIdentity = c.IsIdentity,
                IsKey = c.IsPrimaryKey,
                Header = string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description,
                Width = Math.Clamp(TypeHelper.SuggestWidth(c), 60, 200),
                NoEntry = c.IsIdentity,
            }).ToList(),
        };
        def.Grids.Add(grid);
        SelectBox(AddGridBox(grid));
        ApplyCanvasSize();
        UpdateStatus(isMasterList ? "已加入單頭清單 Grid：使用者查詢後會列出結果，點選一列切換單頭"
                   : grid.Links.Count == 0 ? "推測不出關聯欄位，請點一下 Grid 設定"
                   : $"已自動帶入關聯欄位（請確認是否正確）：{grid.LinkText}");
    }

    /// <summary>
    /// 推測單頭、單身的關聯欄位（只用單頭主鍵；單頭沒有主鍵時用全部欄位）：
    /// 1. 同名欄位，例如 單號 = 單號；
    /// 2. 找不到同名時，用鼎新的命名規則：兩碼表代號 + 三碼流水號，例如 SFCTA.TA001 = SFCTB.TB001
    /// </summary>
    private static string GuessLinks(List<ColumnInfo> masterColumns, List<ColumnInfo> detailColumns)
    {
        var masterKeys = masterColumns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        var candidates = masterKeys.Count > 0 ? masterKeys : masterColumns.Select(c => c.Name).ToList();

        var sameName = candidates.Where(n => detailColumns.Any(d => d.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                                 .Select(n => $"{n}={detailColumns.First(d => d.Name.Equals(n, StringComparison.OrdinalIgnoreCase)).Name}")
                                 .ToList();
        if (sameName.Count > 0) return string.Join(", ", sameName);

        static bool IsErpName(string n) => n.Length == 5 && char.IsLetter(n[0]) && char.IsLetter(n[1]) && n[2..].All(char.IsDigit);
        var erpLinks = new List<string>();
        foreach (var m in candidates.Where(IsErpName))
        {
            var d = detailColumns.FirstOrDefault(c => IsErpName(c.Name) && c.Name[2..] == m[2..]
                                                    && !c.Name[..2].Equals(m[..2], StringComparison.OrdinalIgnoreCase));
            if (d != null) erpLinks.Add($"{m}={d.Name}");
        }
        return string.Join(", ", erpLinks);
    }

    /// <summary>開啟單身欄位設定：勾選要顯示的欄位、改標題寬度、設定關聯欄位</summary>
    private void EditGridColumns(GridBox box)
    {
        if (box == null)
        {
            MessageBox.Show("畫面上還沒有單身 Grid。把左邊整個資料表拖到畫面上就會產生。", "提醒");
            return;
        }
        var masterColumns = def.Fields.Select(f => f.Column)
            .Concat(defTableColumns.Select(c => c.Name)).Distinct().ToList();
        using var dlg = new GridColumnsDialog(box.GridDef, masterColumns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        box.RefreshFromModel();
        propertyGrid.Refresh();
        UpdateStatus();
    }

    /// <summary>
    /// 在欄位上按下滑鼠：按住 Ctrl 是加選 / 取消選取；
    /// 沒按 Ctrl 時，點到未選取的欄位就改成只選它，點到已選取的欄位則保留多選，準備一起移動
    /// </summary>
    private void OnBoxPressed(DesignBox box)
    {
        if (ModifierKeys.HasFlag(Keys.Control))
        {
            SetSelection(selection.Contains(box) ? selection.Where(b => b != box).ToList() : selection.Append(box).ToList());
            dragStarts = null;
            return;
        }
        if (!selection.Contains(box)) SelectBox(box);
        dragStarts = selection.ToDictionary(b => b, b => b.ModelLocation);
    }

    /// <summary>所有選取的欄位一起移動；位移對齊格線，且最左、最上的欄位不會被拖出畫面</summary>
    private void MoveSelection(DesignBox lead, Size delta)
    {
        if (dragStarts == null || dragStarts.Count == 0) return;
        int dx = (int)Math.Round(delta.Width / (double)DesignBox.Grid) * DesignBox.Grid;
        int dy = (int)Math.Round(delta.Height / (double)DesignBox.Grid) * DesignBox.Grid;
        // 靠近其他元件時自動貼齊（以滑鼠拖的那個元件為準）
        if (dragStarts.TryGetValue(lead, out var leadStart))
            (dx, dy) = AlignHelper.Magnetize(lead, leadStart, dx, dy,
                canvas.Controls.OfType<DesignBox>().Where(b => !dragStarts.ContainsKey(b) && b is not GridBox { IsDocked: true }));
        dx = Math.Max(dx, -dragStarts.Values.Min(p => p.X));
        dy = Math.Max(dy, -dragStarts.Values.Min(p => p.Y));
        foreach (var (box, start) in dragStarts)
            box.SetModelLocation(start.X + dx, start.Y + dy);
    }

    /// <summary>只選取這一個欄位；null = 全部取消選取</summary>
    private void SelectBox(DesignBox box) => SetSelection(box == null ? Array.Empty<DesignBox>() : new[] { box });

    private void SetSelection(IEnumerable<DesignBox> boxes)
    {
        var list = boxes.Distinct().ToList();
        foreach (var b in selection) b.IsSelected = false;
        selection.Clear();
        selection.AddRange(list);
        foreach (var b in selection) b.IsSelected = true;

        // 屬性面板：沒選欄位顯示畫面本身；選一個顯示它；選多個時只列共同屬性，改了會套用到全部
        if (selection.Count == 0) propertyGrid.SelectedObject = def;
        else if (selection.Count == 1) propertyGrid.SelectedObject = selection[0].Model;
        else propertyGrid.SelectedObjects = selection.Select(b => b.Model).ToArray();

        if (selection.Count > 0)
            ActiveControl = null;      // 讓焦點離開篩選框等輸入框，Delete 鍵才會刪欄位而不是刪文字
        if (selection.Count > 1) UpdateStatus($"已選取 {selection.Count} 個欄位");
    }

    private void RemoveSelectedField()
    {
        if (selection.Count == 0)
        {
            MessageBox.Show("請先點選畫面上要移除的欄位。", "提醒");
            return;
        }
        foreach (var box in selection.ToList())
        {
            if (box is FieldBox fb) def.Fields.Remove(fb.Field);
            if (box is GridBox gb) def.Grids.Remove(gb.GridDef);
            if (box is ButtonBox bb) def.Buttons.Remove(bb.ButtonDef);
            canvas.Controls.Remove(box);
            box.Dispose();
        }
        selection.Clear();
        SelectBox(null);
        if (def.Fields.Count == 0 && def.Grids.Count == 0)
        {
            // 單頭欄位、單身都移除了，畫面可以改綁別的資料表
            def.TableSchema = null;
            def.TableName = null;
            defTableColumns = new List<ColumnInfo>();
        }
        UpdateStatus();
    }

    /// <summary>畫面大小至少要容納所有欄位</summary>
    private void ApplyCanvasSize()
    {
        Size min = MinCanvasSize();
        def.Width = Math.Max(def.Width, min.Width);
        def.Height = Math.Max(def.Height, min.Height);
        canvas.Size = new Size(def.Width, def.Height);
        LayoutDockedGrids();
        canvas.Invalidate();                               // 重畫右下角的拖曳格紋
    }

    /// <summary>設計時停靠 Grid 的下邊、右邊各留一點空間，畫面的邊緣才抓得到（不然會被 Grid 蓋住，沒辦法拖曳延伸）</summary>
    private const int DockGap = 8;

    /// <summary>停靠在下方的 Grid：寬度 = 畫面寬度，由下往上排（先放的在最下面，跟執行時一樣）</summary>
    private void LayoutDockedGrids()
    {
        int bottom = canvas.ClientSize.Height - DockGap;
        foreach (var box in DockedGridBoxes())
        {
            bottom -= box.GridDef.Height;
            box.GridDef.X = 0;
            box.GridDef.Y = bottom;
            box.GridDef.Width = canvas.ClientSize.Width - DockGap;
            box.RefreshFromModel();
        }
    }

    private IEnumerable<GridBox> DockedGridBoxes() =>
        canvas.Controls.OfType<GridBox>().Where(b => b.IsDocked).OrderBy(b => def.Grids.IndexOf(b.GridDef));

    /// <summary>
    /// 畫面最小大小：要容納所有沒停靠的元件，高度再加上下方停靠 Grid 的高度；不小於 200 × 120。
    /// 停靠的 Grid 位置跟著畫面大小走，不能拿來算最小值，不然畫面會越長越大
    /// </summary>
    private Size MinCanvasSize()
    {
        int w = 200, h = 0;
        foreach (var box in canvas.Controls.OfType<DesignBox>().Where(b => b is not GridBox { IsDocked: true }))
        {
            w = Math.Max(w, box.Right + 20);
            h = Math.Max(h, box.Bottom + 20);
        }
        var docked = DockedGridBoxes().ToList();
        if (docked.Count > 0) h += docked.Sum(b => b.GridDef.Height) + DockGap;
        return new Size(w, Math.Max(h, 120));
    }

    // ================= 拖曳畫面的右邊、下邊、右下角調整大小 =================

    private readonly CanvasResizer canvasResizer;

    // 圈選：從按下的位置拉出選取框，放開時選取框內（含碰到框）的欄位
    private SelectionBand band;
    private bool bandAdditive;                             // 按住 Ctrl 圈選 = 加選，不清掉原本的選取

    private void OnCanvasMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (canvasResizer.TryStart(e)) return;             // 抓到邊緣 = 調整畫面大小
        bandAdditive = ModifierKeys.HasFlag(Keys.Control);
        if (!bandAdditive) SelectBox(null);                // 點空白處：屬性面板改顯示畫面本身
        band ??= new SelectionBand(canvas);
        band.Begin(e.Location);
    }

    private void OnCanvasMouseMove(MouseEventArgs e)
    {
        if (canvasResizer.Active)
        {
            canvasResizer.Move(e);
            return;
        }
        if (band is { Active: true })
        {
            band.Move(e.Location);
            return;
        }
        canvasResizer.Move(e);                             // 沒在拖曳時：滑鼠移到邊緣換成調整大小的游標
    }

    private void OnCanvasMouseUp()
    {
        if (canvasResizer.End())
        {
            propertyGrid.Refresh();                        // 屬性面板的寬度、高度跟著更新
            return;
        }
        if (band is not { Active: true }) return;
        Rectangle rect = band.End();
        if (rect.IsEmpty) return;                          // 只是點一下，沒有拉框
        var hit = canvas.Controls.OfType<DesignBox>().Where(b => b.Bounds.IntersectsWith(rect));
        SetSelection(bandAdditive ? selection.Concat(hit) : hit);
    }

    private void UpdateStatus(string message = null)
    {
        var notes = new List<string>();
        if (message != null) notes.Add(message);
        if (def.TableName != null)
        {
            notes.Add($"資料表：{def.TableSchema}.{def.TableName}");
            var missing = defTableColumns.Where(c => c.IsRequiredOnInsert && def.Fields.All(f => f.Column != c.Name)).Select(c => c.Name).ToList();
            if (missing.Count > 0) notes.Add("注意：這些必填欄位沒放到畫面上，新增資料會失敗：" + string.Join(", ", missing));
            if (def.MasterKeys.Count == 0) notes.Add("注意：單頭欄位與單頭清單都沒有主鍵，使用者只能新增，不能修改、刪除");
            foreach (var g in def.Grids.Where(g => !g.IsMasterList))
            {
                if (g.Links.Count == 0) notes.Add($"注意：單身 {g.TableName} 沒有設定關聯欄位");
                if (g.KeyColumns.Count == 0) notes.Add($"注意：單身 {g.TableName} 沒有主鍵，只能新增明細，不能修改、刪除");
            }
        }
        lblStatus.Text = string.Join("　｜　", notes);
    }

    // ================= 檔案 =================

    private void NewForm()
    {
        def = new FormDefinition { ConnectionString = connectionString };
        defTableColumns = new List<ColumnInfo>();
        currentFilePath = null;
        ClearCanvas();
        SelectBox(null);
        ApplyCanvasSize();
        UpdateStatus();
        history?.Reset();
    }

    private void ClearCanvas()
    {
        foreach (var box in canvas.Controls.OfType<DesignBox>().ToList()) box.Dispose();
        canvas.Controls.Clear();
    }

    /// <summary>把畫面上的元件依定義重新建立（開啟檔案、復原 / 取消復原）</summary>
    private void BuildCanvas()
    {
        foreach (var f in def.Fields) AddBox(f);
        foreach (var g in def.Grids) AddGridBox(g);
        foreach (var b in def.Buttons) AttachBox(new ButtonBox(b));
        SelectBox(null);
        ApplyCanvasSize();
    }

    /// <summary>復原 / 取消復原：把畫面定義換成快照的內容，重建畫面上的元件</summary>
    private void RestoreSnapshot(string json)
    {
        var restored = FormDefinition.FromJson(json);
        restored.SourcePath = def.SourcePath;
        bool tableChanged = !string.Equals(restored.TableName, def.TableName, StringComparison.OrdinalIgnoreCase)
                         || !string.Equals(restored.TableSchema, def.TableSchema, StringComparison.OrdinalIgnoreCase);
        ClearCanvas();
        def = restored;
        BuildCanvas();
        if (tableChanged) ReloadDefTableColumns();
    }

    private void OpenForm()
    {
        using var dlg = new OpenFileDialog { Filter = "畫面定義 (*.json)|*.json", InitialDirectory = EnsureFolder() };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            NewForm();
            def = FormDefinition.Load(dlg.FileName);
            currentFilePath = dlg.FileName;
            if (!string.IsNullOrWhiteSpace(def.ConnectionString)) SetConnection(def.ConnectionString);
            BuildCanvas();
            ReloadDefTableColumns();
            history.Reset();
            UpdateStatus("已開啟 " + Path.GetFileName(currentFilePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveForm(bool saveAs)
    {
        if (def.Fields.Count == 0)
        {
            MessageBox.Show("畫面上還沒有欄位。", "提醒");
            return;
        }
        if (saveAs || currentFilePath == null)
        {
            using var dlg = new SaveFileDialog { Filter = "畫面定義 (*.json)|*.json", InitialDirectory = EnsureFolder(), FileName = def.Title + ".json" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            currentFilePath = dlg.FileName;
        }
        try
        {
            def.Save(currentFilePath);
            UpdateStatus("已存檔 " + Path.GetFileName(currentFilePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show("存檔失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EnsureFolder()
    {
        Directory.CreateDirectory(FormDefinition.DefaultFolder);
        return FormDefinition.DefaultFolder;
    }

    private void Preview()
    {
        if (def.Fields.Count == 0)
        {
            MessageBox.Show("畫面上還沒有欄位。", "提醒");
            return;
        }
        new RuntimeForm(def).Show(this);
    }
}
