using System.Data;
using Microsoft.Data.SqlClient;

namespace FormBuilder;

/// <summary>
/// 使用者操作的畫面（類似 4GL 的 i 類單檔維護程式）：
/// 查詢 → 輸入條件 → 確定，查到的資料用首筆 / 上筆 / 下筆 / 末筆切換；
/// 新增、修改時輸入完按確定寫回資料表，按取消放棄。
/// 有單身 Grid 時（類似 4GL t 類程式）：Grid 只列出目前單頭的明細，按「單身」後可在 Grid 新增、修改、刪除明細
/// </summary>
public class RuntimeForm : Form, IRecordNavigator
{
    /// <summary>畫面目前的狀態：瀏覽（不可輸入）、輸入查詢條件、新增、修改</summary>
    private enum Mode { Browse, Query, Insert, Modify, Detail }

    private readonly FormDefinition def;
    private readonly Dictionary<FieldDefinition, Control> inputs = new();
    private Mode mode = Mode.Browse;
    /// <summary>查詢結果；current 是目前顯示第幾筆（0 起算，-1 = 沒有資料）</summary>
    private DataTable results = new();
    private int current = -1;

    private readonly ToolStripButton btnQuery, btnInsert, btnModify, btnDelete, btnDetail;

    // 單身
    private readonly GridDefinition gridDef;
    private readonly DataGridView detailGrid;
    private DataTable detailTable = new();

    // 單頭清單：列出查詢結果，點選一列切換單頭
    private readonly GridDefinition listDef;
    private readonly DataGridView listGrid;
    private bool syncingList;                  // 程式在同步選取列時，不要觸發「切換單頭」
    private readonly ToolStripButton btnFirst, btnPrevious, btnNext, btnLast;
    private readonly ToolStripButton btnOk, btnCancel, btnExit;
    private readonly ToolStripSeparator sepNavigate;
    /// <summary>後台設計的按鈕，以及對應的控制項</summary>
    private readonly List<(ButtonDefinition Def, Button Control)> customButtons = new();
    private readonly ToolStripStatusLabel lblMode = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel lblPosition = new();

    public RuntimeForm(FormDefinition definition)
    {
        def = definition;
        Text = def.Title;
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;

        var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4) };
        ToolStripButton Add(string text, Action action)
        {
            var b = new ToolStripButton(text, null, (s, e) => action()) { DisplayStyle = ToolStripItemDisplayStyle.Text, AutoSize = false, Width = 64, Height = 28 };
            toolStrip.Items.Add(b);
            return b;
        }
        btnQuery = Add("查詢", BeginQuery);
        btnInsert = Add("新增", BeginInsert);
        btnModify = Add("修改", BeginModify);
        btnDelete = Add("刪除", () => DeleteRecord());
        btnDetail = Add("單身", BeginDetail);
        sepNavigate = new ToolStripSeparator();
        toolStrip.Items.Add(sepNavigate);
        btnFirst = Add("首筆", () => MoveTo(0));
        btnPrevious = Add("上筆", () => MoveTo(current - 1));
        btnNext = Add("下筆", () => MoveTo(current + 1));
        btnLast = Add("末筆", () => MoveTo(results.Rows.Count - 1));
        toolStrip.Items.Add(new ToolStripSeparator());
        btnOk = Add("確定", Accept);
        btnCancel = Add("取消", CancelEdit);
        toolStrip.Items.Add(new ToolStripSeparator());
        btnExit = Add("離開", Close);

        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        foreach (var f in def.Fields)
        {
            body.Controls.Add(new Label
            {
                Text = f.Label,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Bounds = new Rectangle(f.X, f.Y + 2, f.LabelWidth, 22),
            });
            Control input = CreateInput(f);
            input.SetBounds(f.X + f.LabelWidth + 4, f.Y, Math.Max(f.Width, 30), 26);
            inputs[f] = input;
            body.Controls.Add(input);
        }

        // 後台從工具箱拖進來的按鈕
        foreach (var b in def.Buttons)
        {
            var button = ButtonRunner.CreateControl(b);
            button.Click += (s, e) => RunButton(b);
            customButtons.Add((b, button));
            body.Controls.Add(button);
        }

        gridDef = def.Grids.FirstOrDefault(g => !g.IsMasterList);
        btnDetail.Visible = gridDef != null;
        if (gridDef != null)
        {
            detailGrid = CreateDetailGrid(gridDef);
            HookEditDialog(detailGrid, EditDetailRow);
        }
        listDef = def.Grids.FirstOrDefault(g => g.IsMasterList);
        if (listDef != null)
        {
            listGrid = CreateDetailGrid(listDef);
            listGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            listGrid.MultiSelect = false;
            listGrid.SelectionChanged += (s, e) => OnListSelectionChanged();
            HookEditDialog(listGrid, EditMasterRow);
            // 空白處雙擊（還沒有資料時）也能開編輯視窗，才有地方按「新增」
            listGrid.DoubleClick += (s, e) =>
            {
                var hit = listGrid.HitTest(listGrid.PointToClient(MousePosition).X, listGrid.PointToClient(MousePosition).Y);
                if (hit.Type == DataGridViewHitTestType.None) EditMasterRow(-1);
            };
            // 有單頭清單時，新增、修改、刪除、首筆～末筆都移到雙擊後的編輯視窗
            foreach (ToolStripItem item in new ToolStripItem[] { btnInsert, btnModify, btnDelete, sepNavigate, btnFirst, btnPrevious, btnNext, btnLast })
                item.Visible = false;
        }
        // 停靠在下方的 Grid：WinForms 是「後加入的先停靠（最外側）」，所以倒著加，
        // 讓先放的 Grid 在最下面，跟設計畫面一樣
        foreach (var g in def.Grids.AsEnumerable().Reverse())
        {
            DataGridView grid = g == gridDef ? detailGrid : g == listDef ? listGrid : null;
            if (grid == null) continue;
            if (g.Dock == GridDock.Bottom)
            {
                grid.Dock = DockStyle.Bottom;
                grid.Height = g.Height;
            }
            body.Controls.Add(grid);
        }

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(lblMode);
        statusStrip.Items.Add(lblPosition);

        Controls.Add(body);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);
        ClientSize = new Size(Math.Max(def.Width + 20, 820), def.Height + 40 + statusStrip.Height);

        ShowCurrent();
        SetMode(Mode.Browse);
    }

    /// <summary>
    /// 輸入時按 Enter = 確定、Esc = 取消（像 4GL 的 accept / cancel）。
    /// 單身輸入時不攔截，讓 Enter / Esc 照常在 Grid 裡換列、取消儲存格編輯
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool headerEditing = mode is Mode.Query or Mode.Insert or Mode.Modify;
        if (headerEditing && keyData == Keys.Escape) { CancelEdit(); return true; }
        if (headerEditing && keyData == Keys.Enter) { Accept(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 模式與按鈕狀態 =================

    private bool HasKey => def.MasterKeys.Count > 0;
    private bool HasCurrent => current >= 0 && current < results.Rows.Count;

    private void SetMode(Mode newMode)
    {
        mode = newMode;
        bool browsing = mode == Mode.Browse;
        btnQuery.Enabled = browsing;
        btnInsert.Enabled = browsing;
        btnModify.Enabled = browsing && HasCurrent && HasKey;
        btnDelete.Enabled = browsing && HasCurrent && HasKey;
        btnDetail.Enabled = browsing && HasCurrent && gridDef != null;
        if (listGrid != null) listGrid.Enabled = browsing;        // 輸入中不能切換單頭
        if (detailGrid != null)
        {
            bool detailEditing = mode == Mode.Detail;
            detailGrid.ReadOnly = !detailEditing;
            detailGrid.AllowUserToAddRows = detailEditing;
            detailGrid.AllowUserToDeleteRows = detailEditing;
            detailGrid.BackgroundColor = detailEditing ? SystemColors.Window : SystemColors.Control;
            // 只顯示、自動編號、關聯欄位（自動帶單頭的值）不能輸入
            foreach (DataGridViewColumn col in detailGrid.Columns)
                if (col.Tag is GridColumnDefinition c && (c.NoEntry || c.IsIdentity || IsLinkColumn(c.Column)))
                    col.ReadOnly = true;
        }
        btnFirst.Enabled = btnPrevious.Enabled = browsing && current > 0;
        btnNext.Enabled = btnLast.Enabled = browsing && current < results.Rows.Count - 1;
        btnOk.Enabled = btnCancel.Enabled = !browsing;
        btnExit.Enabled = browsing;

        foreach (var f in def.Fields)
        {
            // 查詢時每個欄位都能下條件；新增、修改時 NOENTRY 與自動編號不能輸入，修改時主鍵也不能改
            bool editable = mode switch
            {
                Mode.Query => true,
                Mode.Insert => !f.NoEntry && !f.IsIdentity,
                Mode.Modify => !f.NoEntry && !f.IsIdentity && !f.IsKey,
                _ => false,
            };
            if (inputs[f] is TextBox tb) tb.ReadOnly = !editable;
            else inputs[f].Enabled = editable;
        }

        lblMode.Text = mode switch
        {
            Mode.Query => "查詢：輸入條件（文字可用 * 或 ? 萬用字元），按「確定」或 Enter 開始查詢，Esc 取消",
            Mode.Insert => "新增：輸入資料後按「確定」存檔，Esc 取消",
            Mode.Modify => "修改：改完後按「確定」存檔，Esc 取消",
            Mode.Detail => "單身：在下方 Grid 新增（最後一列）、修改、刪除（選列後按 Delete）明細，按「確定」存檔、「取消」放棄",
            _ => HasKey ? "" : "這個畫面沒有設定主鍵，只能查詢與新增",
        };
        lblPosition.Text = HasCurrent ? $"第 {current + 1} / {results.Rows.Count} 筆" : "";
        foreach (var (b, button) in customButtons) button.Enabled = IsActionEnabled(b.Action);
        if (mode == Mode.Detail) detailGrid.Focus();
        else if (!browsing) inputs.Values.FirstOrDefault(c => c is TextBox { ReadOnly: false } || c is not TextBox && c.Enabled)?.Focus();
    }

    // ================= 按鈕動作 =================

    private void BeginQuery()
    {
        ClearInputs();
        ClearDetail();
        SetMode(Mode.Query);
    }

    private void BeginInsert()
    {
        ClearInputs();
        ClearDetail();
        SetMode(Mode.Insert);
    }

    /// <summary>輸入查詢條件或新單頭時，先清掉上一筆的單身，避免看起來像是這筆的明細</summary>
    private void ClearDetail()
    {
        if (detailGrid == null) return;
        detailTable = new DataTable();
        detailGrid.DataSource = detailTable;
    }

    /// <summary>修改前先重讀這筆資料，確保改的是資料庫裡最新的內容</summary>
    private void BeginModify()
    {
        if (!HasCurrent) return;
        if (!RefreshCurrentRow())
        {
            MessageBox.Show("這筆資料已不存在，可能已被其他人刪除。", "修改");
            RemoveCurrentRow();
            return;
        }
        SetMode(Mode.Modify);
    }

    private void Accept()
    {
        switch (mode)
        {
            case Mode.Query: RunQuery(); break;
            case Mode.Insert: InsertRecord(); break;
            case Mode.Modify: UpdateRecord(); break;
            case Mode.Detail: SaveDetail(); break;
        }
    }

    /// <summary>取消：放棄輸入，回到原本顯示的那筆</summary>
    private void CancelEdit()
    {
        ShowCurrent();
        SetMode(Mode.Browse);
    }

    private void MoveTo(int index)
    {
        if (index < 0 || index >= results.Rows.Count) return;
        current = index;
        ShowCurrent();
        SetMode(Mode.Browse);
    }

    // ================= 查詢 =================

    /// <summary>有輸入的欄位當條件，查出符合的資料，顯示第一筆</summary>
    private void RunQuery()
    {
        using var cmd = new SqlCommand();
        string where;
        try
        {
            where = BuildQueryCondition(cmd);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }

        var orderColumns = HasKey ? def.MasterKeys.Select(k => k.Column).ToList() : def.Fields.Take(1).Select(f => f.Column).ToList();
        cmd.CommandText = $"SELECT TOP 5000 {SelectColumns()} FROM {def.FullTableName}"
                        + (where != "" ? " WHERE " + where : "")
                        + " ORDER BY " + string.Join(", ", orderColumns.Select(SqlName.Quote));
        var table = new DataTable();
        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            cmd.Connection = conn;
            conn.Open();
            new SqlDataAdapter(cmd).Fill(table);
        }
        catch (Exception ex)
        {
            MessageBox.Show("查詢失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        results = table;
        current = table.Rows.Count > 0 ? 0 : -1;
        ShowCurrent();
        SetMode(Mode.Browse);
        if (table.Rows.Count == 0) MessageBox.Show("查無資料。", "查詢");
        else if (table.Rows.Count >= 5000) MessageBox.Show("資料超過 5000 筆，只取前 5000 筆，請縮小查詢條件。", "查詢");
    }

    /// <summary>組出查詢條件（類似 4GL 的 CONSTRUCT）：文字欄位有 * 或 ? 時用 LIKE，否則用 =</summary>
    private string BuildQueryCondition(SqlCommand cmd)
    {
        var where = new List<string>();
        int i = 0;
        foreach (var f in def.Fields)
        {
            Control c = inputs[f];
            string col = SqlName.Quote(f.Column);
            if (c is TextBox tb && tb.Text.Trim() != "")
            {
                string text = tb.Text.Trim();
                if (TypeHelper.IsString(f.DataType))
                {
                    bool wildcard = text.Contains('*') || text.Contains('?');
                    where.Add(wildcard ? $"{col} LIKE @q{i}" : $"{col} = @q{i}");
                    cmd.Parameters.AddWithValue($"@q{i}", wildcard ? text.Replace('*', '%').Replace('?', '_') : text);
                }
                else
                {
                    try
                    {
                        cmd.Parameters.AddWithValue($"@q{i}", TypeHelper.Parse(text, f.DataType));
                    }
                    catch (Exception)
                    {
                        throw new FormatException($"「{f.Label}」的查詢條件格式不正確：{text}");
                    }
                    where.Add($"{col} = @q{i}");
                }
                i++;
            }
            else if (c is DateTimePicker dtp && dtp.ShowCheckBox && dtp.Checked)
            {
                where.Add($"CAST({col} AS date) = @q{i}");
                cmd.Parameters.AddWithValue($"@q{i}", dtp.Value.Date);
                i++;
            }
            else if (c is ComboBox && InputHelper.GetValue(f, c) is var comboValue && comboValue != DBNull.Value
                     && !(comboValue is string cs && cs == ""))
            {
                // 下拉選單有選值就當條件（選空白 = 不限）
                where.Add($"{col} = @q{i}");
                cmd.Parameters.AddWithValue($"@q{i}", comboValue);
                i++;
            }
        }
        return string.Join(" AND ", where);
    }

    // ================= 後台設計的按鈕 =================

    /// <summary>按鈕能不能按：內建動作跟工具列對應的按鈕一樣；執行SQL、開啟畫面只在瀏覽時可以按</summary>
    private bool IsActionEnabled(ButtonAction action) => action switch
    {
        ButtonAction.查詢 => btnQuery.Enabled,
        ButtonAction.新增 => btnInsert.Enabled,
        ButtonAction.修改 => btnModify.Enabled,
        ButtonAction.刪除 => btnDelete.Enabled,
        ButtonAction.首筆 => btnFirst.Enabled,
        ButtonAction.上筆 => btnPrevious.Enabled,
        ButtonAction.下筆 => btnNext.Enabled,
        ButtonAction.末筆 => btnLast.Enabled,
        ButtonAction.確定 => btnOk.Enabled,
        ButtonAction.取消 => btnCancel.Enabled,
        ButtonAction.單身 => btnDetail.Enabled,
        ButtonAction.離開 => btnExit.Enabled,
        _ => mode == Mode.Browse,
    };

    private void RunButton(ButtonDefinition b)
    {
        if (!ButtonRunner.Confirm(b)) return;
        switch (b.Action)
        {
            case ButtonAction.查詢: BeginQuery(); break;
            case ButtonAction.新增: BeginInsert(); break;
            case ButtonAction.修改: BeginModify(); break;
            case ButtonAction.刪除: DeleteRecord(); break;
            case ButtonAction.首筆: MoveTo(0); break;
            case ButtonAction.上筆: MoveTo(current - 1); break;
            case ButtonAction.下筆: MoveTo(current + 1); break;
            case ButtonAction.末筆: MoveTo(results.Rows.Count - 1); break;
            case ButtonAction.確定: Accept(); break;
            case ButtonAction.取消: CancelEdit(); break;
            case ButtonAction.單身: BeginDetail(); break;
            case ButtonAction.離開: Close(); break;
            case ButtonAction.執行SQL: RunCustomSql(b); break;
            case ButtonAction.開啟畫面: OpenOtherForm(b); break;
        }
    }

    /// <summary>
    /// 執行按鈕設定的 SQL。@欄位名稱 帶入目前這筆的值：
    /// 先找查詢結果裡的欄位，沒有就找畫面上的輸入欄位；都沒有就是 NULL
    /// </summary>
    private void RunCustomSql(ButtonDefinition b)
    {
        bool ok = ButtonRunner.RunSql(b, def.ConnectionString, name =>
        {
            if (HasCurrent && results.Columns.Contains(name)) return results.Rows[current][name];
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            return field != null ? GetValue(field) : null;
        });
        // 資料可能被 SQL 改過，重讀目前這筆
        if (ok && HasCurrent && HasKey)
        {
            RefreshCurrentRow();
            ShowCurrent();
            SetMode(mode);
        }
    }

    private void OpenOtherForm(ButtonDefinition b) => ButtonRunner.OpenForm(b, def.SourcePath, this);

    /// <summary>單頭要查的欄位：畫面欄位，加上單身關聯、單頭清單會用到但沒放在畫面上的單頭欄位</summary>
    private string SelectColumns() =>
        string.Join(", ", def.Fields.Select(f => f.Column)
            .Concat(gridDef?.Links.Select(l => l.Master) ?? Enumerable.Empty<string>())
            .Concat(listDef?.Columns.Select(c => c.Column) ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SqlName.Quote));

    // ================= 新增、修改、刪除 =================

    private void InsertRecord()
    {
        var values = CollectValues();
        if (values == null) return;
        if (InsertValues(values.ToDictionary(v => v.Key.Column, v => v.Value)) && gridDef != null && HasCurrent)
            BeginDetail();                                 // 像 4GL：單頭存好直接進單身
    }

    /// <summary>新增一筆單頭（key = 欄位名稱）；成功後讀回這筆、加到查詢結果並設為目前這筆</summary>
    private bool InsertValues(Dictionary<string, object> values)
    {
        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var list = values.ToList();
            for (int i = 0; i < list.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", list[i].Value);
            cmd.CommandText = $"INSERT INTO {def.FullTableName} ({string.Join(", ", list.Select(v => SqlName.Quote(v.Key)))}) "
                            + $"VALUES ({string.Join(", ", list.Select((v, i) => $"@p{i}"))}); "
                            + "SELECT CAST(SCOPE_IDENTITY() AS bigint);";
            object identity = cmd.ExecuteScalar();

            // 新增成功後，用主鍵把這筆讀回來，加到查詢結果並顯示
            if (HasKey)
            {
                // 主鍵值：自動編號用資料庫產生的值，其他用剛輸入的值（主鍵沒有輸入時找不到，就讀不回來）
                var key = def.MasterKeys.ToDictionary(k => k.Column, k => k.IsIdentity ? identity
                    : values.FirstOrDefault(v => v.Key.Equals(k.Column, StringComparison.OrdinalIgnoreCase)).Value ?? DBNull.Value);
                DataRow row = LoadRowByKey(key);
                if (row != null)
                {
                    if (results.Columns.Count == 0) results = row.Table.Clone();
                    results.ImportRow(row);
                    current = results.Rows.Count - 1;
                }
            }
            ShowCurrent();
            SetMode(Mode.Browse);
            MessageBox.Show("新增完成。", "新增");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("新增失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void UpdateRecord()
    {
        var values = CollectValues();
        if (values == null) return;
        // 修改模式不改主鍵
        UpdateValues(values.Where(v => !v.Key.IsKey).ToDictionary(v => v.Key.Column, v => v.Value));
    }

    /// <summary>修改目前這筆單頭（key = 欄位名稱，不含主鍵）；成功後重讀並顯示</summary>
    private bool UpdateValues(Dictionary<string, object> values)
    {
        if (values.Count == 0)
        {
            SetMode(Mode.Browse);
            return true;
        }
        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var list = values.ToList();
            for (int i = 0; i < list.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", list[i].Value);
            cmd.CommandText = $"UPDATE {def.FullTableName} SET {string.Join(", ", list.Select((v, i) => $"{SqlName.Quote(v.Key)} = @p{i}"))} "
                            + "WHERE " + AddKeyCondition(cmd, CurrentKey());
            if (cmd.ExecuteNonQuery() == 0)
            {
                MessageBox.Show("找不到原本的資料，可能已被其他人刪除。", "修改");
                return false;
            }
            RefreshCurrentRow();
            ShowCurrent();
            SetMode(Mode.Browse);
            MessageBox.Show("修改完成。", "修改");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("修改失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>刪除目前這筆（會先詢問；有單身時連同明細一起刪）；回傳是否真的刪除了</summary>
    private bool DeleteRecord()
    {
        if (!HasCurrent || !HasKey) return false;
        bool withDetail = gridDef != null && gridDef.Links.Count > 0;
        string question = withDetail ? "確定要刪除這筆資料（連同單身明細）嗎？" : "確定要刪除這筆資料嗎？";
        if (MessageBox.Show(question, "刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();             // 單身、單頭一起刪，失敗就全部不變
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            if (withDetail)
            {
                cmd.CommandText = $"DELETE FROM {gridDef.FullTableName} WHERE " + AddDetailCondition(cmd);
                cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();
            }
            cmd.CommandText = $"DELETE FROM {def.FullTableName} WHERE " + AddKeyCondition(cmd, CurrentKey());
            int rows = cmd.ExecuteNonQuery();
            tran.Commit();
            RemoveCurrentRow();
            MessageBox.Show(rows > 0 ? "已刪除。" : "找不到這筆資料，可能已被其他人刪除。", "刪除");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("刪除失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>從查詢結果拿掉目前這筆，改顯示原本的下一筆（刪的是最後一筆就顯示新的最後一筆）</summary>
    private void RemoveCurrentRow()
    {
        if (!HasCurrent) return;
        results.Rows.RemoveAt(current);
        if (current >= results.Rows.Count) current = results.Rows.Count - 1;
        ShowCurrent();
        SetMode(Mode.Browse);
    }

    /// <summary>取出要寫回資料庫的欄位值並檢查必填；有問題時顯示訊息並回傳 null</summary>
    private Dictionary<FieldDefinition, object> CollectValues()
    {
        var values = new Dictionary<FieldDefinition, object>();
        try
        {
            foreach (var f in def.Fields.Where(f => !f.NoEntry && !f.IsIdentity))
                values[f] = GetValue(f);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return null;
        }
        var empty = values.Where(v => (v.Value == DBNull.Value && !v.Key.IsNullable)
                                   || (v.Key.IsKey && v.Value is string s && s.Trim() == ""))
                          .Select(v => v.Key.Label).ToList();
        if (empty.Count > 0)
        {
            MessageBox.Show("請輸入：" + string.Join("、", empty), "提醒");
            return null;
        }
        if (values.Count == 0)
        {
            MessageBox.Show("畫面上沒有可以存檔的欄位。", "提醒");
            return null;
        }
        return values;
    }

    // ================= 讀取單筆 =================

    private Dictionary<string, object> CurrentKey() =>
        def.MasterKeys.ToDictionary(k => k.Column, k => results.Rows[current][k.Column]);

    private DataRow LoadRowByKey(Dictionary<string, object> key)
    {
        using var conn = new SqlConnection(def.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns()} FROM {def.FullTableName} WHERE " + AddKeyCondition(cmd, key);
        var table = new DataTable();
        new SqlDataAdapter(cmd).Fill(table);
        return table.Rows.Count > 0 ? table.Rows[0] : null;
    }

    /// <summary>用主鍵重讀目前這筆，更新到查詢結果裡；資料已不存在時回傳 false</summary>
    private bool RefreshCurrentRow()
    {
        if (!HasCurrent || !HasKey) return HasCurrent;
        try
        {
            DataRow fresh = LoadRowByKey(CurrentKey());
            if (fresh == null) return false;
            foreach (DataColumn col in fresh.Table.Columns) results.Rows[current][col.ColumnName] = fresh[col.ColumnName];
            ShowCurrent();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取資料失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>組出「主鍵 = 值」條件，參數名稱用 @k0、@k1…</summary>
    private static string AddKeyCondition(SqlCommand cmd, Dictionary<string, object> key)
    {
        var conditions = new List<string>();
        int i = 0;
        foreach (var k in key)
        {
            conditions.Add($"{SqlName.Quote(k.Key)} = @k{i}");
            cmd.Parameters.AddWithValue($"@k{i}", k.Value ?? DBNull.Value);
            i++;
        }
        return string.Join(" AND ", conditions);
    }

    // ================= 欄位控制項 =================

    private void ShowCurrent()
    {
        if (HasCurrent)
            foreach (var f in def.Fields) SetValue(f, results.Rows[current][f.Column]);
        else
            ClearInputs();
        LoadDetail();
        SyncList();
    }

    private void ClearInputs()
    {
        foreach (var f in def.Fields) SetValue(f, null);
    }

    // ================= 單身 =================

    private bool IsLinkColumn(string detailColumn) =>
        gridDef != null && gridDef.Links.Any(l => string.Equals(l.Detail, detailColumn, StringComparison.OrdinalIgnoreCase));

    private static DataGridView CreateDetailGrid(GridDefinition g)
    {
        var grid = new DataGridView
        {
            Bounds = new Rectangle(g.X, g.Y, g.Width, g.Height),
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect,
            RowHeadersWidth = 30,
        };
        foreach (var c in g.Columns.Where(c => c.Visible))
        {
            DataGridViewColumn col = TypeHelper.IsBool(c.DataType) ? new DataGridViewCheckBoxColumn() : new DataGridViewTextBoxColumn();
            col.DataPropertyName = c.Column;
            col.HeaderText = c.Header;
            col.Width = c.Width;
            col.Tag = c;
            col.SortMode = DataGridViewColumnSortMode.NotSortable;   // 不排序，列的順序才會跟資料一致
            if (TypeHelper.IsNumber(c.DataType)) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (TypeHelper.IsDate(c.DataType)) col.DefaultCellStyle.Format = c.DataType == "date" ? "yyyy/MM/dd" : "yyyy/MM/dd HH:mm:ss";
            if (col is DataGridViewTextBoxColumn tc && TypeHelper.IsString(c.DataType) && c.MaxLength > 0) tc.MaxInputLength = c.MaxLength;
            grid.Columns.Add(col);
        }
        grid.DataError += (s, e) =>
        {
            string header = grid.Columns[e.ColumnIndex].HeaderText;
            MessageBox.Show($"第 {e.RowIndex + 1} 列「{header}」的格式不正確。", "提醒");
            e.ThrowException = false;
        };
        return grid;
    }

    /// <summary>讀取目前單頭的明細（單身關聯欄位 = 單頭的值）</summary>
    private void LoadDetail()
    {
        if (gridDef == null) return;
        var table = new DataTable();
        if (HasCurrent)
        {
            try
            {
                using var conn = new SqlConnection(def.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                string where = AddDetailCondition(cmd);
                var orderColumns = gridDef.KeyColumns.Count > 0 ? gridDef.KeyColumns : gridDef.Columns.Take(1).ToList();
                cmd.CommandText = $"SELECT TOP 5000 {string.Join(", ", gridDef.Columns.Select(c => SqlName.Quote(c.Column)))} FROM {gridDef.FullTableName}"
                                + (where != "" ? " WHERE " + where : "")
                                + " ORDER BY " + string.Join(", ", orderColumns.Select(c => SqlName.Quote(c.Column)));
                new SqlDataAdapter(cmd).Fill(table);
            }
            catch (Exception ex)
            {
                MessageBox.Show("讀取單身失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // 新增明細時，關聯欄位自動帶入目前單頭的值
        table.TableNewRow += (s, e) =>
        {
            if (!HasCurrent) return;
            foreach (var (master, detail) in gridDef.Links)
                if (e.Row.Table.Columns.Contains(detail))
                    e.Row[detail] = results.Rows[current][master];
        };
        detailTable = table;
        detailGrid.DataSource = detailTable;
    }

    /// <summary>組出「單身關聯欄位 = 目前單頭的值」條件，參數名稱用 @m0、@m1…；沒有關聯欄位時回傳空字串</summary>
    private string AddDetailCondition(SqlCommand cmd)
    {
        var conditions = new List<string>();
        int i = 0;
        foreach (var (master, detail) in gridDef.Links)
        {
            conditions.Add($"{SqlName.Quote(detail)} = @m{i}");
            cmd.Parameters.AddWithValue($"@m{i}", results.Rows[current][master]);
            i++;
        }
        return string.Join(" AND ", conditions);
    }

    private void BeginDetail()
    {
        if (gridDef == null || !HasCurrent) return;
        SetMode(Mode.Detail);
    }

    /// <summary>
    /// 單身存檔：新增的列 INSERT、改過的列 UPDATE、刪掉的列 DELETE，全部在同一個交易裡，
    /// 任何一筆失敗就全部不變（像 4GL 單身的 BEGIN WORK / COMMIT WORK）
    /// </summary>
    private void SaveDetail()
    {
        detailGrid.EndEdit();
        if (BindingContext[detailTable] is CurrencyManager cm) cm.EndCurrentEdit();
        var changed = detailTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Unchanged).ToList();
        if (changed.Count == 0)
        {
            SetMode(Mode.Browse);
            return;
        }
        var keys = gridDef.KeyColumns;
        if (keys.Count == 0 && changed.Any(r => r.RowState is DataRowState.Modified or DataRowState.Deleted))
        {
            MessageBox.Show("單身沒有設定主鍵，只能新增明細，不能修改或刪除。請按「取消」放棄修改的部分。", "提醒");
            return;
        }

        // 必填檢查：顯示在 Grid 上、不允許 NULL 的欄位
        foreach (var r in changed.Where(r => r.RowState != DataRowState.Deleted))
        {
            var empty = gridDef.Columns.FirstOrDefault(c => c.Visible && !c.IsNullable && !c.IsIdentity && !c.NoEntry && r[c.Column] == DBNull.Value);
            if (empty != null)
            {
                MessageBox.Show($"第 {detailTable.Rows.IndexOf(r) + 1} 列請輸入「{empty.Header}」。", "提醒");
                return;
            }
        }

        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();
            foreach (var r in changed)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tran;
                switch (r.RowState)
                {
                    case DataRowState.Added:
                    {
                        // 空值不寫，讓資料庫預設值生效；關聯欄位一定要寫
                        var cols = gridDef.Columns.Where(c => !c.IsIdentity && (IsLinkColumn(c.Column) || (!c.NoEntry && r[c.Column] != DBNull.Value))).ToList();
                        for (int i = 0; i < cols.Count; i++) cmd.Parameters.AddWithValue($"@p{i}", r[cols[i].Column]);
                        cmd.CommandText = $"INSERT INTO {gridDef.FullTableName} ({string.Join(", ", cols.Select(c => SqlName.Quote(c.Column)))}) "
                                        + $"VALUES ({string.Join(", ", cols.Select((c, i) => $"@p{i}"))})";
                        break;
                    }
                    case DataRowState.Modified:
                    {
                        var cols = gridDef.Columns.Where(c => !c.IsIdentity && !c.NoEntry && !c.IsKey && !IsLinkColumn(c.Column)).ToList();
                        if (cols.Count == 0) continue;
                        for (int i = 0; i < cols.Count; i++) cmd.Parameters.AddWithValue($"@p{i}", r[cols[i].Column]);
                        cmd.CommandText = $"UPDATE {gridDef.FullTableName} SET {string.Join(", ", cols.Select((c, i) => $"{SqlName.Quote(c.Column)} = @p{i}"))} "
                                        + "WHERE " + AddOriginalKeyCondition(cmd, r, keys);
                        break;
                    }
                    case DataRowState.Deleted:
                        cmd.CommandText = $"DELETE FROM {gridDef.FullTableName} WHERE " + AddOriginalKeyCondition(cmd, r, keys);
                        break;
                }
                cmd.ExecuteNonQuery();
            }
            tran.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show("單身儲存失敗（全部未存）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        LoadDetail();
        SetMode(Mode.Browse);
        MessageBox.Show($"單身已儲存（{changed.Count} 筆異動）。", "單身");
    }

    /// <summary>用資料列「改之前」的主鍵值組條件，參數名稱用 @k0、@k1…</summary>
    private static string AddOriginalKeyCondition(SqlCommand cmd, DataRow row, List<GridColumnDefinition> keys)
    {
        var conditions = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            conditions.Add($"{SqlName.Quote(keys[i].Column)} = @k{i}");
            cmd.Parameters.AddWithValue($"@k{i}", row[keys[i].Column, DataRowVersion.Original]);
        }
        return string.Join(" AND ", conditions);
    }

    // ================= Grid 單筆編輯視窗 =================

    /// <summary>
    /// 雙擊某一列、或選好列後按 Enter，開啟單筆編輯視窗。
    /// 單擊保留給選取 / 切換單頭，才不會每點一下就跳出視窗
    /// </summary>
    private void HookEditDialog(DataGridView grid, Action<int> edit)
    {
        grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) edit(e.RowIndex); };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || mode == Mode.Detail || grid.CurrentRow == null) return;
            e.SuppressKeyPress = true;                     // 不要讓 Enter 跳到下一列
            edit(grid.CurrentRow.Index);
        };
    }

    /// <summary>
    /// 單頭清單雙擊：開啟單筆編輯視窗，裡面可以新增、修改、刪除、首筆～末筆；
    /// index = -1 表示在空白處雙擊（沒有資料時也能開來新增）
    /// </summary>
    private void EditMasterRow(int index)
    {
        if (mode != Mode.Browse) return;
        if (index >= 0 && index < results.Rows.Count && index != current) MoveTo(index);
        listDef.EnsureEditLayout();                         // 沒設計過版面時排成一欄
        var fields = listDef.EditColumns
            .Select(c => InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity))
            .ToList();
        // 主鍵：清單欄位的主鍵設定，加上單頭欄位裡勾了主鍵的
        var keyColumns = def.MasterKeys.Select(k => k.Column).ToList();
        foreach (var f in fields) f.IsKey = keyColumns.Contains(f.Column, StringComparer.OrdinalIgnoreCase);
        using var dlg = new RecordNavigatorDialog(this, def.Title, fields, listDef.EditFormSize, def.ConnectionString, listDef.EditButtons, def.SourcePath);
        dlg.ShowDialog(this);
    }

    // ---- 給單筆編輯視窗呼叫（IRecordNavigator）----
    int IRecordNavigator.Count => results.Rows.Count;
    int IRecordNavigator.Current => current;
    DataRow IRecordNavigator.CurrentRow => HasCurrent ? results.Rows[current] : null;
    bool IRecordNavigator.CanModify => HasKey;
    void IRecordNavigator.MoveTo(int index) => MoveTo(index);
    bool IRecordNavigator.Refresh()
    {
        if (RefreshCurrentRow()) return true;
        MessageBox.Show("這筆資料已不存在，可能已被其他人刪除。", "提醒");
        RemoveCurrentRow();
        return false;
    }
    bool IRecordNavigator.Insert(Dictionary<string, object> values) => InsertValues(values);
    bool IRecordNavigator.Update(Dictionary<string, object> values) => UpdateValues(values);
    bool IRecordNavigator.Delete() => DeleteRecord();

    /// <summary>
    /// 單身的某一列：瀏覽時改完直接存回資料表；
    /// 在「單身」輸入中則只改 Grid 上的資料，等按「確定」時跟其他明細一起存
    /// </summary>
    private void EditDetailRow(int index)
    {
        if (gridDef == null || index < 0 || index >= detailGrid.Rows.Count) return;
        if (detailGrid.Rows[index].DataBoundItem is not DataRowView view) return;   // 最後一列的「新增列」還沒有資料
        DataRow row = view.Row;
        bool inDetailMode = mode == Mode.Detail;
        if (!inDetailMode && mode != Mode.Browse) return;
        var keys = gridDef.KeyColumns;
        if (!inDetailMode && keys.Count == 0)
        {
            MessageBox.Show("單身沒有設定主鍵，不能修改明細。", "提醒");
            return;
        }
        // 主鍵只有剛新增、還沒存的明細可以改；關聯欄位、自動編號、只顯示欄位都不能改
        bool keyEditable = inDetailMode && row.RowState == DataRowState.Added;
        gridDef.EnsureEditLayout();
        var fields = gridDef.EditColumns
            .Select(c => InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity || IsLinkColumn(c.Column) || (c.IsKey && !keyEditable)))
            .ToList();
        using var dlg = new RecordEditDialog($"{(inDetailMode ? "編輯" : "修改")}明細：{gridDef.Title ?? gridDef.TableName}", fields, col => row[col],
                                             gridDef.EditFormSize, def.ConnectionString, gridDef.EditButtons, def.SourcePath);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Values.Count == 0) return;

        if (inDetailMode)
        {
            try
            {
                foreach (var (col, value) in dlg.Values) row[col] = value;
            }
            catch (Exception ex)
            {
                MessageBox.Show("資料格式不正確：" + ex.Message, "提醒");
            }
            detailGrid.Refresh();
            return;
        }

        try
        {
            using var conn = new SqlConnection(def.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var list = dlg.Values.ToList();
            for (int i = 0; i < list.Count; i++) cmd.Parameters.AddWithValue($"@p{i}", list[i].Value);
            cmd.CommandText = $"UPDATE {gridDef.FullTableName} SET {string.Join(", ", list.Select((v, i) => $"{SqlName.Quote(v.Key)} = @p{i}"))} "
                            + "WHERE " + AddOriginalKeyCondition(cmd, row, keys);
            if (cmd.ExecuteNonQuery() == 0)
                MessageBox.Show("找不到原本的明細，可能已被其他人刪除。", "修改");
            else
                MessageBox.Show("明細修改完成。", "修改");
        }
        catch (Exception ex)
        {
            MessageBox.Show("修改失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadDetail();
    }

    // ================= 單頭清單 =================

    /// <summary>清單顯示查詢結果，並把目前這筆設為選取列</summary>
    private void SyncList()
    {
        if (listGrid == null) return;
        syncingList = true;
        try
        {
            if (listGrid.DataSource != results) listGrid.DataSource = results;
            listGrid.ClearSelection();
            if (HasCurrent && current < listGrid.Rows.Count)
            {
                var firstVisible = listGrid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
                if (firstVisible != null) listGrid.CurrentCell = listGrid.Rows[current].Cells[firstVisible.Index];
                listGrid.Rows[current].Selected = true;
            }
        }
        finally
        {
            syncingList = false;
        }
    }

    /// <summary>使用者在清單點選另一列：切換單頭（同首筆 / 上筆 / 下筆）</summary>
    private void OnListSelectionChanged()
    {
        if (syncingList || mode != Mode.Browse) return;
        int index = listGrid.CurrentRow?.Index ?? -1;
        if (index >= 0 && index < results.Rows.Count && index != current) MoveTo(index);
    }

    private Control CreateInput(FieldDefinition f) => InputHelper.CreateInput(f, def.ConnectionString);

    private object GetValue(FieldDefinition f) => InputHelper.GetValue(f, inputs[f]);

    private void SetValue(FieldDefinition f, object value) => InputHelper.SetValue(inputs[f], value);
}
