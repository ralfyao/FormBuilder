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
        // 管理者：選單上按右鍵 → 開啟 / 設計這個畫面
        if (user.IsAdmin)
        {
            var nodeMenu = new ContextMenuStrip();
            var miOpen = nodeMenu.Items.Add("開啟", null, (s, e) => OpenSelected());
            var miDesign = nodeMenu.Items.Add("設計這個畫面", null, (s, e) => OpenDesigner());
            var miRename = nodeMenu.Items.Add("重新命名…", null, (s, e) => RenameSelected());
            var sepNew = new ToolStripSeparator();
            nodeMenu.Items.Add(sepNew);
            // 新增表單：開空白的畫面設計（設計好用「檔案 → 發布到系統」，再拖進選單的資料夾）
            nodeMenu.Items.Add("新增表單", null, (s, e) => new DesignerForm().Show());
            nodeMenu.Opening += (s, e) =>
            {
                // 在空白處按右鍵：取消選取，選單只會出現「新增表單」
                var point = treeMenu.PointToClient(MousePosition);
                if (treeMenu.ClientRectangle.Contains(point) && treeMenu.GetNodeAt(point) == null) treeMenu.SelectedNode = null;
                // 開啟、設計只對畫面；新增表單在哪裡按右鍵都有
                bool onForm = treeMenu.SelectedNode?.Tag is MenuNode { IsFolder: false };
                miOpen.Visible = miDesign.Visible = onForm;
                miRename.Visible = sepNew.Visible = treeMenu.SelectedNode?.Tag is MenuNode;     // 畫面、資料夾都可以改名
            };
            treeMenu.KeyDown += (s, e) => { if (e.KeyCode == Keys.F2) RenameSelected(); };
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

    /// <summary>載入選單：沒有開啟權限的畫面不顯示，裡面沒有畫面的資料夾也不顯示</summary>
    private void LoadMenu()
    {
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
                // 一直顯示（沒有畫面時也顯示），把畫面拖到這裡 = 從選單拿掉
                unlistedFolder = new TreeNode("📁 （未放進選單的畫面）") { ForeColor = SystemColors.GrayText };
                foreach (var p in others) unlistedFolder.Nodes.Add(new TreeNode(p.Title ?? p.FormKey) { Tag = new MenuNode { Title = p.Title ?? p.FormKey, FormKey = p.FormKey } });
                treeMenu.Nodes.Add(unlistedFolder);
            }
            treeMenu.ExpandAll();
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
    /// 拖到「未放進選單的畫面」= 從選單拿掉；放開就存進系統資料庫（同「選單管理」按儲存）
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
            else if (InUnlisted(target)) unlistedFolder.Nodes.Add(dragged);                                      // 從選單拿掉
            else if (target.Tag is MenuNode { IsFolder: true }) target.Nodes.Add(dragged);
            else (target.Parent?.Nodes ?? treeMenu.Nodes).Insert(target.Index + 1, dragged);
            SaveMenuFromTree($"移動「{node.Title}」");
        };
    }

    private bool InUnlisted(TreeNode node)
    {
        for (var p = node; p != null; p = p.Parent) if (p == unlistedFolder) return true;
        return false;
    }

    /// <summary>不能拖到自己或自己的子節點裡；資料夾不能拖到「未放進選單的畫面」</summary>
    private bool CanDrop(TreeNode dragged, TreeNode target)
    {
        for (var p = target; p != null; p = p.Parent) if (p == dragged) return false;
        if (target != null && target.Tag is not MenuNode && target != unlistedFolder) return false;   // 提示文字之類的
        if (target != null && InUnlisted(target) && ((MenuNode)dragged.Tag).IsFolder) return false;
        return true;
    }

    /// <summary>依目前的樹存回選單（「未放進選單的畫面」不存），存完重新載入</summary>
    private void SaveMenuFromTree(string action)
    {
        List<MenuTree> Build(TreeNodeCollection nodes) =>
            nodes.Cast<TreeNode>().Where(tn => tn != unlistedFolder && tn.Tag is MenuNode).Select(tn =>
            {
                var tree = new MenuTree { Node = (MenuNode)tn.Tag };
                tree.Children.AddRange(Build(tn.Nodes));
                return tree;
            }).ToList();
        try
        {
            SysDb.SaveMenu(SysContext.Connection, Build(treeMenu.Nodes));
            Audit.Write(null, "SYS_Menu", "修改選單", null, action);
        }
        catch (Exception ex)
        {
            MessageBox.Show("儲存選單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        LoadMenu();
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
