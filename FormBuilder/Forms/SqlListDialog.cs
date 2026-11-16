using System.Data;

namespace FormBuilder;

/// <summary>
/// SQL 清單設定：標題、SELECT（可以用 @單頭欄位）、按「讀取欄位」從 SQL 結果帶出欄位，
/// 再勾選要顯示的欄位、改標題與寬度；也可以 JOIN 其他資料表帶出欄位（SQL 當子查詢，外層 LEFT JOIN）。按「確定」寫回 GridDefinition
/// </summary>
public partial class SqlListDialog : Form
{
    private readonly GridDefinition grid;
    private readonly string connectionString;
    private readonly DataTable columnTable = new();
    /// <summary>JOIN（按「確定」才寫回）與 JOIN 帶進來的欄位（欄位名稱 → 欄位）</summary>
    private readonly List<GridJoinDefinition> workJoins;
    private readonly Dictionary<string, GridColumnDefinition> joinColumns = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>自訂欄位（勾選框、按鈕、日期選擇、文字框）：不是 SQL 的欄位，不查詢、不存檔（欄位名稱 → 欄位）</summary>
    private readonly Dictionary<string, GridColumnDefinition> customColumns = new(StringComparer.OrdinalIgnoreCase);
    private const string CustomSource = "自訂欄位（不查詢、不存檔）";

    /// <param name="masterColumns">單頭的欄位（提示可以用的 @參數）</param>
    /// <param name="allowLayout">主畫面的 SQL 清單：有「編輯視窗版面…」按鈕（編輯視窗裡的 SQL 清單沒有）</param>
    /// <param name="screenFont">畫面字型（編輯視窗版面設計器預覽用）</param>
    public SqlListDialog(GridDefinition grid, string connectionString, IEnumerable<string> masterColumns, bool allowLayout = false, Font screenFont = null)
    {
        InitializeComponent();
        this.grid = grid;
        this.connectionString = connectionString;
        txtTitle.Text = grid.Title;
        txtSql.Text = grid.Sql;
        var names = masterColumns.ToList();
        lblParams.Text = names.Count == 0 ? "可以用的 @參數：（還沒有單頭欄位）"
                                          : "可以用的 @參數：" + string.Join("、", names.Select(n => "@" + n));

        columnTable.Columns.Add("Visible", typeof(bool));
        columnTable.Columns.Add("Column", typeof(string));
        columnTable.Columns.Add("Header", typeof(string));
        columnTable.Columns.Add("Width", typeof(int));
        columnTable.Columns.Add("Source", typeof(string));
        columnTable.Columns.Add("Click", typeof(string));       // 點擊開啟的說明（顯示用，設定在 clickButtons）
        columnTable.Columns.Add("HeaderGroup", typeof(string)); // 群組標題（兩層標題的上面一層）
        columnTable.Columns.Add("ForeColor", typeof(string));   // 這一欄資料的文字顏色（#RRGGBB）
        columnTable.Columns.Add("AfterSelect", typeof(string)); // 欄位的執行後動作（SQL 摘要，顯示用；設定在 afterSelects）
        foreach (var c in grid.Columns.Where(GridCells.HasAfterSelect))
            afterSelects[c.Column] = (c.EditAfterSelectSql, c.EditAfterSelectParams, c.EditAfterSelectReturn);
        foreach (var c in grid.Columns.Where(c => c.IsLink)) clickButtons[c.Column] = c.Button;
        foreach (var c in grid.Columns.Where(c => !string.IsNullOrWhiteSpace(c.HeaderGroup))) headerGroups[c.Column] = c.HeaderGroup;
        foreach (var c in grid.Columns.Where(c => !string.IsNullOrWhiteSpace(c.ForeColorHtml))) foreColors[c.Column] = c.ForeColorHtml;
        workJoins = grid.Joins.Select(j => j.Clone()).ToList();
        foreach (var c in grid.Columns.Where(c => c.IsJoin && workJoins.Any(j => j.Alias == c.JoinAlias))) joinColumns[c.Column] = c;
        foreach (var c in grid.Columns.Where(c => c.IsCustom)) customColumns[c.Column] = c;
        foreach (var c in grid.Columns.Where(c => c.IsCustom || !c.ScreenOnly && (!c.IsJoin || joinColumns.ContainsKey(c.Column))))   // 編輯視窗的畫面欄位不是 SQL 的欄位
            columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Source", HeaderText = "來源（JOIN）", ReadOnly = true, Width = 220 });
        var colClick = new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Click", HeaderText = "點擊開啟", ReadOnly = true, Width = 180,
            ToolTipText = "雙擊設定：執行時這一欄的文字變成藍色底線，點一下就用那一列的值開啟畫面（可以依條件開不同畫面）；右鍵清除",
        };
        dgvColumns.Columns.Add(colClick);
        var colHeaderGroup = new DataGridViewTextBoxColumn
        {
            DataPropertyName = "HeaderGroup", HeaderText = "群組標題", Width = 90,
            ToolTipText = "兩層標題的上面一層：相鄰的欄位填一樣的名稱（例如「設計」），執行時合併成一格橫跨這幾欄；空白 = 標題佔滿上下兩層",
        };
        dgvColumns.Columns.Add(colHeaderGroup);
        var colHeaderText = dgvColumns.Columns.Cast<DataGridViewColumn>().FirstOrDefault(x => x.DataPropertyName == "Header");
        if (colHeaderText != null) colHeaderGroup.DisplayIndex = colHeaderText.DisplayIndex;   // 放在「標題」前面
        dgvColumns.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "AfterSelect", HeaderText = "執行後動作", ReadOnly = true, Width = 200,
            ToolTipText = "雙擊設定：執行時這一欄可以輸入（淡黃底），輸入完離開格子就執行 SQL（@欄位 取那一列的值，可以 UPDATE 資料表），"
                        + "SQL 查出來的第一筆依回傳對應帶進同一列的其他欄位",
        });
        var colFore = GridCells.AddColorColumn(dgvColumns, "ForeColor", "文字顏色", "Grid 上這一欄資料的文字顏色：雙擊選顏色，按 Delete 清除（= 預設顏色）");
        if (colHeaderText != null) colFore.DisplayIndex = colHeaderText.DisplayIndex + 1;     // 放在「標題」後面
        RefreshClickText(keep: false);
        dgvColumns.DataSource = columnTable;
        // 點擊開啟：右鍵清除
        var ctxClick = new ContextMenuStrip();
        var miEditClick = new ToolStripMenuItem("設定點擊開啟…");
        var miClearClick = new ToolStripMenuItem("清除點擊開啟");
        ctxClick.Items.AddRange(new ToolStripItem[] { miEditClick, miClearClick });
        string clickColumn = null;
        miEditClick.Click += (s, e) => EditClick(clickColumn);
        miClearClick.Click += (s, e) =>
        {
            clickButtons.Remove(clickColumn);
            RefreshClickText();
        };
        dgvColumns.CellMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex != colClick.Index) return;
            if (dgvColumns.Rows[e.RowIndex].DataBoundItem is not DataRowView v || customColumns.ContainsKey((string)v.Row["Column"])) return;
            clickColumn = (string)v.Row["Column"];
            dgvColumns.CurrentCell = dgvColumns[e.ColumnIndex, e.RowIndex];
            miClearClick.Enabled = clickButtons.ContainsKey(clickColumn);
            ctxClick.Show(Cursor.Position);
        };
        dgvColumns.DataError += (s, e) => e.Cancel = true;
        // Delete：選的是 JOIN 帶進來的欄位時，移除那個 JOIN（連同它帶進來的欄位）
        dgvColumns.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Delete || dgvColumns.IsCurrentCellInEditMode) return;
            if (dgvColumns.CurrentCell?.OwningColumn.DataPropertyName == "ForeColor") return;   // 文字顏色欄的 Delete 是清除顏色
            if (dgvColumns.CurrentRow?.DataBoundItem is DataRowView v && joinColumns.TryGetValue((string)v.Row["Column"], out var jc))
            {
                RemoveJoin(jc.JoinAlias);
                e.Handled = true;
            }
            else if (dgvColumns.CurrentRow?.DataBoundItem is DataRowView cv && customColumns.TryGetValue((string)cv.Row["Column"], out var cc))
            {
                RemoveCustom(cc);
                e.Handled = true;
            }
        };
        // 自訂按鈕欄位：雙擊設定按鈕動作
        dgvColumns.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0 || dgvColumns.Rows[e.RowIndex].DataBoundItem is not DataRowView v) return;
            if (e.ColumnIndex >= 0 && dgvColumns.Columns[e.ColumnIndex].DataPropertyName == "ForeColor") return;   // 文字顏色欄雙擊是選顏色
            string name = (string)v.Row["Column"];
            if (customColumns.TryGetValue(name, out var c))
            {
                if (c.CellType == GridCellKind.按鈕) EditButton(c);
            }
            else if (dgvColumns.Columns[e.ColumnIndex].DataPropertyName == "Click") EditClick(name);   // 一般欄位：設定點擊開啟
            else if (dgvColumns.Columns[e.ColumnIndex].DataPropertyName == "AfterSelect") EditAfterSelect(name);
        };

        // 自訂欄位：例如勾選框，使用者打勾的列，按「執行SQL」按鈕時只對打勾的列執行
        var btnCustom = new Button { Text = "新增自訂欄位…", AutoSize = true, Height = 30 };
        btnCustom.Click += (s, e) => AddCustom();
        bottomPanel.Controls.Add(btnCustom);

        var btnJoin = new Button { Text = "JOIN 其他資料表…", AutoSize = true, Height = 30 };
        btnJoin.Click += (s, e) => AddJoin();
        bottomPanel.Controls.Add(btnJoin);
        // 執行後動作（同 Grid 屬性的「執行後動作」）：清單載入、變動後計算，結果填進畫面上的欄位或文字標籤
        afterChange = grid.AfterChange;
        masterNames = names;
        var btnAfter = new Button { AutoSize = true, Height = 30 };
        // 跟欄位的「執行後動作」分開叫：這個是整個清單的加總計算（Grid 屬性的執行後動作）
        void ShowAfter() => btnAfter.Text = string.IsNullOrWhiteSpace(afterChange) ? "清單加總計算…" : "清單加總計算（已設定）…";
        ShowAfter();
        btnAfter.Click += (s, e) =>
        {
            if (EditAfterChange()) ShowAfter();
        };
        bottomPanel.Controls.Add(btnAfter);
        // 標題顏色（同 Grid 屬性的標題底色、群組標題底色、標題文字顏色），按確定才寫回
        headerBack = grid.HeaderBackColor;
        headerGroupBack = grid.HeaderGroupBackColor;
        headerFore = grid.HeaderForeColor;
        AddColorButton("標題文字顏色", () => headerFore, c => headerFore = c, isText: true);
        AddColorButton("群組標題底色", () => headerGroupBack, c => headerGroupBack = c, isText: false);
        AddColorButton("標題底色", () => headerBack, c => headerBack = c, isText: false);
        btnReadColumns.Click += (s, e) => ReadColumns();
        btnOk.Click += (s, e) => Apply();
        if (allowLayout)
        {
            // 編輯視窗版面：先把目前的 SQL、欄位套用到 SQL 清單，再開版面設計器（版面設計器有自己的確定 / 取消）
            var btnLayout = new Button { Text = "編輯視窗版面…", AutoSize = true, Height = 30 };
            btnLayout.Click += (s, e) =>
            {
                if (!ApplyColumns()) return;
                using var layout = new EditLayoutDialog(grid, connectionString, screenFont: screenFont);
                layout.ShowDialog(this);
                // 版面設計器 JOIN 進來的（已經加到 SQL 清單上了）也加進這裡，按「確定」時才不會被蓋掉
                foreach (var j in grid.Joins.Where(j => !workJoins.Any(x => x.Alias == j.Alias))) workJoins.Add(j.Clone());
                foreach (var c in grid.Columns.Where(c => c.IsJoin && !joinColumns.ContainsKey(c.Column)))
                {
                    joinColumns[c.Column] = c;
                    columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
                }
            };
            bottomPanel.Controls.Add(btnLayout);
        }
    }

    /// <summary>執行 SQL 取欄位結構：原本有的欄位保留標題、寬度、顯示設定，新的欄位加在後面</summary>
    private bool ReadColumns()
    {
        if (string.IsNullOrWhiteSpace(txtSql.Text))
        {
            MessageBox.Show("請先輸入 SQL（SELECT …）。", "提醒");
            return false;
        }
        List<GridColumnDefinition> columns;
        try
        {
            Cursor = Cursors.WaitCursor;
            columns = SqlList.ReadColumns(connectionString, txtSql.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("SQL 執行失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        // 先把原本的設定複製出來：Clear() 之後原本的 DataRow 就讀不到值了
        var old = columnTable.Rows.Cast<DataRow>()
            .GroupBy(r => (string)r["Column"], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (Visible: g.First()["Visible"], Header: g.First()["Header"], Width: g.First()["Width"]), StringComparer.OrdinalIgnoreCase);
        KeepHeaderGroups();
        columnTable.Clear();
        foreach (var c in columns.Where(c => !joinColumns.ContainsKey(c.Column) && !customColumns.ContainsKey(c.Column)))
        {
            if (old.TryGetValue(c.Column, out var o)) columnTable.Rows.Add(o.Visible, c.Column, o.Header, o.Width, "");
            else columnTable.Rows.Add(true, c.Column, c.Column, c.Width, "");
        }
        // JOIN 帶進來的欄位、自訂欄位接在後面
        foreach (var c in joinColumns.Values.Concat(customColumns.Values))
        {
            if (old.TryGetValue(c.Column, out var o)) columnTable.Rows.Add(o.Visible, c.Column, o.Header, o.Width, SourceText(c));
            else columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        }
        readColumns = columns;
        RefreshClickText(keep: false);
        return true;
    }

    /// <summary>最近一次讀到的欄位（資料型別用）</summary>
    private List<GridColumnDefinition> readColumns;

    private void Apply()
    {
        if (ApplyColumns()) DialogResult = DialogResult.OK;
    }

    /// <summary>標題、SQL、欄位設定寫回 SQL 清單；SQL 有問題時提醒並回傳 false</summary>
    private bool ApplyColumns()
    {
        dgvColumns.EndEdit();
        if (string.IsNullOrWhiteSpace(txtSql.Text))
        {
            MessageBox.Show("請輸入 SQL（SELECT …）。", "提醒");
            return false;
        }
        // SQL 改了沒按「讀取欄位」：先讀一次，確認 SQL 能執行
        if ((readColumns == null || txtSql.Text.Trim() != (grid.Sql ?? "").Replace("\r\n", "\n").Trim()) && !ReadColumns()) return false;
        if (columnTable.Rows.Count == 0)
        {
            MessageBox.Show("SQL 沒有傳回任何欄位。", "提醒");
            return false;
        }
        var types = (readColumns ?? new()).ToDictionary(c => c.Column, StringComparer.OrdinalIgnoreCase);
        grid.Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "SQL 清單" : txtTitle.Text.Trim();
        grid.Sql = txtSql.Text.Trim().Replace("\n", "\r\n");      // RichTextBox 的換行是 \n，存回 \r\n
        grid.Columns = columnTable.Rows.Cast<DataRow>().Select(r =>
        {
            string name = (string)r["Column"];
            string group = Convert.ToString(r["HeaderGroup"]).Trim() is { Length: > 0 } hg ? hg : null;
            string fore = Convert.ToString(r["ForeColor"]) is { Length: > 0 } fcText ? fcText : null;
            if (customColumns.TryGetValue(name, out var custom))
            {
                custom.HeaderGroup = group;
                custom.ForeColorHtml = fore;
                custom.Visible = r["Visible"] is true;
                custom.Header = string.IsNullOrWhiteSpace(Convert.ToString(r["Header"])) ? name : Convert.ToString(r["Header"]);
                custom.Width = r["Width"] is int cw && cw > 0 ? Math.Clamp(cw, 30, 600) : 90;
                if (custom.Button != null) custom.Button.Text = GridCells.ButtonText(custom);
                return custom;                             // 自訂欄位維持「不查詢、不存檔」
            }
            var existing = grid.Columns.FirstOrDefault(c => c.Column.Equals(name, StringComparison.OrdinalIgnoreCase) && !c.IsJoin && !c.IsCustom);
            var c = joinColumns.TryGetValue(name, out var joined) ? joined
                  : existing ?? new GridColumnDefinition { Column = name, IsNullable = true, NoEntry = true };
            if (!c.IsJoin && types.TryGetValue(name, out var t)) c.DataType = t.DataType;
            c.ScreenOnly = false;                          // SQL 現在有這個欄位了
            c.Visible = r["Visible"] is true;
            c.Header = string.IsNullOrWhiteSpace(Convert.ToString(r["Header"])) ? name : Convert.ToString(r["Header"]);
            c.Width = r["Width"] is int w && w > 0 ? Math.Clamp(w, 30, 600) : 100;
            c.HeaderGroup = group;
            c.ForeColorHtml = fore;
            var after = afterSelects.TryGetValue(name, out var a) ? a : default;          // 欄位的執行後動作
            c.EditAfterSelectSql = after.Sql;
            c.EditAfterSelectParams = after.Params;
            c.EditAfterSelectReturn = after.Return;
            c.Button = clickButtons.TryGetValue(name, out var click) ? click : null;   // 點擊開啟
            if (c.Button != null) c.Button.Text = c.Header;
            return c;
        }).Concat(grid.Columns.Where(c => c.ScreenOnly && !c.IsCustom && !columnTable.Rows.Cast<DataRow>().Any(r => ((string)r["Column"]).Equals(c.Column, StringComparison.OrdinalIgnoreCase))))
          .ToList();                                       // 編輯視窗的畫面欄位保留（SQL 有同名欄位時就用 SQL 的）
        grid.Joins = workJoins.Where(j => grid.Columns.Any(c => c.JoinAlias == j.Alias)).ToList();
        grid.AfterChange = string.IsNullOrWhiteSpace(afterChange) ? null : afterChange.Trim();
        grid.HeaderBackColor = headerBack;
        grid.HeaderGroupBackColor = headerGroupBack;
        grid.HeaderForeColor = headerFore;
        return true;
    }

    private string SourceText(GridColumnDefinition c)
    {
        if (c.IsCustom) return CustomSource + (c.CellType == GridCellKind.按鈕 ? "，雙擊設定按鈕動作" : "");
        if (!c.IsJoin) return "";
        var join = workJoins.FirstOrDefault(j => j.Alias == c.JoinAlias);
        return join == null ? c.JoinAlias : $"{c.JoinAlias}：{join.TableName}.{c.SourceColumn}";
    }

    /// <summary>JOIN 其他資料表：用 SQL 的欄位對應關聯資料表的欄位，帶進來的欄位加在最後面</summary>
    private void AddJoin()
    {
        dgvColumns.EndEdit();
        if (columnTable.Rows.Count == 0 && !ReadColumns()) return;
        int n = 1;
        while (workJoins.Any(j => j.Alias == $"J{n}")) n++;
        var types = (readColumns ?? grid.Columns).ToDictionary(c => c.Column, c => c.DataType, StringComparer.OrdinalIgnoreCase);
        var names = columnTable.Rows.Cast<DataRow>().Select(r => (string)r["Column"]).ToList();
        var localColumns = names.Where(name => !joinColumns.ContainsKey(name))
            .Select(name => new ColumnInfo { Name = name, DataType = types.TryGetValue(name, out var t) ? t : "nvarchar" }).ToList();
        using var dlg = new JoinDialog(connectionString, $"J{n}", localColumns, names);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        workJoins.Add(dlg.Join);
        foreach (var c in dlg.NewColumns)
        {
            joinColumns[c.Column] = c;
            columnTable.Rows.Add(true, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        }
    }

    /// <summary>新增自訂欄位（勾選框、按鈕、日期選擇、文字框），加在清單最後面；按鈕接著設定動作</summary>
    private void AddCustom()
    {
        dgvColumns.EndEdit();
        var used = columnTable.Rows.Cast<DataRow>().Select(r => (string)r["Column"]).ToList();
        using var dlg = new CustomColumnDialog(used);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var c = dlg.Column;
        customColumns[c.Column] = c;
        columnTable.Rows.Add(c.Visible, c.Column, c.Header ?? c.Column, c.Width, SourceText(c));
        if (c.CellType == GridCellKind.按鈕) EditButton(c);
    }

    private void EditButton(GridColumnDefinition c)
    {
        var rowColumns = columnTable.Rows.Cast<DataRow>().Select(r => (string)r["Column"]).Where(n => n != c.Column);
        var edited = CustomColumnDialog.EditButton(this, c.Button, c.Header ?? c.Column, rowColumns);
        if (edited != null) c.Button = edited;
    }

    /// <summary>一般欄位的點擊開啟（欄位名稱 → 動作，按「確定」才寫回欄位的 Button）</summary>
    private readonly Dictionary<string, ButtonDefinition> clickButtons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>點擊開啟：跟按鈕動作同一個設定視窗，預設是開啟畫面（傳入參數用 @欄位 取那一列的值）</summary>
    private void EditClick(string name)
    {
        if (name == null || customColumns.ContainsKey(name)) return;
        dgvColumns.EndEdit();
        var row = columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => ((string)r["Column"]).Equals(name, StringComparison.OrdinalIgnoreCase));
        string title = Convert.ToString(row?["Header"]) is { Length: > 0 } h ? h : name;
        var rowColumns = columnTable.Rows.Cast<DataRow>().Select(r => (string)r["Column"])
                                    .Where(n => !(customColumns.TryGetValue(n, out var cc) && cc.CellType == GridCellKind.按鈕));
        var edited = CustomColumnDialog.EditButton(this, clickButtons.TryGetValue(name, out var b) ? b : new ButtonDefinition { Action = ButtonAction.開啟畫面 },
                                                   $"點擊「{title}」", rowColumns);
        if (edited == null) return;
        clickButtons[name] = edited;
        RefreshClickText();
    }

    private Color headerBack, headerGroupBack, headerFore;
    private string afterChange;
    private readonly List<string> masterNames;

    /// <summary>
    /// 執行後動作：一行一組「畫面欄位=公式」，例如 AMOUNT=SUM(金額)、標籤8=AVG(單價)；
    /// 清單的資料載入或變動就計算，結果填進畫面上的欄位或文字標籤。按確定回傳 true
    /// </summary>
    private bool EditAfterChange()
    {
        dgvColumns.EndEdit();
        using var form = new Form
        {
            Text = $"執行後動作：{(string.IsNullOrWhiteSpace(txtTitle.Text) ? "SQL 清單" : txtTitle.Text.Trim())}",
            Font = new Font("Microsoft JhengHei UI", 9F),
            Size = new Size(640, 440),
            MinimumSize = new Size(480, 300),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            ShowInTaskbar = false,
        };
        var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
                                Font = new Font("Consolas", 10F), Text = (afterChange ?? "").ReplaceLineEndings() };
        // 插入清單欄位：包成 SUM(欄位)（可以改成 AVG / COUNT / MAX / MIN）；插入畫面欄位：放在等號左邊當結果要填的地方
        var cboList = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        cboList.Items.Add("插入清單欄位 SUM(…)");
        cboList.Items.AddRange(columnTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted)
                                          .Select(r => (object)(string)r["Column"]).ToArray());
        cboList.SelectedIndex = 0;
        cboList.SelectedIndexChanged += (s, e) =>
        {
            if (cboList.SelectedIndex <= 0) return;
            box.SelectedText = $"SUM({cboList.SelectedItem})";
            cboList.SelectedIndex = 0;
            box.Focus();
        };
        var cboScreen = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        cboScreen.Items.Add("插入畫面欄位 / 文字標籤…");
        cboScreen.Items.AddRange(masterNames.Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        cboScreen.SelectedIndex = 0;
        cboScreen.SelectedIndexChanged += (s, e) =>
        {
            if (cboScreen.SelectedIndex <= 0) return;
            box.SelectedText = cboScreen.SelectedItem.ToString();
            cboScreen.SelectedIndex = 0;
            box.Focus();
        };
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = SystemColors.GrayText,
            Text = "清單的資料載入、變動就計算，結果填進畫面上的欄位或文字標籤。一行一組「畫面欄位=公式」，例如：\n"
                 + "AMOUNT=SUM(金額)        標籤8=AVG(單價)\n"
                 + "可以用 SUM、AVG、COUNT、MAX、MIN、STD、STDP，括號裡是清單的欄位；也可以用畫面上的欄位和數字做 + - * /。清空 = 不計算。",
        };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4) };
        top.Controls.AddRange(new Control[] { cboScreen, cboList });
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(4) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        var btnOk = new Button { Text = "確定", DialogResult = DialogResult.OK, Width = 80 };
        bottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
        form.Controls.Add(box);
        form.Controls.Add(top);
        form.Controls.Add(hint);
        form.Controls.Add(bottom);
        form.CancelButton = btnCancel;
        form.Shown += (s, e) => { box.SelectionStart = box.TextLength; box.SelectionLength = 0; };   // 不要整段選取，一打字就蓋掉
        if (form.ShowDialog(this) != DialogResult.OK) return false;
        afterChange = string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim().ReplaceLineEndings("\r\n");
        return true;
    }

    /// <summary>
    /// 下方的標題顏色按鈕：按鈕本身顯示目前的顏色（底色畫在按鈕底色、文字顏色畫在字上），
    /// 按下去選「選擇顏色…」或「恢復預設」
    /// </summary>
    private void AddColorButton(string text, Func<Color> get, Action<Color> set, bool isText)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, UseVisualStyleBackColor = true };
        void Show()
        {
            var color = get();
            if (isText) button.ForeColor = color.IsEmpty ? SystemColors.ControlText : color;
            else
            {
                button.UseVisualStyleBackColor = color.IsEmpty;
                button.BackColor = color.IsEmpty ? SystemColors.Control : color;
                button.ForeColor = color.IsEmpty ? SystemColors.ControlText : ButtonLook.Contrast(color);
            }
            toolTip.SetToolTip(button, $"{text}：{(color.IsEmpty ? "預設" : ColorText.Format(color))}（按一下選顏色或恢復預設）");
        }
        var menu = new ContextMenuStrip();
        menu.Items.Add("選擇顏色…", null, (s, e) =>
        {
            using var dlg = new ColorDialog { FullOpen = true, Color = get().IsEmpty ? (isText ? Color.Black : SystemColors.Control) : get() };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            set(dlg.Color);
            Show();
        });
        menu.Items.Add("恢復預設", null, (s, e) =>
        {
            set(Color.Empty);
            Show();
        });
        button.Click += (s, e) => menu.Show(button, new Point(0, button.Height));
        Show();
        bottomPanel.Controls.Add(button);
    }

    private readonly ToolTip toolTip = new();

    /// <summary>群組標題（欄位名稱 → 群組）：讀取欄位重建清單時保留</summary>
    private readonly Dictionary<string, string> headerGroups = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>欄位的執行後動作（欄位名稱 → SQL、傳入參數、回傳對應），按確定才寫回欄位</summary>
    private readonly Dictionary<string, (string Sql, string Params, string Return)> afterSelects = new(StringComparer.OrdinalIgnoreCase);

    private static string SqlSummary(string sql) =>
        string.IsNullOrWhiteSpace(sql) ? null : string.Join(" ", sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    /// <summary>欄位的執行後動作：跟單身欄位設定的「執行後動作」同一個設定視窗；SQL 清空 = 取消</summary>
    private void EditAfterSelect(string name)
    {
        if (name == null || customColumns.ContainsKey(name)) return;
        dgvColumns.EndEdit();
        var row = columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => r.RowState != DataRowState.Deleted && ((string)r["Column"]).Equals(name, StringComparison.OrdinalIgnoreCase));
        string title = Convert.ToString(row?["Header"]) is { Length: > 0 } h ? h : name;
        afterSelects.TryGetValue(name, out var current);
        var rowColumns = columnTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted).Select(r => (string)r["Column"])
                                    .Where(n => !(customColumns.TryGetValue(n, out var cc) && cc.CellType == GridCellKind.按鈕));
        using var dlg = new AfterSelectDialog(title, current.Sql, current.Params, current.Return, rowColumns);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(dlg.Sql)) afterSelects.Remove(name);
        else afterSelects[name] = (dlg.Sql, dlg.Params, dlg.Return);
        RefreshClickText();
    }

    /// <summary>文字顏色（欄位名稱 → #RRGGBB）：讀取欄位重建清單時保留</summary>
    private readonly Dictionary<string, string> foreColors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>清單上改過的群組標題記到 headerGroups（重建清單前呼叫）</summary>
    private void KeepHeaderGroups()
    {
        foreach (DataRow r in columnTable.Rows)
            if (r.RowState != DataRowState.Deleted)
            {
                string name = (string)r["Column"];
                if (Convert.ToString(r["HeaderGroup"]).Trim() is { Length: > 0 } g) headerGroups[name] = g;
                else headerGroups.Remove(name);
                if (Convert.ToString(r["ForeColor"]) is { Length: > 0 } fc) foreColors[name] = fc;
                else foreColors.Remove(name);
            }
    }

    /// <summary>清單的「點擊開啟」、「群組標題」欄：依 clickButtons、headerGroups 重新顯示（讀取欄位會重建清單）</summary>
    /// <param name="keep">true = 先把清單上改過的群組標題記下來（清單是重建前的那一份）；剛重建、還沒填值時傳 false</param>
    private void RefreshClickText(bool keep = true)
    {
        if (keep) KeepHeaderGroups();
        foreach (DataRow r in columnTable.Rows)
            if (r.RowState != DataRowState.Deleted)
            {
                r["Click"] = clickButtons.TryGetValue((string)r["Column"], out var b) ? GridCells.LinkSummary(b) : DBNull.Value;
                r["HeaderGroup"] = headerGroups.TryGetValue((string)r["Column"], out var g) ? g : DBNull.Value;
                r["ForeColor"] = foreColors.TryGetValue((string)r["Column"], out var fc) ? fc : DBNull.Value;
                r["AfterSelect"] = afterSelects.TryGetValue((string)r["Column"], out var a) ? SqlSummary(a.Sql) : DBNull.Value;
            }
    }

    private void RemoveCustom(GridColumnDefinition c)
    {
        if (MessageBox.Show($"確定要移除自訂欄位「{c.Header ?? c.Column}」嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        dgvColumns.EndEdit();
        customColumns.Remove(c.Column);
        columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => ((string)r["Column"]).Equals(c.Column, StringComparison.OrdinalIgnoreCase))?.Delete();
        columnTable.AcceptChanges();
    }

    /// <summary>移除一個 JOIN，連同它帶進來的欄位</summary>
    private void RemoveJoin(string alias)
    {
        var columns = joinColumns.Values.Where(c => c.JoinAlias == alias).ToList();
        if (MessageBox.Show($"確定要移除 JOIN {alias}（{columns.Count} 個欄位）嗎？", "提醒", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        dgvColumns.EndEdit();
        foreach (var c in columns)
        {
            joinColumns.Remove(c.Column);
            columnTable.Rows.Cast<DataRow>().FirstOrDefault(r => ((string)r["Column"]).Equals(c.Column, StringComparison.OrdinalIgnoreCase))?.Delete();
        }
        columnTable.AcceptChanges();
        workJoins.RemoveAll(j => j.Alias == alias);
    }
}
