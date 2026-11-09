using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>屬性面板上有「開窗畫面」的欄位（畫面設計的欄位、編輯視窗版面的欄位）</summary>
public interface IPickerField
{
    string Column { get; }
    string PickerForm { get; }
    string PickerReturn { get; set; }
}

/// <summary>開「開窗畫面設定」的視窗：提供本畫面可以接回傳值的欄位、畫面定義檔所在的資料夾</summary>
public interface IPickerHost
{
    /// <summary>本畫面的欄位（回傳值可以填進去的）；null = 交給擁有者視窗</summary>
    IEnumerable<string> PickerLocalColumns { get; }
    /// <summary>畫面定義檔的資料夾（相對路徑以它為準）；null = 交給擁有者視窗</summary>
    string PickerFolder { get; }
    /// <summary>傳入參數可以用的本畫面欄位（@名稱 取得到值的）；預設同 PickerLocalColumns</summary>
    IEnumerable<string> ParamColumns => PickerLocalColumns;
    /// <summary>回傳對應改了：重新整理屬性面板</summary>
    void PickerChanged();
}

/// <summary>
/// 屬性面板「開窗畫面」的「…」按鈕：開「開窗畫面設定」，選畫面定義檔、看對方畫面有哪些欄位，勾選要帶回本畫面的哪個欄位（寫進回傳對應）
/// </summary>
public class PickerFormEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        var owner = Form.ActiveForm;
        // 多選時沒有單一欄位可以對應，只選檔案
        if (context?.Instance is not IPickerField field)
        {
            using var open = new OpenFileDialog { Filter = "畫面定義檔 (*.json)|*.json|所有檔案 (*.*)|*.*" };
            return open.ShowDialog(owner) == DialogResult.OK ? open.FileName : value;
        }
        IEnumerable<string> locals = null;
        string folder = null;
        var hosts = new List<IPickerHost>();
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h)
            {
                hosts.Add(h);
                locals ??= h.PickerLocalColumns;
                folder ??= h.PickerFolder;
            }
        using var dialog = new PickerFormDialog(field.Column, value as string, field.PickerReturn,
                                                folder ?? FormDefinition.DefaultFolder, locals ?? new[] { field.Column });
        if (dialog.ShowDialog(owner) != DialogResult.OK) return value;
        field.PickerReturn = dialog.ReturnMap;
        foreach (var h in hosts) h.PickerChanged();
        return dialog.FormFile;
    }
}

/// <summary>
/// 開窗畫面設定：選開窗畫面（畫面定義檔），列出選取時帶得回來的欄位（單頭清單 / 單頭欄位，或 SQL 清單的欄位），
/// 每個欄位選要填進本畫面的哪個欄位，按確定寫成「回傳對應」；「開啟畫面看看」直接打開對方畫面對照
/// </summary>
public class PickerFormDialog : Form
{
    private readonly string column;
    private readonly string folder;
    private readonly TextBox txtFile = new() { Dock = DockStyle.Fill };
    private readonly Label lblInfo = new() { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };
    private readonly DataGridViewComboBoxColumn colTarget;
    private readonly List<string> locals;
    /// <summary>目前的回傳對應（對方欄位 → 本畫面欄位），換畫面時保留同名的</summary>
    private readonly Dictionary<string, string> mapping = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>開窗畫面（照使用者輸入或選的；空白 = null）</summary>
    public string FormFile => string.IsNullOrWhiteSpace(txtFile.Text) ? null : txtFile.Text.Trim();

    /// <summary>回傳對應：「本畫面欄位=對方欄位」；只帶回這個欄位的同名欄位時是 null（預設值）</summary>
    public string ReturnMap { get; private set; }

    /// <param name="column">這個欄位的名稱（回傳對應空白時，帶回對方同名的欄位）</param>
    /// <param name="localColumns">本畫面可以接回傳值的欄位</param>
    public PickerFormDialog(string column, string formFile, string returnMap, string folder, IEnumerable<string> localColumns)
    {
        this.column = column;
        this.folder = folder;
        locals = localColumns.Prepend(column).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var (mine, theirs) in ParseReturn(column, returnMap)) mapping[theirs] = mine;
        foreach (var mine in mapping.Values.Where(m => !locals.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList()) locals.Add(mine);

        Text = $"開窗畫面設定：{column}";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 520);
        txtFile.Text = formFile ?? "";

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Theirs", HeaderText = "開窗畫面的欄位", ReadOnly = true, FillWeight = 35 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Header", HeaderText = "標題", ReadOnly = true, FillWeight = 30 });
        colTarget = new DataGridViewComboBoxColumn
        {
            Name = "Mine",
            HeaderText = "帶回本畫面的欄位（空白 = 不帶回）",
            FillWeight = 35,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colTarget.Items.Add("");
        colTarget.Items.AddRange(locals.ToArray());
        grid.Columns.Add(colTarget);
        // 下拉一選就生效（不用離開儲存格）
        grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != colTarget.Index) return;
            string theirs = (string)grid.Rows[e.RowIndex].Cells["Theirs"].Value;
            string mine = grid.Rows[e.RowIndex].Cells["Mine"].Value as string;
            if (string.IsNullOrEmpty(mine)) mapping.Remove(theirs);
            else mapping[theirs] = mine;
        };
        grid.DataError += (s, e) => e.ThrowException = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var btnBrowse = new Button { Text = "瀏覽…", Dock = DockStyle.Fill };
        var btnPreview = new Button { Text = "開啟畫面看看", Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "開窗畫面", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(txtFile, 1, 0);
        layout.Controls.Add(btnBrowse, 2, 0);
        layout.Controls.Add(btnPreview, 3, 0);
        layout.Controls.Add(lblInfo, 0, 1);
        layout.SetColumnSpan(lblInfo, 4);
        layout.Controls.Add(grid, 0, 2);
        layout.SetColumnSpan(grid, 4);
        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"使用者在開窗畫面選一筆後，有指定的欄位帶回本畫面。只帶回「{column}」同名欄位時，回傳對應會保持空白（預設）。",
        };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 4);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);
        Controls.Add(layout);
        Controls.Add(bottom);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnBrowse.Click += (s, e) =>
        {
            using var open = new OpenFileDialog
            {
                Filter = "畫面定義檔 (*.json)|*.json|所有檔案 (*.*)|*.*",
                InitialDirectory = Directory.Exists(folder) ? folder : null,
            };
            if (open.ShowDialog(this) != DialogResult.OK) return;
            txtFile.Text = ButtonRunner.RelativePath(open.FileName, folder);
            LoadTarget();
        };
        txtFile.Leave += (s, e) => LoadTarget();
        btnPreview.Click += (s, e) =>
        {
            var target = LoadDefinition(out string error);
            if (target == null) { MessageBox.Show(error, "開窗畫面"); return; }
            try
            {
                new RuntimeForm(target).Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("開啟畫面失敗：" + ex.Message, "開窗畫面", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        btnOk.Click += (s, e) => ReturnMap = BuildReturn();
        LoadTarget();
    }

    private string loaded;

    /// <summary>讀開窗畫面，列出帶得回來的欄位（畫面沒換就不重讀）</summary>
    private void LoadTarget()
    {
        if (loaded == txtFile.Text) return;
        loaded = txtFile.Text;
        grid.Rows.Clear();
        var columns = new List<(string Column, string Header)>();
        if (string.IsNullOrWhiteSpace(txtFile.Text))
            lblInfo.Text = "請選擇開窗畫面（畫面定義檔）。";
        else
        {
            var target = LoadDefinition(out string error);
            if (target == null)
                lblInfo.Text = error;
            else
            {
                columns = ReturnableColumns(target);
                lblInfo.Text = $"「{target.Title}」" + (string.IsNullOrWhiteSpace(target.TableName) ? "（SQL 清單）" : $"（資料表 {target.TableName}）")
                             + $"：選一筆後帶得回來的欄位有 {columns.Count} 個";
            }
        }
        // 回傳對應裡有、但開窗畫面找不到的欄位也列出來，免得按確定時被悄悄拿掉
        foreach (var theirs in mapping.Keys.Where(k => !columns.Any(c => c.Column.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList())
            columns.Add((theirs, "（開窗畫面找不到這個欄位）"));
        foreach (var (theirs, header) in columns)
        {
            int i = grid.Rows.Add(theirs, header, mapping.TryGetValue(theirs, out var mine) ? locals.First(l => l.Equals(mine, StringComparison.OrdinalIgnoreCase)) : "");
            if (header == "（開窗畫面找不到這個欄位）") grid.Rows[i].DefaultCellStyle.ForeColor = Color.Firebrick;
        }
    }

    private FormDefinition LoadDefinition(out string error) => LoadDefinition(txtFile.Text, folder, out error);

    /// <summary>讀畫面定義檔（只寫檔名時找 folder 裡的，可以省略 .json）；讀不到回傳 null、error 是原因</summary>
    internal static FormDefinition LoadDefinition(string file, string folder, out string error)
    {
        error = null;
        string path = ButtonRunner.ResolvePath((file ?? "").Trim(), folder);
        if (path != null && !File.Exists(path) && File.Exists(path + ".json")) path += ".json";
        if (path == null || !File.Exists(path))
        {
            error = "找不到畫面定義檔：" + path;
            return null;
        }
        try
        {
            return FormDefinition.Load(path);
        }
        catch (Exception ex)
        {
            error = "讀取畫面定義檔失敗：" + ex.Message;
            return null;
        }
    }

    /// <summary>
    /// 開窗畫面選一筆時帶得回來的欄位：有單頭資料表時是單頭清單的欄位、SQL 清單的欄位（在 SQL 清單上選的那列帶回）與單頭欄位；
    /// 只放 SQL 清單的畫面是 SQL 清單的欄位
    /// </summary>
    internal static List<(string Column, string Header)> ReturnableColumns(FormDefinition target)
    {
        var list = new List<(string Column, string Header)>();
        void Add(string name, string header)
        {
            if (string.IsNullOrWhiteSpace(name) || list.Any(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
            list.Add((name, string.IsNullOrWhiteSpace(header) || header == name ? "" : header));
        }
        if (!string.IsNullOrWhiteSpace(target.TableName))
        {
            foreach (var c in target.Grids.Where(g => g.IsMasterList && !g.IsSqlList).SelectMany(g => g.Columns).Where(c => !c.ScreenOnly))
                Add(c.Column, c.Header);
            foreach (var c in target.Grids.Where(g => g.IsSqlList).SelectMany(g => g.Columns).Where(c => !c.IsCustom))
                Add(c.Column, c.Header);
            foreach (var f in target.Fields.Where(f => !f.IsCalculated))
                Add(f.Column, f.Label);
        }
        else
            foreach (var c in target.Grids.Where(g => g.IsSqlList).SelectMany(g => g.Columns).Where(c => !c.IsCustom))
                Add(c.Column, c.Header);
        return list;
    }

    /// <summary>回傳對應拆成（本畫面欄位, 對方欄位）：空白 = 帶回同名欄位；只寫對方欄位時第一個帶回這個欄位、其他帶回同名欄位（同 ButtonRunner.PickerButton）</summary>
    private static List<(string Mine, string Theirs)> ParseReturn(string column, string returnMap)
    {
        if (string.IsNullOrWhiteSpace(returnMap)) return new() { (column, column) };
        bool first = true;
        var pairs = new List<(string, string)>();
        foreach (var part in returnMap.Split(new[] { ',', '，', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) pairs.Add((part[..eq].Trim(), part[(eq + 1)..].Trim().TrimStart('@')));
            else
            {
                string theirs = part.TrimStart('@');
                pairs.Add((first ? column : theirs, theirs));
                first = false;
            }
        }
        return pairs;
    }

    /// <summary>勾選的對應組成回傳對應；只有「這個欄位 = 同名欄位」時用空白（預設）</summary>
    private string BuildReturn()
    {
        var pairs = grid.Rows.Cast<DataGridViewRow>()
            .Select(r => (Mine: r.Cells["Mine"].Value as string, Theirs: (string)r.Cells["Theirs"].Value))
            .Where(p => !string.IsNullOrEmpty(p.Mine)).ToList();
        if (pairs.Count == 1 && pairs[0].Mine.Equals(column, StringComparison.OrdinalIgnoreCase) && pairs[0].Theirs.Equals(column, StringComparison.OrdinalIgnoreCase))
            return null;
        return pairs.Count == 0 ? null : string.Join(", ", pairs.Select(p => $"{p.Mine}={p.Theirs}"));
    }
}
