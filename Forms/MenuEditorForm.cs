namespace FormBuilder;

/// <summary>選單管理（管理者用）：用資料夾把已發布的畫面排成樹狀選單；拖曳調整位置，按「儲存」才寫入系統資料庫</summary>
public partial class MenuEditorForm : Form
{
    private static string Cs => SysContext.Connection;

    public MenuEditorForm()
    {
        InitializeComponent();
        btnAddFolder.Click += (s, e) => AddFolder();
        btnAddForm.Click += (s, e) => AddForms();
        btnRename.Click += (s, e) => treeMenu.SelectedNode?.BeginEdit();
        btnDelete.Click += (s, e) => RemoveSelected();
        btnUp.Click += (s, e) => MoveSelected(-1);
        btnDown.Click += (s, e) => MoveSelected(1);
        btnOutdent.Click += (s, e) => OutdentSelected();
        btnSave.Click += (s, e) => Save();
        treeMenu.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2) treeMenu.SelectedNode?.BeginEdit();
            if (e.KeyCode == Keys.Delete) RemoveSelected();
            if (e.KeyData == (Keys.Control | Keys.Left)) { OutdentSelected(); e.Handled = true; }
        };
        treeMenu.AfterLabelEdit += (s, e) =>
        {
            if (e.Label == null) return;                    // 按 Esc 取消
            if (string.IsNullOrWhiteSpace(e.Label)) { e.CancelEdit = true; return; }
            ((MenuNode)e.Node.Tag).Title = e.Label.Trim();
            BeginInvoke(() => e.Node.Text = DisplayText((MenuNode)e.Node.Tag));
        };
        treeMenu.BeforeLabelEdit += (s, e) => BeginInvoke(() => { });   // 編輯時顯示的是不含圖示的標題（見 DisplayText）

        // ---- 拖曳調整位置 ----
        treeMenu.ItemDrag += (s, e) => DoDragDrop(e.Item, DragDropEffects.Move);
        treeMenu.DragOver += (s, e) => e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        treeMenu.DragDrop += (s, e) =>
        {
            if (e.Data.GetData(typeof(TreeNode)) is not TreeNode dragged) return;
            var target = treeMenu.GetNodeAt(treeMenu.PointToClient(new Point(e.X, e.Y)));
            if (target == dragged || IsAncestor(dragged, target)) return;
            dragged.Remove();
            if (target == null) treeMenu.Nodes.Add(dragged);
            else if (((MenuNode)target.Tag).IsFolder && !ModifierKeys.HasFlag(Keys.Shift)) { target.Nodes.Add(dragged); target.Expand(); }   // Shift = 放在資料夾下面同一層
            else (target.Parent?.Nodes ?? treeMenu.Nodes).Insert(target.Index + 1, dragged);
            treeMenu.SelectedNode = dragged;
        };

        LoadTree();
    }

    private static bool IsAncestor(TreeNode node, TreeNode of)
    {
        for (var p = of; p != null; p = p.Parent) if (p == node) return true;
        return false;
    }

    private static string DisplayText(MenuNode n) => n.IsFolder ? "📁 " + n.Title : $"{n.Title}　［{n.FormKey}］";

    private TreeNode CreateNode(MenuNode n) => new(DisplayText(n)) { Tag = n, ForeColor = n.IsFolder ? Color.DarkGoldenrod : SystemColors.WindowText };

    private void LoadTree()
    {
        var nodes = SysDb.LoadMenu(Cs);
        void Add(TreeNodeCollection target, int? parentId)
        {
            foreach (var n in nodes.Where(n => n.ParentId == parentId))
            {
                var tn = CreateNode(n);
                target.Add(tn);
                if (n.IsFolder) Add(tn.Nodes, n.Id);
            }
        }
        treeMenu.BeginUpdate();
        treeMenu.Nodes.Clear();
        Add(treeMenu.Nodes, null);
        treeMenu.ExpandAll();
        treeMenu.EndUpdate();
    }

    /// <summary>新節點放在：選的是資料夾 → 資料夾裡；選的是畫面 → 它的同一層；沒選 → 最上層</summary>
    private TreeNodeCollection TargetCollection() =>
        treeMenu.SelectedNode is { } sel ? (((MenuNode)sel.Tag).IsFolder ? sel.Nodes : sel.Parent?.Nodes ?? treeMenu.Nodes) : treeMenu.Nodes;

    private void AddFolder()
    {
        var v = PromptDialog.Ask(this, "新增資料夾", values => values[0] == "" ? "請輸入資料夾名稱。" : null, new PromptDialog.Field("資料夾名稱"));
        if (v == null) return;
        var node = CreateNode(new MenuNode { Title = v[0] });
        TargetCollection().Add(node);
        treeMenu.SelectedNode = node;
    }

    private void AddForms()
    {
        var published = SysDb.LoadPublished(Cs, "form");
        if (published.Count == 0)
        {
            MessageBox.Show("還沒有已發布的畫面：請在「畫面設計」設計好後，用「檔案 → 發布到系統」。", Text);
            return;
        }
        using var dlg = new PickListDialog("加入畫面（可以勾多個）", published.Select(p => new PickListDialog.Item(p.FormKey, $"{p.Title ?? p.FormKey}　［{p.FormKey}］")));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var target = TargetCollection();
        TreeNode last = null;
        foreach (string key in dlg.PickedKeys)
        {
            var p = published.First(x => x.FormKey == key);
            last = CreateNode(new MenuNode { Title = p.Title ?? p.FormKey, FormKey = p.FormKey });
            target.Add(last);
        }
        if (last != null)
        {
            last.EnsureVisible();
            treeMenu.SelectedNode = last;
        }
    }

    private void RemoveSelected()
    {
        var node = treeMenu.SelectedNode;
        if (node == null) return;
        if (node.Nodes.Count > 0 &&
            MessageBox.Show($"「{((MenuNode)node.Tag).Title}」裡面還有 {node.Nodes.Count} 個項目，要一起移除嗎？（畫面本身不會被刪除）", Text,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        node.Remove();
    }

    private void MoveSelected(int delta)
    {
        var node = treeMenu.SelectedNode;
        if (node == null) return;
        var siblings = node.Parent?.Nodes ?? treeMenu.Nodes;
        int index = node.Index + delta;
        if (index < 0 || index >= siblings.Count) return;
        node.Remove();
        siblings.Insert(index, node);
        treeMenu.SelectedNode = node;
    }

    /// <summary>移到上一層：從資料夾裡拿出來，放在那個資料夾的下面（同一層）</summary>
    private void OutdentSelected()
    {
        var node = treeMenu.SelectedNode;
        if (node?.Parent is not { } parent) return;                 // 已經在最上層
        node.Remove();
        (parent.Parent?.Nodes ?? treeMenu.Nodes).Insert(parent.Index + 1, node);
        treeMenu.SelectedNode = node;
    }

    private void Save()
    {
        List<MenuTree> Build(TreeNodeCollection nodes) =>
            nodes.Cast<TreeNode>().Select(tn =>
            {
                var tree = new MenuTree { Node = (MenuNode)tn.Tag };
                tree.Children.AddRange(Build(tn.Nodes));
                return tree;
            }).ToList();
        try
        {
            var roots = Build(treeMenu.Nodes);
            SysDb.SaveMenu(Cs, roots);
            Audit.Write(null, "SYS_Menu", "修改選單", null, $"{CountNodes(treeMenu.Nodes)} 個節點");
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("儲存選單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static int CountNodes(TreeNodeCollection nodes) => nodes.Cast<TreeNode>().Sum(n => 1 + CountNodes(n.Nodes));
}
