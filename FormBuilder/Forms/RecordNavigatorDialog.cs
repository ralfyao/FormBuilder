using System.Data;

namespace FormBuilder;

/// <summary>單筆編輯視窗要用到的操作，由執行畫面提供（資料、主鍵、存檔都在執行畫面那邊）</summary>
public interface IRecordNavigator
{
    /// <summary>單頭資料表（含 Schema），自動編號要到這張表查最大流水號</summary>
    string FullTableName { get; }
    int Count { get; }
    int Current { get; }
    /// <summary>目前這筆；沒有資料時是 null</summary>
    DataRow CurrentRow { get; }
    /// <summary>有主鍵才能修改、刪除</summary>
    bool CanModify { get; }
    /// <summary>有新增、刪除、列印權限（登入時依角色權限）</summary>
    bool CanInsert { get; }
    bool CanDelete { get; }
    bool CanPrint { get; }
    /// <summary>有核准權限（生效 / 取消生效）</summary>
    bool CanApprove { get; }
    /// <summary>使用者對這個畫面的權限（按鈕「需要的權限」用）</summary>
    FormPermission Permission { get; }
    void MoveTo(int index);
    /// <summary>重讀目前這筆；已被刪除時回傳 false</summary>
    bool Refresh();
    bool Insert(Dictionary<string, object> values);
    bool Update(Dictionary<string, object> values);
    /// <summary>只改目前這筆的一個欄位（生效 / 取消生效），成功時顯示 doneMessage</summary>
    bool UpdateColumn(string column, object value, string doneMessage);
    bool Delete();
}

/// <summary>
/// 雙擊單頭清單開啟的單筆編輯視窗（類似 4GL 的 i 類單檔維護畫面）：
/// 新增、修改、刪除、首筆～末筆都在這裡，跟主畫面的清單同步
/// </summary>
public partial class RecordNavigatorDialog : Form
{
    private enum EditMode { Browse, Insert, Modify }

    private readonly IRecordNavigator nav;
    private readonly string baseTitle;
    private readonly List<FieldDefinition> fields;
    private readonly Dictionary<FieldDefinition, Control> inputs;
    /// <summary>輸入框的游標順序（畫面位置由上而下、由左而右）</summary>
    private readonly List<Control> focusOrder;
    private EditMode mode = EditMode.Browse;

    /// <summary>後台在編輯視窗版面放的按鈕</summary>
    private readonly List<(ButtonDefinition Def, Button Control)> customButtons = new();
    /// <summary>後台在編輯視窗版面放的明細清單</summary>
    private readonly List<EditListView> listViews = new();
    private readonly string connectionString;
    private readonly string sourcePath;
    /// <summary>生效欄位；null = 不使用生效 / 取消生效</summary>
    private readonly string effectiveColumn;

    /// <param name="bodySize">後台設計的欄位區大小；null = 依欄位自動決定</param>
    public RecordNavigatorDialog(IRecordNavigator navigator, string title, List<FieldDefinition> editFields, Size? bodySize = null,
                                 string connectionString = null, List<ButtonDefinition> buttons = null, string sourcePath = null,
                                 string effectiveColumn = null, List<EditListDefinition> lists = null, Color backColor = default,
                                 Font font = null, List<FieldDefinition> textLabels = null)
    {
        InitializeComponent();
        body.BackColor = ColorText.BackOrDefault(backColor);               // 後台設定的編輯視窗底色（沒設定 = 預設灰色）
        if (font != null) body.Font = font;                                // 畫面設定的字型、大小
        nav = navigator;
        this.connectionString = connectionString;
        this.sourcePath = sourcePath;
        this.effectiveColumn = string.IsNullOrWhiteSpace(effectiveColumn) ? null : effectiveColumn;
        baseTitle = title;
        fields = editFields;

        // 工具列（新增、修改、刪除、首筆～末筆）與下方按鈕在 Designer.cs
        btnInsert.Click += (s, e) => BeginInsert();
        btnModify.Click += (s, e) => BeginModify();
        btnDelete.Click += (s, e) => DeleteRecord();
        btnFirst.Click += (s, e) => MoveTo(0);
        btnPrevious.Click += (s, e) => MoveTo(nav.Current - 1);
        btnNext.Click += (s, e) => MoveTo(nav.Current + 1);
        btnLast.Click += (s, e) => MoveTo(nav.Count - 1);
        btnOk.Click += (s, e) => Accept();
        btnCancel.Click += (s, e) => CancelEdit();
        btnClose.Click += (s, e) => Close();
        btnEffective.Click += (s, e) => SetEffective(true);
        btnUneffective.Click += (s, e) => SetEffective(false);
        btnPrint.Click += (s, e) => PrintRecord();

        // 欄位照後台「編輯視窗版面」設計的位置擺放（依定義動態產生到 body）
        // 文字標籤也放上去（inputs 裡有，fields 裡沒有：不讀資料、不存檔，只讓執行後動作填值）
        inputs = InputHelper.LayoutFields(body, fields.Concat(textLabels ?? new List<FieldDefinition>()), connectionString);     // 連線字串給下拉選單的選項 SQL 用
        focusOrder = InputHelper.ApplyFocusOrder(inputs);    // Tab / Enter 照畫面位置由左而右、由上而下
        // 欄位的開窗選取（跟「開啟畫面」按鈕一樣開啟、帶回）；瀏覽時不開
        ButtonRunner.AttachPickers(inputs, b => { if (mode != EditMode.Browse) RunButton(b); });
        // 下拉選單的執行後動作：@名稱 先找視窗上的欄位，沒有就用目前這筆資料；查出來的值填回視窗上的欄位
        ButtonRunner.AttachAfterSelect(inputs, connectionString, ValueOf);
        InputHelper.AttachDependentItems(inputs, connectionString, ValueOf);   // 選項 SQL 用到 @視窗欄位的下拉選單：欄位改了就重查選項
        foreach (var b in buttons ?? new List<ButtonDefinition>())
        {
            var button = ButtonRunner.CreateControl(b);
            button.Click += (s, e) => RunButton(b);
            customButtons.Add((b, button));
            body.Controls.Add(button);
            ButtonRunner.ApplyFont(b, button);              // 按鈕自己設的字型、大小
        }
        // 後台在編輯視窗版面放的明細清單（唯讀，顯示目前這筆的明細）
        foreach (var l in lists ?? new List<EditListDefinition>())
        {
            var view = new EditListView(l, connectionString);
            listViews.Add(view);
            body.Controls.Add(view.Host);                   // 圓角外框
        }
        EditListView.Connect(listViews);                    // 有設上層清單的：點上層清單的一列才列出明細
        foreach (var view in listViews) view.HookOpenForm(sourcePath, this, ValueOf, connectionString);   // 雙擊一列開啟畫面
        var content = bodySize ?? InputHelper.ContentSize(fields, 560);
        var buttonExtent = ButtonRunner.Extent(buttons ?? new List<ButtonDefinition>());
        var listExtent = EditListView.Extent(lists ?? new List<EditListDefinition>());
        content = new Size(Math.Max(content.Width, Math.Max(buttonExtent.Width, listExtent.Width)),
                           Math.Max(content.Height, Math.Max(buttonExtent.Height, listExtent.Height)));

        // 視窗大小照版面；超過螢幕可用範圍時縮到螢幕內，欄位區可以捲動
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int chromeHeight = toolStrip.PreferredSize.Height + bottom.Height;
        // 視窗至少要放得下整條工具列，「列印」等按鈕才不會被擠到溢位選單（生效 / 取消生效同時只顯示一個）
        btnEffective.Visible = this.effectiveColumn != null;
        btnUneffective.Visible = false;
        int toolbarWidth = toolStrip.PreferredSize.Width + (btnUneffective.Width - btnEffective.Width) + 8;
        ClientSize = new Size(Math.Min(Math.Max(Math.Max(content.Width, 560), toolbarWidth), screen.Width - 40),
                              Math.Min(content.Height + chromeHeight, screen.Height - 80));

        ShowRecord();
        SetMode(EditMode.Browse);
    }

    /// <summary>Esc：輸入中 = 取消，瀏覽時 = 關閉視窗；輸入中 Enter 跳到下一個輸入框，最後一個輸入框再按 = 確定</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (mode == EditMode.Browse) Close();
            else CancelEdit();
            return true;
        }
        if (keyData == Keys.Enter && mode != EditMode.Browse && InputHelper.FocusNext(focusOrder, out _)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>輸入到一半關掉視窗時先確認，避免改了沒存</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (mode != EditMode.Browse &&
            MessageBox.Show("還沒按「確定」存檔，確定要放棄這次的輸入嗎？", "提醒", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    // ================= 顯示與狀態 =================

    private void ShowRecord()
    {
        DataRow row = nav.CurrentRow;
        foreach (var f in fields.Where(f => !f.QueryOnly))                    // 畫面欄位不從資料帶值
            InputHelper.SetValue(inputs[f], row != null && row.Table.Columns.Contains(f.Column) ? row[f.Column] : null);
        ButtonRunner.RefreshTextLabels(inputs, connectionString, ValueOf);     // 文字標籤用欄位的執行後動作帶出來
        if (row != null) ButtonRunner.RunAfterShow(inputs, connectionString, ValueOf);
        ReloadLists(row);
    }

    /// <summary>SQL 的 @名稱：先找視窗上的欄位，沒有就用目前這筆資料</summary>
    private object ValueOf(string name)
    {
        var f = inputs.Keys.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));   // 含文字標籤
        if (f != null) return InputHelper.GetValue(f, inputs[f]);
        DataRow row = nav.CurrentRow;
        return row != null && row.Table.Columns.Contains(name) ? row[name] : null;
    }

    /// <summary>依目前這筆的欄位值重新載入明細清單；row = null（沒有資料、新增中）時清單是空的</summary>
    private void ReloadLists(DataRow row)
    {
        foreach (var view in listViews)
            view.Reload(col => row != null && row.Table.Columns.Contains(col) ? row[col] : null);
    }

    private void SetMode(EditMode newMode)
    {
        mode = newMode;
        bool browsing = mode == EditMode.Browse;
        bool hasRow = nav.CurrentRow != null;
        btnInsert.Enabled = browsing && nav.CanInsert;
        btnModify.Enabled = browsing && hasRow && nav.CanModify;
        btnDelete.Enabled = browsing && hasRow && nav.CanDelete;
        btnFirst.Enabled = btnPrevious.Enabled = browsing && nav.Current > 0;
        btnNext.Enabled = btnLast.Enabled = browsing && hasRow && nav.Current < nav.Count - 1;
        btnOk.Enabled = btnCancel.Enabled = !browsing;
        btnClose.Enabled = browsing;
        // 生效的資料顯示「取消生效」，未生效的顯示「生效」；列印要生效才能按（沒指定生效欄位時有資料就能印）
        bool effective = hasRow && IsEffective(nav.CurrentRow);
        btnEffective.Visible = effectiveColumn != null && !effective;
        btnUneffective.Visible = effectiveColumn != null && effective;
        btnEffective.Enabled = btnUneffective.Enabled = browsing && hasRow && nav.CanApprove;
        btnPrint.Enabled = browsing && hasRow && nav.CanPrint && (effectiveColumn == null || effective);
        AcceptButton = browsing ? null : btnOk;          // 輸入中按 Enter = 確定

        foreach (var f in fields)
        {
            // 新增：只顯示 / 自動編號以外都能輸入；修改：主鍵也不能改
            bool editable = mode switch
            {
                EditMode.Insert => !f.NoEntry && !f.IsReadOnly,
                EditMode.Modify => !f.NoEntry && !f.IsKey && !f.IsReadOnly,
                _ => false,
            };
            if (inputs[f] is TextBox tb) tb.ReadOnly = !editable;
            else inputs[f].Enabled = editable;
        }

        Text = mode switch
        {
            EditMode.Insert => $"新增：{baseTitle}",
            EditMode.Modify => $"修改：{baseTitle}",
            _ => baseTitle,
        };
        lblPosition.Text = mode != EditMode.Browse ? "輸入完按「確定」存檔，Esc 取消"
                         : hasRow ? $"第 {nav.Current + 1} / {nav.Count} 筆"
                         : "沒有資料，可以按「新增」";
        // 選取視窗按鈕（有回傳對應）要在新增、修改時才能按，帶回的值填進正在輸入的欄位
        foreach (var (b, button) in customButtons)
            button.Enabled = ButtonRunner.Allowed(b, nav.Permission)       // 按鈕「需要的權限」：沒有就一直是灰的
                && (ButtonRunner.IsPicker(b) ? mode is EditMode.Insert or EditMode.Modify : IsActionEnabled(b.Action));
        if (!browsing) InputHelper.FocusFirst(focusOrder);                 // 游標放在第一個可以輸入的欄位（照畫面位置）
    }

    // ================= 後台設計的按鈕 =================

    /// <summary>編輯視窗裡能用的動作；查詢、單身是主畫面的功能，這裡不能按</summary>
    private bool IsActionEnabled(ButtonAction action) => action switch
    {
        ButtonAction.新增 => btnInsert.Enabled,
        ButtonAction.修改 => btnModify.Enabled,
        ButtonAction.刪除 => btnDelete.Enabled,
        ButtonAction.首筆 => btnFirst.Enabled,
        ButtonAction.上筆 => btnPrevious.Enabled,
        ButtonAction.下筆 => btnNext.Enabled,
        ButtonAction.末筆 => btnLast.Enabled,
        ButtonAction.確定 => btnOk.Enabled,
        ButtonAction.取消 => btnCancel.Enabled,
        ButtonAction.離開 => btnClose.Enabled,
        ButtonAction.執行SQL => mode is EditMode.Insert or EditMode.Modify && nav.Permission.Modify,   // 執行SQL 只在新增、修改中可以按
        ButtonAction.開啟畫面 => mode == EditMode.Browse,
        ButtonAction.列印報表 => mode == EditMode.Browse && nav.CurrentRow != null && nav.CanPrint,
        _ => false,
    };

    private void RunButton(ButtonDefinition b)
    {
        if (!ButtonRunner.CheckAllowed(b, nav.Permission)) return;
        if (!ButtonRunner.Confirm(b)) return;
        switch (b.Action)
        {
            case ButtonAction.新增: BeginInsert(); break;
            case ButtonAction.修改: BeginModify(); break;
            case ButtonAction.刪除: DeleteRecord(); break;
            case ButtonAction.首筆: MoveTo(0); break;
            case ButtonAction.上筆: MoveTo(nav.Current - 1); break;
            case ButtonAction.下筆: MoveTo(nav.Current + 1); break;
            case ButtonAction.末筆: MoveTo(nav.Count - 1); break;
            case ButtonAction.確定: Accept(); break;
            case ButtonAction.取消: CancelEdit(); break;
            case ButtonAction.離開: Close(); break;
            case ButtonAction.執行SQL:
            {
                // @欄位名稱：有來源清單時先取清單選取那一列，再找視窗上的欄位，沒有就用目前這筆資料；SQL 查出來的值等重讀完再填進視窗上的欄位
                var sqlValueOf = ButtonRunner.WithSourceList(b, listViews, ValueOf);
                if (sqlValueOf == null) break;
                IReadOnlyDictionary<string, object> result = null;
                if (ButtonRunner.RunSql(b, connectionString, sqlValueOf, onResult: values => result = values) && nav.CurrentRow != null && mode == EditMode.Browse)   // 新增、修改中不重讀，才不會蓋掉正在輸入的內容
                {
                    nav.Refresh();                         // 資料可能被 SQL 改過，重讀
                    ShowRecord();
                    SetMode(EditMode.Browse);
                }
                if (result != null) ButtonRunner.FillFields(result, inputs, reportMissing: !string.IsNullOrWhiteSpace(b.SqlReturn));
                break;
            }
            case ButtonAction.列印報表:
            {
                // 欄位值先找目前這筆資料，沒有就用視窗上的欄位
                DataRow current = nav.CurrentRow;
                Func<string, object> currentValue = name => current != null && current.Table.Columns.Contains(name) ? current[name]
                    : fields.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } f ? InputHelper.GetValue(f, inputs[f]) : null;
                // 有設定「列印來源清單」：依下方清單選取的資料列印
                if (ButtonRunner.PrintFromList(b, listViews, sourcePath, connectionString, currentValue, this)) break;
                ButtonRunner.Print(b, sourcePath, connectionString, new[] { currentValue }, this);
                break;
            }
            case ButtonAction.開啟畫面:
            {
                // 傳入參數的 @欄位：有來源清單時先取清單選取那一列，再找視窗上的欄位，沒有就用目前這筆資料；選取視窗帶回的值填進欄位
                var openValueOf = ButtonRunner.WithSourceList(b, listViews, ValueOf);
                if (openValueOf == null) break;
                ButtonRunner.OpenForm(b, sourcePath, this, openValueOf, values => ButtonRunner.FillFields(values, inputs), connectionString: connectionString);
                break;
            }
        }
    }

    // ================= 生效 / 取消生效 / 列印 =================

    private bool IsEffective(DataRow row) =>
        effectiveColumn != null && row.Table.Columns.Contains(effectiveColumn) && GridDefinition.IsEffectiveValue(row[effectiveColumn]);

    /// <summary>生效 → 生效欄位改成 1；取消生效 → 改成 0</summary>
    private void SetEffective(bool effective)
    {
        if (effectiveColumn == null || nav.CurrentRow == null) return;
        if (nav.UpdateColumn(effectiveColumn, effective ? 1 : 0, effective ? "已生效。" : "已取消生效。"))
        {
            ShowRecord();
            SetMode(EditMode.Browse);
        }
    }

    /// <summary>列印目前這筆：視窗上每個欄位印成「標籤：值」</summary>
    private void PrintRecord()
    {
        DataRow row = nav.CurrentRow;
        if (row == null) return;
        var lines = fields.Select(f => (f.Label, Value: inputs[f] switch
        {
            CheckBox cb => cb.Checked ? "是" : "否",
            Control c => c.Text,
        })).ToList();

        using var doc = new System.Drawing.Printing.PrintDocument { DocumentName = baseTitle };
        int next = 0;
        doc.BeginPrint += (s, e) => next = 0;
        doc.PrintPage += (s, e) =>
        {
            using var titleFont = new Font(Font.FontFamily, 14, FontStyle.Bold);
            using var font = new Font(Font.FontFamily, 10);
            var area = e.MarginBounds;
            float y = area.Top;
            e.Graphics.DrawString(baseTitle, titleFont, Brushes.Black, area.Left, y);
            y += titleFont.GetHeight(e.Graphics) * 1.8f;
            float labelWidth = lines.Count == 0 ? 0 : lines.Max(l => e.Graphics.MeasureString(l.Label, font).Width) + 16;
            float lineHeight = font.GetHeight(e.Graphics) * 1.5f;
            while (next < lines.Count && y + lineHeight <= area.Bottom)
            {
                e.Graphics.DrawString(lines[next].Label, font, Brushes.Black, area.Left, y);
                e.Graphics.DrawString(lines[next].Value, font, Brushes.Black,
                                      new RectangleF(area.Left + labelWidth, y, area.Width - labelWidth, lineHeight));
                y += lineHeight;
                next++;
            }
            e.HasMorePages = next < lines.Count;
        };
        // 預覽列印要有印表機才能排版：沒有預設印表機時先讓使用者挑一台
        if (!doc.PrinterSettings.IsValid)
        {
            using var pick = new PrintDialog { Document = doc, UseEXDialog = true };
            if (pick.ShowDialog(this) != DialogResult.OK) return;
        }
        try
        {
            using var preview = new PrintPreviewDialog { Document = doc, Width = 900, Height = 700, Text = "列印：" + baseTitle };
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("列印失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ================= 按鈕動作 =================

    private void MoveTo(int index)
    {
        if (index < 0 || index >= nav.Count) return;
        nav.MoveTo(index);
        ShowRecord();
        SetMode(EditMode.Browse);
    }

    private void BeginInsert()
    {
        foreach (var f in fields) InputHelper.SetValue(inputs[f], null);
        foreach (var f in inputs.Keys.Where(f => f.IsTextLabel)) InputHelper.SetValue(inputs[f], f.Label);   // 文字標籤恢復原本的文字
        ReloadLists(null);
        DefaultValue.FillInputs(inputs, connectionString);                       // 欄位的預設值（可以是 SQL）
        NumberRule.FillInputs(inputs, connectionString, nav.FullTableName);     // 有編號規則的欄位自動帶入下一個編號
        SetMode(EditMode.Insert);
    }

    /// <summary>修改前先重讀這筆，確保改的是資料庫裡最新的內容</summary>
    private void BeginModify()
    {
        bool exists = nav.Refresh();
        ShowRecord();
        SetMode(exists ? EditMode.Modify : EditMode.Browse);
    }

    private void DeleteRecord()
    {
        if (!nav.Delete()) return;
        ShowRecord();
        SetMode(EditMode.Browse);
    }

    private void CancelEdit()
    {
        ShowRecord();
        SetMode(EditMode.Browse);
    }

    private void Accept()
    {
        var values = new Dictionary<string, object>();
        var editable = fields.Where(f => !f.NoEntry && !f.QueryOnly && (mode == EditMode.Insert || !f.IsKey)).ToList();     // 畫面欄位不寫回資料庫
        try
        {
            foreach (var f in editable)
                values[f.Column] = InputHelper.GetValue(f, inputs[f]);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "提醒");
            return;
        }
        var empty = editable.Where(f => (!f.IsNullable && values[f.Column] == DBNull.Value)
                                     || (f.IsKey && values[f.Column] is string s && s.Trim() == ""))
                            .Select(f => f.Label).ToList();
        if (empty.Count > 0)
        {
            MessageBox.Show("請輸入：" + string.Join("、", empty), "提醒");
            return;
        }

        bool ok = mode == EditMode.Insert ? nav.Insert(values) : nav.Update(values);
        if (!ok) return;                                   // 失敗時留在輸入狀態，資料還在，可以修正後再按確定
        mode = EditMode.Browse;                            // 先切回瀏覽，ShowRecord 才不會被當成放棄輸入
        ShowRecord();
        SetMode(EditMode.Browse);
    }
}
