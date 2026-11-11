using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>
/// 編輯計算欄位的公式：左邊點選（雙擊）欄位插入名稱，按運算符號按鈕插入 + - * / ( ) &amp; 和 "文字"，下方即時檢查公式
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
        : this(formula, fields.Select(f => (f.Column, f.Label)))
    {
    }

    /// <param name="fields">可以用的欄位名稱與標籤（例如 Grid 同一列的其他欄位）</param>
    /// <param name="masterFields">另外列出的畫面上的欄位（Grid 公式用到同一列沒有的欄位時取畫面上的值）；同名的以同一列為準</param>
    /// <param name="detailFields">明細欄位（列印樣板用）：雙擊插入 SUM(欄位)，可以再改成 AVG、COUNT、MAX、MIN</param>
    public FormulaDialog(string formula, IEnumerable<(string Column, string Label)> fields, string fieldsTitle = "畫面上的欄位（雙擊插入）",
                         IEnumerable<(string Column, string Label)> masterFields = null,
                         IEnumerable<(string Column, string Label)> detailFields = null)
    {
        Text = "公式：欄位名稱和數字做 + - * /，可以用括號；& 接文字";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 420);
        var list = fields.ToList();
        var masters = (masterFields ?? Enumerable.Empty<(string Column, string Label)>())
            .Where(m => !list.Any(f => f.Column.Equals(m.Column, StringComparison.OrdinalIgnoreCase))).ToList();
        knownNames = list.Select(f => f.Column).Concat(masters.Select(m => m.Column)).ToList();
        foreach (var (column, label) in list) lstFields.Items.Add(new FieldItem(column, label));
        if (masters.Count > 0)
        {
            lstFields.Items.Add("── 畫面上的欄位 ──");
            foreach (var (column, label) in masters) lstFields.Items.Add(new FieldItem(column, label));
        }
        var details = (detailFields ?? Enumerable.Empty<(string Column, string Label)>()).ToList();
        if (details.Count > 0)
        {
            lstFields.Items.Add("── 明細欄位（雙擊插入 SUM 加總）──");
            foreach (var (column, label) in details) lstFields.Items.Add(new FieldItem(column, label, Aggregate: true));
        }
        txtFormula.Text = formula ?? "";

        // 左邊：欄位清單（雙擊插入）
        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(8, 8, 4, 8) };
        left.Controls.Add(lstFields);
        left.Controls.Add(new Label { Text = fieldsTitle, Dock = DockStyle.Top, Height = 22 });

        // 右邊：運算符號按鈕、公式、檢查結果
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 8, 8) };
        var ops = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 };
        foreach (string op in new[] { "+", "-", "*", "/", "(", ")", "&" })
        {
            var b = new Button { Text = op, Width = 40, Height = 28, UseMnemonic = false };     // & 不要當成快速鍵符號
            b.Click += (s, e) => Insert($" {op} ");
            ops.Controls.Add(b);
        }
        var btnText = new Button { Text = "\"文字\"", Width = 60, Height = 28 };
        btnText.Click += (s, e) =>
        {
            Insert("\"\"");
            txtFormula.SelectionStart--;                   // 游標放在引號中間，直接打字
        };
        ops.Controls.Add(btnText);
        var btnNow = new Button { Text = "NOW()", Width = 64, Height = 28 };
        btnNow.Click += (s, e) => Insert("NOW()");                 // 目前的日期
        ops.Controls.Add(btnNow);
        right.Controls.Add(txtFormula);
        right.Controls.Add(ops);
        right.Controls.Add(new Label
        {
            Text = "例如：(達成客戶要求的能力 + 提升經營效能的企圖) * 0.5。欄位名稱有空白或運算符號時用 [ ] 括起來；欄位沒填當成 0。\n"
                 + "接文字用 &，文字用雙引號：100 - 單價1 / 單價 * 100 & \"%\"、單號 & \"-\" & 品名（& 最後才接，結果是文字）；NOW() = 今天的日期",
            Dock = DockStyle.Top, Height = 58, UseMnemonic = false,      // & 照字面顯示
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
            if (lstFields.SelectedItem is FieldItem item) Insert(item.Aggregate ? $"SUM({item.Column})" : NameText(item.Column));
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
    private static string NameText(string name) => name.Any(c => char.IsWhiteSpace(c) || "+-*/&()（）[]\"'".Contains(c)) ? $"[{name}]" : name;

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

    private sealed record FieldItem(string Column, string Label, bool Aggregate = false)
    {
        public override string ToString() => (Aggregate ? "SUM：" : "") + (string.IsNullOrWhiteSpace(Label) || Label == Column ? Column : $"{Label}（{Column}）");
    }
}

/// <summary>列印樣板元件「公式」的「…」：左邊列出單頭欄位、明細欄位（雙擊插入 SUM(…)），欄位由列印樣板設計器提供</summary>
public class PrintFormulaEditor : UITypeEditor
{
    /// <summary>單頭可以用的欄位（列印樣板設計器開啟時設定）</summary>
    public static Func<IEnumerable<(string Column, string Label)>> HeaderFields { get; set; } = () => Enumerable.Empty<(string, string)>();
    /// <summary>明細欄位</summary>
    public static Func<IEnumerable<(string Column, string Label)>> DetailFields { get; set; } = () => Enumerable.Empty<(string, string)>();

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        using var dlg = new FormulaDialog(value as string, HeaderFields(), "單頭欄位（雙擊插入）", detailFields: DetailFields());
        dlg.Text = "公式：單頭欄位做 + - * /，SUM(明細欄位) 加總明細；& 接文字";
        return dlg.ShowDialog(Form.ActiveForm) == DialogResult.OK ? (dlg.Formula == "" ? null : dlg.Formula) : value;
    }
}

/// <summary>明細表格欄位「公式」的「…」：左邊列出單身欄位（同一列的值），另外列出單頭欄位</summary>
public class PrintColumnFormulaEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        var self = (context?.Instance as PrintTableColumn)?.Column;
        using var dlg = new FormulaDialog(value as string,
            PrintFormulaEditor.DetailFields().Where(f => !string.Equals(f.Column, self, StringComparison.OrdinalIgnoreCase)),
            "單身欄位（同一列，雙擊插入）", masterFields: PrintFormulaEditor.HeaderFields());
        dlg.Text = "表格欄位公式：同一列的單身欄位做 + - * /，例如 數量2 * 單價2；& 接文字";
        return dlg.ShowDialog(Form.ActiveForm) == DialogResult.OK ? (dlg.Formula == "" ? null : dlg.Formula) : value;
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
