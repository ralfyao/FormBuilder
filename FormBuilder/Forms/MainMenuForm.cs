namespace FormBuilder;

/// <summary>
/// 登入後的主選單：依「選單管理」排的樹狀選單，只列出有「開啟」權限的畫面（管理者全部看得到），雙擊開啟。
/// 管理者另外有「系統管理」：畫面設計、使用者與權限、選單管理、稽核紀錄、系統設定
/// </summary>
public partial class MainMenuForm : Form
{
    private readonly SysConfig config;

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
            nodeMenu.Items.Add("開啟", null, (s, e) => OpenSelected());
            nodeMenu.Items.Add("設計這個畫面", null, (s, e) => OpenDesigner());
            nodeMenu.Opening += (s, e) => e.Cancel = treeMenu.SelectedNode?.Tag is not MenuNode { IsFolder: false };
            treeMenu.NodeMouseClick += (s, e) => { if (e.Button == MouseButtons.Right) treeMenu.SelectedNode = e.Node; };
            treeMenu.ContextMenuStrip = nodeMenu;
        }
        miUsers.Click += (s, e) => { using var dlg = new UserAdminForm(); dlg.ShowDialog(this); };
        miMenuEditor.Click += (s, e) => { using var dlg = new MenuEditorForm(); if (dlg.ShowDialog(this) == DialogResult.OK) LoadMenu(); };
        miAudit.Click += (s, e) => new AuditLogForm().Show(this);
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
                        if (folder.Nodes.Count > 0) target.Add(folder);
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
                if (others.Count > 0)
                {
                    var folder = new TreeNode("📁 （未放進選單的畫面）") { ForeColor = SystemColors.GrayText };
                    foreach (var p in others) folder.Nodes.Add(new TreeNode(p.Title ?? p.FormKey) { Tag = new MenuNode { Title = p.Title ?? p.FormKey, FormKey = p.FormKey } });
                    treeMenu.Nodes.Add(folder);
                }
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
