using System.Data;
using System.Data.Common;

namespace FormBuilder;

/// <summary>
/// 使用者操作的畫面（類似 4GL 的 i 類單檔維護程式）：
/// 查詢 → 輸入條件 → 確定，查到的資料用首筆 / 上筆 / 下筆 / 末筆切換；
/// 新增、修改時輸入完按確定寫回資料表，按取消放棄。
/// 有單身 Grid 時（類似 4GL t 類程式）：Grid 只列出目前單頭的明細，按「單身」後可在 Grid 新增、修改、刪除明細
/// </summary>
public partial class RuntimeForm : Form, IRecordNavigator
{
    /// <summary>畫面目前的狀態：瀏覽（不可輸入）、輸入查詢條件、新增、修改</summary>
    private enum Mode { Browse, Query, Insert, Modify, Detail }

    private readonly FormDefinition def;
    private readonly Dictionary<FieldDefinition, Control> inputs = new();
    private Mode mode = Mode.Browse;
    /// <summary>查詢結果；current 是目前顯示第幾筆（0 起算，-1 = 沒有資料）</summary>
    private DataTable results = new();
    private int current = -1;

    /// <summary>一個單身：Grid 與它目前的明細資料</summary>
    private sealed class DetailPart
    {
        public GridDefinition Def { get; init; }
        public DataGridView Grid { get; init; }
        public DataTable Table { get; set; } = new();
    }

    // 單身：可以有好幾個（各自的資料表與關聯欄位），按「單身」時全部一起輸入、一起存檔
    private readonly List<DetailPart> details = new();
    private bool HasDetail => details.Count > 0;

    // 單頭清單：列出查詢結果，點選一列切換單頭
    private readonly GridDefinition listDef;
    private readonly DataGridView listGrid;
    private bool syncingList;                  // 程式在同步選取列時，不要觸發「切換單頭」

    // SQL 清單：資料來自後台寫的 SELECT（唯讀），切換單頭時用 @單頭欄位 重查
    private readonly List<(GridDefinition Def, DataGridView Grid)> sqlGrids = new();
    /// <summary>後台設計的按鈕，以及對應的控制項</summary>
    private readonly List<(ButtonDefinition Def, Button Control)> customButtons = new();
    /// <summary>查詢模式是由哪個「自訂查詢」按鈕開始的；null = 內建查詢</summary>
    private ButtonDefinition customQuery;

    /// <summary>選取視窗模式（別的畫面的「開啟畫面」按鈕設了回傳對應）：雙擊清單或按「選取帶回」就關閉</summary>
    private readonly bool pickMode;
    /// <summary>開啟時帶進來的參數（「傳入參數」）；同名的欄位當查詢條件，執行SQL 的 @名稱 也查得到</summary>
    private readonly Dictionary<string, object> openParams;
    /// <summary>選取視窗模式下使用者選的那筆</summary>
    public DataRow PickedRow { get; private set; }
    /// <summary>查詢條件畫面模式（別的畫面的「開啟查詢條件畫面」按鈕）：輸入條件按「查詢」就關閉，把條件帶回去，不在這裡查</summary>
    private readonly bool conditionMode;
    /// <summary>查詢條件畫面上的「全部符合 / 任一符合」</summary>
    private ToolStripComboBox cboMatch;
    /// <summary>查詢條件畫面模式下使用者按「查詢」帶回的條件</summary>
    public ConditionResult ConditionResult { get; private set; }
    /// <summary>新增、修改、刪除成功後自動關閉（別的畫面的「開啟畫面」按鈕設了「存檔後關閉並重新整理」）</summary>
    public bool CloseAfterSave { get; set; }
    /// <summary>這個視窗開著的期間有沒有存檔成功過（開啟它的畫面據此重新整理）</summary>
    public bool Saved { get; private set; }
    /// <summary>最後存檔那筆的主鍵（刪除時是 null）</summary>
    public Dictionary<string, object> SavedKey { get; private set; }
    /// <summary>上一次查詢的 SQL 與參數：別的畫面存檔後，用同樣的條件重新查詢</summary>
    private (string Sql, List<(string Name, object Value)> Params)? lastQuery;
    /// <summary>目前使用者對這個畫面的權限（沒登入 = 全開）</summary>
    private FormPermission perm = FormPermission.All;

    public RuntimeForm(FormDefinition definition) : this(definition, null, false) { }

    /// <summary>開成查詢條件畫面；matchAny = 預設「任一符合」（OR）</summary>
    public static RuntimeForm ForConditions(FormDefinition definition, bool matchAny) =>
        new(definition, null, pickMode: false, conditionMode: true, matchAny: matchAny);

    public RuntimeForm(FormDefinition definition, IDictionary<string, object> parameters, bool pickMode,
                       bool conditionMode = false, bool matchAny = false)
    {
        InitializeComponent();
        this.pickMode = pickMode;
        this.conditionMode = conditionMode;
        openParams = parameters == null ? new(StringComparer.OrdinalIgnoreCase) : new(parameters, StringComparer.OrdinalIgnoreCase);
        def = definition;
        perm = SysContext.PermissionFor(def.FormKey);                        // 登入時依角色權限；沒登入、系統管理者 = 全開
        // 「編修」權限 = 新增、修改、刪除（主畫面、編輯視窗的按鈕都看這裡；舊資料的新增、刪除勾選不再使用）
        perm = perm with { Insert = perm.Modify, Delete = perm.Modify };
        Text = def.Title;

        // 工具列按鈕在 Designer.cs
        btnQuery.Click += (s, e) => BeginQuery();
        btnInsert.Click += (s, e) => BeginInsert();
        btnModify.Click += (s, e) => BeginModify();
        btnDelete.Click += (s, e) => DeleteRecord();
        btnDetail.Click += (s, e) => BeginDetail();
        btnFirst.Click += (s, e) => MoveTo(0);
        btnPrevious.Click += (s, e) => MoveTo(current - 1);
        btnNext.Click += (s, e) => MoveTo(current + 1);
        btnLast.Click += (s, e) => MoveTo(results.Rows.Count - 1);
        btnOk.Click += (s, e) => Accept();
        btnCancel.Click += (s, e) => CancelEdit();
        btnExit.Click += (s, e) => Close();

        // 欄位、按鈕、Grid 依畫面定義動態產生到 body（body 在 Designer.cs）
        body.BackColor = ColorText.BackOrDefault(def.BackColor);             // 後台設定的畫面底色
        body.Font = def.ScreenFont(body.Font);                               // 後台設定的畫面字型、大小（欄位、Grid、按鈕都跟著）
        Color labelColor = ColorText.LabelColor(def.BackColor);              // 底色深時標籤用白字
        foreach (var f in def.Fields)
        {
            if (!f.IsTextLabel)                              // 文字標籤本身就是文字，不另外放標籤
            body.Controls.Add(new Label
            {
                Text = f.Label,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                ForeColor = labelColor,
                Font = LabelGeometry.Font(f, body.Font),                                              // 標籤字體大小
                Bounds = new Rectangle(f.X, f.Y, f.LabelWidth, LabelGeometry.Height(LabelGeometry.Font(f, body.Font))),   // 字放大時標籤跟著變高
            });
            Control input = CreateInput(f);
            if (input is RadioGroup || f.IsTextLabel) input.ForeColor = labelColor;
            InputHelper.ApplyLook(f, input, body.Font);      // 字體大小、底色
            RoundedStyle.Place(body, input, new Rectangle(f.X + f.LabelWidth + 4, f.Y, Math.Max(f.Width, 30), Math.Max(f.Height, FieldDefinition.DefaultHeight)));   // 圓角外觀
            inputs[f] = input;
        }
        // 欄位的開窗選取：瀏覽時不開（跟選取視窗按鈕一樣，輸入中、查詢中才能用）
        ButtonRunner.AttachPickers(inputs, b => { if (mode != Mode.Browse) OpenOtherForm(b); });
        // 下拉選單的執行後動作：@名稱 先找畫面上的欄位，再找目前這筆、開啟時的傳入參數；查出來的值填回畫面上的欄位
        ButtonRunner.AttachAfterSelect(inputs, def.ConnectionString, name =>
        {
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field != null) return GetValue(field);
            if (HasCurrent && results.Columns.Contains(name)) return results.Rows[current][name];
            return openParams.TryGetValue(name, out var param) ? param : null;
        }, def.FormKey);
        HookJoinRefresh();
        HookFormulas();

        // 後台從工具箱拖進來的按鈕
        foreach (var b in def.Buttons)
        {
            var button = ButtonRunner.CreateControl(b);
            button.Click += (s, e) => RunButton(b);
            customButtons.Add((b, button));
            body.Controls.Add(button);
            ButtonRunner.ApplyFont(b, button);              // 按鈕自己設的字型、大小
        }

        foreach (var g in def.Grids.Where(g => !g.IsMasterList && !g.IsSqlList))
        {
            var part = new DetailPart { Def = g, Grid = CreateDetailGrid(g, def.ConnectionString) };
            HookEditDialog(part.Grid, index => EditDetailRow(part, index));
            details.Add(part);
        }
        btnDetail.Visible = HasDetail;
        foreach (var g in def.Grids.Where(g => g.IsSqlList))
            sqlGrids.Add((g, CreateDetailGrid(g, def.ConnectionString)));   // 唯讀
        listDef = def.Grids.FirstOrDefault(g => g.IsMasterList && !g.IsSqlList);
        if (listDef != null)
        {
            listGrid = CreateDetailGrid(listDef, def.ConnectionString);
            listGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            listGrid.MultiSelect = true;                     // 按住 Ctrl / Shift 可以選多筆，右鍵選單批次刪除、生效
            SetupListBatchMenu();
            listGrid.SelectionChanged += (s, e) => OnListSelectionChanged();
            HookEditDialog(listGrid, EditMasterRow);
            if (listDef.AllowInsert) SetupListInsert();
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
            DataGridView grid = details.FirstOrDefault(p => p.Def == g)?.Grid ?? (g == listDef ? listGrid : sqlGrids.FirstOrDefault(x => x.Def == g).Grid);
            if (grid == null) continue;
            if (g.Dock == GridDock.Bottom)
            {
                grid.Dock = DockStyle.Bottom;
                grid.Height = g.Height;
            }
            body.Controls.Add(RoundedGridHost.Wrap(grid));      // 圓角外框（位置、停靠方式由外框接手）
        }

        ClientSize = new Size(Math.Max(def.Width + 20, 820), def.Height + 40 + statusStrip.Height);

        // 沒有單頭資料表（只放 SQL 清單的畫面）：沒有資料可以查詢、新增、修改，只留「離開」，SQL 清單打開就查
        if (!HasMasterTable)
            foreach (ToolStripItem item in new ToolStripItem[] { btnQuery, btnInsert, btnModify, btnDelete, btnDetail, sepNavigate, btnFirst, btnPrevious, btnNext, btnLast, sepEdit, btnOk, btnCancel, sepExit })
                item.Visible = false;

        if (pickMode) SetupPickMode();
        if (conditionMode) SetupConditionMode(matchAny);
        ShowCurrent();
        SetMode(Mode.Browse);
        // 查詢條件畫面：打開就是輸入條件
        if (conditionMode)
            Shown += (s, e) => BeginQuery();
        // 有傳入參數時，打開後用同名的欄位當條件自動查詢
        else if (openParams.Keys.Any(k => def.Fields.Any(f => f.Column.Equals(k, StringComparison.OrdinalIgnoreCase))))
            Shown += (s, e) => QueryByParams();
        // 只放單頭清單、沒有可以輸入查詢條件的單頭欄位：打開就列出資料
        else if (HasMasterTable && listDef != null && def.Fields.All(f => f.IsCalculated))
            Shown += (s, e) => RunQuery();
    }

    /// <summary>選取視窗：工具列最前面加「選取帶回」，視窗標題加上「選取：」</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 一起關掉這個畫面開的編輯視窗；有視窗還在輸入、使用者選擇不放棄時，主畫面也不關
        foreach (var (dialog, _) in editWindows.ToList())
        {
            if (dialog.IsDisposed) continue;
            dialog.Close();
            if (!dialog.IsDisposed)
            {
                e.Cancel = true;
                dialog.Activate();
                return;
            }
        }
        base.OnFormClosing(e);
    }

    /// <summary>有單頭資料表；false = 只放 SQL 清單的畫面</summary>
    private bool HasMasterTable => !string.IsNullOrWhiteSpace(def.TableName);

    private void SetupPickMode()
    {
        Text = "選取：" + def.Title;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var btnPick = new ToolStripButton("選取帶回")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Size = new Size(80, 28),
            Font = new Font(toolStrip.Font, FontStyle.Bold),
            ToolTipText = "把目前選的這筆帶回原來的畫面（在清單上雙擊也可以）",
        };
        btnPick.Click += (s, e) => Pick();
        // 只放 SQL 清單的畫面：在清單上雙擊一列 = 選這筆帶回
        if (!HasMasterTable)
            foreach (var (_, grid) in sqlGrids)
                grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Pick(); };
        toolStrip.Items.Insert(0, btnPick);
        toolStrip.Items.Insert(1, new ToolStripSeparator());
        btnExit.Text = "取消";
    }

    /// <summary>查詢條件畫面：工具列只留「查詢」（確定）、「取消」與條件組合，視窗標題加上「查詢條件：」</summary>
    private void SetupConditionMode(bool matchAny)
    {
        Text = "查詢條件：" + def.Title;
        ShowInTaskbar = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        foreach (ToolStripItem item in toolStrip.Items) item.Visible = false;
        btnOk.Visible = btnCancel.Visible = true;
        btnOk.Text = "查詢";
        btnOk.ToolTipText = "依輸入的條件查詢，結果放到原來畫面的清單（Enter）";
        btnCancel.ToolTipText = "不查詢，關閉這個視窗（Esc）";
        cboMatch = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AutoSize = false,
            Size = new Size(160, 28),
            ToolTipText = "有填多個條件時：全部都要符合（AND），還是符合任一個就好（OR）",
        };
        cboMatch.Items.AddRange(new object[] { "全部符合（AND）", "任一符合（OR）" });
        cboMatch.SelectedIndex = matchAny ? 1 : 0;
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("條件："));
        toolStrip.Items.Add(cboMatch);
    }

    /// <summary>查詢條件畫面按「查詢」：把有填的條件與每個欄位的值帶回去，關閉視窗</summary>
    private void ReturnConditions()
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        List<QueryCondition> conditions;
        try
        {
            conditions = CollectConditions();
            // 同一個欄位放了好幾個時，@欄位名稱 用第一個有填的值
            foreach (var f in def.Fields.Where(f => !f.IsCalculated))
            {
                object value = QueryParamValue(f, GetValue(f));
                if (!values.TryGetValue(f.Column, out var had) || had == DBNull.Value || had is string s && s == "")
                    values[f.Column] = value;
            }
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }
        ConditionResult = new ConditionResult { Conditions = conditions, MatchAny = cboMatch.SelectedIndex == 1, Values = values };
        DialogResult = DialogResult.OK;
    }

    /// <summary>選取目前這筆，關閉選取視窗</summary>
    private void Pick()
    {
        if (mode != Mode.Browse) return;
        if (!HasMasterTable)
        {
            // 只放 SQL 清單的畫面：帶回清單上選的那列（有多個清單時先看正在操作的那個）
            var row = sqlGrids.Select(x => x.Grid).OrderByDescending(g => g.ContainsFocus)
                              .Select(g => (g.CurrentRow?.DataBoundItem as DataRowView)?.Row).FirstOrDefault(r => r != null);
            if (row == null)
            {
                MessageBox.Show("請先在清單上選一筆資料。", "選取");
                return;
            }
            PickedRow = row;
            DialogResult = DialogResult.OK;
            return;
        }
        if (!HasCurrent)
        {
            MessageBox.Show("請先查詢並選一筆資料。", "選取");
            return;
        }
        PickedRow = results.Rows[current];
        DialogResult = DialogResult.OK;
    }

    /// <summary>傳入參數裡跟欄位同名的，當查詢條件查一次</summary>
    private void QueryByParams()
    {
        BeginQuery();
        foreach (var f in def.Fields)
            if (openParams.TryGetValue(f.Column, out var value)) SetValue(f, value);
        RunQuery();
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
    /// <summary>這個畫面連的資料庫的 SQL 方言</summary>
    private DbDialect Dialect => def.Dialect;
    private bool HasCurrent => current >= 0 && current < results.Rows.Count;

    private void SetMode(Mode newMode)
    {
        mode = newMode;
        if (mode != Mode.Query) customQuery = null;
        bool browsing = mode == Mode.Browse;
        btnQuery.Enabled = browsing;
        // 權限：沒有新增 / 修改 / 刪除權限的按鈕不能按（單身輸入算修改）
        btnInsert.Enabled = browsing && perm.Insert;
        btnModify.Enabled = browsing && HasCurrent && HasKey && perm.Modify;
        btnDelete.Enabled = browsing && HasCurrent && HasKey && perm.Delete;
        btnDetail.Enabled = browsing && HasCurrent && HasDetail && perm.Modify;
        if (listGrid != null)
        {
            listGrid.Enabled = browsing;                          // 輸入中不能切換單頭
            listGrid.AllowUserToAddRows = CanInsertInList;
        }
        foreach (var part in details)
        {
            bool detailEditing = mode == Mode.Detail;
            part.Grid.ReadOnly = !detailEditing;
            part.Grid.AllowUserToAddRows = detailEditing;
            part.Grid.AllowUserToDeleteRows = detailEditing;
            part.Grid.BackgroundColor = detailEditing ? SystemColors.Window : SystemColors.Control;
            // 只顯示、自動編號、關聯欄位（自動帶單頭的值）不能輸入
            foreach (DataGridViewColumn col in part.Grid.Columns)
                if (col.Tag is GridColumnDefinition c && (c.NoEntry || c.IsIdentity || IsLinkColumn(part, c.Column)))
                    col.ReadOnly = true;
        }
        btnFirst.Enabled = btnPrevious.Enabled = browsing && current > 0;
        btnNext.Enabled = btnLast.Enabled = browsing && current < results.Rows.Count - 1;
        btnOk.Enabled = btnCancel.Enabled = !browsing;
        btnExit.Enabled = browsing;

        foreach (var f in def.Fields.Where(f => !f.IsTextLabel))        // 文字標籤一直是顯示文字，不用切換能不能輸入
        {
            // 查詢時每個欄位都能下條件（計算欄位除外）；新增、修改時 NOENTRY、唯讀、自動編號、單頭 JOIN 與有公式的欄位不能輸入（唯讀、公式的值照樣存檔），修改時主鍵也不能改
            bool editable = mode switch
            {
                Mode.Query => !f.IsCalculated,
                Mode.Insert => !f.NoEntry && !f.IsIdentity && !f.IsReadOnly && !f.IsJoin && !f.HasFormula && !f.QueryOnly,
                Mode.Modify => !f.NoEntry && !f.IsIdentity && !f.IsKey && !f.IsReadOnly && !f.IsJoin && !f.HasFormula && !f.QueryOnly,
                _ => false,
            };
            if (inputs[f] is TextBox tb) tb.ReadOnly = !editable;
            else inputs[f].Enabled = editable;
        }

        lblMode.Text = mode switch
        {
            Mode.Query when conditionMode => "輸入查詢條件（文字可用 * 或 ? 萬用字元），按「查詢」或 Enter 帶回原畫面查詢，Esc 取消",
            Mode.Query when customQuery != null => $"查詢：輸入條件後，再按一次「{customQuery.Text}」、「確定」或 Enter 開始查詢，Esc 取消",
            Mode.Query => "查詢：輸入條件（文字可用 * 或 ? 萬用字元），按「確定」或 Enter 開始查詢，Esc 取消",
            Mode.Insert => "新增：輸入資料後按「確定」存檔，Esc 取消",
            Mode.Modify => "修改：改完後按「確定」存檔，Esc 取消",
            Mode.Detail => "單身：在下方 Grid 新增（最後一列）、修改、刪除（選列後按 Delete）明細，按「確定」存檔、「取消」放棄",
            _ => HasKey || !HasMasterTable ? "" : "這個畫面沒有設定主鍵，只能查詢與新增",   // 只放 SQL 清單的畫面不提示
        };
        lblPosition.Text = HasCurrent ? $"第 {current + 1} / {results.Rows.Count} 筆" : "";
        // 選取視窗按鈕（有回傳對應）要在輸入中才能按，帶回的值填進正在輸入的欄位
        foreach (var (b, button) in customButtons)
            button.Enabled = ButtonRunner.Allowed(b, perm)                 // 按鈕「需要的權限」：沒有就一直是灰的
                && (ButtonRunner.IsPicker(b) ? mode is Mode.Insert or Mode.Modify or Mode.Query : IsActionEnabled(b.Action));
        RecalcFormulas();                                  // 進入新增、修改時算出有公式的欄位；查詢時清空計算欄位
        if (mode == Mode.Detail) details[0].Grid.Focus();
        else if (!browsing) inputs.Values.FirstOrDefault(c => c is TextBox { ReadOnly: false } || c is not TextBox && c.Enabled)?.Focus();
    }

    // ================= 按鈕動作 =================

    private void BeginQuery(ButtonDefinition custom = null)
    {
        ClearInputs();
        ClearDetail();
        SetMode(Mode.Query);
        customQuery = custom;
        SetMode(Mode.Query);                               // 再設一次，更新狀態列與按鈕
    }

    private void BeginInsert()
    {
        ClearInputs();
        ClearDetail();
        DefaultValue.FillInputs(inputs);                                          // 欄位的預設值
        NumberRule.FillInputs(inputs, def.ConnectionString, def.FullTableName);   // 有編號規則的欄位自動帶入下一個編號
        FillParams();                                                             // 別的畫面開啟時帶進來的值（例如廠商編號）直接帶入
        SetMode(Mode.Insert);
        QueueJoinRefresh(def.Joins);                                              // 預設值帶到關聯欄位時，JOIN 欄位跟著帶出來
    }

    /// <summary>輸入查詢條件或新單頭時，先清掉上一筆的單身，避免看起來像是這筆的明細</summary>
    private void ClearDetail()
    {
        foreach (var part in details)
        {
            part.Table = new DataTable();
            part.Grid.DataSource = part.Table;
        }
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
            case Mode.Query when conditionMode: ReturnConditions(); break;
            case Mode.Query when customQuery != null: RunCustomQuery(customQuery); break;
            case Mode.Query: RunQuery(); break;
            case Mode.Insert: InsertRecord(); break;
            case Mode.Modify: UpdateRecord(); break;
            case Mode.Detail: SaveDetail(); break;
        }
    }

    /// <summary>取消：放棄輸入，回到原本顯示的那筆</summary>
    private void CancelEdit()
    {
        if (conditionMode)                                 // 查詢條件畫面：取消 = 不查詢，關閉
        {
            DialogResult = DialogResult.Cancel;
            return;
        }
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
    private void RunQuery() => RunQuery(BuildQueryCondition);

    /// <summary>buildWhere 組出條件（不含 WHERE；格式不對丟 FormatException），查出符合的資料，顯示第一筆</summary>
    private void RunQuery(Func<DbCommand, string> buildWhere)
    {
        using var cmd = Dialect.CreateCommand();
        string where;
        try
        {
            where = buildWhere(cmd);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }

        // 排序：主鍵；沒有主鍵用第一個單頭欄位，畫面上沒有單頭欄位（只放單頭清單）時用清單的第一個欄位
        var orderColumns = HasKey ? def.MasterKeys.Select(k => GridSql.Base(Dialect, k.Column)).ToList()
            : def.Fields.Where(f => !f.IsCalculated).Take(1).Select(MasterExpr)
                 .Concat(listDef?.Columns.Where(c => !c.IsJoin).Take(1).Select(c => GridSql.Base(Dialect, c.Column)) ?? Enumerable.Empty<string>())
                 .Take(1).ToList();
        cmd.CommandText = $"SELECT {Dialect.Top(5000)}{SelectColumns()} FROM {MasterFrom()}"
                        + (where != "" ? " WHERE " + where : "")
                        + (orderColumns.Count > 0 ? " ORDER BY " + string.Join(", ", orderColumns) : "")
                        + Dialect.Limit(5000);
        LoadResults(cmd);
    }

    /// <summary>
    /// 自訂查詢：執行按鈕設定的 SELECT，@欄位名稱 帶入畫面輸入框的值
    /// （文字欄位沒填 = 空字串，LIKE '%' + @x + '%' 才會查全部；其他型別沒填 = NULL）
    /// </summary>
    private void RunCustomQuery(ButtonDefinition b)
    {
        if (string.IsNullOrWhiteSpace(b.Sql))
        {
            MessageBox.Show($"按鈕「{b.Text}」還沒有設定 SQL。", "提醒");
            return;
        }
        RunSelect(b.Sql, name =>
        {
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            return field == null ? DBNull.Value : QueryParamValue(field, GetValue(field));
        });
    }

    /// <summary>執行使用者寫的 SELECT，結果放到單頭（清單）；@名稱 的值由 valueOf 提供（格式不對丟 FormatException）</summary>
    private void RunSelect(string userSql, Func<string, object> valueOf)
    {
        var (sql, parameters) = Dialect.PrepareUserSql(userSql);     // @欄位名稱 轉成這個資料庫的參數寫法
        using var cmd = Dialect.CreateCommand(sql: sql);
        try
        {
            foreach (var (name, parameter) in parameters)
                Dialect.AddParam(cmd, parameter, valueOf(name));
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }
        LoadResults(cmd);
    }

    /// <summary>查詢用的 @參數值：文字欄位沒填 = 空字串（LIKE '%' + @x + '%' 才會查全部），其他型別沒填 = NULL</summary>
    private static object QueryParamValue(FieldDefinition field, object value) =>
        value == DBNull.Value && TypeHelper.IsString(field.DataType) ? "" : value;

    /// <summary>
    /// 開啟查詢條件畫面：在條件畫面輸入條件按「查詢」後，先清空清單，再依條件查本畫面的資料表。
    /// 按鈕有填 SQL 時改執行那個 SELECT，@欄位名稱 帶入條件畫面上的值
    /// </summary>
    private void QueryByConditionForm(ButtonDefinition b)
    {
        var input = ButtonRunner.AskConditions(b, def.SourcePath, this);
        if (input == null) return;                                    // 取消
        if (!HasMasterTable)
        {
            FilterSqlLists(b, input);
            return;
        }
        results = new DataTable();                                   // 先清空清單，查詢失敗時也不會留著上一次的結果
        current = -1;
        ShowCurrent();
        SetMode(Mode.Browse);
        if (!string.IsNullOrWhiteSpace(b.Sql))
        {
            RunSelect(b.Sql, name => input.Values.TryGetValue(name, out var v) ? v : DBNull.Value);
            return;
        }
        RunQuery(cmd => QueryCondition.ToSql(input.Conditions, cmd, Dialect, c => ColumnExpr(c.Field.Column), input.MatchAny));
    }

    /// <summary>
    /// 只放 SQL 清單的畫面：先清空清單，重查後只留下符合條件的資料列（條件畫面的欄位名稱要跟清單 SELECT 出來的欄位一樣）。
    /// 在程式裡篩選，後台寫的 SELECT（GROUP BY、ORDER BY、@參數）不用改。按鈕有填 SQL 時改把那個 SELECT 的結果放到第一個清單
    /// </summary>
    private void FilterSqlLists(ButtonDefinition b, ConditionResult input)
    {
        foreach (var (_, grid) in sqlGrids) grid.DataSource = null;
        if (!string.IsNullOrWhiteSpace(b.Sql))
        {
            try
            {
                sqlGrids[0].Grid.DataSource = SqlList.Load(def.ConnectionString, b.Sql, name => input.Values.TryGetValue(name, out var v) ? v : DBNull.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show("查詢失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        else
        {
            LoadSqlLists();
            foreach (var (g, grid) in sqlGrids)
            {
                if (grid.DataSource is not DataTable table) continue;      // 讀取失敗（LoadSqlLists 已經提醒過）
                var missing = input.Conditions.Select(c => c.Field.Column).Distinct(StringComparer.OrdinalIgnoreCase)
                                   .Where(c => !table.Columns.Contains(c)).ToList();
                if (missing.Count > 0)
                {
                    MessageBox.Show($"「{g.Title}」的 SQL 沒有這些欄位，不能當條件：{string.Join("、", missing)}", "提醒");
                    grid.DataSource = table.Clone();
                    continue;
                }
                var filtered = table.Clone();
                foreach (DataRow row in table.Rows)
                    if (input.Conditions.Count == 0
                        || (input.MatchAny ? input.Conditions.Any(c => c.Matches(row[c.Field.Column])) : input.Conditions.All(c => c.Matches(row[c.Field.Column]))))
                        filtered.ImportRow(row);
                grid.DataSource = filtered;
            }
        }
        if (sqlGrids.All(x => x.Grid.DataSource is not DataTable t || t.Rows.Count == 0)) MessageBox.Show("查無資料。", "查詢");
    }

    /// <summary>
    /// 條件畫面的欄位名稱對到本畫面的寫法：本畫面的單頭欄位（含單頭 JOIN）、單頭清單的 JOIN 欄位，都沒有就當單頭資料表的欄位
    /// </summary>
    private string ColumnExpr(string column)
    {
        var field = def.Fields.FirstOrDefault(f => !f.IsCalculated && f.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
        if (field != null) return MasterExpr(field);
        var listColumn = listDef?.Columns.FirstOrDefault(c => c.IsJoin && c.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
        if (listColumn != null) return GridSql.Joined(Dialect, listColumn.JoinAlias, listColumn.SourceColumn);
        return GridSql.Base(Dialect, column);
    }

    /// <summary>執行查詢，結果放到單頭（清單），顯示第一筆；quiet = 不跳「查無資料」之類的提醒（重新整理用）</summary>
    private void LoadResults(DbCommand cmd, bool quiet = false)
    {
        // 記下這次的查詢，別的畫面存檔後用同樣的條件重新整理
        lastQuery = (cmd.CommandText, cmd.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value)).ToList());
        DataTable table;
        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            cmd.Connection = conn;
            table = Dialect.Fill(cmd);
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
        if (quiet) return;
        if (table.Rows.Count == 0)
            MessageBox.Show(listGrid == null && def.Fields.Any(f => !f.IsJoin && openParams.ContainsKey(f.Column)) && perm.Insert
                ? "查無資料。按「新增」可以建立一筆（帶進來的值會自動填入）。" : "查無資料。", "查詢");
        else if (table.Rows.Count >= 5000) MessageBox.Show("資料超過 5000 筆，只取前 5000 筆，請縮小查詢條件。", "查詢");
    }

    /// <summary>組出查詢條件（類似 4GL 的 CONSTRUCT）：文字欄位有 * 或 ? 時用 LIKE，否則用 =；單頭 JOIN 欄位用關聯資料表的欄位當條件</summary>
    private string BuildQueryCondition(DbCommand cmd) =>
        QueryCondition.ToSql(CollectConditions(), cmd, Dialect, c => MasterExpr(c.Field));

    /// <summary>輸入框有填值的欄位轉成查詢條件；格式不對時丟 FormatException</summary>
    private List<QueryCondition> CollectConditions()
    {
        var conditions = new List<QueryCondition>();
        foreach (var f in def.Fields.Where(f => !f.IsCalculated))       // 計算欄位不是資料表的欄位，不能當條件
        {
            Control c = inputs[f];
            if (c is TextBox tb && tb.Text.Trim() != "")
            {
                string text = tb.Text.Trim();
                if (TypeHelper.IsString(f.DataType))
                {
                    bool wildcard = text.Contains('*') || text.Contains('?');
                    conditions.Add(wildcard ? new(f, QueryOp.Like, text.Replace('*', '%').Replace('?', '_')) : new(f, QueryOp.Equal, text));
                }
                else
                {
                    object value;
                    try
                    {
                        value = TypeHelper.Parse(text, f.DataType);
                    }
                    catch (Exception)
                    {
                        throw new FormatException($"「{f.Label}」的查詢條件格式不正確：{text}");
                    }
                    // 日期只輸入到日（畫面也只顯示日期）：只比對日期部分，不管時間
                    if (TypeHelper.IsDate(f.DataType) && DateTime.TryParse(text, out var day) && day.TimeOfDay == TimeSpan.Zero)
                        conditions.Add(new(f, QueryOp.SameDate, day));
                    else
                        conditions.Add(new(f, QueryOp.Equal, value));
                }
            }
            else if (c is DateTimePicker dtp && dtp.ShowCheckBox && dtp.Checked)
            {
                // 文字、數字欄位存的是日期格式文字（例如 yyyyMMdd），直接用同樣格式的文字比對
                conditions.Add(TypeHelper.IsDate(f.DataType) ? new(f, QueryOp.SameDate, dtp.Value.Date) : new(f, QueryOp.Equal, InputHelper.GetValue(f, c)));
            }
            else if ((c is ComboBox || c is RadioGroup) && InputHelper.GetValue(f, c) is var comboValue && comboValue != DBNull.Value
                     && !(comboValue is string cs && cs == ""))
            {
                // 下拉選單有選值就當條件（選空白 = 不限）
                conditions.Add(new(f, QueryOp.Equal, comboValue));
            }
        }
        return conditions;
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
        ButtonAction.自訂查詢 => mode == Mode.Browse || mode == Mode.Query,
        ButtonAction.執行SQL => mode is Mode.Insert or Mode.Modify && perm.Modify,    // 執行SQL 只在新增、修改中可以按（例如取號、更名）
        ButtonAction.列印報表 => mode == Mode.Browse && perm.Print,
        ButtonAction.開啟查詢條件畫面 => mode == Mode.Browse && (HasMasterTable || sqlGrids.Count > 0),
        _ => mode == Mode.Browse,
    };

    private void RunButton(ButtonDefinition b)
    {
        if (!ButtonRunner.CheckAllowed(b, perm)) return;
        // 自訂查詢的第一下只是進入輸入條件，不用確認；SQL 沒用到畫面上的欄位時沒有條件可輸入，按一下就查
        bool oneStep = b.Action == ButtonAction.自訂查詢 && !UsesScreenFields(b.Sql);
        bool startingQuery = b.Action == ButtonAction.自訂查詢 && !oneStep && !(mode == Mode.Query && customQuery == b);
        if (!startingQuery && !PassesPreCheck(b)) return;
        if (!startingQuery && !ButtonRunner.Confirm(b)) return;
        // 「執行後動作」：先對選取的資料執行 SQL，成功了才做後續的動作（例如開啟畫面）
        if (!startingQuery && !string.IsNullOrWhiteSpace(b.PreActionSql))
        {
            if (!perm.Modify)
            {
                MessageBox.Show($"你沒有這個畫面的編修權限，不能執行按鈕「{b.Text}」。", "提醒");
                return;
            }
            if (!RunSqlOnSelection(b, b.PreActionSql, quiet: true)) return;
        }
        switch (b.Action)
        {
            case ButtonAction.查詢: BeginQuery(); break;
            case ButtonAction.自訂查詢:
                if (oneStep || (mode == Mode.Query && customQuery == b)) RunCustomQuery(b);   // 第二下（或不用輸入條件）：查詢
                else BeginQuery(b);                                                         // 第一下：輸入條件
                break;
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
            case ButtonAction.列印報表: PrintReport(b); break;
            case ButtonAction.開啟查詢條件畫面: QueryByConditionForm(b); break;
        }
    }

    /// <summary>
    /// 按鈕的「執行前檢查」：選取的資料每一筆都要符合條件才執行；沒有選取、不符合、條件寫錯時提醒並回傳 false
    /// </summary>
    private bool PassesPreCheck(ButtonDefinition b)
    {
        if (string.IsNullOrWhiteSpace(b.PreCheck)) return true;
        var (rows, labelOf) = SelectedDataRows();
        if (rows.Count == 0)
        {
            MessageBox.Show("請先在清單選取要處理的資料。", b.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        List<DataRow> failing;
        try
        {
            failing = ButtonRunner.FailingRows(rows, b.PreCheck);
        }
        catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException)
        {
            MessageBox.Show($"按鈕「{b.Text}」的執行前檢查寫法有誤：{ex.Message}", "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (failing.Count == 0) return true;
        string message = string.IsNullOrWhiteSpace(b.PreCheckMessage) ? $"選取的資料不符合條件：{b.PreCheck}" : b.PreCheckMessage;
        string names = string.Join("、", failing.Take(10).Select(labelOf)) + (failing.Count > 10 ? $" 等 {failing.Count} 筆" : "");
        MessageBox.Show(message + Environment.NewLine + Environment.NewLine + "不符合的資料：" + names, b.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>
    /// 按鈕要處理的資料列：單頭清單選取的列（沒有清單時是目前這筆）；只放 SQL 清單的畫面是正在操作的清單上選取的列。
    /// labelOf 是提醒訊息裡用來指出那一筆的文字（主鍵，沒有主鍵用第一個顯示的欄位）
    /// </summary>
    private (List<DataRow> Rows, Func<DataRow, string> LabelOf) SelectedDataRows()
    {
        if (HasMasterTable)
        {
            var indexes = listGrid != null ? SelectedRowIndexes() : HasCurrent ? new List<int> { current } : new List<int>();
            var keys = def.MasterKeys.Select(k => k.Column).Where(results.Columns.Contains).ToList();
            return (indexes.Select(i => results.Rows[i]).ToList(),
                    r => keys.Count > 0 ? string.Join("/", keys.Select(k => Convert.ToString(r[k]))) : Convert.ToString(r[0]));
        }
        var grid = sqlGrids.Select(x => x.Grid).OrderByDescending(g => g.ContainsFocus).FirstOrDefault(g => g.SelectedCells.Count > 0);
        if (grid == null) return (new List<DataRow>(), null);
        var rows = grid.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow).Distinct().OrderBy(r => r.Index)
                       .Select(r => (r.DataBoundItem as DataRowView)?.Row).Where(r => r != null).ToList();
        string first = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex)
                           .Select(c => c.DataPropertyName).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return (rows, r => first != null && r.Table.Columns.Contains(first) ? Convert.ToString(r[first]) : Convert.ToString(r[0]));
    }

    /// <summary>SQL 的 @參數有沒有用到畫面上的欄位（計算欄位不算）；沒有 = 不用先輸入條件</summary>
    private bool UsesScreenFields(string sql) =>
        !string.IsNullOrWhiteSpace(sql)
        && ButtonRunner.ParameterNames(sql).Any(n => def.Fields.Any(f => !f.IsCalculated && f.Column.Equals(n, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// 執行按鈕設定的 SQL。@欄位名稱 帶入目前這筆的值：
    /// 先找查詢結果裡的欄位，沒有就找畫面上的輸入欄位；都沒有就是 NULL
    /// </summary>
    private void RunCustomSql(ButtonDefinition b) => RunSqlOnSelection(b, b.Sql, quiet: false);

    /// <summary>
    /// 對選取的資料執行 SQL（同一個交易，失敗全部還原）：清單選了多筆時每一筆各執行一次，沒選就用目前這筆；
    /// 只放 SQL 清單的畫面用 SQL 清單上選取的列。@欄位名稱 先找那一筆的欄位，沒有就找畫面上的輸入欄位、開啟時的傳入參數。
    /// quiet = 成功時不顯示「執行完成」（按鈕的「執行後動作」）；回傳是否成功
    /// </summary>
    private bool RunSqlOnSelection(ButtonDefinition b, string sql, bool quiet)
    {
        object Other(string name)
        {
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field != null) return GetValue(field);
            return openParams.TryGetValue(name, out var param) ? param : null;    // 別的畫面開啟時傳進來的參數
        }
        Func<string, object> ValueOf(DataRow row) => name => row != null && row.Table.Columns.Contains(name) ? row[name] : Other(name);

        // 新增、修改中：只執行一次，@欄位名稱 先取畫面上正在輸入的值；執行完不重讀資料，才不會蓋掉正在輸入的內容
        bool editing = mode is Mode.Insert or Mode.Modify;
        var indexes = editing ? new List<int>() : SelectedRowIndexes();
        if (!editing && indexes.Count == 0 && HasCurrent) indexes.Add(current);
        var rows = editing ? new List<DataRow>() : HasMasterTable ? indexes.Select(i => results.Rows[i]).ToList() : SelectedDataRows().Rows;
        Func<string, object> editingValue = name =>
        {
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field != null) return GetValue(field);
            if (mode == Mode.Modify && HasCurrent && results.Columns.Contains(name)) return results.Rows[current][name];
            return openParams.TryGetValue(name, out var param) ? param : null;
        };
        var valueOfs = editing ? new List<Func<string, object>> { editingValue }
                     : rows.Count > 0 ? rows.Select(ValueOf).ToList() : new List<Func<string, object>> { ValueOf(null) };
        IReadOnlyDictionary<string, object> result = null;
        bool ok = ButtonRunner.RunSql(b, def.ConnectionString, valueOfs, def.FormKey, sql, quiet, values => result = values);
        // 資料可能被 SQL 改過，重讀執行過的那幾筆（瀏覽時）
        if (ok && !editing && HasMasterTable && HasKey && indexes.Count > 0)
        {
            RefreshRows(indexes);
            SetMode(mode);
        }
        // SQL 查出來的值（「SQL 回傳對應」）等重讀完再填進畫面上的欄位，才不會被重讀蓋掉
        if (result != null) ButtonRunner.FillFields(result, inputs, reportMissing: !string.IsNullOrWhiteSpace(b.SqlReturn));
        return ok;
    }

    /// <summary>列印報表：印目前這筆；清單選了多筆時每筆一頁</summary>
    private void PrintReport(ButtonDefinition b)
    {
        var rows = SelectedRowIndexes();
        if (rows.Count == 0 && HasCurrent) rows.Add(current);
        var records = rows.Select(r => (Func<string, object>)(name =>
            results.Columns.Contains(name) ? results.Rows[r][name]
            : def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } field ? GetValue(field) : null)).ToList();
        ButtonRunner.Print(b, def.SourcePath, def.ConnectionString, records, this);
    }

    /// <summary>開啟畫面：傳入參數的 @欄位 先找畫面上的輸入欄位，再找目前這筆；選取視窗帶回的值填進欄位（欄位的開窗選取也用這個）</summary>
    private void OpenOtherForm(ButtonDefinition b) =>
        ButtonRunner.OpenForm(b, def.SourcePath, this, name =>
        {
            var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field != null && mode != Mode.Browse) return GetValue(field);
            if (HasCurrent && results.Columns.Contains(name)) return results.Rows[current][name];
            // 只放 SQL 清單的畫面：帶 SQL 清單上選取的那一列
            if (!HasMasterTable && SelectedDataRows().Rows.FirstOrDefault() is { } row && row.Table.Columns.Contains(name)) return row[name];
            return field != null ? GetValue(field) : null;
        }, values => ButtonRunner.FillFields(values, inputs), RefreshResults,     // 「存檔後關閉並重新整理」：開啟的畫面存檔後重新查詢
           def.ConnectionString);                                                 // 傳入參數的 SQL 運算式用這個畫面的資料庫計算

    /// <summary>
    /// 單頭要查的欄位：畫面欄位，加上單身關聯、單頭清單會用到但沒放在畫面上的單頭欄位。
    /// 單頭資料表固定用別名 T0；單頭 JOIN 帶進來的欄位用「關聯資料表別名.原始欄位 AS 欄位名稱」
    /// </summary>
    private string SelectColumns()
    {
        var joins = def.Joins.Select(j => j.Alias).ToHashSet();
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string alias, string source)
        {
            if (string.IsNullOrEmpty(alias)) columns.TryAdd(name, GridSql.Base(Dialect, name));
            else if (joins.Contains(alias)) columns.TryAdd(name, $"{GridSql.Joined(Dialect, alias, source)} AS {Dialect.Quote(name)}");
        }
        foreach (var f in def.Fields.Where(f => !f.IsCalculated)) Add(f.Column, f.JoinAlias, f.SourceColumn);
        foreach (var master in details.SelectMany(p => p.Def.Links).Select(l => l.Master))
            if (!columns.ContainsKey(master)) Add(master, null, null);
        foreach (var c in listDef?.Columns ?? new List<GridColumnDefinition>()) Add(c.Column, c.JoinAlias, c.SourceColumn);
        return string.Join(", ", columns.Values);
    }

    /// <summary>單頭的 FROM：單頭資料表（別名 T0），有單頭 JOIN 欄位時 LEFT JOIN 關聯資料表</summary>
    private string MasterFrom() =>
        GridSql.From(Dialect, def.FullTableName, def.Joins,
                     def.Fields.Select(f => f.JoinAlias).Concat(listDef?.Columns.Select(c => c.JoinAlias) ?? Enumerable.Empty<string>()).Where(a => !string.IsNullOrEmpty(a)));

    /// <summary>單頭欄位在 SELECT / WHERE 裡的寫法：本身的欄位加別名 T0，JOIN 欄位用關聯資料表的欄位</summary>
    private string MasterExpr(FieldDefinition f) =>
        f.IsJoin ? GridSql.Joined(Dialect, f.JoinAlias, f.SourceColumn) : GridSql.Base(Dialect, f.Column);

    // ================= 計算欄位：公式用到的欄位改了就重算 =================

    private bool recalculating;

    /// <summary>公式用到的欄位（輸入、帶出、查詢顯示）有變動時重算</summary>
    private void HookFormulas()
    {
        var used = def.Fields.Where(f => f.HasFormula).SelectMany(f => SafeNames(f.Formula)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (used.Count == 0) return;
        foreach (var f in def.Fields.Where(f => used.Contains(f.Column)))
        {
            EventHandler changed = (s, e) => RecalcFormulas();
            inputs[f].TextChanged += changed;
            if (inputs[f] is DateTimePicker dtp) dtp.ValueChanged += changed;
            if (inputs[f] is CheckBox cb) cb.CheckedChanged += changed;
        }
    }

    private static List<string> SafeNames(string formula)
    {
        try { return Formula.Names(formula); }
        catch (FormatException) { return new List<string>(); }
    }

    /// <summary>
    /// 重算有公式的欄位：計算欄位每個模式都算（查詢時清空）；資料表欄位設了公式，只在新增、修改時算（瀏覽時顯示資料庫裡存的值）。
    /// 有單頭清單時欄位只當查詢條件，計算欄位也不算
    /// </summary>
    private void RecalcFormulas()
    {
        if (recalculating) return;
        recalculating = true;
        try
        {
            bool editing = mode is Mode.Insert or Mode.Modify;
            bool showing = editing || (mode == Mode.Browse && HasCurrent && listGrid == null);
            var cache = new Dictionary<FieldDefinition, decimal?>();
            foreach (var f in def.Fields.Where(f => f.HasFormula && (f.IsCalculated || editing)))
            {
                decimal? value = showing ? Compute(f, cache, new HashSet<FieldDefinition>()) : null;
                SetValue(f, value == null ? null : Formula.Format(value.Value, f.DataType is "int" or "bigint" or "smallint" or "tinyint" ? 0 : f.Decimals));
            }
        }
        finally
        {
            recalculating = false;
        }
    }

    /// <summary>算一個有公式的欄位；用到其他有公式的欄位時先算它（循環參照 = 算不出來）</summary>
    private decimal? Compute(FieldDefinition f, Dictionary<FieldDefinition, decimal?> cache, HashSet<FieldDefinition> visiting)
    {
        if (cache.TryGetValue(f, out var done)) return done;
        if (!visiting.Add(f)) throw new FormatException("公式循環參照");
        decimal? result = Formula.Evaluate(f.Formula, name =>
        {
            var source = def.Fields.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (source == null) throw new FormatException($"公式裡的欄位「{name}」不在畫面上");
            if (source.HasFormula && (source.IsCalculated || mode is Mode.Insert or Mode.Modify))
                return Compute(source, cache, visiting) ?? throw new FormatException("算不出來");
            return GetValue(source);
        });
        cache[f] = result;
        return result;
    }

    // ================= 單頭 JOIN：關聯欄位改了，重新帶出 JOIN 欄位 =================

    /// <summary>新增、修改時改了關聯欄位（例如廠商編號），停一下再查，不要每打一個字就查一次</summary>
    private readonly System.Windows.Forms.Timer joinTimer = new() { Interval = 300 };
    private readonly HashSet<GridJoinDefinition> pendingJoins = new();

    /// <summary>單頭 JOIN 的關聯欄位（在畫面上的）有變動時，排程重新帶出那個 JOIN 的欄位</summary>
    private void HookJoinRefresh()
    {
        var usedJoins = def.Joins.Where(j => def.Fields.Any(f => f.JoinAlias == j.Alias)).ToList();
        if (usedJoins.Count == 0) return;
        joinTimer.Tick += (s, e) =>
        {
            joinTimer.Stop();
            RefreshJoinFields();
        };
        FormClosed += (s, e) => joinTimer.Dispose();
        foreach (var f in def.Fields.Where(f => !f.IsJoin))
        {
            var joins = usedJoins.Where(j => j.Links.Any(l => l.Master.Equals(f.Column, StringComparison.OrdinalIgnoreCase))).ToList();
            if (joins.Count == 0) continue;
            EventHandler changed = (s, e) => QueueJoinRefresh(joins);
            inputs[f].TextChanged += changed;            // 文字框、下拉選單；開窗選取帶回的值也會觸發
            if (inputs[f] is DateTimePicker dtp) dtp.ValueChanged += changed;
            if (inputs[f] is CheckBox cb) cb.CheckedChanged += changed;
        }
    }

    private void QueueJoinRefresh(IEnumerable<GridJoinDefinition> joins)
    {
        if (mode is not (Mode.Insert or Mode.Modify)) return;      // 瀏覽時的值是查詢帶出來的，查詢條件不用帶
        pendingJoins.UnionWith(joins.Where(j => def.Fields.Any(f => f.JoinAlias == j.Alias)));
        if (pendingJoins.Count == 0) return;
        joinTimer.Stop();
        joinTimer.Start();
    }

    /// <summary>
    /// 依關聯欄位目前的值重新帶出 JOIN 欄位；關聯欄位沒填或對不到資料 = JOIN 欄位空白。
    /// joins = null：排程中的 JOIN（新增、修改時才帶）；force = 瀏覽時也帶（查無資料時顯示傳入參數對應的資料）
    /// </summary>
    private void RefreshJoinFields(IEnumerable<GridJoinDefinition> joinsToRefresh = null, bool force = false)
    {
        var joins = (joinsToRefresh ?? pendingJoins).ToList();
        if (joinsToRefresh == null) pendingJoins.Clear();
        if (!force && mode is not (Mode.Insert or Mode.Modify)) return;
        var d = Dialect;
        foreach (var j in joins)
        {
            var fields = def.Fields.Where(f => f.JoinAlias == j.Alias).ToList();
            var links = j.Links;
            if (links.Count == 0 || fields.Count == 0) continue;
            try
            {
                var values = links.Select(l => MasterValue(l.Master)).ToList();
                DataRow row = null;
                if (values.All(v => v != null && v != DBNull.Value && !(v is string s && s.Trim() == "")))
                {
                    using var conn = d.Open(def.ConnectionString);
                    using var cmd = d.CreateCommand(conn);
                    for (int i = 0; i < links.Count; i++) d.AddParam(cmd, $"j{i}", values[i]);
                    cmd.CommandText = $"SELECT {d.Top(1)}{string.Join(", ", fields.Select(f => $"{GridSql.Joined(d, j.Alias, f.SourceColumn)} AS {d.Quote(f.Column)}"))} "
                                    + $"FROM {j.FullTableName(d)}{d.TableAlias(j.Alias)} WHERE "
                                    + string.Join(" AND ", links.Select((l, i) => $"{GridSql.Joined(d, j.Alias, l.Detail)} = {d.Param($"j{i}")}"))
                                    + d.Limit(1);
                    var table = d.Fill(cmd);
                    row = table.Rows.Count > 0 ? table.Rows[0] : null;
                }
                foreach (var f in fields) SetValue(f, row?[f.Column]);
            }
            catch (Exception)
            {
                // 帶不出來只是少了顯示，不影響存檔
            }
        }
    }

    /// <summary>單頭欄位目前的值：畫面上有這個欄位就用輸入的值（格式不對 = null），沒有就用目前這筆的值</summary>
    private object MasterValue(string column)
    {
        var field = def.Fields.FirstOrDefault(f => !f.IsJoin && f.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
        if (field != null)
        {
            try { return GetValue(field); }
            catch (FormatException) { return null; }
        }
        return mode == Mode.Modify && HasCurrent && results.Columns.Contains(column) ? results.Rows[current][column] : null;
    }

    // ================= 新增、修改、刪除 =================

    private void InsertRecord()
    {
        var values = CollectValues();
        if (values == null) return;
        if (InsertValues(values.ToDictionary(v => v.Key.Column, v => v.Value)) && HasDetail && HasCurrent)
            BeginDetail();                                 // 像 4GL：單頭存好直接進單身
    }

    /// <summary>新增一筆單頭（key = 欄位名稱）；成功後讀回這筆、加到查詢結果並設為目前這筆</summary>
    /// <param name="notify">false = 不跳「新增完成」（清單直接新增，新的那筆出現在清單上就知道了）</param>
    private bool InsertValues(Dictionary<string, object> values, bool notify = true)
    {
        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            using var cmd = Dialect.CreateCommand(conn);
            var list = values.ToList();
            for (int i = 0; i < list.Count; i++)
                Dialect.AddParam(cmd, $"p{i}", list[i].Value);
            string insert = $"INSERT INTO {def.FullTableName} ({string.Join(", ", list.Select(v => Dialect.Quote(v.Key)))}) "
                          + $"VALUES ({string.Join(", ", list.Select((v, i) => Dialect.Param($"p{i}")))})";
            // 自動編號由資料庫產生，新增後取回（SQL Server：SCOPE_IDENTITY；Oracle：RETURNING；MySQL：LAST_INSERT_ID）
            object identity = Dialect.InsertReturningIdentity(cmd, insert, def.MasterKeys.FirstOrDefault(k => k.IsIdentity)?.Column);

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
            Audit.Write(def.FormKey, def.TableName, "新增", HasCurrent && HasKey ? Audit.KeyText(CurrentKey()) : null,
                        values.ToDictionary(v => v.Key, v => Audit.Format(v.Value)));
            if (notify) MessageBox.Show("新增完成。", "新增");
            AfterSave(HasCurrent && HasKey ? CurrentKey() : null, closing: !HasDetail);   // 有單身時等單身存檔再關
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
    private bool UpdateValues(Dictionary<string, object> values, string doneMessage = "修改完成。")
    {
        if (values.Count == 0)
        {
            SetMode(Mode.Browse);
            return true;
        }
        try
        {
            // 稽核紀錄要記修改前的值
            DataRow before = null;
            if (HasCurrent)
            {
                var copy = results.Clone();
                copy.ImportRow(results.Rows[current]);
                before = copy.Rows[0];
            }
            string keyText = HasCurrent && HasKey ? Audit.KeyText(CurrentKey()) : null;
            using var conn = Dialect.Open(def.ConnectionString);
            using var cmd = Dialect.CreateCommand(conn);
            var list = values.ToList();
            for (int i = 0; i < list.Count; i++)
                Dialect.AddParam(cmd, $"p{i}", list[i].Value);
            cmd.CommandText = $"UPDATE {def.FullTableName} SET {string.Join(", ", list.Select((v, i) => $"{Dialect.Quote(v.Key)} = {Dialect.Param($"p{i}")}"))} "
                            + "WHERE " + AddKeyCondition(cmd, CurrentKey());
            if (cmd.ExecuteNonQuery() == 0)
            {
                MessageBox.Show("找不到原本的資料，可能已被其他人刪除。", "修改");
                return false;
            }
            // 生效 / 取消生效也走這裡（doneMessage 是「已生效。」之類）
            string action = doneMessage == "修改完成。" ? "修改" : doneMessage.TrimEnd('。').Replace("已", "");
            Audit.Write(def.FormKey, def.TableName, action, keyText, Audit.Changes(before, values));
            RefreshCurrentRow();
            ShowCurrent();
            SetMode(Mode.Browse);
            MessageBox.Show(doneMessage, Text);
            AfterSave(HasCurrent && HasKey ? CurrentKey() : null);
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
        bool withDetail = details.Any(p => p.Def.Links.Count > 0);
        string question = withDetail ? "確定要刪除這筆資料（連同單身明細）嗎？" : "確定要刪除這筆資料嗎？";
        if (MessageBox.Show(question, "刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            using var tran = conn.BeginTransaction();             // 單身、單頭一起刪，失敗就全部不變
            using var cmd = Dialect.CreateCommand(conn);
            cmd.Transaction = tran;
            foreach (var part in details.Where(p => p.Def.Links.Count > 0))
            {
                cmd.CommandText = $"DELETE FROM {part.Def.FullTableName(Dialect)} WHERE " + AddDetailCondition(cmd, part);
                cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();
            }
            cmd.CommandText = $"DELETE FROM {def.FullTableName} WHERE " + AddKeyCondition(cmd, CurrentKey());
            int rows = cmd.ExecuteNonQuery();
            tran.Commit();
            Audit.Write(def.FormKey, def.TableName, "刪除", Audit.KeyText(CurrentKey()), withDetail ? "連同單身明細" : null);
            RemoveCurrentRow();
            MessageBox.Show(rows > 0 ? "已刪除。" : "找不到這筆資料，可能已被其他人刪除。", "刪除");
            AfterSave(null);
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
        RecalcFormulas();                                  // 有公式的欄位用最新的值存檔
        try
        {
            foreach (var f in def.Fields.Where(f => !f.NoEntry && !f.IsIdentity && !f.IsJoin && !f.IsCalculated && !f.QueryOnly))   // 單頭 JOIN、計算欄位、只當查詢條件的欄位不寫回資料庫
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

    private Dictionary<string, object> CurrentKey() => KeyOf(current);

    /// <summary>查詢結果第 row 筆的主鍵值</summary>
    private Dictionary<string, object> KeyOf(int row) =>
        def.MasterKeys.ToDictionary(k => k.Column, k => results.Rows[row][k.Column]);

    private DataRow LoadRowByKey(Dictionary<string, object> key)
    {
        using var conn = Dialect.Open(def.ConnectionString);
        using var cmd = Dialect.CreateCommand(conn);
        cmd.CommandText = $"SELECT {SelectColumns()} FROM {MasterFrom()} WHERE " + AddKeyCondition(cmd, key, GridSql.BaseAlias);
        var table = Dialect.Fill(cmd);
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

    /// <summary>組出「主鍵 = 值」條件，參數名稱用 k0、k1…；tableAlias = 查詢單頭（有別名 T0）時加在欄位前面</summary>
    private string AddKeyCondition(DbCommand cmd, Dictionary<string, object> key, string tableAlias = null)
    {
        var conditions = new List<string>();
        int i = 0;
        foreach (var k in key)
        {
            conditions.Add($"{(tableAlias != null ? Dialect.Quote(tableAlias) + "." : "")}{Dialect.Quote(k.Key)} = {Dialect.Param($"k{i}")}");
            Dialect.AddParam(cmd, $"k{i}", k.Value);
            i++;
        }
        return string.Join(" AND ", conditions);
    }

    // ================= 欄位控制項 =================

    private void ShowCurrent()
    {
        // 有單頭清單時，欄位只當查詢條件：點選清單不帶入，查詢完也清空（資料看清單、編輯用雙擊的編輯視窗）
        if (HasCurrent && listGrid == null)
        {
            // 自訂查詢的 SELECT 不一定有每個欄位，沒有的就空白
            // 只當查詢條件的欄位不顯示資料
            foreach (var f in def.Fields.Where(f => !f.IsTextLabel))    // 文字標籤另外用執行後動作帶出
                SetValue(f, !f.QueryOnly && results.Columns.Contains(f.Column) ? results.Rows[current][f.Column] : null);
            ButtonRunner.RefreshTextLabels(inputs, def.ConnectionString, name =>
            {
                var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (field != null) return GetValue(field);
                return results.Columns.Contains(name) ? results.Rows[current][name] : null;
            });
        }
        else
        {
            ClearInputs();
            // 別的畫面開啟時帶進來的參數（例如廠商編號）：查無資料時也顯示在欄位上，JOIN 欄位跟著帶出，至少看得到單頭是誰
            if (!HasCurrent && listGrid == null && FillParams()) RefreshJoinFields(def.Joins, force: true);
        }
        RecalcFormulas();
        LoadDetail();
        LoadSqlLists();
        SyncList();
    }

    /// <summary>重查 SQL 清單：@名稱 先取目前這筆單頭的值，再找畫面上的欄位、開啟時的傳入參數</summary>
    private void LoadSqlLists()
    {
        foreach (var (g, grid) in sqlGrids)
        {
            try
            {
                grid.DataSource = SqlList.Load(def.ConnectionString, g.Sql, name =>
                {
                    if (HasCurrent && results.Columns.Contains(name)) return results.Rows[current][name];
                    var field = def.Fields.FirstOrDefault(f => f.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (field != null) return GetValue(field);
                    return openParams.TryGetValue(name, out var v) ? v : null;
                });
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                MessageBox.Show($"讀取「{g.Title}」失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ClearInputs()
    {
        foreach (var f in def.Fields.Where(f => !f.IsTextLabel)) SetValue(f, null);     // 文字標籤保留文字
    }

    /// <summary>開啟時帶進來的參數填到同名的單頭欄位（JOIN、計算欄位除外）；有填到任何欄位回傳 true</summary>
    private bool FillParams()
    {
        bool any = false;
        foreach (var f in def.Fields.Where(f => !f.IsJoin && !f.IsCalculated && !f.QueryOnly))
        {
            if (!openParams.TryGetValue(f.Column, out var value) || value == null || value == DBNull.Value) continue;
            SetValue(f, value);
            any = true;
        }
        return any;
    }

    // ================= 單身 =================

    private static bool IsLinkColumn(DetailPart part, string detailColumn) =>
        part.Def.Links.Any(l => string.Equals(l.Detail, detailColumn, StringComparison.OrdinalIgnoreCase));

    /// <param name="connectionString">下拉選單欄位的選項 SQL 用</param>
    private static DataGridView CreateDetailGrid(GridDefinition g, string connectionString)
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
        GridCells.ApplyGridLines(grid);
        // 欄位依「輸入方式」建立：文字框、下拉選單、勾選框、日期選擇
        foreach (var c in g.Columns.Where(c => c.Visible))
        {
            // 生效欄位（輸入方式沒特別設定時）：不管欄位是文字還是數字，都顯示成唯讀的勾選框
            bool effective = c.CellType == GridCellKind.自動 && !TypeHelper.IsBool(c.DataType)
                             && c.Column.Equals(g.EffectiveColumn ?? "", StringComparison.OrdinalIgnoreCase);
            DataGridViewColumn col = effective ? GridCells.CreateEffectiveColumn(c) : GridCells.CreateColumn(c, connectionString);
            col.SortMode = DataGridViewColumnSortMode.NotSortable;   // 不排序，列的順序才會跟資料一致
            grid.Columns.Add(col);
        }
        GridCells.HookEffectiveColumn(grid);
        grid.DataError += (s, e) =>
        {
            if (GridCells.IgnoreDisplayError(grid, e)) return;     // 下拉選單裡沒有的值：顯示空白，不跳訊息
            string header = grid.Columns[e.ColumnIndex].HeaderText;
            MessageBox.Show($"第 {e.RowIndex + 1} 列「{header}」的格式不正確。", "提醒");
            e.ThrowException = false;
        };
        return grid;
    }

    /// <summary>讀取目前單頭的明細（每個單身：關聯欄位 = 單頭的值）</summary>
    private void LoadDetail()
    {
        foreach (var part in details) LoadDetail(part);
    }

    private void LoadDetail(DetailPart part)
    {
        var gridDef = part.Def;
        var table = new DataTable();
        if (HasCurrent)
        {
            try
            {
                var d = Dialect;
                using var conn = d.Open(def.ConnectionString);
                using var cmd = d.CreateCommand(conn);
                string where = AddDetailCondition(cmd, part, GridSql.BaseAlias);    // 有 JOIN 時，本身的欄位要加別名
                var orderColumns = gridDef.KeyColumns.Count > 0 ? gridDef.KeyColumns : gridDef.Columns.Where(c => !c.IsJoin).Take(1).ToList();
                cmd.CommandText = GridSql.Select(d, gridDef.FullTableName(d), gridDef.Columns, gridDef.Joins, 5000)
                                + (where != "" ? " WHERE " + where : "")
                                + " ORDER BY " + string.Join(", ", orderColumns.Select(c => GridSql.Base(d, c.Column)))
                                + d.Limit(5000);
                table = d.Fill(cmd);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"讀取單身「{gridDef.Title ?? gridDef.TableName}」失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // 新增明細時，關聯欄位自動帶入目前單頭的值
        table.TableNewRow += (s, e) =>
        {
            // 新增明細時先帶入欄位的預設值
            foreach (var c in gridDef.Columns.Where(c => !string.IsNullOrWhiteSpace(c.EditDefaultValue) && e.Row.Table.Columns.Contains(c.Column)))
            {
                try { e.Row[c.Column] = DefaultValue.Resolve(c.EditDefaultValue); }
                catch (Exception) { }                           // 型別對不上就不帶
            }
            if (!HasCurrent) return;
            foreach (var (master, detail) in gridDef.Links)
                if (e.Row.Table.Columns.Contains(detail))
                    e.Row[detail] = results.Rows[current][master];
        };
        part.Table = table;
        part.Grid.DataSource = part.Table;
    }

    /// <summary>組出「單身關聯欄位 = 目前單頭的值」條件，參數名稱用 m0、m1…；沒有關聯欄位時回傳空字串</summary>
    /// <param name="row">查詢結果的第幾筆；null = 目前這筆</param>
    private string AddDetailCondition(DbCommand cmd, DetailPart part, string tableAlias = null, int? row = null)
    {
        var conditions = new List<string>();
        int i = 0;
        foreach (var (master, detail) in part.Def.Links)
        {
            conditions.Add($"{(tableAlias != null ? Dialect.Quote(tableAlias) + "." : "")}{Dialect.Quote(detail)} = {Dialect.Param($"m{i}")}");
            Dialect.AddParam(cmd, $"m{i}", results.Rows[row ?? current][master]);
            i++;
        }
        return string.Join(" AND ", conditions);
    }

    private void BeginDetail()
    {
        if (!HasDetail || !HasCurrent) return;
        SetMode(Mode.Detail);
    }

    /// <summary>
    /// 單身存檔：每個單身新增的列 INSERT、改過的列 UPDATE、刪掉的列 DELETE，全部單身在同一個交易裡，
    /// 任何一筆失敗就全部不變（像 4GL 單身的 BEGIN WORK / COMMIT WORK）
    /// </summary>
    private void SaveDetail()
    {
        var changes = new List<(DetailPart Part, List<DataRow> Rows)>();
        foreach (var part in details)
        {
            part.Grid.EndEdit();
            if (BindingContext[part.Table] is CurrencyManager cm) cm.EndCurrentEdit();
            var rows = part.Table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Unchanged).ToList();
            if (rows.Count > 0) changes.Add((part, rows));
        }
        if (changes.Count == 0)
        {
            SetMode(Mode.Browse);
            return;
        }
        string NameOf(DetailPart part) => details.Count > 1 ? $"單身「{part.Def.Title ?? part.Def.TableName}」" : "單身";
        foreach (var (part, changed) in changes)
        {
            if (part.Def.KeyColumns.Count == 0 && changed.Any(r => r.RowState is DataRowState.Modified or DataRowState.Deleted))
            {
                MessageBox.Show($"{NameOf(part)}沒有設定主鍵，只能新增明細，不能修改或刪除。請按「取消」放棄修改的部分。", "提醒");
                return;
            }
            // 必填檢查：顯示在 Grid 上、不允許 NULL 的欄位
            foreach (var r in changed.Where(r => r.RowState != DataRowState.Deleted))
            {
                var empty = part.Def.Columns.FirstOrDefault(c => c.Visible && !c.IsNullable && !c.IsIdentity && !c.NoEntry && r[c.Column] == DBNull.Value);
                if (empty != null)
                {
                    MessageBox.Show($"{NameOf(part)}第 {part.Table.Rows.IndexOf(r) + 1} 列請輸入「{empty.Header}」。", "提醒");
                    part.Grid.Focus();
                    return;
                }
            }
        }

        try
        {
            var d = Dialect;
            using var conn = d.Open(def.ConnectionString);
            using var tran = conn.BeginTransaction();
            foreach (var (part, changed) in changes)
            foreach (var r in changed)
            {
                var gridDef = part.Def;
                var keys = gridDef.KeyColumns;
                string table = gridDef.FullTableName(d);
                using var cmd = d.CreateCommand(conn);
                cmd.Transaction = tran;
                switch (r.RowState)
                {
                    case DataRowState.Added:
                    {
                        // 空值不寫，讓資料庫預設值生效；關聯欄位一定要寫
                        var cols = gridDef.Columns.Where(c => !c.IsIdentity && (IsLinkColumn(part, c.Column) || (!c.NoEntry && r[c.Column] != DBNull.Value))).ToList();
                        for (int i = 0; i < cols.Count; i++) d.AddParam(cmd, $"p{i}", r[cols[i].Column]);
                        cmd.CommandText = $"INSERT INTO {table} ({string.Join(", ", cols.Select(c => d.Quote(c.Column)))}) "
                                        + $"VALUES ({string.Join(", ", cols.Select((c, i) => d.Param($"p{i}")))})";
                        break;
                    }
                    case DataRowState.Modified:
                    {
                        var cols = gridDef.Columns.Where(c => !c.IsIdentity && !c.NoEntry && !c.IsKey && !IsLinkColumn(part, c.Column)).ToList();
                        if (cols.Count == 0) continue;
                        for (int i = 0; i < cols.Count; i++) d.AddParam(cmd, $"p{i}", r[cols[i].Column]);
                        cmd.CommandText = $"UPDATE {table} SET {string.Join(", ", cols.Select((c, i) => $"{d.Quote(c.Column)} = {d.Param($"p{i}")}"))} "
                                        + "WHERE " + AddOriginalKeyCondition(cmd, r, keys);
                        break;
                    }
                    case DataRowState.Deleted:
                        cmd.CommandText = $"DELETE FROM {table} WHERE " + AddOriginalKeyCondition(cmd, r, keys);
                        break;
                }
                cmd.ExecuteNonQuery();
            }
            tran.Commit();
            foreach (var (part, changed) in changes)
                Audit.Write(def.FormKey, part.Def.TableName, "單身存檔", HasKey ? Audit.KeyText(CurrentKey()) : null, new
                {
                    新增 = changed.Count(r => r.RowState == DataRowState.Added),
                    修改 = changed.Count(r => r.RowState == DataRowState.Modified),
                    刪除 = changed.Count(r => r.RowState == DataRowState.Deleted),
                });
        }
        catch (Exception ex)
        {
            MessageBox.Show("單身儲存失敗（全部未存）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        LoadDetail();
        SetMode(Mode.Browse);
        MessageBox.Show($"單身已儲存（{changes.Sum(c => c.Rows.Count)} 筆異動）。", "單身");
        AfterSave(HasCurrent && HasKey ? CurrentKey() : null);
    }

    /// <summary>
    /// 存檔成功後：記下這筆的主鍵（開啟這個畫面的畫面重新整理時停在這筆）；
    /// 「存檔後關閉並重新整理」時關閉視窗（等成功訊息關掉後才關）
    /// </summary>
    private void AfterSave(Dictionary<string, object> key, bool closing = true)
    {
        Saved = true;
        SavedKey = key;
        if (CloseAfterSave && closing) BeginInvoke(() => DialogResult = DialogResult.OK);
    }

    /// <summary>
    /// 別的畫面（「存檔後關閉並重新整理」）存檔後：用上一次的條件重新查詢，停在 key 那筆；
    /// 還沒查詢過就只查 key 那筆。只放 SQL 清單的畫面重查 SQL 清單
    /// </summary>
    private void RefreshResults(IReadOnlyDictionary<string, object> key)
    {
        if (!HasMasterTable)
        {
            LoadSqlLists();
            return;
        }
        if (lastQuery is { } last)
        {
            using var cmd = Dialect.CreateCommand(sql: last.Sql);
            foreach (var (name, value) in last.Params)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                cmd.Parameters.Add(p);
            }
            LoadResults(cmd, quiet: true);
        }
        else if (key != null && key.Count > 0)
            RunQuery(cmd => AddKeyCondition(cmd, key.ToDictionary(k => k.Key, k => k.Value), GridSql.BaseAlias));
        // 停在剛存檔的那筆（刪除時 key 是空的，停在第一筆）
        if (key == null || key.Count == 0 || !key.Keys.All(results.Columns.Contains)) return;
        for (int i = 0; i < results.Rows.Count; i++)
        {
            if (!key.All(k => SameValue(results.Rows[i][k.Key], k.Value))) continue;
            MoveTo(i);
            return;
        }
    }

    /// <summary>主鍵值相同（文字不分大小寫、忽略字尾空白，跟 SQL Server 比對一樣）</summary>
    private static bool SameValue(object a, object b) =>
        Equals(a, b) || (a is string || b is string)
            && string.Equals(Convert.ToString(a)?.TrimEnd(), Convert.ToString(b)?.TrimEnd(), StringComparison.OrdinalIgnoreCase);

    /// <summary>用資料列「改之前」的主鍵值組條件，參數名稱用 k0、k1…</summary>
    private string AddOriginalKeyCondition(DbCommand cmd, DataRow row, List<GridColumnDefinition> keys)
    {
        var conditions = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            conditions.Add($"{Dialect.Quote(keys[i].Column)} = {Dialect.Param($"k{i}")}");
            Dialect.AddParam(cmd, $"k{i}", row[keys[i].Column, DataRowVersion.Original]);
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
            edit(grid.CurrentRow.Index);                   // 單頭清單選了多筆時，EditMasterRow 會每筆各開一個視窗
        };
    }

    /// <summary>
    /// 單頭清單雙擊：開啟單筆編輯視窗，裡面可以新增、修改、刪除、首筆～末筆；
    /// index = -1 表示在空白處雙擊（沒有資料時也能開來新增）
    /// </summary>
    private void EditMasterRow(int index)
    {
        if (mode != Mode.Browse) return;
        if (pickMode)
        {
            // 選取視窗：雙擊 = 選這筆帶回
            if (index >= 0 && index < results.Rows.Count)
            {
                if (index != current) MoveTo(index);
                Pick();
            }
            return;
        }
        // 直接新增的空白列、還沒存的列：在清單上輸入，不開編輯視窗
        if (listDef.AllowInsert && index >= 0 && (index >= results.Rows.Count ? index < listGrid.Rows.Count : IsPendingRow(results.Rows[index]))) return;
        // 清單選了多筆、雙擊（或 Enter）的是其中一筆：每筆各開一個編輯視窗
        var rows = SelectedRowIndexes();
        if (index >= 0 && rows.Count > 1 && rows.Contains(index))
        {
            OpenEditWindows(rows);
            return;
        }
        if (index >= 0 && index < results.Rows.Count && index != current) MoveTo(index);
        OpenEditWindow(index >= 0 && index < results.Rows.Count ? results.Rows[index] : null);
    }

    /// <summary>開著的編輯視窗（主畫面關閉時一起關；同一筆不重複開）</summary>
    private readonly List<(RecordNavigatorDialog Dialog, RecordCursor Cursor)> editWindows = new();

    /// <summary>清單選取的每一筆各開一個編輯視窗（一次太多時先確認）</summary>
    private void OpenEditWindows(IList<int> rows)
    {
        if (rows.Count > 10 && MessageBox.Show($"要開 {rows.Count} 個編輯視窗嗎？", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (int r in rows) OpenEditWindow(results.Rows[r]);
    }

    /// <summary>
    /// 開一個編輯視窗（不鎖住主畫面，可以同時開好幾個）：每個視窗有自己的「目前這筆」（RecordCursor），
    /// 在視窗裡切換上一筆 / 下一筆不會影響其他視窗；row = null 是空白處雙擊（沒有資料時用來新增）
    /// </summary>
    private void OpenEditWindow(DataRow row)
    {
        // 這一筆已經有視窗開著：切過去
        var existing = editWindows.FirstOrDefault(w => row != null && w.Cursor.CurrentRow == row && !w.Dialog.IsDisposed);
        if (existing.Dialog != null)
        {
            existing.Dialog.Activate();
            return;
        }
        listDef.EnsureEditLayout();                         // 沒設計過版面時排成一欄
        var fields = listDef.EditColumns
            .Select(c => InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity))
            .ToList();
        // 主鍵：清單欄位的主鍵設定，加上單頭欄位裡勾了主鍵的
        var keyColumns = def.MasterKeys.Select(k => k.Column).ToList();
        foreach (var f in fields) f.IsKey = keyColumns.Contains(f.Column, StringComparer.OrdinalIgnoreCase);
        var cursor = new RecordCursor(this, row);
        var dlg = new RecordNavigatorDialog(cursor, def.Title, fields, listDef.EditFormSize, def.ConnectionString, listDef.EditButtons, def.SourcePath,
                                            listDef.EffectiveColumn, listDef.EditLists, listDef.EditBackColor, listDef.EditFont(body.Font),
                                            listDef.EditTextLabels.Select(f => f.Clone()).ToList());
        // 多個視窗錯開排列，才看得到後面的
        var owner = (TopLevelControl as Form) ?? this;
        int open = editWindows.Count(w => !w.Dialog.IsDisposed);
        dlg.StartPosition = FormStartPosition.Manual;
        var area = Screen.FromControl(owner).WorkingArea;
        var origin = new Point(owner.Left + Math.Max(0, (owner.Width - dlg.Width) / 2), owner.Top + Math.Max(0, (owner.Height - dlg.Height) / 2));
        dlg.Location = new Point(Math.Min(origin.X + open * 28, area.Right - dlg.Width), Math.Min(origin.Y + open * 28, area.Bottom - dlg.Height));
        editWindows.Add((dlg, cursor));
        dlg.FormClosed += (s, e) => editWindows.RemoveAll(w => w.Dialog == dlg);
        dlg.Show(owner);
    }

    /// <summary>
    /// 一個編輯視窗的「目前這筆」：記住資料列本身（不是第幾筆），別的視窗刪掉資料時才不會跑掉。
    /// 存檔、刪除、重讀時先把主畫面切到這一筆，再用主畫面原本的存檔邏輯
    /// </summary>
    private sealed class RecordCursor : IRecordNavigator
    {
        private readonly RuntimeForm form;
        private DataRow row;

        public RecordCursor(RuntimeForm form, DataRow row)
        {
            this.form = form;
            this.row = row;
        }

        private int Index => row == null ? -1 : form.results.Rows.IndexOf(row);
        private IRecordNavigator Main => form;

        /// <summary>主畫面切到這一筆（存檔、刪除都是對主畫面的目前這筆做）</summary>
        private void Select()
        {
            int i = Index;
            if (i >= 0 && i != form.current)
            {
                form.current = i;
                form.ShowCurrent();
                form.SetMode(form.mode);
            }
        }

        /// <summary>操作之後，這個視窗跟著主畫面的目前這筆（新增後是新的那筆、刪除後是下一筆）</summary>
        private void Follow() => row = form.HasCurrent ? form.results.Rows[form.current] : null;

        public string FullTableName => Main.FullTableName;
        public int Count => Main.Count;
        public int Current => Index;
        public DataRow CurrentRow => Index >= 0 ? row : null;
        public bool CanModify => Main.CanModify;
        public bool CanInsert => Main.CanInsert;
        public bool CanDelete => Main.CanDelete;
        public bool CanPrint => Main.CanPrint;
        public bool CanApprove => Main.CanApprove;
        public FormPermission Permission => Main.Permission;

        public void MoveTo(int index)
        {
            if (index >= 0 && index < form.results.Rows.Count) row = form.results.Rows[index];
        }

        public bool Refresh()
        {
            Select();
            bool ok = Main.Refresh();
            if (!ok) Follow();
            return ok;
        }

        public bool Insert(Dictionary<string, object> values)
        {
            bool ok = Main.Insert(values);
            if (ok) Follow();
            return ok;
        }

        public bool Update(Dictionary<string, object> values)
        {
            Select();
            return Main.Update(values);
        }

        public bool UpdateColumn(string column, object value, string doneMessage)
        {
            Select();
            return Main.UpdateColumn(column, value, doneMessage);
        }

        public bool Delete()
        {
            Select();
            bool ok = Main.Delete();
            if (ok) Follow();
            return ok;
        }
    }

    // ---- 給單筆編輯視窗呼叫（IRecordNavigator）----
    string IRecordNavigator.FullTableName => def.FullTableName;
    int IRecordNavigator.Count => results.Rows.Count;
    int IRecordNavigator.Current => current;
    DataRow IRecordNavigator.CurrentRow => HasCurrent ? results.Rows[current] : null;
    bool IRecordNavigator.CanModify => HasKey && perm.Modify;
    bool IRecordNavigator.CanInsert => perm.Insert;
    bool IRecordNavigator.CanDelete => HasKey && perm.Delete;
    bool IRecordNavigator.CanPrint => perm.Print;
    bool IRecordNavigator.CanApprove => HasKey && perm.Approve;
    FormPermission IRecordNavigator.Permission => perm;
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
    bool IRecordNavigator.UpdateColumn(string column, object value, string doneMessage) =>
        UpdateValues(new Dictionary<string, object> { [column] = value }, doneMessage);
    bool IRecordNavigator.Delete() => DeleteRecord();

    /// <summary>
    /// 單身的某一列：瀏覽時改完直接存回資料表；
    /// 在「單身」輸入中則只改 Grid 上的資料，等按「確定」時跟其他明細一起存
    /// </summary>
    private void EditDetailRow(DetailPart part, int index)
    {
        var gridDef = part.Def;
        if (index < 0 || index >= part.Grid.Rows.Count) return;
        if (part.Grid.Rows[index].DataBoundItem is not DataRowView view) return;   // 最後一列的「新增列」還沒有資料
        DataRow row = view.Row;
        bool inDetailMode = mode == Mode.Detail;
        if (!inDetailMode && mode != Mode.Browse) return;
        if (!inDetailMode && !perm.Modify)
        {
            MessageBox.Show("你沒有修改這個畫面的權限。", "提醒");
            return;
        }
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
            .Select(c => InputHelper.FromGridColumn(c, c.NoEntry || c.IsIdentity || IsLinkColumn(part, c.Column) || (c.IsKey && !keyEditable)))
            .ToList();
        using var dlg = new RecordEditDialog($"{(inDetailMode ? "編輯" : "修改")}明細：{gridDef.Title ?? gridDef.TableName}", fields, col => row[col],
                                             gridDef.EditFormSize, def.ConnectionString, gridDef.EditButtons, def.SourcePath, gridDef.EditLists, gridDef.EditBackColor, gridDef.EditFont(body.Font), perm);
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
            part.Grid.Refresh();
            return;
        }

        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            using var cmd = Dialect.CreateCommand(conn);
            var list = dlg.Values.ToList();
            for (int i = 0; i < list.Count; i++) Dialect.AddParam(cmd, $"p{i}", list[i].Value);
            cmd.CommandText = $"UPDATE {gridDef.FullTableName(Dialect)} SET {string.Join(", ", list.Select((v, i) => $"{Dialect.Quote(v.Key)} = {Dialect.Param($"p{i}")}"))} "
                            + "WHERE " + AddOriginalKeyCondition(cmd, row, keys);
            if (cmd.ExecuteNonQuery() == 0)
                MessageBox.Show("找不到原本的明細，可能已被其他人刪除。", "修改");
            else
            {
                Audit.Write(def.FormKey, gridDef.TableName, "修改明細",
                            Audit.KeyText(keys.Select(k => new KeyValuePair<string, object>(k.Column, row[k.Column]))), Audit.Changes(row, dlg.Values));
                MessageBox.Show("明細修改完成。", "修改");
                AfterSave(HasCurrent && HasKey ? CurrentKey() : null);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("修改失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadDetail(part);
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
            listGrid.AllowUserToAddRows = CanInsertInList;            // 查詢之後才有欄位可以新增
            // 使用者在清單上點選（含 Ctrl / Shift 多選）造成的切換：保留使用者選的列
            if (HasCurrent && current < listGrid.Rows.Count && listGrid.CurrentRow?.Index == current && listGrid.Rows[current].Selected) return;
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
        if (index >= 0 && index < results.Rows.Count && index != current && !IsPendingRow(results.Rows[index])) MoveTo(index);
    }

    // ================= 單頭清單：直接新增 =================

    /// <summary>清單現在可以直接新增：後台勾了「允許直接新增」、有新增權限、瀏覽中，而且查詢過（有欄位）</summary>
    private bool CanInsertInList =>
        listDef?.AllowInsert == true && perm.Insert && !pickMode && !conditionMode && mode == Mode.Browse && results.Columns.Count > 0;

    /// <summary>清單上使用者正在輸入、還沒存進資料表的列</summary>
    private static bool IsPendingRow(DataRow row) => row.RowState is DataRowState.Added or DataRowState.Detached;

    /// <summary>
    /// 單頭清單「允許直接新增」：最下面的空白列可以輸入，已經存在的列不能改（修改還是用雙擊的編輯視窗）；
    /// 離開這一列時檢查必填，通過就 INSERT 到資料表，再用主鍵讀回來（自動編號、預設值、JOIN 欄位都帶出來）
    /// </summary>
    private void SetupListInsert()
    {
        listGrid.ReadOnly = false;
        foreach (DataGridViewColumn col in listGrid.Columns)
        {
            var c = listDef.Columns.FirstOrDefault(x => x.Column == col.DataPropertyName);
            if (c == null || c.IsJoin || c.IsIdentity || c.NoEntry) col.ReadOnly = true;
        }
        listGrid.CellBeginEdit += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < listGrid.Rows.Count && listGrid.Rows[e.RowIndex].IsNewRow) return;
            if (ListRowView(e.RowIndex) is not { } v || !IsPendingRow(v.Row)) e.Cancel = true;
        };
        listGrid.RowValidating += (s, e) =>
        {
            if (e.RowIndex < listGrid.Rows.Count && listGrid.Rows[e.RowIndex].IsNewRow) return;      // 空白的新增列（沒輸入任何東西）
            if (ListRowView(e.RowIndex) is not { } view || !IsPendingRow(view.Row)) return;
            listGrid.EndEdit();
            var missing = listDef.Columns.Where(c => c.Visible && !c.IsNullable && !c.IsIdentity && !c.IsJoin && !c.NoEntry
                                                     && results.Columns.Contains(c.Column) && view.Row[c.Column] == DBNull.Value)
                                         .Select(c => c.Header ?? c.Column).ToList();
            if (missing.Count > 0)
            {
                MessageBox.Show($"請輸入：{string.Join("、", missing)}（不新增請按 Esc）", "新增");
                e.Cancel = true;
                return;
            }
            var row = view.Row;
            BeginInvoke(() => InsertListRow(row));                // 等 Grid 把這一列交給 DataTable 後再存
        };
    }

    /// <summary>
    /// 清單第 index 列對應的資料；沒有（空白的新增列、重新查詢換了資料後 Grid 還在驗證舊的列）時回傳 null。
    /// 不用 DataGridViewRow.DataBoundItem：列數跟資料對不上時它會丟 IndexOutOfRangeException
    /// </summary>
    private DataRowView ListRowView(int index)
    {
        if (listGrid.DataSource != results) return null;
        var view = results.DefaultView;
        return index >= 0 && index < view.Count ? view[index] : null;
    }

    /// <summary>把清單上新輸入的一列 INSERT 到資料表；失敗時把輸入的值放回清單，讓使用者修正</summary>
    private void InsertListRow(DataRow row)
    {
        if (row.Table != results || !IsPendingRow(row)) return;
        var values = listDef.Columns
            .Where(c => !c.IsJoin && !c.IsIdentity && !c.NoEntry && results.Columns.Contains(c.Column) && row[c.Column] != DBNull.Value)
            .ToDictionary(c => c.Column, c => row[c.Column]);
        var typed = row.ItemArray;
        results.Rows.Remove(row);
        if (InsertValues(values, notify: false)) return;
        var back = results.NewRow();
        back.ItemArray = typed;
        results.Rows.Add(back);                                 // 還是「未存檔」的列，改好離開這一列會再存一次
        listGrid.CurrentCell = listGrid.Rows[results.Rows.IndexOf(back)].Cells.Cast<DataGridViewCell>().FirstOrDefault(c => !c.ReadOnly && c.Visible);
    }

    /// <summary>清單上選取的列（由上到下）；沒有清單時是空的</summary>
    private List<int> SelectedRowIndexes() =>
        listGrid == null ? new List<int>()
        : listGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).Where(i => i >= 0 && i < results.Rows.Count).OrderBy(i => i).ToList();

    /// <summary>清單的右鍵選單：刪除、生效、取消生效選取的資料（可多選）；Delete 鍵也可以刪除</summary>
    private void SetupListBatchMenu()
    {
        var menu = new ContextMenuStrip();
        var miDelete = new ToolStripMenuItem("刪除選取的資料") { ShortcutKeyDisplayString = "Delete" };
        var miEffective = new ToolStripMenuItem("生效");
        var miUneffective = new ToolStripMenuItem("取消生效");
        var miSelectAll = new ToolStripMenuItem("全選") { ShortcutKeyDisplayString = "Ctrl+A" };
        var miExport = new ToolStripMenuItem("輸出到 Excel（CSV）…");
        var miOpen = new ToolStripMenuItem("開啟選取的資料") { ShortcutKeyDisplayString = "Enter" };
        menu.Items.Add(miOpen);
        menu.Items.Add(new ToolStripSeparator());
        miOpen.Click += (s, e) => OpenEditWindows(SelectedRowIndexes());
        menu.Items.AddRange(new ToolStripItem[] { miDelete, new ToolStripSeparator(), miEffective, miUneffective, new ToolStripSeparator(), miExport, miSelectAll });
        // 工具列也放一個「輸出」（有輸出權限才看得到）
        var btnExport = new ToolStripButton("輸出") { DisplayStyle = ToolStripItemDisplayStyle.Text, AutoSize = false, Size = new Size(64, 28),
                                                     ToolTipText = "把清單匯出成 CSV（Excel 可以直接開）；清單選了多筆時只匯出選取的", Visible = perm.Export };
        btnExport.Click += (s, e) => ExportList();
        toolStrip.Items.Insert(toolStrip.Items.IndexOf(sepExit), btnExport);
        menu.Opening += (s, e) =>
        {
            int n = SelectedRowIndexes().Count;
            bool canEdit = mode == Mode.Browse && HasKey && n > 0;
            bool effective = !string.IsNullOrWhiteSpace(listDef.EffectiveColumn);
            miDelete.Text = n > 1 ? $"刪除選取的 {n} 筆資料" : "刪除選取的資料";
            miEffective.Text = n > 1 ? $"選取的 {n} 筆生效" : "生效";
            miUneffective.Text = n > 1 ? $"選取的 {n} 筆取消生效" : "取消生效";
            miDelete.Enabled = canEdit && perm.Delete;
            miEffective.Visible = miUneffective.Visible = effective;
            miEffective.Enabled = miUneffective.Enabled = canEdit && perm.Approve;
            miExport.Visible = perm.Export;
            miOpen.Text = n > 1 ? $"開啟選取的 {n} 筆（每筆一個視窗）" : "開啟選取的資料";
            miOpen.Enabled = mode == Mode.Browse && n > 0;
            miExport.Enabled = mode == Mode.Browse && results.Rows.Count > 0;
            miExport.Text = n > 1 ? $"輸出選取的 {n} 筆到 Excel（CSV）…" : "輸出到 Excel（CSV）…";
            miSelectAll.Enabled = mode == Mode.Browse && results.Rows.Count > 0;
        };
        miDelete.Click += (s, e) => DeleteSelectedRows();
        miEffective.Click += (s, e) => SetEffectiveSelected(true);
        miUneffective.Click += (s, e) => SetEffectiveSelected(false);
        miSelectAll.Click += (s, e) => listGrid.SelectAll();
        miExport.Click += (s, e) => ExportList();
        listGrid.ContextMenuStrip = menu;
        // 右鍵點在沒選取的列上：改成只選那一列（跟檔案總管一樣）；點在已選取的列上保留多選
        listGrid.CellMouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || listGrid.Rows[e.RowIndex].Selected) return;
            listGrid.ClearSelection();
            listGrid.CurrentCell = listGrid[Math.Max(0, e.ColumnIndex), e.RowIndex];
            listGrid.Rows[e.RowIndex].Selected = true;
        };
        listGrid.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && mode == Mode.Browse && HasKey && perm.Delete)
            {
                e.Handled = true;
                DeleteSelectedRows();
            }
        };
    }

    /// <summary>
    /// 輸出：把清單匯出成 CSV（UTF-8 含 BOM，Excel 直接開中文不會亂碼），欄位、標題、格式跟清單上看到的一樣；
    /// 選了多筆時只匯出選取的，否則匯出全部查詢結果
    /// </summary>
    private void ExportList()
    {
        if (!perm.Export || results.Rows.Count == 0) return;
        var rows = SelectedRowIndexes();
        if (rows.Count <= 1) rows = Enumerable.Range(0, Math.Min(results.Rows.Count, listGrid.Rows.Count)).ToList();
        using var dlg = new SaveFileDialog { Filter = "CSV（Excel 可開）(*.csv)|*.csv", FileName = $"{def.Title}_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var columns = listGrid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        static string Cell(object v) => "\"" + Convert.ToString(v)?.Replace("\"", "\"\"") + "\"";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => Cell(c.HeaderText))));
        foreach (int r in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => Cell(listGrid.Rows[r].Cells[c.Index].FormattedValue))));
        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
        }
        catch (Exception ex)
        {
            MessageBox.Show("輸出失敗（檔案是不是正在 Excel 開著？）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Audit.Write(def.FormKey, def.TableName, "輸出", null, $"{rows.Count} 筆 → {Path.GetFileName(dlg.FileName)}");
        MessageBox.Show($"已輸出 {rows.Count} 筆到 {dlg.FileName}", "輸出");
    }

    /// <summary>刪除清單上選取的資料（可多筆；有單身時連同明細），全部在同一個交易裡</summary>
    private void DeleteSelectedRows()
    {
        var rows = SelectedRowIndexes();
        if (rows.Count == 0 || !HasKey) return;
        bool withDetail = details.Any(p => p.Def.Links.Count > 0);
        string what = rows.Count > 1 ? $"選取的 {rows.Count} 筆資料" : "這筆資料";
        if (MessageBox.Show($"確定要刪除{what}{(withDetail ? "（連同單身明細）" : "")}嗎？", "刪除",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        int deleted = 0;
        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            using var tran = conn.BeginTransaction();             // 任何一筆失敗就全部不變
            using var cmd = Dialect.CreateCommand(conn);
            cmd.Transaction = tran;
            foreach (int row in rows)
            {
                foreach (var part in details.Where(p => p.Def.Links.Count > 0))
                {
                    cmd.Parameters.Clear();
                    cmd.CommandText = $"DELETE FROM {part.Def.FullTableName(Dialect)} WHERE " + AddDetailCondition(cmd, part, row: row);
                    cmd.ExecuteNonQuery();
                }
                cmd.Parameters.Clear();
                cmd.CommandText = $"DELETE FROM {def.FullTableName} WHERE " + AddKeyCondition(cmd, KeyOf(row));
                deleted += cmd.ExecuteNonQuery();
            }
            tran.Commit();
            Audit.Write(def.FormKey, def.TableName, "刪除", string.Join(" ; ", rows.Select(r => Audit.KeyText(KeyOf(r)))), $"批次刪除 {rows.Count} 筆");
        }
        catch (Exception ex)
        {
            MessageBox.Show("刪除失敗（已全部還原）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        // 從查詢結果拿掉（由下往上刪，前面的索引才不會跑掉），改顯示原本位置的下一筆
        foreach (int row in rows.OrderByDescending(r => r)) results.Rows.RemoveAt(row);
        current = results.Rows.Count == 0 ? -1 : Math.Min(rows[0], results.Rows.Count - 1);
        listGrid.ClearSelection();
        ShowCurrent();
        SetMode(Mode.Browse);
        MessageBox.Show(deleted == rows.Count ? $"已刪除 {deleted} 筆。" : $"已刪除 {deleted} 筆（有 {rows.Count - deleted} 筆找不到，可能已被其他人刪除）。", "刪除");
        AfterSave(null);
    }

    /// <summary>清單上選取的資料一起生效（生效欄位改成 1）或取消生效（改成 0）</summary>
    private void SetEffectiveSelected(bool effective)
    {
        var rows = SelectedRowIndexes();
        string column = listDef.EffectiveColumn;
        if (rows.Count == 0 || !HasKey || string.IsNullOrWhiteSpace(column)) return;
        try
        {
            using var conn = Dialect.Open(def.ConnectionString);
            using var tran = conn.BeginTransaction();
            using var cmd = Dialect.CreateCommand(conn);
            cmd.Transaction = tran;
            foreach (int row in rows)
            {
                cmd.Parameters.Clear();
                Dialect.AddParam(cmd, "v", effective ? 1 : 0);
                cmd.CommandText = $"UPDATE {def.FullTableName} SET {Dialect.Quote(column)} = {Dialect.Param("v")} WHERE " + AddKeyCondition(cmd, KeyOf(row));
                cmd.ExecuteNonQuery();
            }
            tran.Commit();
            Audit.Write(def.FormKey, def.TableName, effective ? "生效" : "取消生效", string.Join(" ; ", rows.Select(r => Audit.KeyText(KeyOf(r)))), $"批次 {rows.Count} 筆");
        }
        catch (Exception ex)
        {
            MessageBox.Show((effective ? "生效" : "取消生效") + "失敗（已全部還原）：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        RefreshRows(rows);
        MessageBox.Show($"已{(effective ? "生效" : "取消生效")} {rows.Count} 筆。", Text);
    }

    /// <summary>用主鍵重讀查詢結果裡的這幾筆（批次處理後清單顯示最新的值），選取的列維持不變</summary>
    private void RefreshRows(IEnumerable<int> rows)
    {
        try
        {
            foreach (int row in rows)
            {
                DataRow fresh = LoadRowByKey(KeyOf(row));
                if (fresh == null) continue;
                foreach (DataColumn col in fresh.Table.Columns)
                    if (results.Columns.Contains(col.ColumnName)) results.Rows[row][col.ColumnName] = fresh[col.ColumnName];
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("重新讀取資料失敗：" + ex.Message, "提醒");
        }
        LoadDetail();
    }

    private Control CreateInput(FieldDefinition f) => InputHelper.CreateInput(f, def.ConnectionString);

    private object GetValue(FieldDefinition f) => InputHelper.GetValue(f, inputs[f]);

    private void SetValue(FieldDefinition f, object value) => InputHelper.SetValue(inputs[f], value);
}
