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
        using var dialog = new ReturnGridMapDialog(b.TargetForm, b.ReturnGrid, value as string, folder ?? FormDefinition.DefaultFolder, columns);
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

    /// <param name="gridColumns">本畫面 Grid 的欄位；null = 還沒選「帶回到 Grid」或找不到那個 Grid</param>
    public ReturnGridMapDialog(string formFile, string gridName, string returnMap, string folder, IEnumerable<string> gridColumns)
    {
        Text = "帶回 Grid 欄位對應";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 520);

        // 開啟畫面帶得回來的欄位
        var theirs = new List<(string Column, string Header)>();
        string info;
        if (string.IsNullOrWhiteSpace(formFile))
            info = "還沒有設定要開啟的畫面（畫面定義檔），右邊只能自己輸入欄位名稱。";
        else if (PickerFormDialog.LoadDefinition(formFile, folder, out string error) is not { } target)
            info = error;
        else
        {
            theirs = PickerFormDialog.ReturnableColumns(target);
            info = $"開啟「{target.Title}」，選好的每一筆新增成「{gridName}」的一列";
        }
        if (gridColumns == null)
            info += string.IsNullOrWhiteSpace(gridName) ? "；請先選「帶回到 Grid」。" : $"；這個畫面找不到 Grid「{gridName}」。";

        var current = ButtonRunner.ParsePairs(returnMap).Select(p => (Mine: p.Left, Theirs: p.Right.TrimStart('@'))).ToList();
        var mine = (gridColumns ?? Enumerable.Empty<string>()).Concat(current.Select(p => p.Mine))
                   .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var options = theirs.Select(t => t.Column).Concat(current.Select(p => p.Theirs))
                      .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColMine, HeaderText = $"Grid「{gridName}」的欄位", FillWeight = 50 });
        var colTheirs = new DataGridViewComboBoxColumn
        {
            Name = ColTheirs,
            HeaderText = "帶入開啟畫面的欄位（空白 = 不帶）",
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
            string t = current.Count > 0
                ? current.FirstOrDefault(p => p.Mine.Equals(m, StringComparison.OrdinalIgnoreCase)).Theirs
                : options.FirstOrDefault(o => o.Equals(m, StringComparison.OrdinalIgnoreCase));     // 原本空白 = 同名欄位
            int i = grid.Rows.Add(m, t == null ? "" : options.First(o => o.Equals(t, StringComparison.OrdinalIgnoreCase)));
            if (gridColumns != null && !gridColumns.Contains(m, StringComparer.OrdinalIgnoreCase))
            {
                grid.Rows[i].Cells[ColMine].Style.ForeColor = Color.Firebrick;
                grid.Rows[i].Cells[ColMine].ToolTipText = "Grid 上找不到這個欄位";
            }
        }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = info, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "選取視窗選好的每一筆，新增成 Grid 的一列：右邊選的欄位值帶進左邊的 Grid 欄位。"
                 + "關聯欄位（單頭的單號等）、預設值會自動帶，不用設。Grid 沒列到的欄位可以在最下面一列自己加。",
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
            var pairs = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => (Mine: (r.Cells[ColMine].Value as string)?.Trim(), Theirs: (r.Cells[ColTheirs].Value as string)?.Trim()))
                .Where(p => !string.IsNullOrEmpty(p.Mine) && !string.IsNullOrEmpty(p.Theirs))
                .Select(p => $"{p.Mine}={p.Theirs}").ToList();
            ReturnMap = pairs.Count == 0 ? null : string.Join(", ", pairs);
        };
    }
}
