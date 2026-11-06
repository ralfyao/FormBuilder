namespace FormBuilder;

/// <summary>
/// 畫面設計左邊「資料表」樹的欄位多選（TreeView 本身只能選一個）：
/// Ctrl 點選加入 / 移除、Shift 點選選一段（同一個資料表底下），一般點選 = 只選這一個。選到的欄位用底色標出來。
/// 在選好的欄位上拖曳 = 整批拖；雙擊 = 整批交給 DoubleClicked（在多選上點一下，等雙擊時間過了才改成只選這一個）
/// </summary>
public sealed class TreeColumnPicker
{
    private readonly TreeView tree;
    private readonly List<TreeNode> picked = new();
    private TreeNode anchor;
    private readonly System.Windows.Forms.Timer collapse = new();
    private TreeNode collapseTo;

    /// <summary>雙擊欄位：選了好幾個時是全部，沒有就是雙擊的那一個</summary>
    public event Action<IReadOnlyList<TreeNode>> DoubleClicked;

    public TreeColumnPicker(TreeView tree)
    {
        this.tree = tree;
        collapse.Interval = SystemInformation.DoubleClickTime;
        collapse.Tick += (s, e) =>
        {
            collapse.Stop();
            if (collapseTo != null) Set(new[] { collapseTo });
        };
        tree.Disposed += (s, e) => collapse.Dispose();
        tree.NodeMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (!IsColumn(e.Node))
            {
                Set(Array.Empty<TreeNode>());
                return;
            }
            var keys = Control.ModifierKeys;
            if (keys.HasFlag(Keys.Control))
            {
                var list = picked.Where(n => n.Parent == e.Node.Parent).ToList();
                if (!list.Remove(e.Node)) list.Add(e.Node);
                anchor = e.Node;
                Set(list);
            }
            else if (keys.HasFlag(Keys.Shift) && anchor != null && anchor.Parent == e.Node.Parent)
            {
                int a = anchor.Index, b = e.Node.Index;
                Set(e.Node.Parent.Nodes.Cast<TreeNode>().Where(n => n.Index >= Math.Min(a, b) && n.Index <= Math.Max(a, b) && IsColumn(n)));
            }
            else if (picked.Count > 1 && picked.Contains(e.Node))
            {
                collapseTo = e.Node;                       // 可能是雙擊的第一下：等一下才改成只選這一個
                anchor = e.Node;
                collapse.Start();
            }
            else
            {
                anchor = e.Node;
                Set(new[] { e.Node });
            }
        };
        tree.NodeMouseDoubleClick += (s, e) =>
        {
            if (!IsColumn(e.Node)) return;
            bool many = collapse.Enabled && picked.Count > 1 && picked.Contains(e.Node);
            collapse.Stop();
            DoubleClicked?.Invoke(many ? Ordered() : new[] { e.Node });
        };
    }

    private static bool IsColumn(TreeNode n) => n?.Tag is ColumnDragData;

    /// <summary>拖曳時要帶的欄位：拖的是選好的其中一個就整批，否則只拖它（並改成只選它）</summary>
    public IReadOnlyList<TreeNode> DragSet(TreeNode dragged)
    {
        collapse.Stop();
        if (picked.Count > 1 && picked.Contains(dragged)) return Ordered();
        anchor = dragged;
        Set(new[] { dragged });
        return new[] { dragged };
    }

    private List<TreeNode> Ordered() => picked.Where(n => n.TreeView == tree).OrderBy(n => n.Index).ToList();

    private void Set(IEnumerable<TreeNode> nodes)
    {
        foreach (var n in picked.Where(n => n.TreeView == tree))
        {
            n.BackColor = Color.Empty;
            n.ForeColor = Color.Empty;
        }
        picked.Clear();
        picked.AddRange(nodes.Distinct());
        if (picked.Count < 2) return;                      // 只選一個：照 TreeView 原本的選取顏色
        foreach (var n in picked)
        {
            n.BackColor = SystemColors.Highlight;
            n.ForeColor = SystemColors.HighlightText;
        }
    }
}
