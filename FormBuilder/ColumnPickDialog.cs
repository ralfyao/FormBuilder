namespace FormBuilder;

/// <summary>從清單選一個欄位（工具箱拖下拉選單到畫面時，選它要對應哪個欄位）</summary>
public class ColumnPickDialog : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    public ColumnInfo SelectedColumn => list.SelectedItem is Item item ? item.Column : null;

    public ColumnPickDialog(string title, IEnumerable<ColumnInfo> columns)
    {
        Text = title;
        Font = new Font("Microsoft JhengHei UI", 9F);
        Size = new Size(420, 480);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        foreach (var c in columns) list.Items.Add(new Item(c));
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        list.DoubleClick += (s, e) => Accept();

        var btnOk = new Button { Text = "確定", Width = 90, Dock = DockStyle.Right };
        btnOk.Click += (s, e) => Accept();
        var btnCancel = new Button { Text = "取消", Width = 90, Dock = DockStyle.Right, DialogResult = DialogResult.Cancel };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(6) };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(list);
        Controls.Add(new Label { Text = "選擇下拉選單要存到哪個欄位：", Dock = DockStyle.Top, Height = 24 });
        Controls.Add(bottom);
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
