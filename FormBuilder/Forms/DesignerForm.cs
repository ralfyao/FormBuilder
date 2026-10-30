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
    private DbKind databaseType;                           // 連線的資料庫類型（SQL Server / Oracle / MySQL）
    private List<(string Schema, string Name)> allTables = new();

    public DesignerForm()
    {
        InitializeComponent();
        HookListSqlEditor();
        Activated += (s, e) => HookListSqlEditor();

        // ---- 工具列（按鈕在 Designer.cs；對齊按鈕由 AlignHelper 加在後面）----
        btnNewForm.Click += (s, e) => NewForm();
        btnOpen.Click += (s, e) => OpenForm();
        btnSave.Click += (s, e) => SaveForm(false);
        btnSaveAs.Click += (s, e) => SaveForm(true);
        // 「檔案」選單：發布到系統資料庫（登入後才有）
        var miPublish = new ToolStripMenuItem("發布到系統…") { Enabled = SysContext.Active, ToolTipText = "把這個畫面（和它用到的列印樣板）發布到系統資料庫，使用者才看得到" };
        var miOpenPublished = new ToolStripMenuItem("開啟已發布的畫面…") { Enabled = SysContext.Active };
        if (!SysContext.Active) miPublish.ToolTipText = miOpenPublished.ToolTipText = "從 ERP Studio 主選單（系統管理 → 畫面設計）開啟時才能用";
        miPublish.Click += (s, e) => Publish();
        miOpenPublished.Click += (s, e) => OpenPublished();
        btnFileMenu.DropDownItems.Add(new ToolStripSeparator());
        btnFileMenu.DropDownItems.Add(miOpenPublished);
        btnFileMenu.DropDownItems.Add(miPublish);
        btnRemove.Click += (s, e) => RemoveSelectedField();
        btnAddAll.Click += (s, e) => AddAllColumns();
        btnGridColumns.Click += (s, e) => EditGridColumns(canvas.Controls.OfType<GridBox>().FirstOrDefault());
        btnPreview.Click += (s, e) => Preview();
        btnPreviewDebug.Click += (s, e) => Preview(debug: true);
        // 「編輯」選單：移除 / 加入欄位之後，接著是水平、垂直、大小的對齊子選單
        AlignHelper.AddAlignMenu(btnEditMenu.DropDownItems, () => selection.ToList(), () => { propertyGrid.Refresh(); ApplyCanvasSize(); });
        toolStrip.Items.Add(new ToolStripLabel("拖曳時靠近其他欄位會自動貼齊（按住 Alt 不貼齊）") { ForeColor = SystemColors.GrayText });

        // 畫面底色：編輯選單，或在畫面空白處按右鍵（也可以在屬性面板的「背景色」改）
        var backColorMenu = new ToolStripMenuItem("畫面背景色");
        backColorMenu.DropDownItems.Add("選擇顏色…", null, (s, e) => PickBackColor());
        backColorMenu.DropDownItems.Add("還原預設（灰色）", null, (s, e) => SetCanvasBackColor(Color.Empty));
        btnEditMenu.DropDownItems.Add(new ToolStripSeparator());
        btnEditMenu.DropDownItems.Add(backColorMenu);
        // 單頭 JOIN：把其他資料表的欄位拖到畫面上時設定；之後要改關聯條件用這裡
        btnEditMenu.DropDownItems.Add("單頭 JOIN 關聯條件…", null, (s, e) => EditMasterJoin());
        var canvasMenu = new ContextMenuStrip();
        canvasMenu.Items.Add("畫面背景色：選擇顏色…", null, (s, e) => PickBackColor());
        canvasMenu.Items.Add("畫面背景色：還原預設（灰色）", null, (s, e) => SetCanvasBackColor(Color.Empty));
        canvas.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right) canvasMenu.Show(canvas, e.Location);
        };

        // ---- 連線列 ----
        btnConnect.Click += (s, e) => ChooseConnection();
        btnReconnect.Click += (s, e) => Connect();

        // ---- 左上「工具箱」頁籤 ----
        toolbox.AddEntry("SqlList", "SQL 清單（唯讀明細）");
        toolbox.AddEntry("Calc", "計算欄位（公式）");
        toolbox.AddEntry("Label", "文字標籤 (Label)");
        FormulaEditor.Fields = () => def.Fields;                // 屬性面板「公式」的編輯視窗列出畫面上的欄位
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
            // 變數名稱不能跟畫面上其他欄位（或單頭資料表的欄位）同名
            if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(FieldDefinition.VariableName)
                && selection.FirstOrDefault()?.Model is FieldDefinition renamed
                && (def.Fields.Any(f => f != renamed && f.Column.Equals(renamed.Column, StringComparison.OrdinalIgnoreCase))
                    || MasterTableColumns().Any(c => c.Name.Equals(renamed.Column, StringComparison.OrdinalIgnoreCase))))
            {
                MessageBox.Show($"「{renamed.Column}」已經有其他欄位在用了，請換一個變數名稱。", "提醒");
                renamed.Column = e.OldValue as string ?? renamed.Column;
                propertyGrid.Refresh();
            }
            // 清單 SQL 改了：SQL 多出來的欄位加到清單欄位（只顯示），拿掉的跟著移除
            if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(GridDefinition.ListSql) && selection.FirstOrDefault()?.Model is GridDefinition listGrid)
                SyncListSqlColumns(listGrid);
            foreach (var box in selection) box.RefreshFromModel();     // 多選時屬性面板會一次改全部
            ApplyCanvasSize();
            UpdateStatus(e.ChangedItem?.PropertyDescriptor?.Name == nameof(FieldDefinition.Formula) ? "公式已更新" : null);
        };

        SetConnection(null, DbKind.SqlServer);
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
        if (!typing && !UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.C) && selection.Count > 0)
        {
            CopySelection();
            return true;
        }
        if (!typing && !UndoHistory.IsEditingText() && keyData == (Keys.Control | Keys.V) && !DesignClipboard.IsEmpty)
        {
            PasteClipboard();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 單頭清單的清單 SQL =================

    /// <summary>屬性面板「清單 SQL」的「…」用：這個畫面設計提供自動組的 SQL、執行 SQL 取欄位（有好幾個畫面設計開著時用目前作用中的）</summary>
    private void HookListSqlEditor()
    {
        ListSqlEditor.Generate = GeneratedListSql;
        ListSqlEditor.ReadColumns = sql => string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("還沒有連線資料庫。") : SqlList.ReadColumns(connectionString, sql);
    }

    /// <summary>
    /// 單頭清單目前自動組的 SELECT（跟執行時一樣的欄位、JOIN），和畫面要用到的欄位名稱（清單 SQL 要 SELECT 出來）
    /// </summary>
    private (string Sql, IReadOnlyCollection<string> Required) GeneratedListSql(GridDefinition g)
    {
        var d = def.Dialect;
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new HashSet<string>();
        void Add(string name, string alias, string source)
        {
            if (string.IsNullOrEmpty(alias)) columns.TryAdd(name, GridSql.Base(d, name));
            else if (def.Joins.Any(j => j.Alias == alias) && columns.TryAdd(name, $"{GridSql.Joined(d, alias, source)} AS {d.Quote(name)}")) aliases.Add(alias);
        }
        foreach (var f in def.Fields.Where(f => !f.IsCalculated)) Add(f.Column, f.JoinAlias, f.SourceColumn);
        foreach (var master in def.Grids.Where(x => !x.IsMasterList && !x.IsSqlList).SelectMany(x => x.Links).Select(l => l.Master)) Add(master, null, null);
        foreach (var c in g.Columns.Where(c => !c.ScreenOnly && !c.FromListSql)) Add(c.Column, c.JoinAlias, c.SourceColumn);
        string from = GridSql.From(d, def.FullTableName, def.Joins, aliases).Replace(" LEFT JOIN ", "\r\n  LEFT JOIN ");
        return ("SELECT " + string.Join(",\r\n       ", columns.Values) + "\r\nFROM " + from, columns.Keys.ToList());
    }

    /// <summary>清單 SQL 多出來的欄位加到清單欄位最後面（只顯示，預設顯示）；SQL 拿掉的、改回自動組時移除</summary>
    private void SyncListSqlColumns(GridDefinition g)
    {
        List<GridColumnDefinition> sqlColumns = new();
        if (!string.IsNullOrWhiteSpace(g.ListSql) && !string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                sqlColumns = SqlList.ReadColumns(connectionString, g.ListSql);
            }
            catch (Exception ex)
            {
                MessageBox.Show("清單 SQL 執行失敗（清單欄位沒有更新）：" + ex.Message, "清單 SQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        var names = sqlColumns.Select(c => c.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        g.Columns.RemoveAll(c => c.FromListSql && !names.Contains(c.Column));
        foreach (var c in sqlColumns.Where(c => !g.Columns.Any(x => x.Column.Equals(c.Column, StringComparison.OrdinalIgnoreCase))))
        {
            c.Visible = true;
            c.NoEntry = true;
            c.FromListSql = true;
            g.Columns.Add(c);
        }
    }

    // ================= 複製 / 貼上 =================

    /// <summary>
    /// Ctrl+C：複製選取的按鈕、文字標籤、計算欄位、SQL 清單；
    /// 資料表的欄位、資料表的 Grid 一個欄位只能放一次，不複製
    /// </summary>
    private void CopySelection()
    {
        var fields = selection.OfType<FieldBox>().Select(b => b.Field).Where(f => f.IsCalculated).ToList();     // 文字標籤也是 IsCalculated
        var buttons = selection.OfType<ButtonBox>().Select(b => b.ButtonDef).ToList();
        var grids = selection.OfType<GridBox>().Select(b => b.GridDef).Where(g => g.IsSqlList).ToList();
        int copied = fields.Count + buttons.Count + grids.Count;
        if (copied == 0)
        {
            UpdateStatus("選取的是資料表的欄位或 Grid：同一個欄位只能放一次，不能複製（按鈕、文字標籤、計算欄位、SQL 清單可以複製）");
            return;
        }
        DesignClipboard.Copy(fields, buttons, grids, Enumerable.Empty<EditListDefinition>());
        UpdateStatus($"已複製 {copied} 個元件，按 Ctrl+V 貼上" + (copied < selection.Count ? $"（{selection.Count - copied} 個資料表欄位 / Grid 不能複製，略過）" : ""));
    }

    /// <summary>Ctrl+V：貼上複製的元件（新的一份，位置往右下錯開）；文字標籤、計算欄位取新的名稱</summary>
    private void PasteClipboard()
    {
        var (fields, buttons, grids, _) = DesignClipboard.Paste();
        if (fields.Count + buttons.Count + grids.Count == 0)
        {
            UpdateStatus("複製的元件不能貼在這裡（編輯視窗的 SQL 清單只能貼在編輯視窗版面）");
            return;
        }
        var used = new HashSet<string>(UsedFieldNames(), StringComparer.OrdinalIgnoreCase);
        var boxes = new List<DesignBox>();
        foreach (var f in fields)
        {
            f.Column = DesignClipboard.UniqueName(f.IsTextLabel ? "標籤" : "計算", used);
            def.Fields.Add(f);
            boxes.Add(AddBox(f));
        }
        foreach (var b in buttons)
        {
            def.Buttons.Add(b);
            boxes.Add(AttachBox(new ButtonBox(b)));
        }
        foreach (var g in grids)
        {
            def.Grids.Add(g);
            boxes.Add(AddGridBox(g));
        }
        SetSelection(boxes);
        ApplyCanvasSize();
        UpdateStatus($"已貼上 {boxes.Count} 個元件（已選取，可以直接拖曳到要放的位置）");
    }

    private static int Snap(int v) => Math.Max(0, (int)Math.Round(v / (double)DesignBox.Grid) * DesignBox.Grid);

    // ================= 連線與資料表清單 =================

    /// <summary>開啟連線設定視窗，輸入伺服器、資料庫、帳號、密碼後連線</summary>
    private void ChooseConnection()
    {
        using var dlg = new ConnectionDialog(connectionString, databaseType);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        SetConnection(dlg.ConnectionString, dlg.DatabaseType);
        Connect();
    }

    private void SetConnection(string value, DbKind kind)
    {
        connectionString = value;
        databaseType = kind;
        DbDialect.Register(value, kind);
        lblConn.Text = ConnectionDialog.Describe(value);  // 畫面上不顯示資料庫路徑（伺服器、資料庫、帳號）
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
            def.DatabaseType = databaseType;
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
            SetMasterTable(data.Schema, data.Table);      // 第一個放上來的欄位決定這個畫面的單頭資料表
        else if (def.TableSchema != data.Schema || def.TableName != data.Table)
        {
            // 其他資料表的欄位：用 JOIN 帶到單頭（只顯示）；要放其他資料表當單身，是把整個資料表拖到畫面上
            AddJoinField(data, location);
            return;
        }

        var existing = def.Fields.FirstOrDefault(f => f.Column == data.Column.Name);
        if (existing != null)
        {
            AddQueryCopy(existing, location);
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

    /// <summary>
    /// 已經在畫面上的欄位再放一個：複本只當查詢條件（查詢時可以輸入，新增、修改不能輸入、不存檔），
    /// 例如條件畫面放好幾個「產品編號」輸入不同關鍵字
    /// </summary>
    private void AddQueryCopy(FieldDefinition existing, Point location)
    {
        var copy = existing.Clone();
        copy.QueryOnly = true;
        copy.IsKey = false;                                        // 主鍵、公式、編號、預設值都只屬於原本那個欄位
        copy.Formula = null;
        copy.NumberRule = null;
        copy.DefaultValue = null;
        copy.X = location.X;
        copy.Y = location.Y;
        def.Fields.Add(copy);
        SelectBox(AddBox(copy));
        ApplyCanvasSize();
        UpdateStatus($"已再放一個「{existing.Label}」（{existing.Column}）：只當查詢條件，新增、修改時不能輸入、不存檔");
    }

    /// <summary>決定這個畫面的單頭資料表（第一個放上來的欄位，或空白畫面上第一個拖進來的整個資料表）</summary>
    private void SetMasterTable(string schema, string table)
    {
        def.TableSchema = schema;
        def.TableName = table;
        def.DatabaseType = databaseType;
        def.ConnectionString = connectionString;
        if (def.Title == "新畫面") def.Title = table;
        defTableColumns = (GetColumnsCached(schema, table) ?? new List<ColumnInfo>()).ToList();
    }

    // ================= 單頭 JOIN =================

    /// <summary>
    /// 把其他資料表的欄位放到單頭：用 LEFT JOIN 帶出來，只顯示、不寫回資料庫（例如用廠商編號帶出廠商名稱）。
    /// 同一個資料表已經 JOIN 過就沿用原本的關聯條件；還沒有就先設定關聯條件（單頭欄位 = 關聯資料表欄位）
    /// </summary>
    private void AddJoinField(ColumnDragData data, Point location)
    {
        var join = def.Joins.FirstOrDefault(j => string.Equals(j.TableSchema, data.Schema, StringComparison.OrdinalIgnoreCase)
                                              && string.Equals(j.TableName, data.Table, StringComparison.OrdinalIgnoreCase));
        if (join != null)
        {
            var existing = def.Fields.FirstOrDefault(f => f.JoinAlias == join.Alias && string.Equals(f.SourceColumn, data.Column.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                AddQueryCopy(existing, location);
                return;
            }
            var c = data.Column;
            AddJoinFieldBox(join, UniqueFieldName(join.TableName, c.Name), string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description, c, location);
            UpdateStatus($"已加入 {join.TableName}.{c.Name}（沿用 JOIN {join.Alias} 的關聯條件：{join.LinkText}）");
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("請先連線資料庫，再把其他資料表的欄位 JOIN 到單頭。", "提醒");
            return;
        }
        int n = 1;
        while (def.Joins.Any(j => j.Alias == $"J{n}")) n++;
        using var dlg = new JoinDialog(connectionString, $"J{n}", MasterTableColumns(), UsedFieldNames());
        dlg.ForMaster(data.Schema, data.Table, data.Column.Name);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        def.Joins.Add(dlg.Join);
        AddJoinFieldBoxes(dlg.Join, dlg.NewColumns, location);
        UpdateStatus($"已 JOIN {dlg.Join.TableName}（{dlg.Join.LinkText}），帶進來的欄位只顯示、不會存檔。要改關聯條件：選欄位後按「編輯 → 單頭 JOIN 關聯條件」");
    }

    /// <summary>「編輯 → 單頭 JOIN 關聯條件」：修改選取的 JOIN 欄位所屬 JOIN 的關聯條件，也可以再勾選要放到單頭的欄位</summary>
    private void EditMasterJoin()
    {
        var field = selection.OfType<FieldBox>().Select(b => b.Field).FirstOrDefault(f => f.IsJoin);
        var join = field == null ? null : def.Joins.FirstOrDefault(j => j.Alias == field.JoinAlias);
        if (join == null)
        {
            MessageBox.Show("請先點選畫面上 JOIN 帶進來的欄位（標示 [JOIN] 的欄位），再修改它的關聯條件。", "提醒");
            return;
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("請先連線資料庫。", "提醒");
            return;
        }
        using var dlg = new JoinDialog(connectionString, join.Alias, MasterTableColumns(), UsedFieldNames());
        dlg.ForMaster(join.TableSchema, join.TableName, null, join.LinkText, def.Fields.Where(f => f.JoinAlias == join.Alias).Select(f => f.SourceColumn));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        join.LinkText = dlg.Join.LinkText;
        AddJoinFieldBoxes(join, dlg.NewColumns, NextFreePoint());
        UpdateStatus($"JOIN {join.Alias}（{join.TableName}）的關聯條件：{join.LinkText}");
    }

    /// <summary>JOIN 設定視窗勾選的欄位放到單頭：第一個放在 location，其餘往下排</summary>
    private void AddJoinFieldBoxes(GridJoinDefinition join, List<GridColumnDefinition> columns, Point location)
    {
        var remote = GetColumnsCached(join.TableSchema, join.TableName) ?? new List<ColumnInfo>();
        foreach (var (c, i) in columns.Select((c, i) => (c, i)))
        {
            var info = remote.FirstOrDefault(r => r.Name == c.SourceColumn) ?? new ColumnInfo { Name = c.SourceColumn, DataType = c.DataType, MaxLength = c.MaxLength };
            AddJoinFieldBox(join, c.Column, c.Header, info, new Point(location.X, location.Y + i * 30));
        }
    }

    /// <summary>放一個 JOIN 帶進來的欄位到單頭（只顯示），畫面有單頭清單時也加到清單上</summary>
    private void AddJoinFieldBox(GridJoinDefinition join, string name, string label, ColumnInfo c, Point location)
    {
        var field = new FieldDefinition
        {
            Column = name,
            DataType = c.DataType,
            MaxLength = c.MaxLength,
            IsNullable = true,                                       // LEFT JOIN 對不到時是空白
            NoEntry = true,                                          // 關聯資料表的欄位只顯示，不寫回資料庫
            JoinAlias = join.Alias,
            SourceColumn = c.Name,
            Label = label,
            X = location.X,
            Y = location.Y,
            Width = TypeHelper.SuggestWidth(c),
        };
        def.Fields.Add(field);
        var list = def.Grids.FirstOrDefault(g => g.IsMasterList && !g.IsSqlList);
        if (list != null && list.Columns.All(x => !x.Column.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            var column = ToListColumn(field);
            if (list.HasEditLayout) column.EditVisible = false;         // 編輯視窗版面設計過了，不要疊在左上角
            list.Columns.Add(column);
            canvas.Controls.OfType<GridBox>().FirstOrDefault(b => b.GridDef == list)?.RefreshFromModel();
        }
        SelectBox(AddBox(field));
        ApplyCanvasSize();
    }

    /// <summary>單頭 JOIN 欄位放到單頭清單上的欄位（同樣只顯示）</summary>
    private static GridColumnDefinition ToListColumn(FieldDefinition f) => new()
    {
        Column = f.Column,
        DataType = f.DataType,
        MaxLength = f.MaxLength,
        IsNullable = true,
        Header = f.Label,
        Width = Math.Clamp(f.Width, 60, 200),
        NoEntry = true,
        JoinAlias = f.JoinAlias,
        SourceColumn = f.SourceColumn,
    };

    /// <summary>單頭資料表的全部欄位（JOIN 關聯條件左邊的選項）</summary>
    private List<ColumnInfo> MasterTableColumns() =>
        defTableColumns.Count > 0 ? defTableColumns : GetColumnsCached(def.TableSchema, def.TableName) ?? new List<ColumnInfo>();

    /// <summary>單頭已經用掉的欄位名稱（畫面欄位、單頭資料表欄位、單頭清單欄位），JOIN 帶進來的欄位不能重複</summary>
    private List<string> UsedFieldNames() =>
        def.Fields.Select(f => f.Column).Concat(MasterTableColumns().Select(c => c.Name))
            .Concat(def.Grids.Where(g => g.IsMasterList && !g.IsSqlList).SelectMany(g => g.Columns).Select(c => c.Column))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>JOIN 欄位顯示用的名稱：重複時加上資料表名稱，還是重複就加編號（同 JoinDialog）</summary>
    private string UniqueFieldName(string table, string source)
    {
        var used = new HashSet<string>(UsedFieldNames(), StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(source)) return source;
        string name = $"{table}_{source}";
        for (int n = 2; used.Contains(name); n++) name = $"{table}_{source}{n}";
        return name;
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
        if (box is FieldBox) box.RefreshFromModel();       // 加進畫面後才知道底色，標籤文字顏色要重算
        if (box is ButtonBox bb) bb.DoubleClick += (s, e) => OpenPrintDesigner(bb);
        return box;
    }

    /// <summary>雙擊「列印報表」按鈕：開啟列印樣板設計（資料表、主鍵用這個畫面的）</summary>
    private void OpenPrintDesigner(ButtonBox box)
    {
        if (box.ButtonDef.Action != ButtonAction.列印報表) return;
        string folder = currentFilePath != null ? Path.GetDirectoryName(currentFilePath) : FormDefinition.DefaultFolder;
        if (PrintDesignerForm.OpenFor(box.ButtonDef, this, folder, connectionString, def.TableSchema, def.TableName,
                                      def.MasterKeys.Select(k => k.Column)))
            propertyGrid.Refresh();
    }

    // ================= 工具箱 =================

    private void AddToolboxItem(string kind, Point location)
    {
        if (kind == "Button") AddButton(location);
        else if (kind == "ComboBox") AddKindField(location, InputKind.下拉選單);
        else if (kind == "DatePicker") AddKindField(location, InputKind.日期選擇);
        else if (kind == "CheckBox") AddKindField(location, InputKind.勾選框);
        else if (kind == "RadioButton") AddKindField(location, InputKind.選項按鈕);
        else if (kind == "SqlList") AddSqlList(location);
        else if (kind == "Calc") AddCalcField(location);
        else if (kind == "Label") AddTextLabel(location);
    }

    /// <summary>加入計算欄位：不對應資料表的欄位，依公式（畫面欄位、數字做加減乘除）算出來顯示，不會存檔</summary>
    private void AddCalcField(Point location)
    {
        if (def.TableName == null)
        {
            MessageBox.Show("請先把單頭資料表的欄位拖到畫面上（決定單頭），再加入計算欄位。", "提醒");
            return;
        }
        using var dlg = new FormulaDialog(null, def.Fields);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int n = 1;
        var used = new HashSet<string>(UsedFieldNames(), StringComparer.OrdinalIgnoreCase);
        while (used.Contains($"計算{n}")) n++;
        var field = new FieldDefinition
        {
            Column = $"計算{n}",
            DataType = "decimal",
            IsNullable = true,
            NoEntry = true,                                          // 只顯示，不寫回資料庫
            IsCalculated = true,
            Formula = dlg.Formula == "" ? null : dlg.Formula,
            Label = "計算欄位",
            X = location.X,
            Y = location.Y,
            Width = 120,
        };
        def.Fields.Add(field);
        SelectBox(AddBox(field));
        ApplyCanvasSize();
        UpdateStatus("已加入計算欄位：在屬性面板改「標籤」；要改公式按「公式」右邊的「…」");
    }

    /// <summary>
    /// 加入文字標籤：只顯示文字（屬性面板的「標籤」），不對應資料表；有「變數名稱」，
    /// 下拉選單、按鈕的 SQL「回傳對應」寫「變數名稱=SQL 欄位」就能把查出來的值顯示在這裡
    /// </summary>
    private void AddTextLabel(Point location)
    {
        int n = 1;
        var used = new HashSet<string>(UsedFieldNames(), StringComparer.OrdinalIgnoreCase);
        while (used.Contains($"標籤{n}")) n++;
        var field = new FieldDefinition
        {
            Column = $"標籤{n}",
            DataType = "nvarchar",
            IsNullable = true,
            NoEntry = true,                                          // 只顯示，不寫回資料庫
            IsCalculated = true,                                     // 不是資料表的欄位：不查詢、不存檔、不當條件
            InputType = InputKind.文字標籤,
            Label = "文字",
            LabelWidth = 0,
            X = location.X,
            Y = location.Y,
            Width = 120,
        };
        def.Fields.Add(field);
        SelectBox(AddBox(field));
        ApplyCanvasSize();
        UpdateStatus($"已加入文字標籤「{field.Column}」：在屬性面板改「標籤」（顯示的文字）與「變數名稱」");
    }

    /// <summary>加入 SQL 清單：資料來自自己寫的 SELECT（唯讀），可以用 @單頭欄位，切換單頭時重查</summary>
    private void AddSqlList(Point location)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("請先連線資料庫，再加入 SQL 清單。", "提醒");
            return;
        }
        var grid = new GridDefinition
        {
            Title = "SQL 清單",
            X = location.X,
            Y = location.Y,
            Width = Math.Max(400, def.Width - location.X - 20),
            Height = 200,
        };
        using var dlg = new SqlListDialog(grid, connectionString, MasterColumnNames());
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        def.Grids.Add(grid);
        SelectBox(AddGridBox(grid));
        ApplyCanvasSize();
        UpdateStatus("已加入 SQL 清單：點一下可以修改 SQL 與欄位");
    }

    /// <summary>單頭的欄位名稱（畫面上的欄位 + 單頭資料表的欄位）</summary>
    private List<string> MasterColumnNames() =>
        def.Fields.Select(f => f.Column).Concat(defTableColumns.Select(c => c.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>工具箱元件的名稱、加入後的提示、輸入框預設寬度（加入下拉選單、日期選擇、勾選框、選項按鈕共用）</summary>
    internal static (string Name, string Tip, int MinWidth) KindInfo(InputKind kind) => kind switch
    {
        InputKind.日期選擇 => ("日期選擇", "文字、數字欄位存日期時，在屬性面板設定「日期格式」（沒填 = yyyyMMdd）", 150),
        InputKind.勾選框 => ("勾選框", "非 bit 欄位請在屬性面板設定「勾選時的值」「取消時的值」", 30),
        InputKind.選項按鈕 => ("選項按鈕", "在屬性面板設定「固定選項」或「選項 SQL」；選項多時請加大輸入框寬度或高度", 240),
        _ => ("下拉選單", "在屬性面板設定「固定選項」或「選項 SQL」", 0),
    };

    /// <summary>加入按鈕；文字、動作在右邊屬性面板設定</summary>
    private void AddButton(Point location)
    {
        var button = new ButtonDefinition { X = location.X, Y = location.Y };
        def.Buttons.Add(button);
        SelectBox(AttachBox(new ButtonBox(button)));
        ApplyCanvasSize();
        UpdateStatus("已加入按鈕：在右邊屬性面板設定「文字」和「動作」");
    }

    /// <summary>加入下拉選單、日期選擇、勾選框、選項按鈕：先選要對應單頭的哪個欄位，再把那個欄位設成對應的輸入方式</summary>
    private void AddKindField(Point location, InputKind kind)
    {
        var (name, tip, minWidth) = KindInfo(kind);
        if (def.TableName == null)
        {
            MessageBox.Show($"請先把單頭資料表的欄位拖到畫面上（決定單頭），再加入{name}。", "提醒");
            return;
        }
        var columns = (defTableColumns.Count > 0 ? defTableColumns : GetColumnsCached(def.TableSchema, def.TableName) ?? new List<ColumnInfo>())
            .Where(c => def.Fields.All(f => !f.Column.Equals(c.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (columns.Count == 0)
        {
            MessageBox.Show($"單頭資料表的欄位都已經放在畫面上了。要改成{name}，請點選該欄位，在屬性面板把「輸入方式」改成「{name}」。", "提醒");
            return;
        }
        using var dlg = new ColumnPickDialog($"{name}：{def.TableName}", columns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        AddField(new ColumnDragData { Schema = def.TableSchema, Table = def.TableName, Column = dlg.SelectedColumn }, location);
        var field = def.Fields.LastOrDefault(f => f.Column == dlg.SelectedColumn.Name);
        if (field == null) return;
        field.InputType = kind;
        field.Width = Math.Max(field.Width, minWidth);
        canvas.Controls.OfType<FieldBox>().First(b => b.Field == field).RefreshFromModel();
        propertyGrid.Refresh();
        UpdateStatus($"已加入{name}：{tip}");
    }

    // ================= 單身 Grid =================

    /// <summary>
    /// 把整個資料表拖到畫面上產生 Grid，預設放入全部欄位：
    /// 拖的是單頭自己的資料表 → 單頭清單（列出查詢結果，點選切換單頭）；
    /// 拖的是其他資料表 → 單身（可以放好幾個），並自動推測關聯欄位；
    /// 還沒有單頭時（空白畫面）→ 這個資料表就當單頭，產生單頭清單（之後可以再拖欄位上來當查詢條件）
    /// </summary>
    private void AddGrid(TableDragData data, Point location)
    {
        if (def.TableName == null)
        {
            if (GetColumnsCached(data.Schema, data.Table) == null) return;
            SetMasterTable(data.Schema, data.Table);
        }
        bool isMasterList = def.TableSchema == data.Schema && def.TableName == data.Table;
        if (isMasterList && def.Grids.Any(g => !g.IsSqlList && g.IsMasterList))
        {
            MessageBox.Show("畫面上已經有單頭清單 Grid 了。", "提醒");
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
        // 單頭清單也列出單頭 JOIN 帶進來的欄位
        if (isMasterList) grid.Columns.AddRange(def.Fields.Where(f => f.IsJoin).Select(ToListColumn));
        def.Grids.Add(grid);
        SelectBox(AddGridBox(grid));
        ApplyCanvasSize();
        UpdateStatus(isMasterList ? "已加入單頭清單 Grid：使用者查詢後會列出結果（畫面上沒有單頭欄位時，打開就列出資料），雙擊一列開啟編輯視窗"
                   : grid.Links.Count == 0 ? "推測不出關聯欄位，請點一下 Grid 設定"
                   : $"已自動帶入關聯欄位（請確認是否正確）：{grid.LinkText}");
    }

    /// <summary>
    /// 推測單頭、單身的關聯欄位（只用單頭主鍵；單頭沒有主鍵時用全部欄位）：
    /// 1. 同名欄位，例如 單號 = 單號；
    /// 2. 找不到同名時，用鼎新的命名規則：兩碼表代號 + 三碼流水號，例如 SFCTA.TA001 = SFCTB.TB001
    /// </summary>
    internal static string GuessLinks(List<ColumnInfo> masterColumns, List<ColumnInfo> detailColumns)
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
        if (box.GridDef.IsSqlList)
        {
            using var sqlDialog = new SqlListDialog(box.GridDef, connectionString, MasterColumnNames(), allowLayout: true, screenFont: def.ScreenFont(Font));
            if (sqlDialog.ShowDialog(this) != DialogResult.OK) return;
            box.RefreshFromModel();
            propertyGrid.Refresh();
            UpdateStatus();
            return;
        }
        var masterColumns = def.Fields.Select(f => f.Column)
            .Concat(defTableColumns.Select(c => c.Name)).Distinct().ToList();
        // 版面設計器的「檔案 → 儲存」直接存這個畫面定義檔；畫面字型給版面設計器預覽用
        // 單頭清單的 JOIN 加在單頭主查詢上（畫面的單頭 JOIN），帶進來的欄位名稱不能跟單頭欄位重複
        // 不用 ShowDialog：開著的時候還可以切到其他畫面的設計視窗對照；同一個 Grid 只開一個
        if (gridDialogs.TryGetValue(box.GridDef, out var open))
        {
            if (open.WindowState == FormWindowState.Minimized) open.WindowState = FormWindowState.Normal;
            open.Activate();
            return;
        }
        var dlg = new GridColumnsDialog(box.GridDef, masterColumns, connectionString, saveForm: () => SaveForm(false), screenFont: def.ScreenFont(Font),
                                        masterList: box.GridDef.IsMasterList ? (def, UsedFieldNames()) : null);
        gridDialogs[box.GridDef] = dlg;
        dlg.FormClosed += (s, e) =>
        {
            gridDialogs.Remove(box.GridDef);
            if (dlg.DialogResult != DialogResult.OK || IsDisposed) return;
            box.RefreshFromModel();
            propertyGrid.Refresh();
            UpdateStatus();
        };
        dlg.Show(this);
    }

    /// <summary>開著的單身欄位設定視窗（key = Grid）</summary>
    private readonly Dictionary<GridDefinition, GridColumnsDialog> gridDialogs = new();

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

        // 欄位屬性的「所屬資料表」：選到的是資料表欄位才有值（計算欄位等沒有，屬性面板就不顯示）
        foreach (var f in selection.Select(b => b.Model).OfType<FieldDefinition>())
            f.OwnerTable = def.TableOf(f);

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
            def.Joins.Clear();
            defTableColumns = new List<ColumnInfo>();
        }
        UpdateStatus();
    }

    /// <summary>畫面大小至少要容納所有欄位</summary>
    /// <summary>用顏色挑選器改畫面底色</summary>
    private void PickBackColor()
    {
        using var dlg = new ColorDialog { Color = ColorText.BackOrDefault(def.BackColor), FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK) SetCanvasBackColor(dlg.Color);
    }

    private void SetCanvasBackColor(Color color)
    {
        def.BackColor = color;
        ApplyCanvasSize();
        propertyGrid.Refresh();                            // 屬性面板的「背景色」跟著更新
    }

    private void ApplyCanvasSize()
    {
        Size min = MinCanvasSize();
        def.Width = Math.Max(def.Width, min.Width);
        def.Height = Math.Max(def.Height, min.Height);
        canvas.Size = new Size(def.Width, def.Height);
        Color back = ColorText.BackOrDefault(def.BackColor);
        Font font = def.ScreenFont(Font);
        if (canvas.BackColor != back || !canvas.Font.Equals(font))
        {
            canvas.BackColor = back;
            canvas.Font = font;                            // 畫面字型：欄位、Grid、按鈕示意都跟著
            foreach (var box in canvas.Controls.OfType<FieldBox>()) box.RefreshFromModel();   // 標籤顏色、高度跟著底色與字型
        }
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
            foreach (var f in def.Fields.Where(f => f.HasFormula))
            {
                string error = Formula.Check(f.Formula, def.Fields.Where(x => x != f).Select(x => x.Column));
                if (error != null) notes.Add($"注意：「{f.Label}」的{error}");
            }
            foreach (var g in def.Grids.Where(g => !g.IsMasterList && !g.IsSqlList))
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
        def = new FormDefinition { DatabaseType = databaseType, ConnectionString = connectionString };
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
        LoadFile(dlg.FileName);
    }

    /// <summary>
    /// 主選單「系統管理 → 畫面設計」：直接打開選單上選的畫面。
    /// 優先讀本機上次編輯的 json（Forms 資料夾裡同名的檔案）；本機沒有就讀系統上已發布的版本；
    /// 兩邊都有、系統上的比較新（例如別人發布過）時，先問要用哪一個
    /// </summary>
    public void OpenForEditing(string formKey)
    {
        string path = Path.Combine(EnsureFolder(), formKey + ".json");
        var published = SysContext.Active
            ? SysDb.LoadPublished(SysContext.Connection, "form").FirstOrDefault(p => p.FormKey.Equals(formKey, StringComparison.OrdinalIgnoreCase))
            : null;
        if (!File.Exists(path))
        {
            if (published != null) LoadPublishedKey(formKey);
            else MessageBox.Show($"找不到畫面「{formKey}」：本機沒有 {path}，系統上也還沒發布。", "畫面設計");
            return;
        }
        if (published != null && published.PublishedAt > File.GetLastWriteTime(path).AddMinutes(1))
        {
            var answer = MessageBox.Show(
                $"系統上的「{formKey}」（第 {published.Version} 版，{published.PublishedBy} {published.PublishedAt:yyyy/MM/dd HH:mm} 發布）比本機的檔案新"
                + $"（{File.GetLastWriteTime(path):yyyy/MM/dd HH:mm} 存檔）。{Environment.NewLine}{Environment.NewLine}"
                + "要用系統上的版本嗎？（選「否」用本機的檔案）", "畫面設計", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes)
            {
                LoadPublishedKey(formKey);
                return;
            }
        }
        LoadFile(path);
    }

    /// <summary>開啟本機的畫面定義檔</summary>
    private void LoadFile(string path)
    {
        try
        {
            NewForm();
            def = FormDefinition.Load(path);
            currentFilePath = path;
            if (!string.IsNullOrWhiteSpace(def.ConnectionString)) SetConnection(def.ConnectionString, def.DatabaseType);
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

    /// <summary>
    /// 發布到系統：先存檔，再把畫面定義（代號 = 檔名）和按鈕用到的列印樣板寫進系統資料庫。
    /// 「開啟畫面」按鈕指向的畫面（包含那些畫面再開啟的畫面）在本機有畫面定義檔的，問過後一併發布
    /// </summary>
    private void Publish()
    {
        if (!SysContext.Active) return;
        SaveForm(false);
        if (currentFilePath == null || !File.Exists(currentFilePath)) return;
        string key = Path.GetFileNameWithoutExtension(currentFilePath);
        try
        {
            var cs = SysContext.Connection;
            var notes = new List<string>();
            // 這個畫面底下會開到的其他畫面（一層一層找下去，不重複）
            var related = RelatedForms(def, currentFilePath, notes);
            bool withRelated = related.Count > 0 &&
                MessageBox.Show($"這個畫面的按鈕會開啟下列畫面，要一併發布嗎？{Environment.NewLine}{Environment.NewLine}"
                                + string.Join(Environment.NewLine, related.Select(r => $"　{r.Def.Title ?? r.Key}（{r.Key}）"))
                                + $"{Environment.NewLine}{Environment.NewLine}是 = 全部發布（用本機的畫面定義檔與列印樣板）；否 = 只發布這個畫面",
                                "發布", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            int version = PublishOne(cs, key, def, currentFilePath, notes);
            var done = new List<string> { $"「{PublishTitle(def, key)}」（{key}）第 {version} 版" };
            if (withRelated)
                foreach (var r in related)
                    done.Add($"「{PublishTitle(r.Def, r.Key)}」（{r.Key}）第 {PublishOne(cs, r.Key, r.Def, r.Path, notes)} 版");
            else if (related.Count > 0)
            {
                var published = SysDb.LoadPublished(cs, "form").Select(p => p.FormKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
                notes.AddRange(related.Where(r => !published.Contains(r.Key)).Select(r => $"畫面「{r.Def.Title ?? r.Key}」（{r.Key}）還沒發布，按鈕會開不起來"));
            }
            UpdateStatus($"已發布 {done.Count} 個畫面：" + string.Join("、", done));
            MessageBox.Show("已發布：" + Environment.NewLine + string.Join(Environment.NewLine, done.Select(x => "　" + x)) + Environment.NewLine + Environment.NewLine
                            + "第一次發布的畫面，請到「選單管理」放進選單（只從按鈕開啟的畫面不用），並在「使用者、角色與權限」設定哪些角色可以用。"
                            + (notes.Count > 0 ? Environment.NewLine + Environment.NewLine + "注意：" + Environment.NewLine + string.Join(Environment.NewLine, notes.Distinct()) : ""),
                            "發布");
        }
        catch (Exception ex)
        {
            MessageBox.Show("發布失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>畫面上所有的按鈕（主畫面 + 編輯視窗），欄位的開窗選取也當成「開啟畫面」按鈕</summary>
    internal static List<ButtonDefinition> AllButtons(FormDefinition form) =>
        form.Buttons.Concat(form.Grids.SelectMany(g => g.EditButtons))
            .Concat(form.Fields.Select(ButtonRunner.PickerButton))
            .Concat(form.Grids.SelectMany(g => g.Columns).Select(c => ButtonRunner.PickerButton(InputHelper.FromGridColumn(c, false))))
            .Concat(form.Grids.Select(g => ButtonRunner.ListOpenButton(g.Title, g.OpenForm, g.OpenParams)))                  // 清單的雙擊開啟畫面
            .Concat(form.Grids.SelectMany(g => g.EditLists).Select(l => ButtonRunner.ListOpenButton(l.Title, l.OpenForm, l.OpenParams)))
            .Where(b => b != null).ToList();

    /// <summary>發布時的畫面名稱：沒有改過標題（還是預設的「新畫面」或空白）就用檔名（不含副檔名）</summary>
    internal static string PublishTitle(FormDefinition form, string key) =>
        string.IsNullOrWhiteSpace(form.Title) || form.Title == new FormDefinition().Title ? key : form.Title;

    /// <summary>發布一個畫面和它的按鈕用到的列印樣板，回傳畫面的版本</summary>
    internal static int PublishOne(string cs, string key, FormDefinition form, string path, List<string> notes)
    {
        string folder = Path.GetDirectoryName(path);
        string title = PublishTitle(form, key);
        if (title != form.Title)
        {
            form = FormDefinition.FromJson(form.ToJson());     // 複製一份再改標題，不動到設計中的畫面
            form.Title = title;
        }
        int version = SysDb.Publish(cs, key, "form", form.Title, form.ToJson(), SysContext.User.UserId);
        foreach (var b in AllButtons(form).Where(b => b.Action == ButtonAction.列印報表 && !string.IsNullOrWhiteSpace(b.PrintTemplate)))
        {
            string template = ButtonRunner.ResolvePath(b.PrintTemplate, folder);
            if (File.Exists(template)) SysDb.Publish(cs, Path.GetFileName(template), "print", Path.GetFileName(template), File.ReadAllText(template), SysContext.User.UserId);
            else notes.Add($"找不到列印樣板檔：{template}");
        }
        Audit.Write(key, form.TableName, "發布", "FormKey=" + key, $"第 {version} 版");
        return version;
    }

    /// <summary>
    /// 「開啟畫面」按鈕指向的畫面，以及那些畫面再開啟的畫面（不含自己、不重複）；
    /// 本機找不到畫面定義檔的記到 notes
    /// </summary>
    private static List<(string Key, string Path, FormDefinition Def)> RelatedForms(FormDefinition root, string rootPath, List<string> notes)
    {
        var result = new List<(string, string, FormDefinition)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFileNameWithoutExtension(rootPath) };
        var queue = new Queue<(FormDefinition Def, string Path)>();
        queue.Enqueue((root, rootPath));
        while (queue.Count > 0)
        {
            var (form, path) = queue.Dequeue();
            string folder = Path.GetDirectoryName(path);
            foreach (var b in AllButtons(form).Where(b => b.OpensForm && !string.IsNullOrWhiteSpace(b.TargetForm)))
            {
                string key = Path.GetFileNameWithoutExtension(b.TargetForm.Trim());
                if (!seen.Add(key)) continue;
                string file = ButtonRunner.ResolvePath(b.TargetForm.Trim(), folder);
                if (!File.Exists(file)) file = Path.Combine(folder, key + ".json");
                if (!File.Exists(file))
                {
                    notes.Add($"按鈕「{b.Text}」要開啟的畫面「{key}」在本機找不到畫面定義檔，沒有一併發布");
                    continue;
                }
                try
                {
                    var target = FormDefinition.Load(file);
                    result.Add((key, file, target));
                    queue.Enqueue((target, file));
                }
                catch (Exception ex)
                {
                    notes.Add($"畫面「{key}」讀取失敗，沒有一併發布：{ex.Message}");
                }
            }
        }
        return result;
    }

    /// <summary>開啟已發布的畫面：從系統資料庫讀出來編輯，存檔時寫到預設的 Forms 資料夾（同檔名）</summary>
    private void OpenPublished()
    {
        if (!SysContext.Active) return;
        var list = SysDb.LoadPublished(SysContext.Connection, "form");
        using var dlg = new PickListDialog("開啟已發布的畫面（勾一個）",
            list.Select(p => new PickListDialog.Item(p.FormKey, $"{p.Title ?? p.FormKey}　［{p.FormKey}］第 {p.Version} 版　{p.PublishedBy} {p.PublishedAt:yyyy/MM/dd HH:mm}")));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        LoadPublishedKey(dlg.PickedKeys[0]);
    }

    /// <summary>從系統資料庫讀已發布的畫面來編輯，存檔時寫到預設的 Forms 資料夾（同檔名）</summary>
    private void LoadPublishedKey(string key)
    {
        try
        {
            NewForm();
            def = FormDefinition.FromJson(SysDb.LoadContent(SysContext.Connection, key));
            def.FormKey = key;
            currentFilePath = Path.Combine(EnsureFolder(), key + ".json");
            def.SourcePath = currentFilePath;
            if (!string.IsNullOrWhiteSpace(def.ConnectionString)) SetConnection(def.ConnectionString, def.DatabaseType);
            BuildCanvas();
            ReloadDefTableColumns();
            history.Reset();
            UpdateStatus($"已開啟已發布的畫面 {key}（存檔會寫到 {currentFilePath}）");
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveForm(bool saveAs)
    {
        if (def.Fields.Count == 0 && !def.Grids.Any(g => g.IsSqlList || g.IsMasterList))       // 只放 SQL 清單、單頭清單的畫面不需要欄位
        {
            MessageBox.Show("畫面上還沒有欄位（或清單）。", "提醒");
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

    /// <summary>預覽執行；debug = 每個執行的 SQL（參數值、結果、錯誤）都顯示在「SQL 紀錄（Debug）」視窗（從這個預覽開出去的畫面也一樣）</summary>
    private void Preview(bool debug = false)
    {
        if (def.Fields.Count == 0 && !def.Grids.Any(g => g.IsSqlList || g.IsMasterList))       // 只放 SQL 清單、單頭清單的畫面不需要欄位
        {
            MessageBox.Show("畫面上還沒有欄位（或清單）。", "提醒");
            return;
        }
        if (!debug)
        {
            new RuntimeForm(def).Show(this);
            return;
        }
        SqlDebug.Begin();                                  // 建立畫面前就開始，開啟時讀資料的 SQL 也記得到
        try
        {
            var form = new RuntimeForm(def);
            SqlDebug.Attach(form);                         // 關閉時結束 Debug
            form.Show(this);
        }
        catch
        {
            SqlDebug.End();
            throw;
        }
    }
}
