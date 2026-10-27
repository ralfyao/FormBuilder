namespace FormBuilder;

/// <summary>
/// 明細表格要印哪些單身欄位：勾選要印的欄位，上移 / 下移調整順序（由上到下 = 表格由左到右）。
/// 原本就在表格裡的欄位保留寬度、格式等設定；按「確定」後結果在 Columns
/// </summary>
public partial class TableColumnsDialog : Form
{
    /// <summary>清單的一項：單身欄位 + 原本表格裡的設定（沒有 = 新加的）</summary>
    private sealed class Entry
    {
        public string Column { get; init; }
        public string Header { get; init; }
        public PrintTableColumn Existing { get; init; }
        public override string ToString() => string.IsNullOrWhiteSpace(Header) || Header == Column ? Column : $"{Header}（{Column}）";
    }

    public List<PrintTableColumn> Columns { get; } = new();

    /// <param name="detailColumns">單身的全部欄位</param>
    /// <param name="current">表格目前的欄位</param>
    public TableColumnsDialog(IEnumerable<GridColumnDefinition> detailColumns, IEnumerable<PrintTableColumn> current)
    {
        InitializeComponent();
        var all = detailColumns.ToList();
        var used = current.ToList();
        // 已經在表格裡的照表格的順序排在前面，其他單身欄位接在後面
        foreach (var c in used)
        {
            var d = all.FirstOrDefault(x => x.Column.Equals(c.Column ?? "", StringComparison.OrdinalIgnoreCase));
            lstColumns.Items.Add(new Entry { Column = c.Column, Header = d?.Header ?? c.Header, Existing = c }, true);
        }
        foreach (var d in all.Where(d => !used.Any(c => d.Column.Equals(c.Column ?? "", StringComparison.OrdinalIgnoreCase))))
            lstColumns.Items.Add(new Entry { Column = d.Column, Header = d.Header }, false);

        btnUp.Click += (s, e) => Move(-1);
        btnDown.Click += (s, e) => Move(1);
        btnAll.Click += (s, e) => CheckAll(true);
        btnNone.Click += (s, e) => CheckAll(false);
        btnOk.Click += (s, e) => Apply();
    }

    private void Move(int step)
    {
        int i = lstColumns.SelectedIndex;
        int j = i + step;
        if (i < 0 || j < 0 || j >= lstColumns.Items.Count) return;
        var item = lstColumns.Items[i];
        bool check = lstColumns.GetItemChecked(i);
        lstColumns.Items.RemoveAt(i);
        lstColumns.Items.Insert(j, item);
        lstColumns.SetItemChecked(j, check);
        lstColumns.SelectedIndex = j;
    }

    private void CheckAll(bool value)
    {
        for (int i = 0; i < lstColumns.Items.Count; i++) lstColumns.SetItemChecked(i, value);
    }

    private void Apply()
    {
        for (int i = 0; i < lstColumns.Items.Count; i++)
        {
            if (!lstColumns.GetItemChecked(i)) continue;
            var entry = (Entry)lstColumns.Items[i];
            Columns.Add(entry.Existing ?? new PrintTableColumn { Column = entry.Column, Header = entry.Header });
        }
        DialogResult = DialogResult.OK;
    }
}
