namespace FormBuilder;

/// <summary>
/// 拖曳設計畫面的右邊、下邊、右下角調整大小（主設計畫面與編輯視窗版面共用）。
/// 拖到外框可見範圍外時會自動捲動，按住滑鼠不放就會持續往下 / 往右延伸
/// </summary>
public class CanvasResizer
{
    private const int Edge = 8;               // 離邊緣幾個像素內算是抓到邊
    private const int MaxScrollStep = 40;     // 自動捲動每次最多捲幾個像素

    private readonly Panel canvas;
    private readonly Panel host;              // 可捲動的外框（canvas 放在它裡面）
    private readonly Func<Size> getSize;
    private readonly Action<Size> setSize;    // 呼叫端負責套用最小值、更新畫面
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    private bool right, bottom;

    public CanvasResizer(Panel canvas, Panel host, Func<Size> getSize, Action<Size> setSize)
    {
        this.canvas = canvas;
        this.host = host;
        this.getSize = getSize;
        this.setSize = setSize;
        timer.Tick += (s, e) => Step();       // 滑鼠停在外框外面不動時，也繼續延伸
    }

    /// <summary>正在拖曳邊緣</summary>
    public bool Active => right || bottom;

    /// <summary>在畫面上按下滑鼠：抓到邊緣就開始調整大小並回傳 true</summary>
    public bool TryStart(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return false;
        (right, bottom) = Hit(e.Location);
        if (!Active) return false;
        timer.Start();
        return true;
    }

    /// <summary>滑鼠移動：拖曳中就調整大小並回傳 true；沒在拖曳時依位置切換游標</summary>
    public bool Move(MouseEventArgs e)
    {
        if (Active)
        {
            Step();
            return true;
        }
        var (r, b) = Hit(e.Location);
        canvas.Cursor = r && b ? Cursors.SizeNWSE : r ? Cursors.SizeWE : b ? Cursors.SizeNS : Cursors.Default;
        return false;
    }

    /// <summary>放開滑鼠：結束調整大小；剛才有在拖曳就回傳 true</summary>
    public bool End()
    {
        if (!Active) return false;
        right = bottom = false;
        timer.Stop();
        return true;
    }

    private (bool Right, bool Bottom) Hit(Point p) =>
        (p.X >= canvas.ClientSize.Width - Edge, p.Y >= canvas.ClientSize.Height - Edge);

    /// <summary>邊緣跟著滑鼠（用畫面座標，捲動後也對得上）；滑鼠超出外框時自動捲動</summary>
    private void Step()
    {
        Point p = canvas.PointToClient(Control.MousePosition);
        Size size = getSize();
        setSize(new Size(right ? p.X + 2 : size.Width, bottom ? p.Y + 2 : size.Height));

        Point hp = host.PointToClient(Control.MousePosition);
        int dx = right ? Math.Clamp(hp.X - (host.ClientSize.Width - 10), 0, MaxScrollStep) : 0;
        int dy = bottom ? Math.Clamp(hp.Y - (host.ClientSize.Height - 10), 0, MaxScrollStep) : 0;
        if (dx > 0 || dy > 0)
        {
            Point cur = host.AutoScrollPosition;           // 讀出來是負值，設定時要用正值
            host.AutoScrollPosition = new Point(-cur.X + dx, -cur.Y + dy);
        }
    }
}
