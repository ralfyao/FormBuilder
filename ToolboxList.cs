namespace FormBuilder;

/// <summary>工具箱清單（像 Visual Studio 的工具箱）：按住拖到畫面上加入，或雙擊加入</summary>
public class ToolboxList : ListBox
{
    /// <summary>雙擊某個元件時觸發，參數是元件種類（Button / ComboBox）</summary>
    public event Action<string> ItemActivated;

    public ToolboxList()
    {
        Dock = DockStyle.Fill;
        IntegralHeight = false;
        Items.Add(new Entry("Button", "按鈕 (Button)"));
        Items.Add(new Entry("ComboBox", "下拉選單 (ComboBox)"));
        MouseDown += (s, e) =>
        {
            int i = IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && i >= 0 && Items[i] is Entry entry)
                DoDragDrop(new ToolboxDragData { Kind = entry.Kind }, DragDropEffects.Copy);
        };
        DoubleClick += (s, e) =>
        {
            if (SelectedItem is Entry entry) ItemActivated?.Invoke(entry.Kind);
        };
    }

    private record Entry(string Kind, string Text)
    {
        public override string ToString() => Text;
    }
}
