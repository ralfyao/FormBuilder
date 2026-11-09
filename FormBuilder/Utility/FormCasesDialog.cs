using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FormBuilder;

/// <summary>屬性面板「依條件開啟」的「…」按鈕：開「依條件開啟設定」視窗，用下拉選欄位、比較方式，不用自己寫條件</summary>
public class FormCasesEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not ButtonDefinition b) return value;      // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        IEnumerable<string> locals = null;
        string folder = null;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h)
            {
                locals ??= h.ParamColumns;
                folder ??= h.PickerFolder;
            }
        using var dialog = new FormCasesDialog(value as string, b.TargetForm, folder ?? FormDefinition.DefaultFolder, locals ?? Enumerable.Empty<string>());
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Cases : value;
    }
}

/// <summary>
/// 依條件開啟設定：每一列「欄位 + 比較方式 + 值 → 開啟的畫面」，由上往下第一個符合的就開那個畫面。
/// 存回去還是「條件 => 畫面定義檔」的文字；看不懂的條件（自己寫的）放在「自訂條件」原樣保留
/// </summary>
public class FormCasesDialog : Form
{
    private const string ColField = "Field", ColOp = "Op", ColValue = "Value", ColForm = "Form", ColBrowse = "Browse",
                         ColParams = "Params", ColParamsEdit = "ParamsEdit";
    private const string Custom = ConditionText.Custom;

    private readonly string folder;
    private readonly List<string> locals;
    private readonly DataGridViewComboBoxColumn colField;
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToDeleteRows = true,
        RowHeadersWidth = 24,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };

    /// <summary>按確定後的設定（每行「條件 => 畫面定義檔 | 帶入參數」；沒有任何一列 = null）</summary>
    public string Cases { get; private set; }

    public FormCasesDialog(string cases, string defaultForm, string folder, IEnumerable<string> localColumns)
    {
        this.folder = folder;
        locals = localColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Text = "依條件開啟設定";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(1080, 420);

        colField = new DataGridViewComboBoxColumn
        {
            Name = ColField, HeaderText = "本畫面的欄位", FillWeight = 17,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colField.Items.Add("");
        colField.Items.AddRange(locals.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        var colOp = new DataGridViewComboBoxColumn
        {
            Name = ColOp, HeaderText = "比較", FillWeight = 10,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colOp.Items.AddRange(ConditionText.Ops);
        grid.Columns.Add(colField);
        grid.Columns.Add(colOp);
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColValue, HeaderText = "值", FillWeight = 20,
            ToolTipText = "文字直接打，不用加單引號；數字直接比數字（勾選框寫 0 或 1），文字欄位要比對 001 這種純數字代碼時寫 '001'。「是其中之一」用逗號分隔，例如：售後維修,客服。「自訂條件」在這裡寫完整的條件（寫法同執行前檢查）",
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColForm, HeaderText = "開啟的畫面（畫面定義檔）", FillWeight = 22 });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = ColBrowse, HeaderText = "", Text = "…", UseColumnTextForButtonValue = true, FillWeight = 3, ToolTipText = "選畫面定義檔" });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColParams, HeaderText = "帶入參數", FillWeight = 22,
            ToolTipText = "開這個畫面時另外帶的傳入參數：「對方欄位=值」用逗號分隔，值可以是 @本畫面欄位、SQL 運算式或固定值，例如：單據類別=維修, 部門=@部門。"
                        + "加在按鈕的「傳入參數」之後，同名的以這裡為準。按右邊的「…」從對方畫面的欄位選",
        });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = ColParamsEdit, HeaderText = "", Text = "…", UseColumnTextForButtonValue = true, FillWeight = 3, ToolTipText = "設定帶入參數" });

        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.DefaultValuesNeeded += (s, e) => e.Row.Cells[ColOp].Value = "等於";
        grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) Check(grid.Rows[e.RowIndex]); };
        grid.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == grid.Columns[ColParamsEdit].Index)
            {
                EditParams(grid.Rows[e.RowIndex]);
                return;
            }
            if (e.ColumnIndex != grid.Columns[ColBrowse].Index) return;
            using var open = new OpenFileDialog { Filter = "畫面定義檔 (*.json)|*.json", InitialDirectory = Directory.Exists(folder) ? folder : "" };
            if (open.ShowDialog(this) != DialogResult.OK) return;
            if (grid.Rows[e.RowIndex].IsNewRow)
            {
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[ColForm];
                grid.NotifyCurrentCellDirty(true);
            }
            grid.Rows[e.RowIndex].Cells[ColForm].Value = ButtonRunner.RelativePath(open.FileName, folder) ?? open.FileName;
            grid.EndEdit();
        };
        grid.DataError += (s, e) => e.ThrowException = false;

        var btnUp = new Button { Text = "上移", Width = 70, Height = 28 };
        var btnDown = new Button { Text = "下移", Width = 70, Height = 28 };
        btnUp.Click += (s, e) => Move(-1);
        btnDown.Click += (s, e) => Move(1);
        var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 82, FlowDirection = FlowDirection.TopDown, Padding = new Padding(6, 0, 0, 0) };
        side.Controls.Add(btnUp);
        side.Controls.Add(btnDown);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Text = "由上往下比對，第一個符合的就開那個畫面（可以用右邊的上移、下移調整順序）。",
        }, 0, 0);
        var middle = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        middle.Controls.Add(grid);
        middle.Controls.Add(side);
        layout.Controls.Add(middle, 0, 1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText,
            Text = "都不符合時開「畫面定義檔」：" + (string.IsNullOrWhiteSpace(defaultForm) ? "（沒有設定，會提醒使用者）" : defaultForm)
                 + "。每個畫面都帶按鈕的「傳入參數」，再加上那一列的「帶入參數」（同名的以帶入參數為準）；回傳對應照用同一組設定，所以這些畫面的欄位名稱要一樣。",
        }, 0, 2);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new Button { Text = "確定", Dock = DockStyle.Right, Width = 90 };
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
            var lines = new List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                string form = (row.Cells[ColForm].Value as string)?.Trim();
                string condition = ConditionText.Build(row.Cells[ColField].Value as string, row.Cells[ColOp].Value as string, row.Cells[ColValue].Value as string);
                string parameters = (row.Cells[ColParams].Value as string)?.Trim();
                if (string.IsNullOrEmpty(form) && condition == null && string.IsNullOrEmpty(parameters)) continue;   // 整列空白：略過
                if (string.IsNullOrEmpty(form) || condition == null)
                {
                    grid.CurrentCell = row.Cells[string.IsNullOrEmpty(form) ? ColForm : ColField];
                    MessageBox.Show(this, string.IsNullOrEmpty(form) ? "這一列還沒有選要開啟的畫面。" : "這一列的條件還沒設定好（要選欄位，「是空白」以外要填值）。", Text);
                    return;
                }
                try
                {
                    ButtonRunner.ConditionHolds(condition, _ => null);                  // 先試算一次，寫錯就在這裡提醒
                }
                catch (Exception ex) when (ex is System.Data.EvaluateException or System.Data.SyntaxErrorException)
                {
                    grid.CurrentCell = row.Cells[ColValue];
                    MessageBox.Show(this, $"條件寫法有誤：{condition}{Environment.NewLine}{ex.Message}", Text);
                    return;
                }
                lines.Add(ButtonRunner.FormatFormCase(condition, form, parameters));
            }
            Cases = lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
            DialogResult = DialogResult.OK;
        };

        foreach (var (condition, form, parameters) in ButtonRunner.ParseFormCases(cases))
        {
            var (field, op, value) = ConditionText.Parse(condition);
            if (field != null && !colField.Items.Cast<string>().Contains(field, StringComparer.OrdinalIgnoreCase)) colField.Items.Add(field);
            field = colField.Items.Cast<string>().FirstOrDefault(i => i.Equals(field ?? "", StringComparison.OrdinalIgnoreCase)) ?? "";
            int i = grid.Rows.Add(field, op, value, form, null, parameters);
            Check(grid.Rows[i]);
        }
    }

    /// <summary>「帶入參數」的「…」：開傳入參數設定視窗，列出這一列要開的畫面收得到的參數</summary>
    private void EditParams(DataGridViewRow row)
    {
        string form = (row.Cells[ColForm].Value as string)?.Trim();
        if (string.IsNullOrEmpty(form))
        {
            MessageBox.Show(this, "請先選這一列要開啟的畫面，才能列出它收得到的參數。", Text);
            return;
        }
        grid.EndEdit();
        using var dialog = new OpenParamsDialog(form, row.Cells[ColParams].Value as string, folder, locals);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (row.IsNewRow)
        {
            grid.CurrentCell = row.Cells[ColParams];
            grid.NotifyCurrentCellDirty(true);
        }
        row.Cells[ColParams].Value = dialog.Params;
        grid.EndEdit();
    }

    private void Move(int delta)
    {
        if (grid.CurrentCell == null) return;
        int from = grid.CurrentCell.RowIndex, to = from + delta, col = grid.CurrentCell.ColumnIndex;
        if (grid.Rows[from].IsNewRow || to < 0 || to >= grid.Rows.Count || grid.Rows[to].IsNewRow) return;
        grid.EndEdit();
        var row = grid.Rows[from];
        grid.Rows.RemoveAt(from);
        grid.Rows.Insert(to, row);
        grid.CurrentCell = grid.Rows[to].Cells[col];
    }

    /// <summary>「自訂條件」不用選欄位、「是空白／不是空白」不用填值：灰掉不用的格子</summary>
    private void Check(DataGridViewRow row)
    {
        if (row.IsNewRow) return;
        string op = row.Cells[ColOp].Value as string;
        bool custom = op == Custom, noValue = op is "是空白" or "不是空白";
        row.Cells[ColField].Style.BackColor = custom ? SystemColors.Control : Color.Empty;
        row.Cells[ColValue].Style.BackColor = noValue ? SystemColors.Control : Color.Empty;
    }
}

/// <summary>
/// 視窗設定條件用的共用轉換：「欄位 + 比較 + 值」⇄ DataTable 條件運算式（寫法同「執行前檢查」）。
/// 看不懂的寫法（自己寫的）當「自訂條件」原樣保留
/// </summary>
public static class ConditionText
{
    public const string Custom = "自訂條件";
    public static readonly string[] Ops =
        { "等於", "不等於", "大於", "大於等於", "小於", "小於等於", "包含", "不包含", "開頭是", "開頭不是", "是其中之一", "不是其中之一", "是空白", "不是空白", Custom };

    /// <summary>相反的比較方式（舊的「要通過」條件轉成「擋下」規則用）；自訂條件沒有相反的，回傳 null</summary>
    public static string Opposite(string op) => op switch
    {
        "等於" => "不等於", "不等於" => "等於",
        "大於" => "小於等於", "小於等於" => "大於",
        "小於" => "大於等於", "大於等於" => "小於",
        "包含" => "不包含", "不包含" => "包含",
        "開頭是" => "開頭不是", "開頭不是" => "開頭是",
        "是其中之一" => "不是其中之一", "不是其中之一" => "是其中之一",
        "是空白" => "不是空白", "不是空白" => "是空白",
        _ => null,
    };

    private static string Quote(string text) => "'" + text.Replace("'", "''") + "'";

    /// <summary>一組「欄位、比較、值」組成條件運算式；還沒設定好回傳 null</summary>
    public static string Build(string field, string op, string value)
    {
        field = field?.Trim();
        op ??= "等於";
        value = value?.Trim() ?? "";
        if (op == Custom) return value.Length == 0 ? null : value;
        if (string.IsNullOrEmpty(field)) return null;
        string f = "[" + field + "]";
        if (op == "是空白") return $"ISNULL(CONVERT({f}, 'System.String'), '') = ''";
        if (op == "不是空白") return $"ISNULL(CONVERT({f}, 'System.String'), '') <> ''";
        if (value.Length == 0) return null;
        // 數字不加引號（bit 欄位只能寫 = 0、= 1）；文字欄位要比對純數字的代碼（例如 001）時，值自己加單引號寫 '001'
        string literal = value.Length >= 2 && value[0] == '\'' && value[^1] == '\'' ? Quote(value[1..^1])
                       : IsNumber(value) ? value : Quote(value);
        if (op is "不包含" or "開頭不是" or "不是其中之一")
            return $"NOT ({Build(field, op switch { "不包含" => "包含", "開頭不是" => "開頭是", _ => "是其中之一" }, value)})";
        return op switch
        {
            "不等於" => $"{f} <> {literal}",
            "大於" => $"{f} > {literal}",
            "大於等於" => $"{f} >= {literal}",
            "小於" => $"{f} < {literal}",
            "小於等於" => $"{f} <= {literal}",
            "包含" => $"{f} LIKE {Quote("%" + value + "%")}",
            "開頭是" => $"{f} LIKE {Quote(value + "%")}",
            "是其中之一" => $"{f} IN ({string.Join(", ", value.Split(',', '，').Select(v => v.Trim()).Where(v => v.Length > 0).Select(Quote))})",
            _ => $"{f} = {literal}",
        };
    }

    /// <summary>擋下規則轉成「要通過」的條件：NOT (規則1) AND NOT (規則2) …（存進執行前檢查，Grid 按鈕變灰等舊功能照用）</summary>
    public static string PassCondition(IEnumerable<PreCheckBlock> blocks) =>
        string.Join(" AND ", blocks.Select(b => $"NOT ({b.Condition.Trim()})"));

    private static bool IsNumber(string text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    private const string Name = @"(?:\[(?<f>[^\]]+)\]|(?<f>[\p{L}_][\p{L}\p{N}_]*))";
    private const string Str = @"'(?<v>(?:[^']|'')*)'";
    private const string Num = @"(?<v>-?\d+(?:\.\d+)?)";

    /// <summary>把條件拆回「欄位、比較、值」；不是這個視窗組得出來的寫法就當自訂條件</summary>
    public static (string Field, string Op, string Value) Parse(string condition)
    {
        string c = condition.Trim();
        static string Unquote(string v) => v.Replace("''", "'");
        Match m;
        if ((m = Regex.Match(c, @"^NOT\s*\((?<inner>.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success
            && Parse(m.Groups["inner"].Value) is { Field: not null } inner && inner.Op is "包含" or "開頭是" or "是其中之一")
            return (inner.Field, Opposite(inner.Op), inner.Value);
        if ((m = Regex.Match(c, $@"^ISNULL\(CONVERT\({Name}, 'System\.String'\), ''\) (?<op>=|<>) ''$", RegexOptions.IgnoreCase)).Success)
            return (m.Groups["f"].Value, m.Groups["op"].Value == "=" ? "是空白" : "不是空白", "");
        if ((m = Regex.Match(c, $@"^{Name}\s+LIKE\s+'%(?<v>(?:[^'%]|'')*)%'$", RegexOptions.IgnoreCase)).Success)
            return (m.Groups["f"].Value, "包含", Unquote(m.Groups["v"].Value));
        if ((m = Regex.Match(c, $@"^{Name}\s+LIKE\s+'(?<v>(?:[^'%]|'')*)%'$", RegexOptions.IgnoreCase)).Success)
            return (m.Groups["f"].Value, "開頭是", Unquote(m.Groups["v"].Value));
        if ((m = Regex.Match(c, $@"^{Name}\s+IN\s*\((?<list>\s*'(?:[^']|'')*'(?:\s*,\s*'(?:[^']|'')*')*\s*)\)$", RegexOptions.IgnoreCase)).Success)
            return (m.Groups["f"].Value, "是其中之一",
                    string.Join(",", Regex.Matches(m.Groups["list"].Value, Str).Select(x => Unquote(x.Groups["v"].Value))));
        if ((m = Regex.Match(c, $@"^{Name}\s*(?<op>=|<>|>=|<=|>|<)\s*(?:{Str}|{Num})$")).Success)
        {
            string op = m.Groups["op"].Value switch
            {
                "<>" => "不等於", ">" => "大於", ">=" => "大於等於", "<" => "小於", "<=" => "小於等於", _ => "等於",
            };
            string v = Unquote(m.Groups["v"].Value);
            // 用單引號括起來的數字（文字比對）要保留引號，不然存回去會變成數字比對
            return (m.Groups["f"].Value, op, m.Groups["v"].Index > 0 && c[m.Groups["v"].Index - 1] == '\'' && IsNumber(v) ? $"'{v}'" : v);
        }
        return (null, Custom, condition);
    }

    /// <summary>
    /// 依最外層的 AND / OR 切開條件（括號、單引號、中括號裡的不切）；回傳每一段和它前面的連接詞（第一段是 null）
    /// </summary>
    public static List<(string Join, string Condition)> SplitJoins(string condition)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(condition)) return result;
        string text = condition.Trim();
        int depth = 0, start = 0;
        bool quoted = false, bracket = false;
        string join = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted) { if (c == '\'') quoted = false; continue; }      // '' 連寫會關掉再打開，結果不變
            if (bracket) { if (c == ']') bracket = false; continue; }
            if (c == '\'') quoted = true;
            else if (c == '[') bracket = true;
            else if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && char.IsWhiteSpace(c))
            {
                var m = Regex.Match(text[i..], @"^\s+(AND|OR)\s+", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                result.Add((join, text[start..i].Trim()));
                join = m.Groups[1].Value.ToUpperInvariant();
                i += m.Length - 1;
                start = i + 1;
            }
        }
        result.Add((join, text[start..].Trim()));
        return result;
    }
}
