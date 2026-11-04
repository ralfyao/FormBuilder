using System.ComponentModel;

namespace FormBuilder;

/// <summary>
/// 單身欄位設定：Grid 欄位的「開窗選取」設定（開窗畫面、傳入參數、回傳對應），跟編輯視窗欄位的開窗選取是同一個設定。
/// 「…」按鈕沿用畫面設計的開窗畫面設定、傳入參數設定視窗
/// </summary>
public class GridPickerDialog : Form, IPickerHost
{
    private readonly PropertyGrid propertyGrid = new() { Dock = DockStyle.Fill, ToolbarVisible = false, PropertySort = PropertySort.NoSort, HelpVisible = true };
    private readonly IEnumerable<string> rowColumns;
    private readonly IEnumerable<string> paramColumns;

    public PickerSettings Settings { get; }

    /// <param name="rowColumns">同一列的欄位（回傳值可以填進去的）</param>
    /// <param name="masterColumns">單頭（或編輯視窗）的欄位：傳入參數也可以用</param>
    public GridPickerDialog(string column, string header, string form, string parameters, string returnMap,
                            IEnumerable<string> rowColumns, IEnumerable<string> masterColumns)
    {
        this.rowColumns = rowColumns.ToList();
        paramColumns = this.rowColumns.Concat(masterColumns ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Settings = new PickerSettings { Column = column, PickerForm = form, PickerParams = parameters, PickerReturn = returnMap };
        Text = $"開窗選取：{header}";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 330);
        MinimumSize = new Size(420, 280);
        Padding = new Padding(10);

        var hint = new Label
        {
            Text = "執行時點格子右邊的「…」或按 F4 開啟選取視窗，選一筆後依回傳對應把值帶回同一列。編輯視窗上的這個欄位也用同一個設定。",
            Dock = DockStyle.Top, Height = 38, ForeColor = SystemColors.GrayText,
        };
        propertyGrid.SelectedObject = Settings;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 10, 0, 0) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90 };
        var btnCancel = new Button { Text = "取消", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.Cancel };
        var btnClear = new Button { Text = "清除設定", Dock = DockStyle.Left, Width = 90 };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
        bottom.Controls.Add(btnCancel);
        bottom.Controls.Add(btnClear);
        Controls.Add(propertyGrid);
        Controls.Add(hint);
        Controls.Add(bottom);
        CancelButton = btnCancel;
        btnOk.Click += (s, e) =>
        {
            propertyGrid.Focus();                                  // 正在打字的屬性先寫回
            DialogResult = DialogResult.OK;
        };
        btnClear.Click += (s, e) =>
        {
            Settings.PickerForm = Settings.PickerParams = Settings.PickerReturn = null;
            propertyGrid.Refresh();
        };
    }

    IEnumerable<string> IPickerHost.PickerLocalColumns => rowColumns;
    string IPickerHost.PickerFolder => null;                       // 交給擁有者視窗（畫面設計）
    IEnumerable<string> IPickerHost.ParamColumns => paramColumns;
    void IPickerHost.PickerChanged() => propertyGrid.Refresh();

    /// <summary>屬性面板上編輯的開窗設定</summary>
    public class PickerSettings : IPickerField
    {
        [Browsable(false)]
        public string Column { get; set; }

        [DisplayName("1. 開窗畫面"),
         Description("選取視窗要開的畫面定義檔。按右邊的「…」選檔案，並看開窗畫面有哪些欄位，直接選要帶回這一列的哪個欄位（寫進回傳對應）"),
         Editor(typeof(PickerFormEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string PickerForm { get; set; }

        [DisplayName("2. 傳入參數"),
         Description("開窗時帶給對方畫面的值：「對方欄位=@同一列或單頭的欄位」、「對方欄位=SQL 運算式」或「對方欄位=固定值」，例如：客戶編號=@客戶編號。按右邊的「…」可以從欄位清單選"),
         Editor(typeof(OpenParamsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string PickerParams { get; set; }

        [DisplayName("3. 回傳對應"),
         Description("選好後帶回哪些值：「這一列的欄位=對方欄位」，可以一次帶回多個欄位，例如：沖帳碼=單號, 原幣收帳金額=未沖金額。"
                   + "空白 = 只帶回這個欄位（對方畫面同名的欄位）；只寫對方的欄位名稱 = 把它帶回這個欄位")]
        public string PickerReturn { get; set; }
    }
}
