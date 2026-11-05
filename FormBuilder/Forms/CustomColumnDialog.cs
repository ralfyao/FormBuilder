using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>
/// 單身欄位設定的「新增自訂欄位」：輸入欄位名稱、標題，選類型（勾選框、按鈕、日期選擇）。
/// 自訂欄位不是資料表的欄位，不查詢、不存檔，值只留在畫面上
/// </summary>
public class CustomColumnDialog : Form
{
    private readonly TextBox txtName = new() { Width = 220 };
    private readonly TextBox txtHeader = new() { Width = 220 };
    private readonly ComboBox cboKind = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    /// <summary>下拉選單的選項：固定選項（值=顯示，逗號分隔）或 SELECT 開頭的選項 SQL</summary>
    private readonly TextBox txtOptions = new() { Width = 220, Height = 60, Multiline = true, ScrollBars = ScrollBars.Vertical,
                                                  PlaceholderText = "例如：Y=是,N=否 或 SELECT 代碼, 名稱 FROM …" };
    private readonly IReadOnlyCollection<string> usedNames;
    /// <summary>類型的「數字框」= 文字框 + 數字型別（靠右顯示、可以設公式）</summary>
    private const string NumberBox = "數字框";

    /// <summary>按「確定」後建立的欄位</summary>
    public GridColumnDefinition Column { get; private set; }

    /// <param name="usedNames">已經用掉的欄位名稱（不分大小寫，不能重複）</param>
    public CustomColumnDialog(IEnumerable<string> usedNames)
    {
        this.usedNames = usedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Text = "新增自訂欄位";
        Font = new Font("Microsoft JhengHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        cboKind.Items.AddRange(new object[] { GridCellKind.勾選框, GridCellKind.下拉選單, GridCellKind.日期選擇, GridCellKind.按鈕, GridCellKind.文字框, NumberBox });
        cboKind.SelectedIndex = 0;
        int n = 1;
        while (this.usedNames.Contains($"自訂{n}")) n++;
        txtName.Text = $"自訂{n}";

        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        void Row(string label, Control input)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) });
            layout.Controls.Add(input);
        }
        Row("類型", cboKind);
        Row("欄位名稱", txtName);
        Row("標題", txtHeader);
        Row("選項", txtOptions);
        void KindChanged() => txtOptions.Enabled = Equals(cboKind.SelectedItem, GridCellKind.下拉選單);
        cboKind.SelectedIndexChanged += (s, e) => KindChanged();
        KindChanged();
        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 8),
            Text = "自訂欄位不是資料表的欄位：不查詢、不存檔，值只留在畫面上。"
                 + "按鈕的 SQL、執行前檢查可以用 @欄位名稱 取得那一列的值（例如勾選框打勾 = 1）。"
                 + "下拉選單：選項填固定選項（值=顯示，逗號分隔）或 SELECT（第一欄是值、第二欄是顯示文字），之後可以在「選項 / 格式」修改。"
                 + "按鈕的動作在欄位清單上按右鍵（或雙擊那一列）設定。"
                 + "文字框 / 數字框只顯示資料：在欄位清單上雙擊那一列設定顯示的 SQL，數字框也可以雙擊「公式」欄用同一列的欄位計算。",
        };
        layout.Controls.Add(hint);
        layout.SetColumnSpan(hint, 2);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        var btnOk = new Button { Text = "確定", Width = 80 };
        buttons.Controls.AddRange(new Control[] { btnCancel, btnOk });
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        btnOk.Click += (s, e) => Accept();
    }

    private void Accept()
    {
        string name = txtName.Text.Trim();
        // SQL 的 @參數只認字母、數字、底線，名稱有其他符號就取不到值
        if (!Regex.IsMatch(name, @"^[\p{L}\p{N}_]+$"))
        {
            MessageBox.Show("欄位名稱只能用中英文字、數字、底線（按鈕的 SQL 要用 @欄位名稱 取值）。", "提醒");
            txtName.Focus();
            return;
        }
        if (usedNames.Contains(name))
        {
            MessageBox.Show($"欄位名稱「{name}」已經有了，請換一個。", "提醒");
            txtName.Focus();
            return;
        }
        bool number = Equals(cboKind.SelectedItem, NumberBox);
        var kind = number ? GridCellKind.文字框 : (GridCellKind)cboKind.SelectedItem;
        if (kind == GridCellKind.下拉選單 && string.IsNullOrWhiteSpace(txtOptions.Text))
        {
            MessageBox.Show("請填下拉選單的選項，例如：Y=是,N=否，或 SELECT 代碼, 名稱 FROM …", "提醒");
            txtOptions.Focus();
            return;
        }
        string header = string.IsNullOrWhiteSpace(txtHeader.Text) ? name : txtHeader.Text.Trim();
        Column = new GridColumnDefinition
        {
            Column = name,
            Header = header,
            DataType = number ? "numeric" : kind switch { GridCellKind.勾選框 => "bit", GridCellKind.日期選擇 => "datetime", _ => "nvarchar" },
            IsNullable = true,
            Width = kind == GridCellKind.日期選擇 ? 110 : kind == GridCellKind.勾選框 ? 60 : kind is GridCellKind.文字框 or GridCellKind.下拉選單 && !number ? 150 : 90,
            CellOptions = kind == GridCellKind.下拉選單 ? txtOptions.Text.Trim().Replace("\r\n", ",").Replace("\n", ",") : null,
            Visible = true,
            NoEntry = true,                                // 不寫回資料庫
            ScreenOnly = true,                             // 不查詢
            IsCustom = true,
            CellType = kind,
            EditVisible = false,                           // 只放在 Grid 上，不放進編輯視窗
            Button = kind == GridCellKind.按鈕 ? new ButtonDefinition { Text = header, Action = ButtonAction.執行SQL } : null,
        };
        DialogResult = DialogResult.OK;
    }

    /// <summary>
    /// 設定自訂文字框 / 數字框顯示資料的 SQL：SELECT 查出來的第一筆第一個欄位，@欄位 取同一列的值。
    /// 按確定回傳 true（sql 清空 = 不用 SQL）
    /// </summary>
    public static bool EditValueSql(IWin32Window owner, string title, ref string sql, IEnumerable<string> rowColumns)
    {
        using var form = new Form
        {
            Text = $"顯示資料的 SQL：{title}",
            Font = new Font("Microsoft JhengHei UI", 9F),
            Size = new Size(620, 420),
            MinimumSize = new Size(480, 300),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            ShowInTaskbar = false,
        };
        var box = new SqlTextBox { Dock = DockStyle.Fill, Text = (sql ?? "").Replace("\r\n", "\n") };
        var cboInsert = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        cboInsert.Items.Add("插入這一列的欄位…");
        cboInsert.Items.AddRange(rowColumns.Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        cboInsert.SelectedIndex = 0;
        cboInsert.SelectedIndexChanged += (s, e) =>
        {
            if (cboInsert.SelectedIndex <= 0) return;
            box.SelectedText = "@" + cboInsert.SelectedItem;
            cboInsert.SelectedIndex = 0;
            box.Focus();
        };
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            ForeColor = SystemColors.GrayText,
            Text = "每一列顯示這個 SELECT 查出來的第一筆第一個欄位，@欄位名稱 取同一列的值，例如：\n"
                 + "SELECT 單價 FROM D報價明細 WHERE QUONO=@QUONO AND 產品編號=@產品編號\n"
                 + "載入資料、同一列用到的欄位改了就重查。清空 = 不顯示資料（有設公式時用公式）。",
        };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(4) };
        top.Controls.Add(cboInsert);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 38, Padding = new Padding(4) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        var btnOk = new Button { Text = "確定", DialogResult = DialogResult.OK, Width = 80 };
        bottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
        form.Controls.Add(box);
        form.Controls.Add(top);
        form.Controls.Add(hint);
        form.Controls.Add(bottom);
        form.CancelButton = btnCancel;                     // 不設 AcceptButton：SQL 裡按 Enter 是換行
        if (form.ShowDialog(owner) != DialogResult.OK) return false;
        sql = box.Text.Trim().Length == 0 ? null : box.Text.Trim().Replace("\n", "\r\n");
        return true;
    }

    /// <summary>設定自訂按鈕欄位的動作（同畫面上的按鈕屬性）：改的是複本，按「確定」才回傳</summary>
    /// <param name="rowColumns">Grid 這一列有的欄位（「插入欄位」清單用）</param>
    public static ButtonDefinition EditButton(IWin32Window owner, ButtonDefinition button, string title, IEnumerable<string> rowColumns)
    {
        var copy = (button ?? new ButtonDefinition { Action = ButtonAction.執行SQL }).Clone();
        using var form = new Form
        {
            Text = $"按鈕動作：{title}",
            Font = new Font("Microsoft JhengHei UI", 9F),
            Size = new Size(640, 780),
            MinimumSize = new Size(520, 560),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
        };

        // ---- 常用設定：動作、執行的 SQL、要開的畫面、傳入參數、開新視窗 ----
        var cboAction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        cboAction.Items.AddRange(new object[] { ButtonAction.執行SQL, ButtonAction.開啟畫面 });
        var cboInsert = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        cboInsert.Items.Add("插入這一列的欄位…");
        cboInsert.Items.AddRange(rowColumns.Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        cboInsert.SelectedIndex = 0;
        var txtSql = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 90, Dock = DockStyle.Fill, AcceptsReturn = true };
        var txtForm = new TextBox { Dock = DockStyle.Fill };
        var btnBrowse = new Button { Text = "瀏覽…", Width = 70, Margin = new Padding(4, 2, 0, 2) };
        var txtParams = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 70, Dock = DockStyle.Fill, AcceptsReturn = true };
        var btnParams = new Button { Text = "選取…", Width = 70, Margin = new Padding(4, 2, 0, 2) };
        var paramsRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
        paramsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paramsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paramsRow.Controls.Add(txtParams);
        paramsRow.Controls.Add(btnParams);
        var chkNewWindow = new CheckBox { Text = "開新視窗（獨立的視窗，不開在 ERP 外框的分頁）", AutoSize = true };
        var chkInsert = new CheckBox { Text = "開啟後直接新增（傳入參數填進同名欄位）", AutoSize = true };
        var chkRefresh = new CheckBox { Text = "存檔後關閉並重新整理這個清單", AutoSize = true };

        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(8, 8, 8, 0) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control input)
        {
            top.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 8, 4) });
            top.Controls.Add(input);
        }
        var formRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
        formRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        formRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        formRow.Controls.Add(txtForm);
        formRow.Controls.Add(btnBrowse);
        var options = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        options.Controls.AddRange(new Control[] { chkNewWindow, chkInsert, chkRefresh });
        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(590, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 4),
            Text = "@欄位名稱 取按下那一列的值（含自訂勾選框、日期），那一列沒有的再取畫面上的欄位。"
                 + "傳入參數一行一個「對方欄位=值」，值可以是 @這一列的欄位、SQL 運算式或固定值，例如 QUONO=@QUONO；"
                 + "對方畫面上有同名欄位時會自動用它查詢，對方的 SQL 也可以用 @名稱 取得。",
        };
        Row("動作", cboAction);
        Row("", cboInsert);
        Row("執行的 SQL", txtSql);
        Row("開啟畫面", formRow);
        Row("傳入參數", paramsRow);
        Row("", options);
        top.Controls.Add(hint);
        top.SetColumnSpan(hint, 2);

        // ---- 完整設定（同畫面上的按鈕屬性）----
        var grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = copy, ToolbarVisible = false, PropertySort = PropertySort.Categorized };
        var more = new GroupBox { Text = "完整設定（執行前檢查、確認訊息、需要的權限、回傳對應…；外觀、位置用不到）", Dock = DockStyle.Fill, Padding = new Padding(6) };
        more.Controls.Add(grid);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 38, Padding = new Padding(4) };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
        var btnOk = new Button { Text = "確定", DialogResult = DialogResult.OK, Width = 80 };
        bottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
        form.Controls.Add(more);
        form.Controls.Add(top);
        form.Controls.Add(bottom);
        form.CancelButton = btnCancel;                     // 不設 AcceptButton：SQL、參數、屬性面板裡按 Enter 是換行 / 確認輸入

        // 上面的常用設定跟下面的屬性面板同步（改哪邊都一樣）
        bool loading = false;
        void LoadSimple()
        {
            loading = true;
            if (copy.Action is ButtonAction.執行SQL or ButtonAction.開啟畫面) cboAction.SelectedItem = copy.Action;
            else cboAction.SelectedIndex = -1;             // 屬性面板選了其他動作（照畫面按鈕執行）
            txtSql.Text = (copy.Sql ?? "").ReplaceLineEndings();
            txtForm.Text = copy.TargetForm ?? "";
            txtParams.Text = (copy.OpenParams ?? "").ReplaceLineEndings();
            chkNewWindow.Checked = copy.OpenInNewWindow;
            chkInsert.Checked = copy.OpenAsInsert;
            chkRefresh.Checked = copy.RefreshAfterSave;
            bool open = copy.Action == ButtonAction.開啟畫面;
            txtSql.Enabled = copy.Action == ButtonAction.執行SQL;
            txtForm.Enabled = btnBrowse.Enabled = txtParams.Enabled = btnParams.Enabled = chkNewWindow.Enabled = chkInsert.Enabled = chkRefresh.Enabled = open;
            loading = false;
        }
        void Changed(Action apply)
        {
            if (loading) return;
            apply();
            grid.Refresh();
            LoadSimple();
        }
        static string NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        cboAction.SelectedIndexChanged += (s, e) => Changed(() =>
        {
            if (cboAction.SelectedItem is not ButtonAction action) return;
            if (action == ButtonAction.開啟畫面 && copy.Action != action && string.IsNullOrWhiteSpace(copy.TargetForm))
                copy.OpenInNewWindow = true;               // 第一次改成開啟畫面：預設開新視窗
            copy.Action = action;
        });
        // 文字框打字時不重新載入（游標才不會跳走），只同步到屬性面板
        txtSql.TextChanged += (s, e) => { if (!loading) { copy.Sql = NullIfBlank(txtSql.Text); grid.Refresh(); } };
        txtForm.TextChanged += (s, e) => { if (!loading) { copy.TargetForm = NullIfBlank(txtForm.Text); grid.Refresh(); } };
        txtParams.TextChanged += (s, e) => { if (!loading) { copy.OpenParams = NullIfBlank(txtParams.Text); grid.Refresh(); } };
        chkNewWindow.CheckedChanged += (s, e) => Changed(() => copy.OpenInNewWindow = chkNewWindow.Checked);
        chkInsert.CheckedChanged += (s, e) => Changed(() => copy.OpenAsInsert = chkInsert.Checked);
        chkRefresh.CheckedChanged += (s, e) => Changed(() => copy.RefreshAfterSave = chkRefresh.Checked);
        grid.PropertyValueChanged += (s, e) => LoadSimple();
        btnBrowse.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "選擇要開啟的畫面",
                Filter = "畫面定義檔 (*.json)|*.json",
                InitialDirectory = Directory.Exists(FormDefinition.DefaultFolder) ? FormDefinition.DefaultFolder : null,
            };
            if (dlg.ShowDialog(form) == DialogResult.OK) txtForm.Text = dlg.FileName;     // 存檔時會自動改成相對路徑
        };
        // 傳入參數設定：列出對方畫面的參數，從這一列的欄位下拉選
        btnParams.Click += (s, e) =>
        {
            using var dlg = new OpenParamsDialog(copy.TargetForm, copy.OpenParams, FormDefinition.DefaultFolder, rowColumns);
            if (dlg.ShowDialog(form) == DialogResult.OK) txtParams.Text = dlg.Params ?? "";
        };
        // 插入欄位：開啟畫面在傳入參數加一行「欄位=@欄位」；執行SQL 在游標位置插入 @欄位
        cboInsert.SelectedIndexChanged += (s, e) =>
        {
            if (cboInsert.SelectedIndex <= 0) return;
            string name = (string)cboInsert.SelectedItem;
            cboInsert.SelectedIndex = 0;
            if (copy.Action == ButtonAction.開啟畫面)
            {
                bool newLine = txtParams.Text.Length > 0 && !txtParams.Text.EndsWith(Environment.NewLine);
                txtParams.AppendText((newLine ? Environment.NewLine : "") + $"{name}=@{name}");
                txtParams.Focus();
            }
            else if (txtSql.Enabled)
            {
                txtSql.SelectedText = "@" + name;
                txtSql.Focus();
            }
        };
        LoadSimple();
        return form.ShowDialog(owner) == DialogResult.OK ? copy : null;
    }
}
