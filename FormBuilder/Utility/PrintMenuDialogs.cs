using System.Data;
using System.Globalization;

namespace FormBuilder;

/// <summary>
/// 編輯視窗版面「列印選單…」：設定編輯視窗工具列「列印」要開的列印視窗。
/// 上面一張表設參數（名稱、標籤、預設值、唯讀），下面一張表設按鈕（文字、樣板；「設計」開列印樣板設計器）
/// </summary>
public class PrintMenuDesignDialog : Form
{
    private readonly PrintMenuDefinition menu;
    private readonly Func<ButtonDefinition, bool> designTemplate;
    private readonly TextBox txtTitle = new() { Dock = DockStyle.Fill };
    private readonly DataGridView dgvParams = NewGrid();
    private readonly DataGridView dgvButtons = NewGrid();

    /// <summary>按「確定」後的設定（沒有按鈕 = null，工具列「列印」照原本的方式印）</summary>
    public PrintMenuDefinition Result { get; private set; }

    /// <param name="designTemplate">開列印樣板設計器設計按鈕的樣板；回傳 true = 樣板檔有改</param>
    /// <param name="localColumns">畫面（編輯視窗）上的欄位：「參數名稱」可以從這裡選，選了預設值自動帶 @欄位</param>
    public PrintMenuDesignDialog(PrintMenuDefinition current, Func<ButtonDefinition, bool> designTemplate, IEnumerable<string> localColumns = null)
    {
        menu = current?.Clone() ?? new PrintMenuDefinition();
        this.designTemplate = designTemplate;
        Text = "列印選單（編輯視窗工具列「列印」）";
        Font = new Font("Microsoft JhengHei UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(860, 600);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var titleRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.Controls.Add(new Label { Text = "視窗標題", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        txtTitle.Text = menu.Title ?? "";
        titleRow.Controls.Add(txtTitle, 1, 0);
        titleRow.Controls.Add(new Label
        {
            Text = "有設定按鈕時，編輯視窗工具列的「列印」會先開這個視窗；沒有按鈕 = 照「列印樣板…」印（沒設計樣板就印簡易格式）",
            Dock = DockStyle.Fill, ForeColor = Color.DimGray,
        }, 0, 1);
        titleRow.SetColumnSpan(titleRow.GetControlFromPosition(0, 1), 2);
        layout.Controls.Add(titleRow, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "參數（名稱可選畫面欄位，預設值空白 = 帶這個欄位目前的值；預設值也可以寫固定文字、@欄位、SELECT …、{今天}）",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft,
        }, 0, 1);
        var locals = localColumns?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        if (locals.Count > 0)
        {
            // 參數名稱：下拉選畫面上的欄位，也可以自己打（不在清單裡的名稱）
            var nameColumn = new DataGridViewComboBoxColumn { Name = "Name", HeaderText = "參數名稱（畫面欄位）", Width = 170, FlatStyle = FlatStyle.Flat };
            nameColumn.Items.AddRange(locals.Concat(menu.Params.Select(p => p.Name))
                .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
            dgvParams.Columns.Add(nameColumn);
            dgvParams.EditingControlShowing += (s, e) =>
            {
                if (dgvParams.CurrentCell?.OwningColumn.Name == "Name" && e.Control is ComboBox box) box.DropDownStyle = ComboBoxStyle.DropDown;
            };
            dgvParams.CellValidating += (s, e) =>
            {
                if (dgvParams.Columns[e.ColumnIndex].Name != "Name") return;
                string typed = Convert.ToString(e.FormattedValue)?.Trim().TrimStart('@');
                if (!string.IsNullOrEmpty(typed) && !nameColumn.Items.Cast<object>().Any(i => string.Equals(Convert.ToString(i), typed, StringComparison.OrdinalIgnoreCase)))
                    nameColumn.Items.Add(typed);
                if (!string.IsNullOrEmpty(typed)) dgvParams.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = typed;
            };
            // 選了畫面上的欄位、預設值還空著：預設值自動帶 @欄位（目前這筆的值）
            dgvParams.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || dgvParams.Columns[e.ColumnIndex].Name != "Name") return;
                var row = dgvParams.Rows[e.RowIndex];
                string name = Convert.ToString(row.Cells["Name"].Value)?.Trim();
                if (!string.IsNullOrEmpty(name) && string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["Value"].Value))
                    && locals.Contains(name, StringComparer.OrdinalIgnoreCase))
                    row.Cells["Value"].Value = "@" + name;
            };
            dgvParams.DataError += (s, e) => e.ThrowException = false;
        }
        else
            dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "參數名稱", Width = 130 });
        dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "標籤（空白 = 名稱）", Width = 150 });
        dgvParams.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "預設值", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        dgvParams.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Options", HeaderText = "下拉選項（逗號分隔或 SELECT）", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ToolTipText = "空白 = 一般輸入框。固定值用逗號分隔：訂金,出貨,尾款；或寫 SELECT，@欄位 取目前這筆："
                        + "SELECT 款項期別 FROM F分期付款 WHERE 單號 = @單號（列出第一個欄位）。列印視窗會顯示成下拉選單",
        });
        dgvParams.Columns.Add(new DataGridViewCheckBoxColumn { Name = "ReadOnly", HeaderText = "不能改", Width = 60, ToolTipText = "有下拉選項時 = 只能從選項選，不能自己打" });
        dgvParams.AllowUserToAddRows = true;
        foreach (var p in menu.Params) dgvParams.Rows.Add(p.Name, p.Label, p.Value, p.Options, p.ReadOnly);
        layout.Controls.Add(dgvParams, 0, 2);

        layout.Controls.Add(new Label
        {
            Text = "按鈕（每個按鈕印一個樣板，或開啟一個自訂畫面；按「設計…」開列印樣板設計器，「選檔…」可以選列印樣板或畫面定義檔）",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft,
        }, 0, 3);
        dgvButtons.Columns.Add(new DataGridViewTextBoxColumn { Name = "Text", HeaderText = "按鈕文字", Width = 200 });
        dgvButtons.Columns.Add(new DataGridViewTextBoxColumn { Name = "Template", HeaderText = "列印樣板 / 畫面", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        dgvButtons.Columns.Add(new DataGridViewButtonColumn { Name = "Design", HeaderText = "", Text = "設計…", UseColumnTextForButtonValue = true, Width = 70 });
        dgvButtons.Columns.Add(new DataGridViewButtonColumn { Name = "Pick", HeaderText = "", Text = "選檔…", UseColumnTextForButtonValue = true, Width = 70 });
        dgvButtons.AllowUserToAddRows = true;
        foreach (var b in menu.Buttons) dgvButtons.Rows.Add(b.Text, b.Action == ButtonAction.開啟畫面 ? b.TargetForm : b.PrintTemplate);
        dgvButtons.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == dgvButtons.Columns["Design"].Index) DesignRow(e.RowIndex);
            else if (e.ColumnIndex == dgvButtons.Columns["Pick"].Index) PickRow(e.RowIndex);
        };
        layout.Controls.Add(dgvButtons, 0, 4);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        var btnOk = new Button { Text = "確定", Width = 90, Height = 30 };
        btnOk.Click += (s, e) => Accept();
        bottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
        layout.Controls.Add(bottom, 0, 5);
        Controls.Add(layout);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        RowHeadersWidth = 24,
        BackgroundColor = SystemColors.Window,
        AllowUserToResizeRows = false,
        EditMode = DataGridViewEditMode.EditOnEnter,
    };

    /// <summary>按鈕的樣板：開列印樣板設計器（沒有樣板就建新的），存檔後填進「列印樣板」</summary>
    private void DesignRow(int rowIndex)
    {
        dgvButtons.EndEdit();
        var row = dgvButtons.Rows[rowIndex];
        if (row.IsNewRow)
        {
            dgvButtons.Rows.Add("列印");
            row = dgvButtons.Rows[rowIndex];
        }
        string text = Convert.ToString(row.Cells["Text"].Value);
        if (IsFormFile(Convert.ToString(row.Cells["Template"].Value)))
        {
            MessageBox.Show("這一列選的是畫面定義檔（開啟自訂畫面），請到選單上「設計這個畫面」修改它；要改成列印樣板，請先按「選檔…」換成 .print.json。", Text);
            return;
        }
        var b = new ButtonDefinition
        {
            Text = string.IsNullOrWhiteSpace(text) ? "列印" : text.Trim(),
            Action = ButtonAction.列印報表,
            PrintTemplate = Convert.ToString(row.Cells["Template"].Value) is { Length: > 0 } t ? t : null,
        };
        designTemplate(b);
        row.Cells["Text"].Value = b.Text;
        row.Cells["Template"].Value = b.PrintTemplate;
    }

    /// <summary>是畫面定義檔（.json，不是 .print.json）：按鈕開啟這個畫面，不是印樣板</summary>
    internal static bool IsFormFile(string file) =>
        !string.IsNullOrWhiteSpace(file) && file.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && !file.Trim().EndsWith(".print.json", StringComparison.OrdinalIgnoreCase);

    /// <summary>按鈕的樣板：選一個已經設計好的樣板檔，或一個畫面定義檔（按鈕改成開啟這個自訂畫面）</summary>
    private void PickRow(int rowIndex)
    {
        dgvButtons.EndEdit();
        using var dlg = new OpenFileDialog
        {
            Filter = "列印樣板或畫面 (*.json)|*.json|列印樣板 (*.print.json)|*.print.json",
            InitialDirectory = FormDefinition.DefaultFolder,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var row = dgvButtons.Rows[rowIndex];
        if (row.IsNewRow)
        {
            dgvButtons.Rows.Add(Path.GetFileNameWithoutExtension(dlg.FileName).Replace(".print", ""));
            row = dgvButtons.Rows[rowIndex];
        }
        bool sameFolder = string.Equals(Path.GetDirectoryName(dlg.FileName), Path.GetFullPath(FormDefinition.DefaultFolder), StringComparison.OrdinalIgnoreCase);
        row.Cells["Template"].Value = sameFolder ? Path.GetFileName(dlg.FileName) : dlg.FileName;
    }

    private void Accept()
    {
        dgvParams.EndEdit();
        dgvButtons.EndEdit();
        var result = new PrintMenuDefinition { Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? null : txtTitle.Text.Trim() };
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow r in dgvParams.Rows)
        {
            if (r.IsNewRow) continue;
            string name = Convert.ToString(r.Cells["Name"].Value)?.Trim().TrimStart('@');
            if (string.IsNullOrEmpty(name)) continue;
            if (!names.Add(name))
            {
                MessageBox.Show($"參數名稱「{name}」重複了。", Text);
                return;
            }
            result.Params.Add(new PrintMenuParam
            {
                Name = name,
                Label = Convert.ToString(r.Cells["Label"].Value)?.Trim() is { Length: > 0 } l ? l : null,
                Value = Convert.ToString(r.Cells["Value"].Value)?.Trim() is { Length: > 0 } v ? v : null,
                Options = Convert.ToString(r.Cells["Options"].Value)?.Trim() is { Length: > 0 } o ? o : null,
                ReadOnly = r.Cells["ReadOnly"].Value is true,
            });
        }
        foreach (DataGridViewRow r in dgvButtons.Rows)
        {
            if (r.IsNewRow) continue;
            string text = Convert.ToString(r.Cells["Text"].Value)?.Trim();
            string template = Convert.ToString(r.Cells["Template"].Value)?.Trim();
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(template)) continue;
            if (string.IsNullOrEmpty(template))
            {
                MessageBox.Show($"按鈕「{text}」還沒有列印樣板，請按「設計…」或「選檔…」。", Text);
                return;
            }
            if (IsFormFile(template))
            {
                // 畫面定義檔：按鈕開啟這個自訂畫面，列印視窗的參數（例如單號）照同名傳過去
                result.Buttons.Add(new ButtonDefinition
                {
                    Text = string.IsNullOrEmpty(text) ? Path.GetFileNameWithoutExtension(template) : text,
                    Action = ButtonAction.開啟畫面,
                    TargetForm = template,
                    OpenParams = result.Params.Count == 0 ? null : string.Join(", ", result.Params.Select(p => $"{p.Name}=@{p.Name}")),
                });
                continue;
            }
            result.Buttons.Add(new ButtonDefinition { Text = string.IsNullOrEmpty(text) ? "列印" : text, Action = ButtonAction.列印報表, PrintTemplate = template });
        }
        Result = result.Buttons.Count == 0 && result.Params.Count == 0 ? null : result;
        DialogResult = DialogResult.OK;
    }
}

/// <summary>
/// 執行時的列印視窗（編輯視窗工具列「列印」）：上面是參數輸入框（帶預設值），下面每個按鈕印一個樣板。
/// 參數的值傳給樣板（樣板的「列印參數」），印完視窗不關，可以接著印別的
/// </summary>
public class PrintMenuRunDialog : Form
{
    private readonly Dictionary<string, Control> inputs = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="current">目前這筆的欄位值（預設值的 @欄位、SQL 的 @名稱；樣板讀資料的主鍵）</param>
    public PrintMenuRunDialog(PrintMenuDefinition menu, string title, string sourcePath, string connectionString, Func<string, object> current)
    {
        Text = string.IsNullOrWhiteSpace(menu.Title) ? "列印：" + title : menu.Title;
        Font = new Font("Microsoft JhengHei UI", 10f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Location = new Point(12, 12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        foreach (var p in menu.Params)
        {
            string text = "";
            // 預設值空白：參數名稱是畫面上的欄位時，帶目前這筆的值
            try { text = ToText(string.IsNullOrWhiteSpace(p.Value) ? current(p.Name) : ResolveDefault(p.Value, connectionString, current)); }
            catch (Exception ex) { text = "（預設值錯誤：" + ex.Message + "）"; }
            Control box;
            if (!string.IsNullOrWhiteSpace(p.Options))
            {
                // 有下拉選項：下拉選單（不能改 = 只能從選項選），預設值空白時選第一個
                var combo = new ComboBox { DropDownStyle = p.ReadOnly ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };
                try { combo.Items.AddRange(OptionValues(p.Options, connectionString, current).Cast<object>().ToArray()); }
                catch (Exception ex) { MessageBox.Show($"參數「{p.Label ?? p.Name}」的下拉選項讀取失敗：{ex.Message}", "列印"); }
                int index = combo.Items.IndexOf(text);
                if (index >= 0) combo.SelectedIndex = index;
                else if (text == "" && combo.Items.Count > 0) combo.SelectedIndex = 0;
                else if (combo.DropDownStyle == ComboBoxStyle.DropDown) combo.Text = text;
                box = combo;
            }
            else box = new TextBox { Text = text, ReadOnly = p.ReadOnly, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };
            inputs[p.Name] = box;
            layout.Controls.Add(new Label { Text = (p.Label ?? p.Name) + "：", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3) });
            layout.Controls.Add(box);
        }
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 10, 0, 0) };
        foreach (var b in menu.Buttons)
        {
            var button = new Button { Text = b.Text, AutoSize = true, MinimumSize = new Size(110, 34), Margin = new Padding(3) };
            ButtonRunner.ApplyFont(b, button);
            button.Click += (s, e) =>
            {
                if (b.Action == ButtonAction.開啟畫面)    // 選的是畫面定義檔：開啟自訂畫面，@名稱 取視窗上的參數，沒有再取目前這筆
                {
                    // 開了自訂的列印畫面，這個列印視窗就功成身退：參數先記下來，關掉這個視窗，畫面掛在原本的視窗底下
                    var values = inputs.ToDictionary(x => x.Key, x => ValueOf(x.Key, current), StringComparer.OrdinalIgnoreCase);
                    var parent = Owner;
                    Hide();
                    ButtonRunner.OpenForm(b, sourcePath, parent, name => values.TryGetValue(name, out var v) ? v : current(name), connectionString: connectionString);
                    Close();
                }
                else
                    ButtonRunner.Print(b, sourcePath, connectionString, new Func<string, object>[] { name => ValueOf(name, current) }, this);
            };
            buttons.Controls.Add(button);
        }
        var close = new Button { Text = "關閉", AutoSize = true, MinimumSize = new Size(90, 34), Margin = new Padding(3), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(close);
        CancelButton = close;
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
    }

    /// <summary>樣板要的值：參數先用視窗上輸入的，其他用目前這筆的欄位</summary>
    private object ValueOf(string name, Func<string, object> current) =>
        inputs.TryGetValue(name, out var box) ? (box.Text.Trim() == "" ? DBNull.Value : box.Text.Trim()) : current(name);

    /// <summary>下拉選項：SELECT（或 WITH）查出第一個欄位的值，@欄位 取目前這筆；其他用逗號（、或換行）分隔的固定值</summary>
    internal static List<string> OptionValues(string options, string connectionString, Func<string, object> current)
    {
        string text = options?.Trim() ?? "";
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^(SELECT|WITH)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var table = SqlList.Load(connectionString, text, current);
            return table.Columns.Count == 0 ? new() : table.Rows.Cast<DataRow>().Select(r => ToText(r[0])).Where(v => v != "").Distinct().ToList();
        }
        return text.Split(new[] { ',', '，', '、', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).Where(v => v != "").Distinct().ToList();
    }

    /// <summary>預設值：整段 @欄位 = 目前這筆的值；其他照欄位預設值的寫法（固定文字、SELECT、{今天}、{使用者名稱}…）</summary>
    private static object ResolveDefault(string text, string connectionString, Func<string, object> current)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^@([\p{L}\p{N}_]+)$");
        if (m.Success) return current(m.Groups[1].Value);
        return DefaultValue.Resolve(text, connectionString, current);
    }

    private static string ToText(object value) => value switch
    {
        null or DBNull => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : d.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
        string s => s.TrimEnd(),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture),
    };
}
