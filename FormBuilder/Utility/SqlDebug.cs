using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;

namespace FormBuilder;

/// <summary>
/// 預覽執行（Debug）：開著的期間，每個 SQL 執行前記下 SQL 與參數值；
/// 資料庫回報錯誤時跳出「SQL 錯誤（Debug）」視窗，顯示錯誤訊息、出錯的 SQL 與參數（可以複製），
/// 原本畫面上的錯誤訊息照常顯示
/// </summary>
public static class SqlDebug
{
    private static int sessions;
    private static WindowsFormsSynchronizationContext ui;
    private static string lastCommand;
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

    /// <summary>結束一個 Debug 預覽；全部結束後恢復一般執行</summary>
    public static void End()
    {
        if (sessions > 0 && --sessions == 0)
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance;
            lastCommand = null;
        }
    }

    /// <summary>Debug 預覽的畫面：標題加上【Debug】，關閉時結束這個 Debug 預覽</summary>
    public static void Attach(Form form)
    {
        form.Text = "【Debug】" + form.Text;
        form.FormClosed += (s, e) => End();
    }

    /// <summary>命令執行前記下 SQL 與參數（錯誤發生時顯示這一個）</summary>
    public static void Executing(DbCommand cmd)
    {
        if (Enabled) lastCommand = Describe(cmd);
    }

    /// <summary>SQL 與參數值的文字（參數依名稱列出值與型別）</summary>
    public static string Describe(DbCommand cmd)
    {
        var sb = new StringBuilder();
        sb.AppendLine(cmd.CommandText?.Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
        if (cmd.Parameters.Count > 0)
        {
            sb.AppendLine();
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

    /// <summary>資料庫錯誤（DbException）一丟出來就記下，等目前的動作結束後在畫面上顯示</summary>
    private static void OnFirstChance(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is not DbException ex) return;
        lock (reported)
        {
            if (reported.TryGetValue(ex, out _)) return;          // 同一個錯誤往外丟好幾層時只顯示一次
            reported.Add(ex, null);
        }
        string sql = lastCommand ?? "（沒有記到 SQL）";
        string message = ex.Message;
        // 等目前的動作跑完再顯示：排到開著的視窗的訊息佇列（錯誤是在畫面的執行緒上丟出的）
        var owner = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.IsHandleCreated && !f.IsDisposed);
        if (owner != null) owner.BeginInvoke(() => ShowError(message, sql));
        else ui?.Post(_ => ShowError(message, sql), null);
    }

    private static void ShowError(string message, string sql)
    {
        var text = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F), BackColor = SystemColors.Window,
            Text = "錯誤訊息：" + Environment.NewLine + message + Environment.NewLine + Environment.NewLine
                 + "出錯的 SQL：" + Environment.NewLine + sql,
        };
        var copy = new Button { Text = "複製", AutoSize = true };
        var close = new Button { Text = "關閉", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(4) };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        var dlg = new Form
        {
            Text = "SQL 錯誤（Debug）", Size = new Size(760, 460), StartPosition = FormStartPosition.CenterScreen,
            Font = new Font("Microsoft JhengHei UI", 9F), CancelButton = close, ShowInTaskbar = false, TopMost = true,
        };
        dlg.Controls.Add(text);
        dlg.Controls.Add(buttons);
        copy.Click += (s, e) => Clipboard.SetText(text.Text);
        dlg.FormClosed += (s, e) => dlg.Dispose();
        dlg.Show();                                               // 不擋住原本的錯誤訊息，兩個都看得到
        text.Select(0, 0);
    }
}

/// <summary>Debug 預覽用：包住真正的命令，執行前記下 SQL 與參數，其他都交給原本的命令</summary>
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
        return Inner.ExecuteReader(behavior);
    }

    public override int ExecuteNonQuery()
    {
        SqlDebug.Executing(Inner);
        return Inner.ExecuteNonQuery();
    }

    public override object ExecuteScalar()
    {
        SqlDebug.Executing(Inner);
        return Inner.ExecuteScalar();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Inner.Dispose();
        base.Dispose(disposing);
    }
}
