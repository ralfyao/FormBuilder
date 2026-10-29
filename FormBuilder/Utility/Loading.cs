namespace FormBuilder;

/// <summary>
/// 查詢中的 Loading 畫面：工作放到背景執行，超過一下子（0.3 秒）還沒好才顯示「查詢中…」視窗（跑馬燈 + 經過秒數 + 取消），
/// 畫面不會卡住；快的查詢不會閃一下。視窗是 modal，查詢中不能點原本的畫面。工作丟出的例外照原樣丟回來
/// </summary>
public static class Loading
{
    private const int ShowAfterMs = 300;

    /// <param name="cancel">按「取消」時呼叫（例如 DbCommand.Cancel）；null = 沒有取消按鈕</param>
    public static T Run<T>(IWin32Window owner, string message, Func<T> work, Action cancel = null)
    {
        var task = Task.Run(work);
        if (!task.Wait(ShowAfterMs))
        {
            using var dlg = new LoadingDialog(message, task, cancel);
            dlg.ShowDialog(owner);
        }
        try
        {
            return task.GetAwaiter().GetResult();        // 例外照原樣丟出（不包成 AggregateException）
        }
        catch (Exception) when (task.IsFaulted && Canceled)
        {
            throw new OperationCanceledException("已取消查詢。");
        }
        finally
        {
            Canceled = false;
        }
    }

    /// <summary>使用者按了取消（查詢被中斷時丟 OperationCanceledException，呼叫端不用當錯誤顯示）</summary>
    [ThreadStatic] private static bool Canceled;

    private sealed class LoadingDialog : Form
    {
        public LoadingDialog(string message, Task task, Action cancel)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, cancel == null ? 86 : 122);
            Text = "請稍候";
            var label = new Label { Text = message, Location = new Point(16, 14), AutoSize = true };
            var bar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Location = new Point(16, 40), Size = new Size(268, 18) };
            var elapsed = new Label { Location = new Point(16, 62), AutoSize = true, ForeColor = SystemColors.GrayText };
            Controls.AddRange(new Control[] { label, bar, elapsed });
            if (cancel != null)
            {
                var btnCancel = new Button { Text = "取消", Location = new Point(208, 84), Size = new Size(76, 28) };
                btnCancel.Click += (s, e) =>
                {
                    btnCancel.Enabled = false;
                    label.Text = "取消中…";
                    Canceled = true;
                    try { cancel(); } catch { /* 已經結束或不支援取消：等它自己結束 */ }
                };
                Controls.Add(btnCancel);
            }
            var started = DateTime.Now.AddMilliseconds(-ShowAfterMs);
            var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (s, e) =>
            {
                elapsed.Text = $"已經過 {(DateTime.Now - started).TotalSeconds:0} 秒";
                if (task.IsCompleted)
                {
                    timer.Stop();
                    Close();
                }
            };
            Shown += (s, e) => timer.Start();
            FormClosed += (s, e) => timer.Dispose();
        }
    }
}
