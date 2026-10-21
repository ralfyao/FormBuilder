namespace FormBuilder;

/// <summary>從清單選一個欄位（工具箱拖下拉選單到畫面時，選它要對應哪個欄位）</summary>
public partial class ColumnPickDialog : Form
{
    public ColumnInfo SelectedColumn => list.SelectedItem is Item item ? item.Column : null;

    public ColumnPickDialog(string title, IEnumerable<ColumnInfo> columns)
    {
        InitializeComponent();
        Text = title;
        foreach (var c in columns) list.Items.Add(new Item(c));
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        list.DoubleClick += (s, e) => Accept();
        btnOk.Click += (s, e) => Accept();
    }

    private void Accept()
    {
        if (SelectedColumn == null) return;
        DialogResult = DialogResult.OK;
    }

    private record Item(ColumnInfo Column)
    {
        public override string ToString() =>
            $"{Column.Name}  {Column.DataType}" + (string.IsNullOrWhiteSpace(Column.Description) ? "" : $"  {Column.Description}");
    }
}
