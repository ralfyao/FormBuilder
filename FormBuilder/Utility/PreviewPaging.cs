using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace FormBuilder;

/// <summary>
/// 預覽列印視窗的換頁：工具列原本的頁次數字框（按「上」才是下一頁，很容易搞反）換成「上一頁 / 第 x / n 頁 / 下一頁」；
/// 滑鼠滾輪往下 = 下一頁、往上 = 上一頁（放大看、頁面有捲軸時，捲到底再滾才換頁）；PageDown / PageUp 也可以換頁；
/// 按放大鏡直接打開縮放比例選單，Ctrl + 滾輪放大縮小
/// </summary>
public static class PreviewPaging
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SCROLLINFO
    {
        public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos;
    }

    [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr hwnd, int bar, ref SCROLLINFO info);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int GWL_STYLE = -16, WS_VSCROLL = 0x00200000, SB_VERT = 1, SIF_ALL = 0x17, WM_VSCROLL = 0x115, SB_LINEUP = 0, SB_LINEDOWN = 1;

    /// <summary>在 ShowDialog 之前呼叫（doc 是預覽用的同一份文件）</summary>
    public static void Attach(PrintPreviewDialog preview, PrintDocument doc)
    {
        var control = preview.PrintPreviewControl;
        int total = 0, counting = 0;
        doc.BeginPrint += (s, e) => counting = 0;
        doc.PrintPage += (s, e) => counting++;                 // 預覽時每一頁都會排版一次，數出總頁數

        var label = new ToolStripLabel { AutoSize = true };
        var prev = new ToolStripButton("◀ 上一頁") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var next = new ToolStripButton("下一頁 ▶") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        void Refresh()
        {
            int page = control.StartPage;
            label.Text = total > 0 ? $"第 {page + 1} / {total} 頁" : $"第 {page + 1} 頁";
            prev.Enabled = page > 0;
            next.Enabled = total == 0 || page < total - 1;
        }
        void Go(int page)
        {
            int max = total > 0 ? total - 1 : int.MaxValue;
            control.StartPage = Math.Max(0, Math.Min(page, max));
            Refresh();
        }
        doc.EndPrint += (s, e) =>
        {
            total = counting;
            if (preview.IsHandleCreated && !preview.IsDisposed) preview.BeginInvoke(Refresh);
        };
        control.StartPageChanged += (s, e) => Refresh();
        prev.Click += (s, e) => Go(control.StartPage - 1);
        next.Click += (s, e) => Go(control.StartPage + 1);

        // 工具列：拿掉原本的頁次數字框和「頁」標籤，換成上一頁 / 第幾頁 / 下一頁
        var strip = preview.Controls.OfType<ToolStrip>().FirstOrDefault();
        if (strip != null)
        {
            for (int i = 0; i < strip.Items.Count; i++)
            {
                if (strip.Items[i] is not ToolStripControlHost { Control: NumericUpDown } host) continue;
                host.Visible = false;
                if (i > 0 && strip.Items[i - 1] is ToolStripLabel before) before.Visible = false;
                if (i + 1 < strip.Items.Count && strip.Items[i + 1] is ToolStripLabel after) after.Visible = false;
            }
            // 放大鏡：按下去就打開縮放比例選單（原本按放大鏡本身沒有反應，要按旁邊的小箭頭）
            foreach (var zoom in strip.Items.OfType<ToolStripSplitButton>())
                zoom.ButtonClick += (s, e) => zoom.ShowDropDown();
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(prev);
            strip.Items.Add(label);
            strip.Items.Add(next);
        }

        // 滑鼠滾輪：往下 = 下一頁、往上 = 上一頁；有捲軸時先捲，捲到底（頂）再換頁。觸控板的小幅度累積到一格才換
        int wheel = 0;
        control.MouseWheel += (s, e) =>
        {
            bool down = e.Delta < 0;
            // Ctrl + 滾輪 = 放大縮小
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                control.Zoom = Math.Clamp(control.Zoom * (down ? 1 / 1.2 : 1.2), 0.1, 5.0);
                return;
            }
            if (!AtEdge(control, down))
            {
                // 放大看時先捲動頁面（預覽畫面本身不一定會處理滾輪，沒有捲動就自己捲）
                wheel = 0;
                int before = ScrollPos(control);
                control.BeginInvoke(() =>
                {
                    if (ScrollPos(control) != before) return;
                    if (VBar(control) is not { } bar) return;
                    int step = Math.Max(bar.SmallChange * 3, bar.LargeChange / 4);
                    bar.Value = Math.Clamp(bar.Value + (down ? step : -step), bar.Minimum, Math.Max(bar.Minimum, bar.Maximum - bar.LargeChange + 1));
                });
                return;
            }
            wheel += e.Delta;
            if (Math.Abs(wheel) < SystemInformation.MouseWheelScrollDelta) return;
            wheel = 0;
            int page = control.StartPage;
            Go(page + (down ? 1 : -1));
            // 放大看時：換到下一頁從頁面最上面開始看、換回上一頁從最下面開始
            if (control.StartPage != page && VBar(control) is { } bar)
                bar.Value = down ? bar.Minimum : Math.Max(bar.Minimum, bar.Maximum - bar.LargeChange + 1);
        };
        control.MouseEnter += (s, e) => control.Focus();         // 滾輪事件要有焦點才收得到
        preview.Shown += (s, e) => { control.Focus(); Refresh(); };

        // PageDown / PageUp 換頁
        preview.KeyPreview = true;
        preview.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.PageDown) { Go(control.StartPage + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.PageUp) { Go(control.StartPage - 1); e.Handled = true; }
        };
        Refresh();
    }

    /// <summary>預覽畫面放大時的直捲軸（.NET 的預覽畫面用子控制項當捲軸）</summary>
    private static ScrollBar VBar(Control control) => control.Controls.OfType<VScrollBar>().FirstOrDefault(v => v.Visible);

    private static int ScrollPos(Control control) => VBar(control)?.Value ?? 0;

    /// <summary>頁面沒有直捲軸，或已經捲到底（往下時）/ 頂（往上時）</summary>
    private static bool AtEdge(Control control, bool down)
    {
        var bar = VBar(control);
        if (bar == null) return true;
        // 只差幾點也算到底（自動縮放時頁面常常只比畫面高一點點，不要讓使用者多滾好幾下才換頁）
        return down ? bar.Value >= bar.Maximum - bar.LargeChange + 1 - 8 : bar.Value <= bar.Minimum + 8;
    }
}
