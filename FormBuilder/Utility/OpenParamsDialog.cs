using System.ComponentModel;
using System.Drawing.Design;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// 屬性面板「傳入參數」的「…」按鈕（按鈕、清單雙擊開啟畫面、欄位開窗）：開「傳入參數設定」，
/// 列出要開啟的畫面收得到哪些參數，每個從本畫面的欄位下拉選，不用自己打欄位名稱
/// </summary>
public class OpenParamsEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        // 要開的畫面、清單那一列的欄位（雙擊那一列時 @名稱 先取那一列）
        (string form, IEnumerable<string> rowColumns) = context?.Instance switch
        {
            ButtonDefinition b => (b.AllTargetForms.FirstOrDefault(), Enumerable.Empty<string>()),   // 沒填畫面定義檔時用依條件開啟的第一個畫面
            GridDefinition g => (g.OpenForm, g.Columns.Select(c => c.Column)),
            EditListDefinition l => (l.OpenForm, l.Columns.Select(c => c.Column)),
            IPickerField f => (f.PickerForm, Enumerable.Empty<string>()),
            _ => (null, null),
        };
        if (rowColumns == null) return value;               // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        IEnumerable<string> locals = null;
        string folder = null;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h)
            {
                locals ??= h.ParamColumns;
                folder ??= h.PickerFolder;
            }
        using var dialog = new OpenParamsDialog(form, value as string, folder ?? FormDefinition.DefaultFolder,
                                                rowColumns.Concat(locals ?? Enumerable.Empty<string>()));
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Params : value;
    }
}

/// <summary>
/// 傳入參數設定：列出要開啟的畫面收得到的參數（單頭欄位、文字標籤、SQL 裡的 @名稱），
/// 每個參數從本畫面的欄位下拉選（寫成 @欄位），或自己輸入 SQL 運算式、固定值；
/// 本畫面找不到的 @欄位（通常是打錯字）用紅字標出來
/// </summary>
public class OpenParamsDialog : Form
{
    private const string ColName = "Name", ColNote = "Note", ColLocal = "Local", ColCustom = "Custom";
    private static readonly Regex FieldRef = new(@"^@([\p{L}\p{N}_]+)$");
    private static readonly Regex SqlParam = new(@"(?<!@)@([\p{L}\p{N}_]+)");

    private readonly List<string> locals;
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToDeleteRows = true,
        RowHeadersWidth = 24,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };
    private readonly Label lblInfo = new() { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft };
    private bool syncing;

    /// <summary>按確定後的傳入參數（「對方欄位=值」用逗號分隔；沒有任何參數 = null）</summary>
    public string Params { get; private set; }

    /// <param name="formFile">要開啟的畫面（畫面定義檔）</param>
    /// <param name="localColumns">本畫面 @名稱 取得到值的欄位</param>
    public OpenParamsDialog(string formFile, string openParams, string folder, IEnumerable<string> localColumns)
    {
        locals = localColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Text = "傳入參數設定";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 500);

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColName, HeaderText = "對方畫面的參數", FillWeight = 25 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColNote, HeaderText = "說明", ReadOnly = true, FillWeight = 25 });
        var colLocal = new DataGridViewComboBoxColumn
        {
            Name = ColLocal,
            HeaderText = "帶入本畫面的欄位",
            FillWeight = 25,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colLocal.Items.Add("");
        colLocal.Items.AddRange(locals.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        grid.Columns.Add(colLocal);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColCustom, HeaderText = "或輸入值（SQL 運算式 / 固定值）", FillWeight = 25 });
        grid.Columns[ColNote].DefaultCellStyle.ForeColor = SystemColors.GrayText;
        // 下拉一選就生效（不用離開儲存格）
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (s, e) =>
        {
            if (syncing || e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            syncing = true;
            // 選了本畫面欄位就清掉自訂值，反過來也一樣（一個參數只有一個值）
            if (e.ColumnIndex == grid.Columns[ColLocal].Index && !string.IsNullOrEmpty(row.Cells[ColLocal].Value as string))
                row.Cells[ColCustom].Value = null;
            else if (e.ColumnIndex == grid.Columns[ColCustom].Index && !string.IsNullOrWhiteSpace(row.Cells[ColCustom].Value as string))
                row.Cells[ColLocal].Value = "";
            syncing = false;
            Check(row);
        };
        grid.DataError += (s, e) => e.ThrowException = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(lblInfo, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "開啟時把本畫面欄位的值傳過去：對方畫面有同名欄位時自動用它查詢，對方的 SQL 也可以用 @參數名稱 取得。"
                 + "兩邊都空白的參數不傳；清單沒列到的參數可以在最下面一列自己加。紅字 = 本畫面找不到這個欄位（多半是打錯字）。",
        }, 0, 2);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);
        Controls.Add(layout);
        Controls.Add(bottom);
        CancelButton = btnCancel;                         // 不設 AcceptButton：儲存格裡按 Enter 是確認輸入
        btnOk.Click += (s, e) =>
        {
            grid.EndEdit();
            Params = BuildParams();
        };

        Fill(formFile, openParams, folder);
    }

    /// <summary>列出對方畫面的參數，填上目前的設定（目前設定裡有、對方畫面沒列到的也列出來）</summary>
    private void Fill(string formFile, string openParams, string folder)
    {
        var names = new List<(string Name, string Note)>();
        void Add(string name, string note)
        {
            if (!string.IsNullOrWhiteSpace(name) && !names.Any(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) names.Add((name, note));
        }
        if (string.IsNullOrWhiteSpace(formFile))
            lblInfo.Text = "還沒有設定要開啟的畫面，請先選畫面定義檔；也可以直接在下面輸入參數。";
        else if (PickerFormDialog.LoadDefinition(formFile, folder, out string error) is not { } target)
            lblInfo.Text = error;
        else
        {
            lblInfo.Text = $"開啟「{target.Title}」（{formFile}）：可以傳入的參數";
            foreach (var f in target.Fields)
                Add(f.Column, f.IsTextLabel ? "文字標籤（顯示傳入的值）" : (f.Label ?? "") + (f.IsCalculated || f.IsJoin ? "" : "（單頭欄位，自動查詢）"));
            foreach (var sql in target.Grids.SelectMany(g => new[] { g.Sql, g.ListSql }).Concat(target.Buttons.Select(b => b.Sql)))
                foreach (Match m in SqlParam.Matches(sql ?? ""))
                    Add(m.Groups[1].Value, "對方 SQL 用的 @名稱");
        }
        var current = ButtonRunner.ParsePairs(openParams).ToList();
        foreach (var (name, _) in current) Add(name, "（對方畫面沒有列出這個參數）");

        foreach (var (name, note) in names)
        {
            string value = current.FirstOrDefault(p => p.Left.Equals(name, StringComparison.OrdinalIgnoreCase)).Right;
            string local = value != null && FieldRef.Match(value) is { Success: true } m
                ? locals.FirstOrDefault(l => l.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) : null;
            syncing = true;
            int i = grid.Rows.Add(name, note, local ?? "", local == null ? value : null);
            syncing = false;
            Check(grid.Rows[i]);
        }
    }

    /// <summary>值是 @欄位、但本畫面找不到那個欄位：紅字提醒</summary>
    private void Check(DataGridViewRow row)
    {
        if (row.IsNewRow) return;
        var cell = row.Cells[ColCustom];
        bool missing = FieldRef.Match((cell.Value as string ?? "").Trim()) is { Success: true } m
                       && !locals.Contains(m.Groups[1].Value, StringComparer.OrdinalIgnoreCase);
        cell.Style.ForeColor = missing ? Color.Firebrick : Color.Empty;
        cell.ToolTipText = missing ? "本畫面找不到這個欄位，請確認拼字，或改從「帶入本畫面的欄位」下拉選" : "";
    }

    private string BuildParams()
    {
        var pairs = new List<string>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            string name = (row.Cells[ColName].Value as string)?.Trim();
            string local = row.Cells[ColLocal].Value as string;
            string value = !string.IsNullOrEmpty(local) ? "@" + local : (row.Cells[ColCustom].Value as string)?.Trim();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value)) pairs.Add($"{name}={value}");
        }
        return pairs.Count == 0 ? null : string.Join(", ", pairs);
    }
}
