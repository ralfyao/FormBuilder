namespace FormBuilder;

/// <summary>
/// 復原（Ctrl+Z）/ 取消復原（Ctrl+Y）：用整個畫面定義的快照（JSON 字串）記錄每一步。
/// 不必在每個修改的地方各自記錄：定時比對目前的快照，有變就記一步；
/// 滑鼠按著（拖曳、調整大小、拉框）時不記，放開後整個拖曳算一步
/// </summary>
public sealed class UndoHistory
{
    private const int MaxSteps = 100;

    private readonly Func<string> capture;
    private readonly Action<string> restore;
    private readonly List<string> undo = new();
    private readonly Stack<string> redo = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    private string current;
    private bool restoring;

    /// <param name="owner">視窗關閉時一起停止計時器</param>
    /// <param name="capture">取得目前狀態的快照</param>
    /// <param name="restore">把畫面還原成某個快照</param>
    public UndoHistory(Control owner, Func<string> capture, Action<string> restore)
    {
        this.capture = capture;
        this.restore = restore;
        current = capture();
        timer.Tick += (s, e) => Check();
        timer.Start();
        owner.Disposed += (s, e) => timer.Dispose();
    }

    public bool CanUndo { get { Check(); return undo.Count > 0; } }
    public bool CanRedo => redo.Count > 0;

    /// <summary>開新畫面、開啟檔案：清掉歷史，從目前狀態重新開始</summary>
    public void Reset()
    {
        undo.Clear();
        redo.Clear();
        current = capture();
    }

    /// <summary>有變動就記一步（拖曳中不記）</summary>
    public void Check()
    {
        if (restoring || Control.MouseButtons != MouseButtons.None) return;
        string now = capture();
        if (now == current) return;
        undo.Add(current);
        if (undo.Count > MaxSteps) undo.RemoveAt(0);
        redo.Clear();                                     // 做了新的動作，原本可以「取消復原」的步驟就作廢
        current = now;
    }

    /// <summary>回到上一步；沒有可以復原的動作時回傳 false</summary>
    public bool Undo()
    {
        Check();
        if (undo.Count == 0) return false;
        redo.Push(current);
        current = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        Apply(current);
        return true;
    }

    /// <summary>重做剛剛復原的那一步</summary>
    public bool Redo()
    {
        Check();
        if (redo.Count == 0) return false;
        undo.Add(current);
        current = redo.Pop();
        Apply(current);
        return true;
    }

    /// <summary>
    /// 焦點是不是在文字框裡（正在打字）：這時 Ctrl+Z / Ctrl+Y 留給文字框自己復原文字。
    /// 屬性面板沒有在編輯值時不算，才不會一點過屬性面板就不能復原
    /// </summary>
    public static bool IsEditingText() => Control.FromHandle(GetFocus()) is TextBoxBase;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    private void Apply(string snapshot)
    {
        restoring = true;
        try
        {
            restore(snapshot);
            current = capture();                          // 還原後的狀態以畫面實際的為準
        }
        finally
        {
            restoring = false;
        }
    }
}
