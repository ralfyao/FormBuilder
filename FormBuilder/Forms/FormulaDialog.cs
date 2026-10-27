using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>
/// 編輯計算欄位的公式：左邊點選（雙擊）欄位插入名稱，按運算符號按鈕插入 + - * / ( )，下方即時檢查公式
/// </summary>
public class FormulaDialog : Form
{
    private readonly TextBox txtFormula = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Microsoft JhengHei UI", 11F) };
    private readonly ListBox lstFields = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label lblCheck = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly List<string> knownNames;

    public string Formula => txtFormula.Text.Trim();

    /// <param name="fields">可以用的欄位（畫面上的其他欄位）</param>
    public FormulaDialog(string formula, IEnumerable<FieldDefinition> fields)
    {
        Text = "公式：欄位名稱和數字做 + - * /，可以用括號";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 420);
        var list = fields.ToList();
        knownNames = list.Select(f => f.Column).ToList();
        foreach (var f in list) lstFields.Items.Add(new FieldItem(f.Column, f.Label));
        txtFormula.Text = formula ?? "";

        // 左邊：欄位清單（雙擊插入）
        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(8, 8, 4, 8) };
        left.Controls.Add(lstFields);
        left.Controls.Add(new Label { Text = "畫面上的欄位（雙擊插入）", Dock = DockStyle.Top, Height = 22 });

        // 右邊：運算符號按鈕、公式、檢查結果
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 8, 8) };
        var ops = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 };
        foreach (string op in new[] { "+", "-", "*", "/", "(", ")" })
        {
            var b = new Button { Text = op, Width = 40, Height = 28 };
            b.Click += (s, e) => Insert($" {op} ");
            ops.Controls.Add(b);
        }
        right.Controls.Add(txtFormula);
        right.Controls.Add(ops);
        right.Controls.Add(new Label
        {
            Text = "例如：(達成客戶要求的能力 + 提升經營效能的企圖) * 0.5。欄位名稱有空白或運算符號時用 [ ] 括起來；欄位沒填當成 0",
            Dock = DockStyle.Top, Height = 40,
        });

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90 };
        var btnCancel = new Button { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(lblCheck);
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);

        Controls.Add(right);
        Controls.Add(left);
        Controls.Add(bottom);
        AcceptButton = null;                               // 公式可以換行，Enter 不要直接確定
        CancelButton = btnCancel;

        lstFields.DoubleClick += (s, e) =>
        {
            if (lstFields.SelectedItem is FieldItem item) Insert(NameText(item.Column));
        };
        txtFormula.TextChanged += (s, e) => ShowCheck();
        btnOk.Click += (s, e) =>
        {
            string error = Formula.Length == 0 ? null : FormBuilder.Formula.Check(Formula, knownNames);
            if (error != null && MessageBox.Show(error + "\n\n仍要使用這個公式嗎？", "公式", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            DialogResult = DialogResult.OK;
        };
        ShowCheck();
    }

    /// <summary>欄位名稱有空白或運算符號時加上 [ ]</summary>
    private static string NameText(string name) => name.Any(c => char.IsWhiteSpace(c) || "+-*/()（）[]".Contains(c)) ? $"[{name}]" : name;

    private void Insert(string text)
    {
        txtFormula.SelectedText = text;
        txtFormula.Focus();
    }

    private void ShowCheck()
    {
        string error = Formula.Length == 0 ? null : FormBuilder.Formula.Check(Formula, knownNames);
        lblCheck.ForeColor = error == null ? Color.DarkGreen : Color.Firebrick;
        lblCheck.Text = Formula.Length == 0 ? "還沒有輸入公式" : error ?? "公式格式正確";
    }

    private sealed record FieldItem(string Column, string Label)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(Label) || Label == Column ? Column : $"{Label}（{Column}）";
    }
}

/// <summary>屬性面板「公式」右邊的「…」：開啟公式編輯視窗（可以用的欄位由設計畫面提供）</summary>
public class FormulaEditor : UITypeEditor
{
    /// <summary>設計畫面目前的欄位（設計畫面開啟時設定）</summary>
    public static Func<IEnumerable<FieldDefinition>> Fields { get; set; } = () => Enumerable.Empty<FieldDefinition>();

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        var self = context?.Instance as FieldDefinition;
        using var dlg = new FormulaDialog(value as string, Fields().Where(f => f != self));
        return dlg.ShowDialog() == DialogResult.OK ? (dlg.Formula == "" ? null : dlg.Formula) : value;
    }
}
