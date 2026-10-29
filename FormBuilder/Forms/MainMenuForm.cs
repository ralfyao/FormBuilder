namespace FormBuilder;

/// <summary>
/// 登入後的主選單：依「選單管理」排的樹狀選單，只列出有「開啟」權限的畫面（管理者全部看得到），雙擊開啟。
/// 管理者另外有「系統管理」：畫面設計、使用者與權限、選單管理、稽核紀錄、系統設定
/// </summary>
public partial class MainMenuForm : Form
{
    private readonly SysConfig config;
    /// <summary>管理者看到的「未放進選單的畫面」資料夾（不是真的選單節點）</summary>
    private TreeNode unlistedFolder;

    /// <summary>true = 使用者按了「登出」（回到登入畫面），false = 直接關閉程式</summary>
    public bool LoggedOut { get; private set; }

    public MainMenuForm(SysConfig config)
    {
        InitializeComponent();
        this.config = config;
        var user = SysContext.User;
        Text = "ERP Studio";
        lblUser.Text = $"{user.UserName}（{user.UserId}）{(user.IsAdmin ? "　系統管理者" : "")}";
        lblVersion.Text = "版本 " + Updater.CurrentVersion;
        btnAdmin.Visible = sepAdmin.Visible = user.IsAdmin;

        btnOpen.Click += (s, e) => OpenSelected();
        btnRefresh.Click += (s, e) => { SysContext.ReloadPermissions(); LoadMenu(); };
        treeMenu.NodeMouseDoubleClick += (s, e) => OpenSelected();
        treeMenu.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSelected(); };
        btnPassword.Click += (s, e) => LoginForm.ChangePassword(this, SysContext.Connection, user.UserId, forced: false);
        btnLogout.Click += (s, e) =>
        {
            Audit.Write(null, null, "登出", null);
            LoggedOut = true;
            Close();
        };
        FormClosing += (s, e) =>
        {
            // 還有開著的畫面時先確認（各畫面自己會再問有沒有沒存的輸入）
            int open = Application.OpenForms.Cast<Form>().Count(f => f != this && f.Visible);
            if (open > 0 && MessageBox.Show($"還有 {open} 個畫面開著，確定要{(LoggedOut ? "登出" : "關閉")}嗎？", Text,
                                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                LoggedOut = false;
                e.Cancel = true;
            }
        };

        miDesigner.Click += (s, e) => OpenDesigner();
        // 全部發布：本機 Forms 資料夾裡的畫面定義檔全部發布到系統（放在「畫面設計」下面）
        if (user.IsAdmin)
            btnAdmin.DropDownItems.Insert(btnAdmin.DropDownItems.IndexOf(miDesigner) + 1,
                new ToolStripMenuItem("全部發布（本機 Forms 資料夾的畫面）…", null, (s, e) => PublishAll())
                {
                    ToolTipText = "把 Forms 資料夾裡所有的畫面定義檔（*.json）連同用到的列印樣板發布到系統；內容跟系統裡一樣的略過",
                });
        // 管理者：選單上按右鍵 → 開啟 / 設計這個畫面
        if (user.IsAdmin)
        {
            var nodeMenu = new ContextMenuStrip();
            var miOpen = nodeMenu.Items.Add("開啟", null, (s, e) => OpenSelected());
            var miDesign = nodeMenu.Items.Add("設計這個畫面", null, (s, e) => OpenDesigner());
            var miRename = nodeMenu.Items.Add("重新命名…", null, (s, e) => RenameSelected());
            var miCopy = nodeMenu.Items.Add("複製畫面…", null, (s, e) => CopySelected());
            nodeMenu.Items.Add("新增資料夾…", null, (s, e) => AddFolder());
            var miDeleteFolder = nodeMenu.Items.Add("刪除資料夾", null, (s, e) => DeleteFolder());
            var miDeleteForm = nodeMenu.Items.Add("刪除畫面…", null, (s, e) => DeleteFormSelected());
            nodeMenu.Items.Add(new ToolStripSeparator());
            // 新增表單：開空白的畫面設計（設計好用「檔案 → 發布到系統」，再拖進選單的資料夾）
            nodeMenu.Items.Add("新增表單", null, (s, e) => new DesignerForm().Show());
            nodeMenu.Items.Add("全部發布…", null, (s, e) => PublishAll());
            nodeMenu.Opening += (s, e) =>
            {
                // 在空白處按右鍵：取消選取（新增資料夾會加在最上層）
                var point = treeMenu.PointToClient(MousePosition);
                if (treeMenu.ClientRectangle.Contains(point) && treeMenu.GetNodeAt(point) == null) treeMenu.SelectedNode = null;
                // 開啟、設計只對畫面；新增資料夾、新增表單在哪裡按右鍵都有
                bool onForm = treeMenu.SelectedNode?.Tag is MenuNode { IsFolder: false };
                miOpen.Visible = miDesign.Visible = miCopy.Visible = miDeleteForm.Visible = onForm;
                miRename.Visible = treeMenu.SelectedNode?.Tag is MenuNode;                      // 畫面、資料夾都可以改名
                miDeleteFolder.Visible = treeMenu.SelectedNode?.Tag is MenuNode { IsFolder: true };
            };
            treeMenu.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) RenameSelected();
                else if (e.KeyCode == Keys.Delete) { if (treeMenu.SelectedNode?.Tag is MenuNode { IsFolder: true }) DeleteFolder(); else DeleteFormSelected(); }
            };
            treeMenu.NodeMouseClick += (s, e) => { if (e.Button == MouseButtons.Right) treeMenu.SelectedNode = e.Node; };
            treeMenu.ContextMenuStrip = nodeMenu;
            EnableDragToMove();
        }
        miUsers.Click += (s, e) => { using var dlg = new UserAdminForm(); dlg.ShowDialog(this); };
        miMenuEditor.Click += (s, e) => { using var dlg = new MenuEditorForm(); if (dlg.ShowDialog(this) == DialogResult.OK) LoadMenu(); };
        miAudit.Click += (s, e) => new AuditLogForm().Show(this);
        miBranding.Click += (s, e) => { using var dlg = new BrandingForm(); dlg.ShowDialog(this); };
        miSetup.Click += (s, e) => { using var dlg = new SysSetupForm(config); dlg.ShowDialog(this); };

        LoadMenu();
    }

    /// <summary>
    /// 載入選單：沒有開啟權限的畫面不顯示，裡面沒有畫面的資料夾也不顯示。
    /// 重新載入時保留資料夾的展開、收合；select = 載入後要選取的節點（TreeNode.FullPath）
    /// </summary>
    private void LoadMenu(string select = null)
    {
        var expanded = new HashSet<string>();
        var collapsed = new HashSet<string>();
        foreach (var tn in AllNodes(treeMenu.Nodes).Where(tn => tn.Nodes.Count > 0))
            (tn.IsExpanded ? expanded : collapsed).Add(tn.FullPath);
        treeMenu.BeginUpdate();
        treeMenu.Nodes.Clear();
        try
        {
            var nodes = SysDb.LoadMenu(SysContext.Connection);
            void AddChildren(TreeNodeCollection target, int? parentId)
            {
                foreach (var n in nodes.Where(n => n.ParentId == parentId))
                {
                    if (n.IsFolder)
                    {
                        var folder = new TreeNode("📁 " + n.Title) { Tag = n };
                        AddChildren(folder.Nodes, n.Id);
                        if (folder.Nodes.Count > 0 || SysContext.User.IsAdmin) target.Add(folder);   // 管理者看得到空資料夾（才能把畫面拖進去）
                    }
                    else if (SysContext.PermissionFor(n.FormKey).Open)
                    {
                        target.Add(new TreeNode(n.Title) { Tag = n });
                    }
                }
            }
            AddChildren(treeMenu.Nodes, null);
            // 管理者：已發布但還沒放進選單的畫面另外列出來，方便測試
            if (SysContext.User.IsAdmin)
            {
                var inMenu = nodes.Where(n => !n.IsFolder).Select(n => n.FormKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var others = SysDb.LoadPublished(SysContext.Connection, "form").Where(p => !inMenu.Contains(p.FormKey)).ToList();
                var byKey = others.ToDictionary(p => p.FormKey, StringComparer.OrdinalIgnoreCase);
                var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                TreeNode FormNode(PublishedItem p) => new(p.Title ?? p.FormKey) { Tag = new MenuNode { Title = p.Title ?? p.FormKey, FormKey = p.FormKey } };
                // 一直顯示（沒有畫面時也顯示），把畫面拖到這裡 = 從選單拿掉；裡面可以再分子資料夾（存在 SYS_MenuUnlisted）
                unlistedFolder = new TreeNode("📁 （未放進選單的畫面）") { ForeColor = SystemColors.GrayText };
                var saved = SysDb.LoadUnlisted(SysContext.Connection);
                void AddUnlisted(TreeNodeCollection target, int? parentId)
                {
                    foreach (var n in saved.Where(n => n.ParentId == parentId))
                    {
                        if (n.IsFolder)
                        {
                            var folder = new TreeNode("📁 " + n.Title) { Tag = n };
                            AddUnlisted(folder.Nodes, n.Id);
                            target.Add(folder);
                        }
                        // 已經放進選單、或已經不在系統裡的畫面略過（下次存的時候就不見了）
                        else if (byKey.TryGetValue(n.FormKey, out var p) && placed.Add(p.FormKey)) target.Add(FormNode(p));
                    }
                }
                AddUnlisted(unlistedFolder.Nodes, null);
                foreach (var p in others.Where(p => !placed.Contains(p.FormKey))) unlistedFolder.Nodes.Add(FormNode(p));   // 新發布的放最後面
                treeMenu.Nodes.Add(unlistedFolder);
            }
            // 第一次載入：選單全部展開，「未放進選單的畫面」裡的子資料夾收合；之後照使用者展開、收合的
            foreach (var tn in AllNodes(treeMenu.Nodes).Where(tn => tn.Nodes.Count > 0))
            {
                bool inUnlisted = tn != unlistedFolder && InUnlisted(tn);
                if (expanded.Contains(tn.FullPath) || !collapsed.Contains(tn.FullPath) && !inUnlisted) tn.Expand();
            }
            if (select != null && AllNodes(treeMenu.Nodes).FirstOrDefault(tn => tn.FullPath == select) is { } selected)
            {
                selected.EnsureVisible();
                treeMenu.SelectedNode = selected;
            }
            if (treeMenu.Nodes.Count == 0)
                treeMenu.Nodes.Add(new TreeNode(SysContext.User.IsAdmin
                    ? "（還沒有畫面：到「系統管理 → 畫面設計」設計畫面後，用「檔案 → 發布到系統」，再到「選單管理」排進選單）"
                    : "（沒有可以使用的畫面，請洽系統管理者設定權限）") { ForeColor = SystemColors.GrayText });
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取選單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            treeMenu.EndUpdate();
        }
    }

    // ================= 管理者：拖曳調整選單 =================

    /// <summary>
    /// 管理者可以直接在選單上拖曳：拖到資料夾上 = 放進資料夾，拖到畫面上 = 放在它下面，拖到空白處 = 最上層，
    /// 拖到「未放進選單的畫面」（或裡面的子資料夾）= 從選單拿掉；放開就存進系統資料庫（同「選單管理」按儲存）
    /// </summary>
    private void EnableDragToMove()
    {
        treeMenu.AllowDrop = true;
        treeMenu.ItemDrag += (s, e) =>
        {
            if (e.Item is TreeNode { Tag: MenuNode } node) treeMenu.DoDragDrop(node, DragDropEffects.Move);
        };
        treeMenu.DragOver += (s, e) =>
        {
            e.Effect = DragDropEffects.None;
            if (e.Data.GetData(typeof(TreeNode)) is not TreeNode dragged) return;
            var target = treeMenu.GetNodeAt(treeMenu.PointToClient(new Point(e.X, e.Y)));
            treeMenu.SelectedNode = target ?? dragged;             // 指到哪裡就反白哪裡，看得出會放到哪
            if (CanDrop(dragged, target)) e.Effect = DragDropEffects.Move;
        };
        treeMenu.DragDrop += (s, e) =>
        {
            if (e.Data.GetData(typeof(TreeNode)) is not TreeNode dragged) return;
            var target = treeMenu.GetNodeAt(treeMenu.PointToClient(new Point(e.X, e.Y)));
            if (!CanDrop(dragged, target)) return;
            var node = (MenuNode)dragged.Tag;
            dragged.Remove();
            if (target == null) treeMenu.Nodes.Insert(unlistedFolder?.Index ?? treeMenu.Nodes.Count, dragged);   // 最上層（放在「未放進選單」前面）
            else if (target == unlistedFolder) unlistedFolder.Nodes.Add(dragged);                                // 從選單拿掉
            else if (target.Tag is MenuNode { IsFolder: true }) target.Nodes.Add(dragged);
            else (target.Parent?.Nodes ?? treeMenu.Nodes).Insert(target.Index + 1, dragged);
            SaveMenuFromTree($"移動「{node.Title}」", dragged.FullPath);
        };
    }

    private static IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes) =>
        nodes.Cast<TreeNode>().SelectMany(tn => AllNodes(tn.Nodes).Prepend(tn));

    private bool InUnlisted(TreeNode node)
    {
        for (var p = node; p != null; p = p.Parent) if (p == unlistedFolder) return true;
        return false;
    }

    /// <summary>不能拖到自己或自己的子節點裡（資料夾也可以拖進、拖出「未放進選單的畫面」，整個資料夾的畫面一起拿掉或放進選單）</summary>
    private bool CanDrop(TreeNode dragged, TreeNode target)
    {
        for (var p = target; p != null; p = p.Parent) if (p == dragged) return false;
        if (target != null && target.Tag is not MenuNode && target != unlistedFolder) return false;   // 提示文字之類的
        return true;
    }

    /// <summary>
    /// 依目前的樹存回選單，「未放進選單的畫面」裡的子資料夾另外存（ERP 系統讀不到），存完重新載入；
    /// select = 重新載入後要選取的節點（TreeNode.FullPath）
    /// </summary>
    private void SaveMenuFromTree(string action, string select = null)
    {
        List<MenuTree> Build(TreeNodeCollection nodes) =>
            nodes.Cast<TreeNode>().Where(tn => tn != unlistedFolder && tn.Tag is MenuNode).Select(tn =>
            {
                var tree = new MenuTree { Node = (MenuNode)tn.Tag };
                tree.Children.AddRange(Build(tn.Nodes));
                return tree;
            }).ToList();
        select ??= treeMenu.SelectedNode?.FullPath;
        try
        {
            SysDb.SaveMenu(SysContext.Connection, Build(treeMenu.Nodes), unlistedFolder == null ? null : Build(unlistedFolder.Nodes));
            Audit.Write(null, "SYS_Menu", "修改選單", null, action);
        }
        catch (Exception ex)
        {
            MessageBox.Show("儲存選單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadMenu(select);
    }

    // ================= 管理者：新增、刪除資料夾 =================

    /// <summary>新增資料夾：選的是資料夾（含「未放進選單的畫面」）→ 放在裡面；選的是畫面 → 放在它下面；沒選 → 選單最上層</summary>
    private void AddFolder()
    {
        if (!SysContext.User.IsAdmin) return;
        var v = PromptDialog.Ask(this, "新增資料夾", values => values[0] == "" ? "請輸入資料夾名稱。" : null, new PromptDialog.Field("資料夾名稱"));
        if (v == null) return;
        var folder = new TreeNode("📁 " + v[0]) { Tag = new MenuNode { Title = v[0] } };
        var sel = treeMenu.SelectedNode;
        if (sel == unlistedFolder || sel?.Tag is MenuNode { IsFolder: true })
            sel.Nodes.Insert(sel.Nodes.Cast<TreeNode>().LastOrDefault(n => n.Tag is MenuNode { IsFolder: true }) is { } last ? last.Index + 1 : 0, folder);   // 資料夾排在畫面前面
        else if (sel?.Tag is MenuNode) (sel.Parent?.Nodes ?? treeMenu.Nodes).Insert(sel.Index + 1, folder);
        else treeMenu.Nodes.Insert(unlistedFolder?.Index ?? treeMenu.Nodes.Count, folder);
        SaveMenuFromTree($"新增資料夾「{v[0]}」", folder.FullPath);
    }

    /// <summary>刪除資料夾：裡面的畫面、子資料夾移到資料夾原本的位置（畫面不會被刪除）</summary>
    private void DeleteFolder()
    {
        if (!SysContext.User.IsAdmin || treeMenu.SelectedNode is not { Tag: MenuNode { IsFolder: true } node } folder) return;
        if (folder.Nodes.Count > 0 &&
            MessageBox.Show($"「{node.Title}」裡面的 {folder.Nodes.Count} 個項目會移到資料夾原本的位置（畫面不會被刪除），確定要刪除這個資料夾嗎？", "刪除資料夾",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var siblings = folder.Parent?.Nodes ?? treeMenu.Nodes;
        string parentPath = folder.Parent?.FullPath;
        int index = folder.Index;
        var children = folder.Nodes.Cast<TreeNode>().ToList();
        folder.Remove();
        foreach (var child in children)
        {
            child.Remove();
            siblings.Insert(index++, child);
        }
        SaveMenuFromTree($"刪除資料夾「{node.Title}」", parentPath);
    }

    /// <summary>
    /// 刪除畫面（右鍵或 Delete）：從系統刪除已發布的畫面，選單、角色權限一起拿掉；本機 Forms 資料夾的畫面定義檔不刪（還可以重新發布）。
    /// 別的畫面的按鈕還指到這個畫面的，確認時一起列出來
    /// </summary>
    private void DeleteFormSelected()
    {
        if (!SysContext.User.IsAdmin || treeMenu.SelectedNode?.Tag is not MenuNode { IsFolder: false } node) return;
        string key = node.FormKey;
        try
        {
            Cursor = Cursors.WaitCursor;
            var refs = FormsReferencing(key, SysDb.LoadPublished(SysContext.Connection, "form"));
            Cursor = Cursors.Default;
            string message = $"確定要從系統刪除畫面「{node.Title}」（{key}）嗎？"
                             + Environment.NewLine + Environment.NewLine + "選單、角色權限的設定會一起拿掉，無法復原；本機的畫面定義檔不會刪除，之後還可以重新發布。"
                             + (refs.Count == 0 ? "" : Environment.NewLine + Environment.NewLine + "這些畫面的按鈕還會開啟這個畫面，刪除後按了會找不到："
                                                       + Environment.NewLine + string.Join(Environment.NewLine, refs.Select(r => "　" + r)));
            if (MessageBox.Show(message, "刪除畫面", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string select = treeMenu.SelectedNode.Parent?.FullPath;
            SysDb.DeleteForm(SysContext.Connection, key);
            Audit.Write(key, "SYS_Form", "刪除畫面", "FormKey=" + key, $"「{node.Title}」（{key}）");
            SysContext.ReloadPermissions();
            LoadMenu(select);
        }
        catch (Exception ex)
        {
            MessageBox.Show("刪除畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ================= 管理者：全部發布 =================

    /// <summary>
    /// 全部發布：本機 Forms 資料夾裡的畫面定義檔（*.json，列印樣板 *.print.json 除外）全部發布到系統，
    /// 代號 = 檔名，按鈕用到的列印樣板一起發布（同畫面設計的「發布到系統」）；內容跟系統裡已發布的一樣就略過，不增加版本
    /// </summary>
    private void PublishAll()
    {
        if (!SysContext.User.IsAdmin) return;
        string folder = FormDefinition.DefaultFolder;
        var files = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.json").Where(f => !f.EndsWith(".print.json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList()
            : new List<string>();
        if (files.Count == 0)
        {
            MessageBox.Show($"{folder} 裡沒有畫面定義檔。", "全部發布");
            return;
        }
        if (MessageBox.Show($"要把 {folder} 裡的 {files.Count} 個畫面定義檔全部發布到系統嗎？{Environment.NewLine}{Environment.NewLine}"
                            + "代號 = 檔名；內容跟系統裡一樣的略過。第一次發布的畫面會出現在「未放進選單的畫面」。",
                            "全部發布", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var cs = SysContext.Connection;
        var done = new List<string>();
        var failed = new List<string>();
        var notes = new List<string>();
        int unchanged = 0;
        try
        {
            Cursor = Cursors.WaitCursor;
            foreach (var file in files)
            {
                string key = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var form = FormDefinition.Load(file);
                    // 跟發布時一樣先套用標題（沒改標題就用檔名），再跟系統裡的內容比較
                    var publishing = FormDefinition.FromJson(form.ToJson());
                    publishing.Title = DesignerForm.PublishTitle(form, key);
                    if (SysDb.LoadContent(cs, key) == publishing.ToJson())
                    {
                        unchanged++;
                        continue;
                    }
                    done.Add($"「{publishing.Title}」（{key}）第 {DesignerForm.PublishOne(cs, key, form, file, notes)} 版");
                }
                catch (Exception ex)
                {
                    failed.Add($"{key}：{ex.Message}");
                }
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        LoadMenu();
        string NewLines(IEnumerable<string> lines) => string.Join(Environment.NewLine, lines.Select(x => "　" + x));
        MessageBox.Show($"已發布 {done.Count} 個畫面，{unchanged} 個沒有變更略過" + (failed.Count > 0 ? $"，{failed.Count} 個失敗" : "") + "。"
                        + (done.Count > 0 ? Environment.NewLine + Environment.NewLine + NewLines(done) : "")
                        + (failed.Count > 0 ? Environment.NewLine + Environment.NewLine + "失敗：" + Environment.NewLine + NewLines(failed) : "")
                        + (notes.Count > 0 ? Environment.NewLine + Environment.NewLine + "注意：" + Environment.NewLine + NewLines(notes.Distinct()) : ""),
                        "全部發布", MessageBoxButtons.OK, failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    // ================= 管理者：重新命名 =================

    /// <summary>
    /// 重新命名（右鍵或 F2）：資料夾只改名稱；畫面可以改名稱（選單、畫面標題）和代號（檔名，權限、選單跟著改），
    /// 本機 Forms 資料夾裡的畫面定義檔也一起改名。別的畫面的按鈕、開窗選取指到舊代號的，列出來提醒
    /// </summary>
    private void RenameSelected()
    {
        if (!SysContext.User.IsAdmin || treeMenu.SelectedNode?.Tag is not MenuNode node) return;
        if (node.IsFolder)
        {
            var folder = PromptDialog.Ask(this, "重新命名資料夾", v => v[0] == "" ? "請輸入名稱。" : null, new PromptDialog.Field("名稱", node.Title));
            if (folder == null || folder[0] == node.Title) return;
            string old = node.Title;
            node.Title = folder[0];
            SaveMenuFromTree($"資料夾「{old}」改名為「{node.Title}」");
            return;
        }

        string oldKey = node.FormKey;
        var published = SysDb.LoadPublished(SysContext.Connection, "form");
        var item = published.FirstOrDefault(p => p.FormKey.Equals(oldKey, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            MessageBox.Show($"系統裡沒有已發布的畫面「{oldKey}」。", "重新命名");
            return;
        }
        var bad = Path.GetInvalidFileNameChars();
        var v = PromptDialog.Ask(this, "重新命名畫面", values =>
        {
            if (values[0] == "") return "請輸入名稱。";
            if (values[1] == "") return "請輸入代號。";
            if (values[1].IndexOfAny(bad) >= 0) return "代號是檔名，不能有 \\ / : * ? \" < > | 這些符號。";
            if (values[1].Length > 200) return "代號太長（最多 200 個字）。";
            if (!values[1].Equals(oldKey, StringComparison.OrdinalIgnoreCase)
                && published.Any(p => p.FormKey.Equals(values[1], StringComparison.OrdinalIgnoreCase)))
                return $"代號「{values[1]}」已經有別的畫面在用了。";
            return null;
        }, new PromptDialog.Field("名稱", item.Title ?? oldKey), new PromptDialog.Field("代號（檔名）", oldKey));
        if (v == null) return;
        string title = v[0], newKey = v[1];
        if (title == (item.Title ?? oldKey) && newKey == oldKey) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            SysDb.RenameForm(SysContext.Connection, oldKey, newKey, title);
            Audit.Write(newKey, null, "重新命名", "FormKey=" + oldKey, $"「{item.Title ?? oldKey}」（{oldKey}）→「{title}」（{newKey}）");
            string note = RenameLocalFile(oldKey, newKey, title);
            var refs = newKey.Equals(oldKey, StringComparison.OrdinalIgnoreCase) ? new List<string>() : FormsReferencing(oldKey, published);
            SysContext.ReloadPermissions();
            LoadMenu();
            if (note != null || refs.Count > 0)
                MessageBox.Show("已重新命名。" + (note == null ? "" : Environment.NewLine + Environment.NewLine + note)
                                + (refs.Count == 0 ? "" : Environment.NewLine + Environment.NewLine + $"這些畫面的按鈕或開窗選取還指到舊代號「{oldKey}」，請到畫面設計改成「{newKey}」後重新發布："
                                                          + Environment.NewLine + string.Join(Environment.NewLine, refs.Select(r => "　" + r))),
                                "重新命名");
        }
        catch (Exception ex)
        {
            MessageBox.Show("重新命名失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ================= 管理者：複製畫面 =================

    /// <summary>
    /// 複製畫面（右鍵）：用新的名稱、代號複製一份，發布到系統，放在選單上原本畫面的下面，角色權限照原本的畫面給；
    /// 本機 Forms 資料夾也存一份新代號的畫面定義檔（之後用「設計這個畫面」修改）。
    /// 內容用本機的畫面定義檔（比系統上的新時）或系統上已發布的版本，跟「設計這個畫面」打開的是同一份
    /// </summary>
    private void CopySelected()
    {
        if (!SysContext.User.IsAdmin || treeMenu.SelectedNode is not { Tag: MenuNode { IsFolder: false } node } source) return;
        string oldKey = node.FormKey;
        string folder = FormDefinition.DefaultFolder;
        var published = SysDb.LoadPublished(SysContext.Connection, "form");
        var item = published.FirstOrDefault(p => p.FormKey.Equals(oldKey, StringComparison.OrdinalIgnoreCase));
        string localPath = Path.Combine(folder, oldKey + ".json");
        bool useLocal = File.Exists(localPath) && (item == null || item.PublishedAt <= File.GetLastWriteTime(localPath).AddMinutes(1));
        if (item == null && !useLocal)
        {
            MessageBox.Show($"找不到畫面「{oldKey}」：系統裡沒有發布，本機也沒有 {localPath}。", "複製畫面");
            return;
        }

        // 預設代號：原代號-複製、原代號-複製2…（不跟系統上、本機的畫面重複）
        bool Taken(string key) => published.Any(p => p.FormKey.Equals(key, StringComparison.OrdinalIgnoreCase)) || File.Exists(Path.Combine(folder, key + ".json"));
        string suggested = oldKey + "-複製";
        for (int i = 2; Taken(suggested); i++) suggested = $"{oldKey}-複製{i}";
        var bad = Path.GetInvalidFileNameChars();
        var v = PromptDialog.Ask(this, "複製畫面", values =>
        {
            if (values[0] == "") return "請輸入名稱。";
            if (values[1] == "") return "請輸入代號。";
            if (values[1].IndexOfAny(bad) >= 0) return "代號是檔名，不能有 \\ / : * ? \" < > | 這些符號。";
            if (values[1].Length > 200) return "代號太長（最多 200 個字）。";
            if (published.Any(p => p.FormKey.Equals(values[1], StringComparison.OrdinalIgnoreCase))) return $"代號「{values[1]}」已經有別的畫面在用了。";
            if (File.Exists(Path.Combine(folder, values[1] + ".json"))) return $"本機已經有 {values[1]}.json，請換一個代號。";
            return null;
        }, new PromptDialog.Field("名稱", (item?.Title ?? node.Title ?? oldKey) + "（複製）"), new PromptDialog.Field("代號（檔名）", suggested));
        if (v == null) return;
        string title = v[0], newKey = v[1];

        try
        {
            Cursor = Cursors.WaitCursor;
            var def = useLocal ? FormDefinition.Load(localPath) : FormDefinition.FromJson(SysDb.LoadContent(SysContext.Connection, oldKey));
            def.Title = title;
            Directory.CreateDirectory(folder);
            string newPath = Path.Combine(folder, newKey + ".json");
            def.Save(newPath);
            var notes = new List<string>();
            DesignerForm.PublishOne(SysContext.Connection, newKey, def, newPath, notes);
            SysDb.CopyPermissions(SysContext.Connection, oldKey, newKey);
            Audit.Write(newKey, def.TableName, "複製畫面", "FormKey=" + oldKey, $"「{item?.Title ?? oldKey}」（{oldKey}）→「{title}」（{newKey}）");
            SysContext.ReloadPermissions();

            // 選單上放在原本畫面的下面（原本在「未放進選單的畫面」裡就放在那裡）
            var copy = new TreeNode(title) { Tag = new MenuNode { Title = title, FormKey = newKey } };
            (source.Parent?.Nodes ?? treeMenu.Nodes).Insert(source.Index + 1, copy);
            SaveMenuFromTree($"複製畫面「{item?.Title ?? oldKey}」→「{title}」", copy.FullPath);
            if (notes.Count > 0) MessageBox.Show("已複製。" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, notes.Distinct()), "複製畫面");
        }
        catch (Exception ex)
        {
            MessageBox.Show("複製畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>本機 Forms 資料夾裡的畫面定義檔（畫面設計上次編輯的）跟著改檔名與標題；有問題時回傳提醒文字</summary>
    private static string RenameLocalFile(string oldKey, string newKey, string title)
    {
        string folder = FormDefinition.DefaultFolder;
        string oldPath = Path.Combine(folder, oldKey + ".json"), newPath = Path.Combine(folder, newKey + ".json");
        if (!File.Exists(oldPath)) return null;
        try
        {
            bool sameFile = oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase);
            if (!sameFile && File.Exists(newPath)) return $"本機已經有 {newPath}，舊的 {Path.GetFileName(oldPath)} 沒有改名，請自己處理。";
            var def = FormDefinition.Load(oldPath);
            def.Title = title;
            def.Save(oldPath);
            if (!sameFile) File.Move(oldPath, newPath);    // 只改大小寫時不用改檔名（Windows 檔名不分大小寫）
            return null;
        }
        catch (Exception ex)
        {
            return $"本機的畫面定義檔 {oldPath} 沒有跟著改：{ex.Message}";
        }
    }

    /// <summary>已發布的其他畫面裡，按鈕「開啟畫面」或欄位「開窗選取」指到這個代號的（回傳「名稱（代號）」）</summary>
    private static List<string> FormsReferencing(string key, List<PublishedItem> published)
    {
        var result = new List<string>();
        foreach (var p in published.Where(p => !p.FormKey.Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var def = FormDefinition.FromJson(SysDb.LoadContent(SysContext.Connection, p.FormKey));
                if (DesignerForm.AllButtons(def).Any(b => b.OpensForm && !string.IsNullOrWhiteSpace(b.TargetForm)
                                                          && Path.GetFileNameWithoutExtension(b.TargetForm.Trim()).Equals(key, StringComparison.OrdinalIgnoreCase)))
                    result.Add($"{p.Title ?? p.FormKey}（{p.FormKey}）");
            }
            catch
            {
                // 讀不出來的畫面略過
            }
        }
        return result;
    }

    /// <summary>
    /// 系統管理 → 畫面設計：選單上選的是畫面時，直接打開那個畫面的設計（讀本機上次編輯的 json，沒有就讀已發布的版本）；
    /// 沒選畫面就開空白的設計器
    /// </summary>
    private void OpenDesigner()
    {
        var designer = new DesignerForm();
        designer.Show();
        if (treeMenu.SelectedNode?.Tag is MenuNode { IsFolder: false } node)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                designer.OpenForEditing(node.FormKey);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    private void OpenSelected()
    {
        if (treeMenu.SelectedNode?.Tag is not MenuNode { IsFolder: false } node) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            var def = FormStore.LoadForm(node.FormKey, null);
            new RuntimeForm(def).Show(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
