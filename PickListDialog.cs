namespace FormBuilder;

/// <summary>從清單勾選一個或多個項目（可篩選）；回傳勾選項目的 Key</summary>
public partial class PickListDialog : Form
{
    public record Item(string Key, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly List<Item> all;
    private readonly HashSet<string> picked = new();

    public List<string> PickedKeys { get; private set; } = new();

    public PickListDialog(string title, IEnumerable<Item> items)
    {
        InitializeComponent();
        Text = title;
        all = items.ToList();
        txtFilter.TextChanged += (s, e) => Fill();
        // 勾選狀態記在 picked，篩選時才不會遺失
        lstItems.ItemCheck += (s, e) =>
        {
            if (lstItems.Items[e.Index] is not Item item) return;
            if (e.NewValue == CheckState.Checked) picked.Add(item.Key); else picked.Remove(item.Key);
        };
        lstItems.DoubleClick += (s, e) => { if (picked.Count > 0) Accept(); };
        btnOk.Click += (s, e) => Accept();
        Fill();
    }

    private void Fill()
    {
        string filter = txtFilter.Text.Trim();
        lstItems.BeginUpdate();
        lstItems.Items.Clear();
        foreach (var item in all.Where(i => filter == "" || i.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            lstItems.Items.Add(item, picked.Contains(item.Key));
        lstItems.EndUpdate();
    }

    private void Accept()
    {
        if (picked.Count == 0)
        {
            MessageBox.Show("請勾選至少一個項目。", Text);
            return;
        }
        PickedKeys = all.Where(i => picked.Contains(i.Key)).Select(i => i.Key).ToList();
        DialogResult = DialogResult.OK;
    }
}
