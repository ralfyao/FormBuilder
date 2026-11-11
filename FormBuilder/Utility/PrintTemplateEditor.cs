using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>設計畫面、編輯視窗版面：開啟按鈕的列印樣板設計（回傳 true = 樣板檔有改）</summary>
public interface IPrintTemplateHost
{
    bool DesignPrintTemplate(ButtonDefinition b);
}

/// <summary>
/// 按鈕「列印樣板檔」的「…」：交給設計畫面 / 編輯視窗版面開列印樣板設計（拖欄位排版）；
/// 找不到設計視窗時改成選一個現有的 *.print.json
/// </summary>
public class PrintTemplateEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not ButtonDefinition b) return value;   // 多選：直接打字
        var owner = Form.ActiveForm;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPrintTemplateHost host)
            {
                if (b.Action != ButtonAction.列印報表)
                {
                    MessageBox.Show("請先把「動作」改成「列印報表」，再設計列印樣板。", "列印樣板");
                    return value;
                }
                host.DesignPrintTemplate(b);
                return b.PrintTemplate;
            }
        using var dlg = new OpenFileDialog { Filter = "列印樣板 (*.print.json)|*.print.json", InitialDirectory = FormDefinition.DefaultFolder };
        if (dlg.ShowDialog(owner) != DialogResult.OK) return value;
        bool sameFolder = string.Equals(Path.GetDirectoryName(dlg.FileName), Path.GetFullPath(FormDefinition.DefaultFolder), StringComparison.OrdinalIgnoreCase);
        return sameFolder ? Path.GetFileName(dlg.FileName) : dlg.FileName;
    }
}
