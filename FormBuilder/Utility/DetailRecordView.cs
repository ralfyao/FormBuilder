using System.Data;

namespace FormBuilder;

/// <summary>
/// 單身「逐筆顯示」（GridDefinition.RecordView）：用單身的編輯視窗版面一次顯示一筆明細（像 Access 子表單的單一表單），
/// 上方有首筆、上筆、下筆、末筆與「第 n / m 筆」；按「修改」直接在這裡改這一筆（欄位變成可以輸入，上方換成確定、取消），不另外開視窗。
/// 放在單身 Grid 的圓角外框裡蓋住 Grid，資料一律讀 Grid 目前的資料（Grid 照舊負責載入、多筆輸入、存檔）；
/// 單身輸入中（按「單身」多筆一起輸入）隱藏，露出 Grid
/// </summary>
public sealed class DetailRecordView : Panel
{
    private readonly DataGridView grid;
    private readonly Dictionary<FieldDefinition, Control> inputs;
    private readonly List<Control> focusOrder;
    private readonly Label lblPosition;
    private readonly Button btnFirst, btnPrevious, btnNext, btnLast, btnEdit, btnOk, btnCancel;
    private readonly Func<DataRow, Dictionary<string, object>, bool> save;
    private readonly Action<bool> editingChanged;
    private readonly bool canEdit;
    /// <summary>目前顯示第幾筆（Grid 資料的順序，0 起算；-1 = 沒有明細）</summary>
    private int index = -1;
    /// <summary>目前明細屬於哪一筆單頭（關聯欄位的值）；換了單頭就回到第一筆</summary>
    private string boundKey;

    /// <summary>正在原地修改這一筆</summary>
    public bool IsEditing { get; private set; }

    /// <param name="locked">不能改的欄位（自動編號、主鍵、關聯欄位、只顯示）</param>
    /// <param name="save">按「確定」：這一筆的資料列、改好的值（欄位 → 值）；存好回傳 true（存檔後重新載入單身）</param>
    /// <param name="editingChanged">開始 / 結束原地修改（畫面工具列、單頭清單要跟著停用）</param>
    /// <param name="canEdit">有修改權限、單身有主鍵才顯示「修改」</param>
    /// <param name="sourcePath">欄位開窗選取的畫面定義檔以它為準</param>
    public DetailRecordView(GridDefinition g, DataGridView grid, string connectionString, Font font, Func<GridColumnDefinition, bool> locked,
                            Func<DataRow, Dictionary<string, object>, bool> save, Action<bool> editingChanged, bool canEdit, string sourcePath = null)
    {
        this.grid = grid;
        this.save = save;
        this.editingChanged = editingChanged;
        this.canEdit = canEdit;
        Dock = DockStyle.Fill;
        BackColor = ColorText.BackOrDefault(g.EditBackColor);

        // 欄位照編輯視窗版面擺放；平常全部只顯示，按「修改」才開放可以改的欄位；畫面欄位（不存檔）沒有資料，不放
        g.EnsureEditLayout();
        var fields = g.EditColumns.Where(c => !c.ScreenOnly).Select(c => InputHelper.FromGridColumn(c, readOnly: locked(c))).ToList();
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor, Font = g.EditFont(font) };
        inputs = InputHelper.LayoutFields(body, fields, connectionString);
        focusOrder = InputHelper.ApplyFocusOrder(inputs);
        SetLocked(true);
        body.DoubleClick += (s, e) => BeginEdit();

        // 修改中：開窗選取、下拉選單的執行後動作 / 選取回傳對應、連動下拉，@名稱 先找這裡的欄位，沒有就用這一筆明細的值
        ButtonRunner.AttachPickers(inputs, b =>
        {
            if (IsEditing) ButtonRunner.OpenForm(b, sourcePath, FindForm(), ValueOf, values => ButtonRunner.FillFields(values, inputs), connectionString: connectionString);
        });
        ButtonRunner.AttachAfterSelect(inputs, connectionString, ValueOf);
        InputHelper.AttachDependentItems(inputs, connectionString, ValueOf);

        // 上方的切換列
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(4, 3, 4, 0),
            WrapContents = false,
            BackColor = SystemColors.Control,
            Font = font,
        };
        Button NavButton(string text, Action click)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 26, Margin = new Padding(2, 0, 2, 0), UseVisualStyleBackColor = true };
            b.Click += (s, e) => click();
            bar.Controls.Add(b);
            return b;
        }
        btnFirst = NavButton("首筆", () => MoveTo(0));
        btnPrevious = NavButton("上筆", () => MoveTo(index - 1));
        btnNext = NavButton("下筆", () => MoveTo(index + 1));
        btnLast = NavButton("末筆", () => MoveTo(Count - 1));
        lblPosition = new Label { AutoSize = true, Margin = new Padding(10, 6, 10, 0) };
        bar.Controls.Add(lblPosition);
        btnEdit = NavButton("修改", BeginEdit);
        btnEdit.Visible = canEdit;
        btnOk = NavButton("確定", Accept);
        btnCancel = NavButton("取消", CancelEdit);
        btnOk.Visible = btnCancel.Visible = false;

        // Fill 先加、Top 後加：WinForms 由後往前停靠，切換列才會在上面
        Controls.Add(body);
        Controls.Add(bar);

        grid.DataSourceChanged += (s, e) => ShowRecord();      // 單身重新載入、存檔後換了資料
    }

    private DataView Rows => (grid.DataSource as DataTable)?.DefaultView;
    private int Count => Rows?.Count ?? 0;
    private DataRow CurrentRow => index >= 0 && index < Count ? Rows[index].Row : null;

    /// <summary>@名稱的值：先找這裡的欄位，沒有就用這一筆明細的值</summary>
    private object ValueOf(string name)
    {
        var f = inputs.Keys.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (f != null) return InputHelper.GetValue(f, inputs[f]);
        return CurrentRow is { } row && row.Table.Columns.Contains(name) ? row[name] : null;
    }

    /// <summary>
    /// 單身重新載入後呼叫：key = 這批明細屬於哪一筆單頭（null = 沒有單頭）。
    /// 換了單頭回到第一筆；同一筆單頭（例如修改明細後重讀）停在原本那筆
    /// </summary>
    public void Bind(string key)
    {
        if (key != boundKey) index = 0;
        boundKey = key;
        ShowRecord();
    }

    /// <summary>editing = 單身輸入中：隱藏逐筆顯示，露出 Grid 讓使用者多筆一起輸入；結束後回到逐筆顯示</summary>
    public void SetEditing(bool editing)
    {
        if (editing && IsEditing) EndEdit();
        Visible = !editing;
        grid.ShowCellToolTips = editing;                        // 蓋在下面的 Grid 不要跳出儲存格提示
        if (editing) return;
        BringToFront();
        ShowRecord();
    }

    /// <summary>locked = 全部只顯示；false = 開放可以改的欄位（不是只顯示、沒有公式的）</summary>
    private void SetLocked(bool locked)
    {
        foreach (var (f, input) in inputs)
        {
            bool canType = !locked && !f.NoEntry && !f.IsReadOnly;
            if (input is TextBox tb) tb.ReadOnly = !canType;
            else input.Enabled = canType;
            input.TabStop = canType;
        }
    }

    private void MoveTo(int i)
    {
        if (IsEditing || i < 0 || i >= Count) return;
        index = i;
        ShowRecord();
    }

    /// <summary>按「修改」（或雙擊）：這一筆直接在這裡改</summary>
    private void BeginEdit()
    {
        if (IsEditing || !btnEdit.Visible || !btnEdit.Enabled || CurrentRow == null) return;
        IsEditing = true;
        SetLocked(false);
        foreach (var b in new[] { btnFirst, btnPrevious, btnNext, btnLast, btnEdit }) b.Visible = false;
        btnOk.Visible = btnCancel.Visible = true;
        lblPosition.Text = $"修改第 {index + 1} / {Count} 筆：改完按「確定」存檔，Esc 取消";
        editingChanged?.Invoke(true);
        InputHelper.FocusFirst(focusOrder);
    }

    private void Accept()
    {
        if (!IsEditing || CurrentRow is not { } row) return;
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (f, input) in inputs.Where(p => !p.Key.NoEntry && !p.Key.QueryOnly))
                values[f.Column] = InputHelper.GetValue(f, input);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }
        var empty = inputs.Keys.Where(f => !f.NoEntry && !f.QueryOnly && !f.IsNullable && values[f.Column] == DBNull.Value).Select(f => f.Label).ToList();
        if (empty.Count > 0)
        {
            MessageBox.Show("請輸入：" + string.Join("、", empty), "提醒");
            return;
        }
        if (values.Count == 0 || save(row, values)) EndEdit();
    }

    private void CancelEdit()
    {
        if (IsEditing) EndEdit();
    }

    private void EndEdit()
    {
        IsEditing = false;
        SetLocked(true);
        btnOk.Visible = btnCancel.Visible = false;
        foreach (var b in new[] { btnFirst, btnPrevious, btnNext, btnLast }) b.Visible = true;
        btnEdit.Visible = canEdit;
        editingChanged?.Invoke(false);
        ShowRecord();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (IsEditing)
        {
            if (keyData == Keys.Escape) { CancelEdit(); return true; }
            if (keyData == Keys.Enter && !inputs.Values.Any(c => c.ContainsFocus && (c is TextBox { Multiline: true } || c is ComboBox { DroppedDown: true })))
            {
                if (!InputHelper.FocusNext(focusOrder, out _)) Accept();
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>顯示目前這筆，更新「第 n / m 筆」與按鈕狀態，Grid 的目前列也跟著（按鈕、列印用的選取列）</summary>
    private void ShowRecord()
    {
        if (IsEditing) return;                                  // 修改中不要被重新載入蓋掉正在輸入的內容
        int count = Count;
        index = count == 0 ? -1 : Math.Clamp(index < 0 ? 0 : index, 0, count - 1);
        DataRow row = CurrentRow;
        foreach (var (f, input) in inputs)
            InputHelper.SetValue(input, row != null && row.Table.Columns.Contains(f.Column) ? row[f.Column] : DBNull.Value);
        lblPosition.Text = count == 0 ? "沒有資料" : $"第 {index + 1} / {count} 筆";
        btnFirst.Enabled = btnPrevious.Enabled = index > 0;
        btnNext.Enabled = btnLast.Enabled = index >= 0 && index < count - 1;
        btnEdit.Enabled = index >= 0;
        try
        {
            var first = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).FirstOrDefault();
            if (index >= 0 && index < grid.Rows.Count && first != null) grid.CurrentCell = grid.Rows[index].Cells[first.Index];
        }
        catch (InvalidOperationException) { }                   // Grid 正在編輯、還沒建好列時不同步
    }
}
