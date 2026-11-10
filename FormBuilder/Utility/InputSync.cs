namespace FormBuilder;

/// <summary>
/// 編輯視窗上同一個欄位的同步複本：原本的輸入框和複本的輸入框，任一邊改了值另一邊馬上跟著變；
/// 原本的輸入框變成唯讀、停用、隱藏時，複本也跟著變。讀值、存檔只看原本的輸入框
/// </summary>
public static class InputSync
{
    public static void Link(Control original, Control copy)
    {
        bool busy = false;
        void Sync(Control from, Control to)
        {
            if (busy || from.IsDisposed || to.IsDisposed) return;
            busy = true;
            try
            {
                CopyValue(from, to);
            }
            finally
            {
                busy = false;
            }
        }
        OnChanged(original, () => Sync(original, copy));
        OnChanged(copy, () => Sync(copy, original));

        // 唯讀、停用跟著原本的輸入框（底色不跟：複本可以有自己的底色）
        void State()
        {
            if (copy.IsDisposed) return;
            copy.Enabled = original.Enabled;
            if (original is TextBox ot && copy is TextBox ct) ct.ReadOnly = ot.ReadOnly;
        }
        original.EnabledChanged += (s, e) => State();
        // 隱藏也跟著（視窗還沒顯示時 Visible 一律是 false，那時不跟）
        original.VisibleChanged += (s, e) => { if (!copy.IsDisposed && original.Parent is { Visible: true }) copy.Visible = original.Visible; };
        if (original is TextBox t) t.ReadOnlyChanged += (s, e) => State();
        State();
        Sync(original, copy);
    }

    /// <summary>輸入框的值改了（使用者輸入或程式設定）</summary>
    private static void OnChanged(Control c, Action changed)
    {
        switch (c)
        {
            case CheckBox cb:
                cb.CheckedChanged += (s, e) => changed();
                break;
            case DateTimePicker dtp:
                dtp.ValueChanged += (s, e) => changed();
                break;
            case RadioGroup radios:
                radios.SelectedValueChanged += (s, e) => changed();
                break;
            case ComboBox combo:
                combo.SelectedIndexChanged += (s, e) => changed();
                combo.TextChanged += (s, e) => changed();
                break;
            default:
                c.TextChanged += (s, e) => changed();
                break;
        }
    }

    /// <summary>把 from 顯示的值照搬到 to（兩個是同一個欄位建出來的，同一種控制項）</summary>
    private static void CopyValue(Control from, Control to)
    {
        switch (from, to)
        {
            case (CheckBox a, CheckBox b):
                b.Checked = a.Checked;
                break;
            case (DateTimePicker a, DateTimePicker b):
                b.Value = a.Value;
                if (a.ShowCheckBox && b.ShowCheckBox) b.Checked = a.Checked;
                break;
            case (RadioGroup a, RadioGroup b):
                b.SelectedValue = a.SelectedValue;
                break;
            case (ComboBox a, ComboBox b):
                if (a.SelectedItem is ComboItem item)
                {
                    // 兩邊的選項可能不一樣（開窗帶回、連動下拉加進來的值）：找不到就加進去
                    int index = -1;
                    for (int i = 0; i < b.Items.Count; i++)
                        if (b.Items[i] is ComboItem x && x.Value.Trim() == item.Value.Trim()) { index = i; break; }
                    if (index < 0) index = b.Items.Add(new ComboItem(item.Value, item.Text));
                    if (b.SelectedIndex != index) b.SelectedIndex = index;
                }
                else
                {
                    if (b.SelectedIndex != -1) b.SelectedIndex = -1;
                    if (b.DropDownStyle == ComboBoxStyle.DropDown && b.Text != a.Text) b.Text = a.Text;
                }
                break;
            default:
                if (to.Text != from.Text) to.Text = from.Text;
                break;
        }
    }
}
