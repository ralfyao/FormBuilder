namespace FormBuilder;

/// <summary>
/// 設計畫面上可選取、拖曳移動、拖曳邊緣調整大小的元件（欄位、單身 Grid 共用）。
/// 實際移動由設計視窗處理，才能多個元件一起移
/// </summary>
public abstract class DesignBox : Panel
{
    public const int Grid = 10;
    protected const int ResizeEdge = 6;      // 離邊緣幾個像素內算是抓到邊

    /// <summary>在元件上按下滑鼠（不是抓邊緣）</summary>
    public event EventHandler<MouseEventArgs> Pressed;
    /// <summary>按住拖曳中，參數是從按下到現在的滑鼠位移</summary>
    public event EventHandler<Size> Dragging;
    /// <summary>放開滑鼠，結束移動</summary>
    public event EventHandler DragFinished;
    /// <summary>拖曳邊緣改了大小</summary>
    public event EventHandler Resized;
    /// <summary>點一下：按下後沒有拖動就放開（拖曳移動不算）</summary>
    public event EventHandler Clicked;
    private const int ClickTolerance = 3;    // 按下到放開移動不超過幾個像素算點一下

    /// <summary>屬性面板要顯示的定義物件（FieldDefinition 或 GridDefinition）</summary>
    public abstract object Model { get; }
    /// <summary>定義裡的位置</summary>
    public abstract Point ModelLocation { get; }
    /// <summary>
    /// 水平對齊用的基準線（畫面座標）：預設是元件左緣；
    /// 欄位改用輸入框左緣，標籤寬度不同時輸入框才排得整齊
    /// </summary>
    public virtual int AlignX => ModelLocation.X;
    public abstract void SetModelLocation(int x, int y);
    /// <summary>屬性改了之後，依定義重畫</summary>
    public abstract void RefreshFromModel();
    /// <summary>拖曳邊緣時要調整的大小（寬、高）；setter 負責限制最小值並重畫</summary>
    protected abstract Size ResizableSize { get; set; }
    /// <summary>目前滑鼠位置（元件座標）是否在可調整的右邊緣 / 下邊緣（ResizeFromTop 時是上邊緣）</summary>
    protected abstract (bool Right, bool Bottom) HitResize(Point local);
    /// <summary>true = 高度改由上邊緣拖曳（往上拉變高），用在停靠在畫面底部的 Grid</summary>
    protected virtual bool ResizeFromTop => false;

    // ---- 左邊緣拖曳（欄位用來調整標籤寬度）；預設沒有 ----
    protected virtual bool HitLeftResize(Point local) => false;
    protected virtual void BeginLeftResize() { }
    /// <summary>dx = 從按下到現在的水平位移（往右為正）</summary>
    protected virtual void ApplyLeftResize(int dx) { }

    private bool selected;
    public bool IsSelected
    {
        get => selected;
        set { selected = value; OnSelectedChanged(); }
    }
    protected virtual void OnSelectedChanged() => Invalidate();

    private bool moving, resizeRight, resizeBottom, resizeLeft;
    private Point pressMouse;
    private Size pressSize;

    /// <summary>
    /// 拖這個元件的邊緣調整大小時，一起調整的其他元件（例如同時選取的其他欄位）；由設計視窗設定，null = 只調整自己。
    /// 一起調整的元件套用同樣的位移（標籤寬度、輸入框寬度、高度各自加減一樣多）
    /// </summary>
    public Func<IEnumerable<DesignBox>> ResizeCompanions { get; set; }
    /// <summary>這次拖邊緣一起調整的元件與它們按下時的大小</summary>
    private List<(DesignBox Box, Size PressSize)> companions = new();

    /// <summary>子控制項上的滑鼠動作也當成整個元件的動作</summary>
    protected void AttachMouse(Control c)
    {
        c.MouseDown += OnMouseDownBox;
        c.MouseMove += OnMouseMoveBox;
        c.MouseUp += OnMouseUpBox;
    }

    protected static int Snap(int v) => (int)Math.Round(v / (double)Grid) * Grid;

    private void OnMouseDownBox(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        pressMouse = MousePosition;
        // 選取了好幾個同類的元件時，拖其中一個的邊緣，全部一起調整
        companions = (ResizeCompanions?.Invoke() ?? Enumerable.Empty<DesignBox>())
            .Where(b => b != this && b.GetType() == GetType()).Select(b => (b, b.ResizableSize)).ToList();
        if (HitLeftResize(PointToClient(MousePosition)))
        {
            resizeLeft = true;
            BeginLeftResize();
            foreach (var (b, _) in companions) b.BeginLeftResize();
            return;
        }
        (resizeRight, resizeBottom) = HitResize(PointToClient(MousePosition));
        if (resizeRight || resizeBottom)
        {
            pressSize = ResizableSize;
        }
        else
        {
            moving = true;
            Pressed?.Invoke(this, e);
        }
    }

    private void OnMouseMoveBox(object sender, MouseEventArgs e)
    {
        var delta = new Size(MousePosition.X - pressMouse.X, MousePosition.Y - pressMouse.Y);
        if (resizeLeft)
        {
            ApplyLeftResize(delta.Width);
            foreach (var (b, _) in companions) b.ApplyLeftResize(delta.Width);
            return;
        }
        if (resizeRight || resizeBottom)
        {
            // 從上邊緣拖時，滑鼠往上（位移為負）是變高
            int heightDelta = ResizeFromTop ? -delta.Height : delta.Height;
            Size Resize(Size press) => new(
                resizeRight ? Snap(press.Width + delta.Width) : press.Width,
                resizeBottom ? Snap(press.Height + heightDelta) : press.Height);
            ResizableSize = Resize(pressSize);
            foreach (var (b, press) in companions) b.ResizableSize = Resize(press);
            return;
        }
        if (moving)
        {
            Dragging?.Invoke(this, delta);
            return;
        }
        // 沒按住時：依滑鼠所在邊緣切換游標
        Point local = PointToClient(MousePosition);
        var (right, bottom) = HitResize(local);
        Cursor cursor = HitLeftResize(local) ? Cursors.SizeWE
                      : right && bottom ? Cursors.SizeNWSE : right ? Cursors.SizeWE : bottom ? Cursors.SizeNS : Cursors.SizeAll;
        Cursor = cursor;
        foreach (Control c in Controls) c.Cursor = cursor;
    }

    private void OnMouseUpBox(object sender, MouseEventArgs e)
    {
        if (resizeLeft || resizeRight || resizeBottom)
        {
            resizeLeft = resizeRight = resizeBottom = false;
            Resized?.Invoke(this, EventArgs.Empty);
            foreach (var (b, _) in companions) b.Resized?.Invoke(b, EventArgs.Empty);
            companions = new();
        }
        if (moving)
        {
            moving = false;
            DragFinished?.Invoke(this, EventArgs.Empty);
            bool clicked = Math.Abs(MousePosition.X - pressMouse.X) <= ClickTolerance
                        && Math.Abs(MousePosition.Y - pressMouse.Y) <= ClickTolerance;
            if (clicked) Clicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
