using System.Data;

namespace FormBuilder;

/// <summary>單筆編輯視窗要用到的操作，由執行畫面提供（資料、主鍵、存檔都在執行畫面那邊）</summary>
public interface IRecordNavigator
{
    int Count { get; }
    int Current { get; }
    /// <summary>目前這筆；沒有資料時是 null</summary>
    DataRow CurrentRow { get; }
    /// <summary>有主鍵才能修改、刪除</summary>
    bool CanModify { get; }
    void MoveTo(int index);
    /// <summary>重讀目前這筆；已被刪除時回傳 false</summary>
    bool Refresh();
    bool Insert(Dictionary<string, object> values);
    bool Update(Dictionary<string, object> values);
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
    private EditMode mode = EditMode.Browse;

    /// <summary>後台在編輯視窗版面放的按鈕</summary>
    private readonly List<(ButtonDefinition Def, Button Control)> customButtons = new();
    private readonly string connectionString;
    private readonly string sourcePath;

    /// <param name="bodySize">後台設計的欄位區大小；null = 依欄位自動決定</param>
    public RecordNavigatorDialog(IRecordNavigator navigator, string title, List<FieldDefinition> editFields, Size? bodySize = null,
                                 string connectionString = null, List<ButtonDefinition> buttons = null, string sourcePath = null)
    {
        InitializeComponent();
        nav = navigator;
        this.connectionString = connectionString;
        this.sourcePath = sourcePath;
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

        // 欄位照後台「編輯視窗版面」設計的位置擺放（依定義動態產生到 body）
        inputs = InputHelper.LayoutFields(body, fields, connectionString);     // 連線字串給下拉選單的選項 SQL 用
        foreach (var b in buttons ?? new List<ButtonDefinition>())
        {
            var button = ButtonRunner.CreateControl(b);
            button.Click += (s, e) => RunButton(b);
            customButtons.Add((b, button));
            body.Controls.Add(button);
        }
        var content = bodySize ?? InputHelper.ContentSize(fields, 560);
        var buttonExtent = ButtonRunner.Extent(buttons ?? new List<ButtonDefinition>());
        content = new Size(Math.Max(content.Width, buttonExtent.Width), Math.Max(content.Height, buttonExtent.Height));

        // 視窗大小照版面；超過螢幕可用範圍時縮到螢幕內，欄位區可以捲動
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int chromeHeight = toolStrip.PreferredSize.Height + bottom.Height;
        ClientSize = new Size(Math.Min(Math.Max(content.Width, 560), screen.Width - 40),
                              Math.Min(content.Height + chromeHeight, screen.Height - 80));

        ShowRecord();
        SetMode(EditMode.Browse);
    }

    /// <summary>Esc：輸入中 = 取消，瀏覽時 = 關閉視窗</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (mode == EditMode.Browse) Close();
            else CancelEdit();
            return true;
        }
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
        foreach (var f in fields)
            InputHelper.SetValue(inputs[f], row != null && row.Table.Columns.Contains(f.Column) ? row[f.Column] : null);
    }

    private void SetMode(EditMode newMode)
    {
        mode = newMode;
        bool browsing = mode == EditMode.Browse;
        bool hasRow = nav.CurrentRow != null;
        btnInsert.Enabled = browsing;
        btnModify.Enabled = btnDelete.Enabled = browsing && hasRow && nav.CanModify;
        btnFirst.Enabled = btnPrevious.Enabled = browsing && nav.Current > 0;
        btnNext.Enabled = btnLast.Enabled = browsing && hasRow && nav.Current < nav.Count - 1;
        btnOk.Enabled = btnCancel.Enabled = !browsing;
        btnClose.Enabled = browsing;
        AcceptButton = browsing ? null : btnOk;          // 輸入中按 Enter = 確定

        foreach (var f in fields)
        {
            // 新增：只顯示 / 自動編號以外都能輸入；修改：主鍵也不能改
            bool editable = mode switch
            {
                EditMode.Insert => !f.NoEntry,
                EditMode.Modify => !f.NoEntry && !f.IsKey,
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
        foreach (var (b, button) in customButtons) button.Enabled = IsActionEnabled(b.Action);
        if (!browsing) inputs.Values.FirstOrDefault(c => c is TextBox { ReadOnly: false } || c is not TextBox && c.Enabled)?.Focus();
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
        ButtonAction.執行SQL or ButtonAction.開啟畫面 => mode == EditMode.Browse,
        _ => false,
    };

    private void RunButton(ButtonDefinition b)
    {
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
                // @欄位名稱：先找視窗上的欄位，沒有就用目前這筆資料
                if (ButtonRunner.RunSql(b, connectionString, name =>
                    {
                        var f = fields.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (f != null) return InputHelper.GetValue(f, inputs[f]);
                        DataRow row = nav.CurrentRow;
                        return row != null && row.Table.Columns.Contains(name) ? row[name] : null;
                    }) && nav.CurrentRow != null)
                {
                    nav.Refresh();                         // 資料可能被 SQL 改過，重讀
                    ShowRecord();
                    SetMode(EditMode.Browse);
                }
                break;
            case ButtonAction.開啟畫面: ButtonRunner.OpenForm(b, sourcePath, this); break;
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
        var editable = fields.Where(f => !f.NoEntry && (mode == EditMode.Insert || !f.IsKey)).ToList();
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
