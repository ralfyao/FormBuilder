using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;

namespace FormBuilder;

/// <summary>
/// 屬性面板的「多選」編輯器（[Flags] 列舉用）：按右邊的下拉箭頭打開勾選清單，勾選的項目合起來就是值；
/// 不用像 True / False 那樣一個一個屬性下拉選
/// </summary>
public class FlagsCheckEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (provider?.GetService(typeof(IWindowsFormsEditorService)) is not IWindowsFormsEditorService service || value is not Enum current)
            return value;
        var type = current.GetType();
        long bits = Convert.ToInt64(current);
        var list = new CheckedListBox { CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false };
        var flags = Enum.GetValues(type).Cast<Enum>().Where(f => Convert.ToInt64(f) != 0).ToList();   // 0（無）不列出來，全不勾就是 0
        foreach (var f in flags)
            list.Items.Add(f, (bits & Convert.ToInt64(f)) != 0);
        list.Height = Math.Min(flags.Count, 10) * (list.ItemHeight + 2) + 6;
        service.DropDownControl(list);
        long result = list.CheckedItems.Cast<Enum>().Aggregate(0L, (acc, f) => acc | Convert.ToInt64(f));
        list.Dispose();
        return Enum.ToObject(type, result);
    }
}
