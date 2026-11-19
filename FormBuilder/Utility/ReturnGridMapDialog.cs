using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>
/// 屬性面板按鈕「帶回 Grid 欄位對應」的「…」：開「帶回 Grid 欄位對應」，
/// 列出「帶回到 Grid」那個 Grid 的欄位，每個從開啟畫面帶得回來的欄位下拉選
/// </summary>
public class ReturnGridMapEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not ButtonDefinition b) return value;      // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        string folder = null;
        for (var f = owner; f != null && folder == null; f = f.Owner)
            if (f is IPickerHost h) folder = h.PickerFolder;
        var columns = !string.IsNullOrWhiteSpace(b.ReturnGrid) && ReturnGridConverter.Columns.TryGetValue(b.ReturnGrid.Trim(), out var list) ? list : null;
        using var dialog = new ReturnGridMapDialog(TargetForms(b), b.ReturnGrid, value as string, folder ?? FormDefinition.DefaultFolder, columns);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ReturnMap : value;
    }

    /// <summary>按鈕可能開啟的畫面：畫面定義檔 + 依條件開啟的每個畫面（同一個檔只算一次）</summary>
    internal static List<string> TargetForms(ButtonDefinition b) => b.AllTargetForms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// 屬性面板按鈕「回傳對應」的「…」：列出本畫面的欄位，每個從開啟畫面（選取視窗）帶得回來的欄位下拉選，
/// 寫成「本畫面欄位=對方欄位」
/// </summary>
public class ReturnMapEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not ButtonDefinition b) return value;      // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        string folder = null;
        IEnumerable<string> locals = null;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h)
            {
                folder ??= h.PickerFolder;
                locals ??= h.PickerLocalColumns;
            }
        using var dialog = new ReturnGridMapDialog(ReturnGridMapEditor.TargetForms(b), null, value as string, folder ?? FormDefinition.DefaultFolder,
                                                   locals?.ToList() ?? new List<string>(), forFields: true);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ReturnMap : value;
    }
}

/// <summary>
/// 屬性面板下拉選單「選取回傳對應」的「…」：左邊列出本畫面（視窗）的欄位，右邊從「選項 SQL」查出來的欄位下拉選，
/// 寫成「本畫面欄位=SQL 欄位」
/// </summary>
public class ItemsReturnEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is null or object[]) return value;           // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        IEnumerable<string> locals = null;
        string connectionString = null;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h)
            {
                locals ??= h.PickerLocalColumns;
                connectionString ??= h.PickerConnectionString;
            }
        string sql = TypeDescriptor.GetProperties(context.Instance)["ItemsSql"]?.GetValue(context.Instance) as string;
        string self = TypeDescriptor.GetProperties(context.Instance)["Column"]?.GetValue(context.Instance) as string;
        List<string> columns = null;
        string info;
        if (string.IsNullOrWhiteSpace(sql))
            info = "還沒有設定「選項 SQL」，右邊只能自己輸入欄位名稱。";
        else if (string.IsNullOrWhiteSpace(connectionString))
            info = "還沒有資料庫連線，讀不到選項 SQL 的欄位，右邊只能自己輸入欄位名稱。";
        else
        {
            try
            {
                columns = SqlList.ReadColumns(connectionString, sql).Select(c => c.Column).ToList();
                info = "選項 SQL 的欄位：" + string.Join("、", columns) + "（第一欄就是這個下拉選單的值，不用設）";
            }
            catch (Exception ex)
            {
                info = "選項 SQL 執行失敗，右邊只能自己輸入欄位名稱：" + ex.Message;
            }
        }
        var mine = locals?.Where(c => !c.Equals(self, StringComparison.OrdinalIgnoreCase)).ToList();
        using var dialog = new ReturnGridMapDialog(value as string, mine, columns, info);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ReturnMap : value;
    }
}

/// <summary>
/// 帶回 Grid 欄位對應：左邊是本畫面 Grid 的欄位，右邊從開啟畫面（選取視窗）帶得回來的欄位下拉選；
/// 空白 = 這一欄不帶。原本的對應是空白時，先帶出兩邊同名的欄位
/// </summary>
public class ReturnGridMapDialog : Form
{
    private const string ColMine = "Mine", ColTheirs = "Theirs";
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = true,
        AllowUserToDeleteRows = true,
        RowHeadersWidth = 24,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };

    /// <summary>按確定後的對應（「Grid 欄位=對方欄位」用逗號分隔；沒有任何對應 = null）</summary>
    public string ReturnMap { get; private set; }

    /// <param name="formFiles">要開啟的畫面（畫面定義檔 + 依條件開啟的畫面），右邊下拉列出這些畫面的欄位（合併）</param>
    /// <param name="gridColumns">本畫面 Grid 的欄位；null = 還沒選「帶回到 Grid」或找不到那個 Grid</param>
    /// <param name="forFields">true = 按鈕的「回傳對應」：左邊是本畫面（視窗）的欄位，選一筆帶回填進欄位；原本空白時不自動帶同名欄位</param>
    public ReturnGridMapDialog(IReadOnlyList<string> formFiles, string gridName, string returnMap, string folder, IEnumerable<string> gridColumns, bool forFields = false)
    {
        Text = forFields ? "回傳對應" : "帶回 Grid 欄位對應";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 520);

        // 開啟畫面帶得回來的欄位；有「依條件開啟」時列出每個可能開啟的畫面的欄位（合併）
        var theirs = new List<(string Column, string Header)>();
        var targets = new List<FormDefinition>();
        var errors = new List<string>();
        foreach (string file in formFiles ?? Array.Empty<string>())
            if (PickerFormDialog.LoadDefinition(file, folder, out string error) is { } target) targets.Add(target);
            else errors.Add(error);
        string info;
        if (targets.Count == 0)
            info = errors.Count > 0 ? errors[0] : "還沒有設定要開啟的畫面（畫面定義檔或依條件開啟），右邊只能自己輸入欄位名稱。";
        else
        {
            theirs = targets.SelectMany(PickerFormDialog.ReturnableColumns).DistinctBy(t => t.Column, StringComparer.OrdinalIgnoreCase).ToList();
            string titles = string.Join("、", targets.Select(t => $"「{t.Title}」"));
            info = forFields ? $"開啟{titles}當選取視窗，選一筆後把右邊選的欄位值填進左邊的欄位"
                             : $"開啟{titles}，選好的每一筆新增成「{gridName}」的一列";
            if (targets.Count > 1) info += "（依條件開啟的畫面欄位合併列出，選的欄位每個畫面都要有）";
            if (errors.Count > 0) info += "；讀不到：" + string.Join("；", errors);
        }
        if (gridColumns == null && !forFields)
            info += string.IsNullOrWhiteSpace(gridName) ? "；請先選「帶回到 Grid」。" : $"；這個畫面找不到 Grid「{gridName}」。";

        Build(info, targets.Count > 1 || errors.Count > 0, returnMap, gridColumns, theirs.Select(t => t.Column),
              forFields ? "本畫面的欄位" : $"Grid「{gridName}」的欄位", "帶入開啟畫面的欄位（空白 = 不帶）",
              forFields
                ? "右邊選了欄位的才會帶回（空白 = 不帶）；全部空白 = 不是選取視窗，按鈕直接開啟畫面。沒列到的欄位可以在最下面一列自己加。"
                : "選取視窗選好的每一筆，新增成 Grid 的一列：右邊選的欄位值帶進左邊的 Grid 欄位。"
                  + "關聯欄位（單頭的單號等）、預設值會自動帶，不用設。Grid 沒列到的欄位可以在最下面一列自己加。",
              sameNameWhenBlank: !forFields, warnMissing: gridColumns != null, missingTip: forFields ? "畫面上找不到這個欄位" : "Grid 上找不到這個欄位");
    }

    /// <summary>
    /// 下拉選單的「選取回傳對應」：左邊是本畫面（視窗）的欄位，右邊從「選項 SQL」查出來的欄位下拉選
    /// </summary>
    /// <param name="sqlColumns">選項 SQL 的欄位；null = 讀不到（info 說明原因）</param>
    public ReturnGridMapDialog(string returnMap, IEnumerable<string> localColumns, IReadOnlyList<string> sqlColumns, string info)
    {
        Text = "選取回傳對應";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 520);
        Build(info, false, returnMap, localColumns?.ToList(), sqlColumns ?? Array.Empty<string>(),
              "本畫面的欄位", "帶入選項 SQL 的欄位（空白 = 不帶）",
              "下拉選單選了一列後，右邊選的 SQL 欄位值填進左邊的欄位（空白 = 不帶）；選了空白選項時這些欄位一起清空。沒列到的欄位可以在最下面一列自己加。",
              sameNameWhenBlank: false, warnMissing: localColumns != null, missingTip: "畫面上找不到這個欄位");
    }

    /// <param name="sameNameWhenBlank">原本的對應是空白時，先帶出兩邊同名的欄位</param>
    private void Build(string info, bool tallInfo, string returnMap, IEnumerable<string> gridColumns, IEnumerable<string> theirColumns,
                       string mineHeader, string theirsHeader, string footer, bool sameNameWhenBlank, bool warnMissing, string missingTip)
    {
        var current = ButtonRunner.ParsePairs(returnMap).Select(p => (Mine: p.Left, Theirs: p.Right.TrimStart('@'))).ToList();
        var mine = (gridColumns ?? Array.Empty<string>()).Concat(current.Select(p => p.Mine))
                   .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var options = theirColumns.Concat(current.Select(p => p.Theirs))
                      .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColMine, HeaderText = mineHeader, FillWeight = 50 });
        var colTheirs = new DataGridViewComboBoxColumn
        {
            Name = ColTheirs,
            HeaderText = theirsHeader,
            FillWeight = 50,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colTheirs.Items.Add("");
        colTheirs.Items.AddRange(options.Select(o => (object)o).ToArray());
        grid.Columns.Add(colTheirs);
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.DataError += (s, e) => e.ThrowException = false;

        foreach (var m in mine)
        {
            string t = current.Count > 0 || !sameNameWhenBlank // 回傳對應原本空白 = 不是選取視窗，不自動帶（不然主鍵等同名欄位會被蓋掉）
                ? current.FirstOrDefault(p => p.Mine.Equals(m, StringComparison.OrdinalIgnoreCase)).Theirs
                : options.FirstOrDefault(o => o.Equals(m, StringComparison.OrdinalIgnoreCase));     // 原本空白 = 同名欄位
            int i = grid.Rows.Add(m, t == null ? "" : options.First(o => o.Equals(t, StringComparison.OrdinalIgnoreCase)));
            if (warnMissing && !gridColumns.Contains(m, StringComparer.OrdinalIgnoreCase))
            {
                grid.Rows[i].Cells[ColMine].Style.ForeColor = Color.Firebrick;
                grid.Rows[i].Cells[ColMine].ToolTipText = missingTip;
            }
        }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, tallInfo ? 40 : 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = info, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = footer }, 0, 2);

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
            var pairs = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => (Mine: (r.Cells[ColMine].Value as string)?.Trim(), Theirs: (r.Cells[ColTheirs].Value as string)?.Trim()))
                .Where(p => !string.IsNullOrEmpty(p.Mine) && !string.IsNullOrEmpty(p.Theirs))
                .Select(p => $"{p.Mine}={p.Theirs}").ToList();
            ReturnMap = pairs.Count == 0 ? null : string.Join(", ", pairs);
        };
    }
}
