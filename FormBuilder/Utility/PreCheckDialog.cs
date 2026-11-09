using System.ComponentModel;
using System.Drawing.Design;

namespace FormBuilder;

/// <summary>屬性面板「執行前檢查」的「…」按鈕：開「執行前檢查設定」，一列一個條件，寫「什麼情況要擋下來、跳什麼訊息」</summary>
public class PreCheckEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

    public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
    {
        if (context?.Instance is not ButtonDefinition b) return value;      // 多選：直接在屬性面板打字
        var owner = Form.ActiveForm;
        IEnumerable<string> locals = null;
        for (var f = owner; f != null; f = f.Owner)
            if (f is IPickerHost h) locals ??= h.ParamColumns;
        using var dialog = new PreCheckDialog(value as string, b.PreCheckMessage, b.PreCheckBlocks, locals ?? Enumerable.Empty<string>());
        if (dialog.ShowDialog(owner) != DialogResult.OK) return value;
        b.PreCheckMessage = dialog.Message;
        b.PreCheckBlocks = dialog.Blocks;
        return dialog.Condition;
    }
}

/// <summary>
/// 執行前檢查設定：每一列寫「什麼情況要擋下來」。沒填訊息的列用「且 / 或」跟下一列串成同一組一起判斷，
/// 填了「成立時」或「訊息」的列是那一組的結尾。執行時由上往下，第一組成立的就跳它的訊息（停止，或詢問是否繼續）。
/// 存成擋下規則（PreCheckBlocks），「執行前檢查」存由規則算出來的通過條件（NOT 規則1 AND NOT 規則2 …）
/// </summary>
public class PreCheckDialog : Form
{
    private const string ColField = "Field", ColOp = "Op", ColValue = "Value", ColAction = "Action", ColMessage = "Message", ColJoin = "Join";
    private const string And = "且", Or = "或";

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
    private readonly TextBox txtPreview = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox txtMessage = new() { Dock = DockStyle.Fill };

    /// <summary>按確定後的通過條件（存進「執行前檢查」；沒有任何規則 = null）</summary>
    public string Condition { get; private set; }
    /// <summary>按確定後的擋下規則（沒有 = null）</summary>
    public List<PreCheckBlock> Blocks { get; private set; }
    /// <summary>按確定後的預設訊息（規則沒填訊息時用；空白 = null）</summary>
    public string Message { get; private set; }

    public PreCheckDialog(string condition, string message, IEnumerable<PreCheckBlock> blocks, IEnumerable<string> localColumns)
    {
        Text = "執行前檢查設定";
        Font = new Font("Microsoft JhengHei UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(1080, 520);

        colField = new DataGridViewComboBoxColumn
        {
            Name = ColField, HeaderText = "欄位", FillWeight = 22,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colField.Items.Add("");
        colField.Items.AddRange(localColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase)
                                            .Order(StringComparer.OrdinalIgnoreCase).ToArray());
        var colOp = new DataGridViewComboBoxColumn
        {
            Name = ColOp, HeaderText = "比較", FillWeight = 14,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        colOp.Items.AddRange(ConditionText.Ops);
        var colAction = new DataGridViewComboBoxColumn
        {
            Name = ColAction, HeaderText = "成立時", FillWeight = 18,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            ToolTipText = "這一組條件成立時要做什麼。空白、也沒填訊息 = 這一列跟下一列串在一起判斷",
        };
        colAction.Items.Add("");
        colAction.Items.AddRange(Enum.GetNames<PreCheckAction>());
        var colJoin = new DataGridViewComboBoxColumn
        {
            Name = ColJoin, HeaderText = "且 / 或", FillWeight = 9,
            FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            ToolTipText = "沒填訊息的列跟下一列怎麼串在一起判斷；有填訊息的列是一組的結尾，這格用不到",
        };
        colJoin.Items.AddRange(And, Or);
        grid.Columns.Add(colField);
        grid.Columns.Add(colOp);
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColValue, HeaderText = "值", FillWeight = 25,
            ToolTipText = "文字直接打，不用加單引號；數字直接比數字（勾選框寫 0 或 1），文字欄位要比對 001 這種純數字代碼時寫 '001'。「是其中之一」用逗號分隔，例如：A,B。「自訂條件」在這裡寫完整的條件",
        });
        grid.Columns.Add(colAction);
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColMessage, HeaderText = "訊息", FillWeight = 40,
            ToolTipText = "這一組條件成立時跳出的訊息，例如：零件申請單已開立，請勿重複操作！有填訊息、沒選動作 = 顯示訊息並停止",
        });
        grid.Columns.Add(colJoin);

        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.DefaultValuesNeeded += (s, e) =>
        {
            e.Row.Cells[ColJoin].Value = And;
            e.Row.Cells[ColOp].Value = "等於";
        };
        grid.CellValueChanged += (s, e) => { RefreshAll(); UpdatePreview(); };
        grid.RowsRemoved += (s, e) => { RefreshAll(); UpdatePreview(); };
        grid.RowsAdded += (s, e) => { RefreshAll(); UpdatePreview(); };
        grid.DataError += (s, e) => e.ThrowException = false;

        var btnUp = new Button { Text = "上移", Width = 70, Height = 28 };
        var btnDown = new Button { Text = "下移", Width = 70, Height = 28 };
        btnUp.Click += (s, e) => Move(-1);
        btnDown.Click += (s, e) => Move(1);
        var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 82, FlowDirection = FlowDirection.TopDown, Padding = new Padding(6, 0, 0, 0) };
        side.Controls.Add(btnUp);
        side.Controls.Add(btnDown);
        var middle = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        middle.Controls.Add(grid);
        middle.Controls.Add(side);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var info = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Text = "每一列寫「什麼情況要擋下來」，在「訊息」填要跳出的文字。要好幾列一起判斷時，前面的列不填訊息、用「且 / 或」串到下一列，在最後一列填訊息。"
                 + "執行時由上往下，第一組成立的就跳它的訊息（像 if / else if）；都不成立才執行。",
        };
        layout.Controls.Add(info, 0, 0);
        layout.SetColumnSpan(info, 2);
        layout.Controls.Add(middle, 0, 1);
        layout.SetColumnSpan(middle, 2);
        layout.Controls.Add(new Label { Text = "判斷順序：", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight }, 0, 2);
        layout.Controls.Add(txtPreview, 1, 2);
        layout.Controls.Add(new Label { Text = "預設訊息：", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
        layout.Controls.Add(txtMessage, 1, 3);
        txtPreview.ForeColor = SystemColors.GrayText;
        txtMessage.PlaceholderText = "規則沒填訊息時跳出的訊息（空白 = 顯示條件）";
        txtMessage.Text = message ?? "";

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
            var built = BuildBlocks(out var bad);
            if (bad != null)
            {
                grid.CurrentCell = bad.Cells[string.IsNullOrWhiteSpace(bad.Cells[ColField].Value as string) && bad.Cells[ColOp].Value as string != ConditionText.Custom ? ColField : ColValue];
                MessageBox.Show(this, "這一列的條件還沒設定好（要選欄位，「是空白」「不是空白」以外要填值）。", Text);
                return;
            }
            foreach (var block in built)
            {
                try
                {
                    ButtonRunner.ConditionHolds(block.Condition, _ => null);       // 先試算一次，寫錯就在這裡提醒
                }
                catch (Exception ex) when (ex is System.Data.EvaluateException or System.Data.SyntaxErrorException)
                {
                    MessageBox.Show(this, $"條件寫法有誤：{block.Condition}{Environment.NewLine}{ex.Message}", Text);
                    return;
                }
            }
            Blocks = built.Count == 0 ? null : built;
            Condition = built.Count == 0 ? null : ConditionText.PassCondition(built);
            Message = string.IsNullOrWhiteSpace(txtMessage.Text) ? null : txtMessage.Text.Trim();
            DialogResult = DialogResult.OK;
        };
        txtMessage.TextChanged += (s, e) => UpdatePreview();

        LoadRows(condition, blocks?.ToList());
        RefreshAll();
        UpdatePreview();
    }

    /// <summary>
    /// 載入設定：有擋下規則（而且執行前檢查沒被直接改過）就照規則列出來；
    /// 只有舊的「要通過」條件時，全部用「且」串的就每一段反過來變成一條規則，其他寫法整段當一條自訂條件（NOT (…)）
    /// </summary>
    private void LoadRows(string condition, List<PreCheckBlock> blocks)
    {
        if (blocks is { Count: > 0 } && ConditionText.PassCondition(blocks) == condition?.Trim())
        {
            foreach (var block in blocks)
            {
                var parts = ConditionText.SplitJoins(block.Condition);
                for (int k = 0; k < parts.Count; k++)
                {
                    bool last = k == parts.Count - 1;
                    AddRow(ConditionText.Parse(parts[k].Condition), last ? block.Action.ToString() : "", last ? block.Message : null,
                           !last && parts[k + 1].Join == "OR" ? Or : And);
                }
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(condition)) return;
        var old = ConditionText.SplitJoins(condition);
        if (old.Skip(1).All(p => p.Join == "AND"))
        {
            foreach (var (_, part) in old)
            {
                var (field, op, value) = ConditionText.Parse(part);
                string opposite = ConditionText.Opposite(op);
                AddRow(opposite != null ? (field, opposite, value) : (null, ConditionText.Custom, $"NOT ({part})"),
                       nameof(PreCheckAction.顯示訊息並停止), null, And);
            }
        }
        else
            AddRow((null, ConditionText.Custom, $"NOT ({condition.Trim()})"), nameof(PreCheckAction.顯示訊息並停止), null, And);
    }

    private void AddRow((string Field, string Op, string Value) c, string action, string message, string join)
    {
        string field = c.Field;
        if (field != null && !colField.Items.Cast<string>().Contains(field, StringComparer.OrdinalIgnoreCase)) colField.Items.Add(field);
        field = colField.Items.Cast<string>().FirstOrDefault(i => i.Equals(field ?? "", StringComparison.OrdinalIgnoreCase)) ?? "";
        grid.Rows.Add(field, c.Op, c.Value, action ?? "", message, join);
    }

    /// <summary>這一列是一組的結尾（選了「成立時」或填了訊息）</summary>
    private static bool EndsGroup(DataGridViewRow row) =>
        !string.IsNullOrEmpty(row.Cells[ColAction].Value as string) || !string.IsNullOrWhiteSpace(row.Cells[ColMessage].Value as string);

    /// <summary>
    /// 每一組（到填了訊息的列為止）組成一條擋下規則；最後幾列沒填訊息時也算一組（跳預設訊息並停止）。
    /// 有一列沒設定好時 bad = 那一列
    /// </summary>
    private List<PreCheckBlock> BuildBlocks(out DataGridViewRow bad, List<string> display = null)
    {
        bad = null;
        var result = new List<PreCheckBlock>();
        var text = new System.Text.StringBuilder();
        var words = new System.Text.StringBuilder();     // 給人看的：零件工令編號 是空白 且 專案序號 是空白
        string join = null;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            string field = row.Cells[ColField].Value as string, op = row.Cells[ColOp].Value as string, value = row.Cells[ColValue].Value as string;
            string part = ConditionText.Build(field, op, value);
            if (part == null)
            {
                if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(value) && !EndsGroup(row)) continue;   // 整列空白：略過
                bad ??= row;
                continue;
            }
            if (text.Length > 0)
            {
                text.Append(join == Or ? " OR " : " AND ");
                words.Append(join == Or ? " 或 " : " 且 ");
            }
            text.Append(part);
            words.Append(op == ConditionText.Custom ? $"（{value?.Trim()}）"
                         : $"【{field?.Trim()} {op}{(op is "是空白" or "不是空白" ? "" : " " + value?.Trim())}】");
            join = row.Cells[ColJoin].Value as string;
            if (!EndsGroup(row)) continue;
            string message = (row.Cells[ColMessage].Value as string)?.Trim();
            result.Add(new PreCheckBlock
            {
                Condition = text.ToString(),
                Action = Enum.TryParse<PreCheckAction>(row.Cells[ColAction].Value as string, out var a) ? a : PreCheckAction.顯示訊息並停止,
                Message = string.IsNullOrEmpty(message) ? null : message,
            });
            display?.Add(words.ToString());
            text.Clear();
            words.Clear();
            join = null;
        }
        if (text.Length > 0)
        {
            result.Add(new PreCheckBlock { Condition = text.ToString(), Action = PreCheckAction.顯示訊息並停止 });
            display?.Add(words.ToString());
        }
        return result;
    }

    /// <summary>下方用一般的話列出判斷順序：1. 如果 … → 跳「…」並停止</summary>
    private void UpdatePreview()
    {
        var words = new List<string>();
        var blocks = BuildBlocks(out _, words);
        if (blocks.Count == 0)
        {
            txtPreview.Text = "（沒有條件 = 不檢查，直接執行）";
            return;
        }
        var lines = blocks.Select((x, i) =>
        {
            string msg = x.Message ?? (string.IsNullOrWhiteSpace(txtMessage.Text) ? "（預設訊息）" : txtMessage.Text.Trim());
            string act = x.Action == PreCheckAction.詢問是否繼續 ? $"跳「{msg}」詢問是否繼續" : $"跳「{msg}」並停止";
            return $"{i + 1}. {(i == 0 ? "如果" : "否則如果")} {words[i]} → {act}";
        }).Append($"{blocks.Count + 1}. 都不成立 → 執行");
        txtPreview.Text = string.Join(Environment.NewLine, lines);
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
        RefreshAll();
        UpdatePreview();
    }

    /// <summary>「自訂條件」不用選欄位、「是空白／不是空白」不用填值、一組結尾的「且 / 或」用不到：底色灰掉（文字照樣看得到）</summary>
    private void RefreshAll()
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            string op = row.Cells[ColOp].Value as string;
            row.Cells[ColField].Style.BackColor = op == ConditionText.Custom ? SystemColors.Control : Color.Empty;
            row.Cells[ColValue].Style.BackColor = op is "是空白" or "不是空白" ? SystemColors.Control : Color.Empty;
            row.Cells[ColJoin].Style.BackColor = EndsGroup(row) ? SystemColors.Control : Color.Empty;
        }
    }
}
