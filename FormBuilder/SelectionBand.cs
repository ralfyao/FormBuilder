namespace FormBuilder;

/// <summary>
/// 滑鼠拉框圈選時顯示的選取框（設計畫面與編輯視窗版面共用）。
/// 用四條細線控制項疊在最上層，不用 ControlPaint.DrawReversibleFrame：
/// 反相繪製在新版 Windows（桌面合成）上常常看不到
/// </summary>
public sealed class SelectionBand
{
    private static readonly Color LineColor = Color.FromArgb(0, 120, 215);

    private readonly Control host;
    private readonly Panel[] edges;
    private Point start;

    /// <summary>true = 正在拉框</summary>
    public bool Active { get; private set; }
    /// <summary>目前的框（host 的座標）；只是點一下沒有拖曳時是空的</summary>
    public Rectangle Bounds { get; private set; }

    public SelectionBand(Control host)
    {
        this.host = host;
        edges = Enumerable.Range(0, 4).Select(_ => new Panel { BackColor = LineColor, Visible = false, Enabled = false }).ToArray();
    }

    public void Begin(Point location)
    {
        Active = true;
        start = location;
        Bounds = Rectangle.Empty;
    }

    /// <summary>拖曳中：從按下的位置拉到目前位置（往任何方向都可以）</summary>
    public void Move(Point location)
    {
        if (!Active) return;
        Bounds = Rectangle.FromLTRB(Math.Min(start.X, location.X), Math.Min(start.Y, location.Y),
                                    Math.Max(start.X, location.X), Math.Max(start.Y, location.Y));
        if (Bounds.Width < 2 && Bounds.Height < 2) return;
        var r = Bounds;
        foreach (var edge in edges)
            if (edge.Parent != host) host.Controls.Add(edge);   // 畫面清空（開新畫面）後重新加回去
        edges[0].SetBounds(r.X, r.Y, r.Width, 1);
        edges[1].SetBounds(r.X, r.Bottom - 1, r.Width, 1);
        edges[2].SetBounds(r.X, r.Y, 1, r.Height);
        edges[3].SetBounds(r.Right - 1, r.Y, 1, r.Height);
        foreach (var edge in edges)
        {
            edge.Visible = true;
            edge.BringToFront();                          // 蓋在欄位、Grid 上面才看得到
        }
        host.Update();                                     // 拖曳中立刻重畫，框才會跟著滑鼠
    }

    /// <summary>放開滑鼠：收起選取框，回傳框的範圍（只是點一下時回傳空的）</summary>
    public Rectangle End()
    {
        Active = false;
        foreach (var edge in edges) edge.Visible = false;
        var r = Bounds;
        Bounds = Rectangle.Empty;
        return r.Width < 2 && r.Height < 2 ? Rectangle.Empty : r;
    }
}
