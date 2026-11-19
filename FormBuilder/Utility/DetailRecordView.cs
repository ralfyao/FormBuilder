using System.Data;

namespace FormBuilder;

/// <summary>
/// 單身「逐筆顯示」（GridDefinition.RecordView）：用單身的編輯視窗版面一次顯示一筆明細（像 Access 子表單的單一表單），
/// 上方有首筆、上筆、下筆、末筆與「第 n / m 筆」，「修改」開編輯視窗改這一筆。
/// 放在單身 Grid 的圓角外框裡蓋住 Grid，資料一律讀 Grid 目前的資料（Grid 照舊負責載入、輸入、存檔）；
/// 單身輸入中（多筆一起輸入）隱藏，露出 Grid
/// </summary>
public sealed class DetailRecordView : Panel
{
    private readonly DataGridView grid;
    private readonly Dictionary<FieldDefinition, Control> inputs;
    private readonly Label lblPosition;
    private readonly Button btnFirst, btnPrevious, btnNext, btnLast, btnEdit;
    private readonly Action<int> edit;
    /// <summary>目前顯示第幾筆（Grid 資料的順序，0 起算；-1 = 沒有明細）</summary>
    private int index = -1;
    /// <summary>目前明細屬於哪一筆單頭（關聯欄位的值）；換了單頭就回到第一筆</summary>
    private string boundKey;

    /// <param name="edit">按「修改」：參數是 Grid 的第幾列</param>
    /// <param name="canEdit">有修改權限、單身有主鍵才顯示「修改」</param>
    public DetailRecordView(GridDefinition g, DataGridView grid, string connectionString, Font font, Action<int> edit, bool canEdit)
    {
        this.grid = grid;
        this.edit = edit;
        Dock = DockStyle.Fill;
        BackColor = ColorText.BackOrDefault(g.EditBackColor);

        // 欄位照編輯視窗版面擺放，全部只顯示（改資料按「修改」）；畫面欄位（不存檔）沒有資料，不放
        g.EnsureEditLayout();
        var fields = g.EditColumns.Where(c => !c.ScreenOnly).Select(c => InputHelper.FromGridColumn(c, readOnly: true)).ToList();
        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor, Font = g.EditFont(font) };
        inputs = InputHelper.LayoutFields(body, fields, connectionString);
        foreach (var input in inputs.Values)
        {
            if (input is TextBox tb) tb.ReadOnly = true;
            else input.Enabled = false;
            input.TabStop = false;
        }
        body.DoubleClick += (s, e) => EditCurrent();

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
        btnEdit = NavButton("修改", EditCurrent);
        btnEdit.Visible = canEdit;

        // Fill 先加、Top 後加：WinForms 由後往前停靠，切換列才會在上面
        Controls.Add(body);
        Controls.Add(bar);

        grid.DataSourceChanged += (s, e) => ShowRecord();      // 單身重新載入、存檔後換了資料
    }

    private DataView Rows => (grid.DataSource as DataTable)?.DefaultView;
    private int Count => Rows?.Count ?? 0;

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
        Visible = !editing;
        grid.ShowCellToolTips = editing;                        // 蓋在下面的 Grid 不要跳出儲存格提示
        if (editing) return;
        BringToFront();
        ShowRecord();
    }

    private void MoveTo(int i)
    {
        if (i < 0 || i >= Count) return;
        index = i;
        ShowRecord();
    }

    private void EditCurrent()
    {
        if (btnEdit.Visible && index >= 0 && index < Count) edit(index);
    }

    /// <summary>顯示目前這筆，更新「第 n / m 筆」與按鈕狀態，Grid 的目前列也跟著（按鈕、列印用的選取列）</summary>
    private void ShowRecord()
    {
        int count = Count;
        index = count == 0 ? -1 : Math.Clamp(index < 0 ? 0 : index, 0, count - 1);
        DataRow row = index >= 0 ? Rows[index].Row : null;
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
