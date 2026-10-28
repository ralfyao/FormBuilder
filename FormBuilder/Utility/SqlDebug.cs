using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;

namespace FormBuilder;

/// <summary>
/// 預覽執行（Debug）：開著的期間，每個 SQL 執行時都顯示在「SQL 紀錄（Debug）」視窗：SQL、參數值、執行結果（筆數）；
/// 資料庫回報錯誤時，那一筆後面標出紅色的錯誤並把視窗帶到最前面，原本畫面上的錯誤訊息照常顯示
/// </summary>
public static class SqlDebug
{
    private static int sessions;
    private static WindowsFormsSynchronizationContext ui;
    private static int count;
    private static RichTextBox logText;
    private static readonly ConditionalWeakTable<Exception, object> reported = new();

    /// <summary>有 Debug 預覽開著：DbDialect.CreateCommand 會把命令包成 DebugCommand</summary>
    public static bool Enabled => sessions > 0;

    /// <summary>開始一個 Debug 預覽（要在建立畫面之前呼叫，畫面一開啟時讀資料的 SQL 也記得到）</summary>
    public static void Begin()
    {
        if (sessions++ == 0)
        {
            ui = SynchronizationContext.Current as WindowsFormsSynchronizationContext;
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
        }
    }

    /// <summary>結束一個 Debug 預覽；全部結束後恢復一般執行（紀錄視窗留著，看完自己關）</summary>
    public static void End()
    {
        if (sessions > 0 && --sessions == 0)
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance;
    }

    /// <summary>Debug 預覽的畫面：標題加上【Debug】，關閉時結束這個 Debug 預覽</summary>
    public static void Attach(Form form)
    {
        form.Text = "【Debug】" + form.Text;
        form.FormClosed += (s, e) => End();
    }

    /// <summary>命令執行前：在紀錄視窗加一筆 SQL 與參數</summary>
    public static void Executing(DbCommand cmd)
    {
        if (!Enabled) return;
        count++;
        Write($"#{count}　{DateTime.Now:HH:mm:ss}" + Environment.NewLine, Color.Gray, bold: false);
        Write(Describe(cmd), SystemColors.WindowText, bold: false);
    }

    /// <summary>命令執行完：在剛才那一筆後面記下結果（例如「查到 3 筆」「影響 1 筆」）</summary>
    public static void Executed(string result)
    {
        if (Enabled) Write("→ " + result + Environment.NewLine + Environment.NewLine, Color.DarkGreen, bold: true);
    }

    /// <summary>SQL 與參數值的文字（參數依名稱列出值與型別）</summary>
    public static string Describe(DbCommand cmd)
    {
        var sb = new StringBuilder();
        sb.AppendLine(cmd.CommandText?.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
        if (cmd.Parameters.Count > 0)
        {
            sb.AppendLine("參數：");
            foreach (DbParameter p in cmd.Parameters)
            {
                string value = p.Value == null || p.Value == DBNull.Value ? "NULL"
                             : p.Value is string s ? $"'{s.Replace("'", "''")}'"
                             : p.Value is DateTime d ? $"'{d:yyyy-MM-dd HH:mm:ss}'"
                             : Convert.ToString(p.Value);
                string type = p.Value == null || p.Value == DBNull.Value ? "" : $"（{p.Value.GetType().Name}）";
                sb.AppendLine($"  {p.ParameterName} = {value}{type}{(p.Direction == ParameterDirection.Input ? "" : "  [輸出]")}");
            }
        }
        return sb.ToString();
    }

    /// <summary>資料庫錯誤（DbException）一丟出來就記在剛才那一筆 SQL 後面，並把紀錄視窗帶到最前面</summary>
    private static void OnFirstChance(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is not DbException ex) return;
        lock (reported)
        {
            if (reported.TryGetValue(ex, out _)) return;          // 同一個錯誤往外丟好幾層時只記一次
            reported.Add(ex, null);
        }
        Write("✖ 錯誤：" + ex.Message + Environment.NewLine + Environment.NewLine, Color.Red, bold: true);
        RunOnUi(() => logText?.FindForm()?.Activate());
    }

    /// <summary>在紀錄視窗最後面加一段文字（視窗還沒開就先開）</summary>
    private static void Write(string text, Color color, bool bold) => RunOnUi(() =>
    {
        var box = LogBox();
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = color;
        box.SelectionFont = new Font(box.Font, bold ? FontStyle.Bold : FontStyle.Regular);
        box.AppendText(text);
        box.SelectionStart = box.TextLength;
        box.ScrollToCaret();
    });

    /// <summary>不在畫面的執行緒上時，排到畫面的執行緒執行</summary>
    private static void RunOnUi(Action action)
    {
        Control owner = logText != null && !logText.IsDisposed ? logText
                      : Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.IsHandleCreated && !f.IsDisposed);
        if (owner == null || !owner.InvokeRequired) action();
        else if (owner.IsHandleCreated) owner.BeginInvoke(action);
        else ui?.Post(_ => action(), null);
    }

    /// <summary>「SQL 紀錄（Debug）」視窗：放在螢幕右邊、在最上層，可以複製、清除</summary>
    private static RichTextBox LogBox()
    {
        if (logText != null && !logText.IsDisposed) return logText;
        var text = new RichTextBox
        {
            ReadOnly = true, WordWrap = false, Dock = DockStyle.Fill, DetectUrls = false,
            Font = new Font("Consolas", 10F), BackColor = SystemColors.Window, ScrollBars = RichTextBoxScrollBars.Both,
        };
        var close = new Button { Text = "關閉", AutoSize = true };
        var copy = new Button { Text = "複製全部", AutoSize = true };
        var clear = new Button { Text = "清除", AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(4) };
        buttons.Controls.AddRange(new Control[] { close, copy, clear });
        var area = Screen.PrimaryScreen.WorkingArea;
        var dlg = new Form
        {
            Text = "SQL 紀錄（Debug）", StartPosition = FormStartPosition.Manual,
            Size = new Size(Math.Min(640, area.Width / 2), area.Height - 40),
            Font = new Font("Microsoft JhengHei UI", 9F), ShowInTaskbar = false, TopMost = true,
        };
        dlg.Location = new Point(area.Right - dlg.Width - 10, area.Top + 20);
        dlg.Controls.Add(text);
        dlg.Controls.Add(buttons);
        copy.Click += (s, e) => { if (text.TextLength > 0) Clipboard.SetText(text.Text); };
        clear.Click += (s, e) => { text.Clear(); count = 0; };
        close.Click += (s, e) => dlg.Close();
        dlg.FormClosed += (s, e) => { logText = null; count = 0; dlg.Dispose(); };
        logText = text;
        dlg.Show();
        return text;
    }
}

/// <summary>Debug 預覽用：包住真正的命令，執行前後在 SQL 紀錄記下 SQL、參數與結果，其他都交給原本的命令</summary>
public sealed class DebugCommand : DbCommand
{
    public DbCommand Inner { get; }

    public DebugCommand(DbCommand inner) => Inner = inner;

    public override string CommandText { get => Inner.CommandText; set => Inner.CommandText = value; }
    public override int CommandTimeout { get => Inner.CommandTimeout; set => Inner.CommandTimeout = value; }
    public override CommandType CommandType { get => Inner.CommandType; set => Inner.CommandType = value; }
    public override bool DesignTimeVisible { get => Inner.DesignTimeVisible; set => Inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => Inner.UpdatedRowSource; set => Inner.UpdatedRowSource = value; }
    protected override DbConnection DbConnection { get => Inner.Connection; set => Inner.Connection = value; }
    protected override DbParameterCollection DbParameterCollection => Inner.Parameters;
    protected override DbTransaction DbTransaction { get => Inner.Transaction; set => Inner.Transaction = value; }

    public override void Cancel() => Inner.Cancel();
    public override void Prepare() => Inner.Prepare();
    protected override DbParameter CreateDbParameter() => Inner.CreateParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        SqlDebug.Executing(Inner);
        var reader = Inner.ExecuteReader(behavior);
        SqlDebug.Executed(reader.FieldCount > 0 ? "查詢（傳回資料由畫面讀取）" : $"影響 {reader.RecordsAffected} 筆");
        return reader;
    }

    public override int ExecuteNonQuery()
    {
        SqlDebug.Executing(Inner);
        int affected = Inner.ExecuteNonQuery();
        SqlDebug.Executed($"影響 {affected} 筆");
        return affected;
    }

    public override object ExecuteScalar()
    {
        SqlDebug.Executing(Inner);
        var value = Inner.ExecuteScalar();
        SqlDebug.Executed("傳回 " + (value == null || value == DBNull.Value ? "NULL" : Convert.ToString(value)));
        return value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Inner.Dispose();
        base.Dispose(disposing);
    }
}
