using FormBuilder;

namespace ERPClient;

/// <summary>
/// ERP 系統的外框：上方使用者資訊、左邊依權限顯示的功能選單（可以搜尋）、右邊用分頁開啟各個畫面。
/// 畫面就是後台設計、發布到系統資料庫的 RuntimeForm，權限（新增、編修、刪除、核准、報表、輸出）在畫面裡照樣生效
/// </summary>
public partial class ErpShellForm : Form
{
    private readonly SysConfig config;
    private List<MenuNode> menu = new();

    /// <summary>true = 使用者按了「登出」（回到登入畫面），false = 直接關閉程式</summary>
    public bool LoggedOut { get; private set; }

    public ErpShellForm(SysConfig config)
    {
        InitializeComponent();
        this.config = config;
        var user = SysContext.User;
        Text = Program.ProductName;
        lblProduct.Text = Program.ProductName;
        lblUser.Text = $"{user.UserName}（{user.UserId}）";
        lblWelcome.Text = $"歡迎，{user.UserName}";
        // 使用者端不顯示資料庫連線（伺服器、資料庫、帳號），只顯示登入時間
        lblServer.Text = $"登入時間：{DateTime.Now:yyyy/MM/dd HH:mm}";
        lblVersion.Text = "版本 " + Updater.CurrentVersion;
        ApplyBranding();

        // ---- 選單 ----
        treeMenu.NodeMouseDoubleClick += (s, e) => OpenNode(e.Node);
        treeMenu.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenNode(treeMenu.SelectedNode); };
        txtSearch.TextChanged += (s, e) => FillTree();
        txtSearch.KeyDown += (s, e) =>
        {
            // 搜尋框按 Enter：只找到一個功能時直接開啟
            if (e.KeyCode != Keys.Enter) return;
            var leaves = AllNodes(treeMenu.Nodes).Where(n => n.Tag is MenuNode { IsFolder: false }).ToList();
            if (leaves.Count == 1) OpenNode(leaves[0]);
            else treeMenu.Focus();
            e.SuppressKeyPress = true;
        };
        // << 收起選單（畫面變寬），左邊留窄條，按 >> 再叫出來
        var tips = new ToolTip();
        tips.SetToolTip(btnCollapse, "收起功能選單");
        tips.SetToolTip(btnExpand, "展開功能選單");
        btnCollapse.Click += (s, e) => SetMenuCollapsed(true);
        btnExpand.Click += (s, e) => SetMenuCollapsed(false);

        // ---- 分頁：自己畫標題和 ×，點 × 關閉 ----
        tabs.DrawItem += (s, e) => DrawTab(e);
        tabs.MouseDown += (s, e) =>
        {
            for (int i = 1; i < tabs.TabPages.Count; i++)          // 首頁不能關
            {
                if (e.Button == MouseButtons.Middle && tabs.GetTabRect(i).Contains(e.Location)) { CloseTab(tabs.TabPages[i]); return; }
                if (e.Button == MouseButtons.Left && CloseBox(tabs.GetTabRect(i)).Contains(e.Location)) { CloseTab(tabs.TabPages[i]); return; }
            }
        };

        // ---- 使用者 ----
        btnPassword.Click += (s, e) => LoginForm.ChangePassword(this, SysContext.Connection, user.UserId, forced: false);
        btnLogout.Click += (s, e) =>
        {
            LoggedOut = true;
            Close();
        };
        FormClosing += (s, e) =>
        {
            // 一個一個關掉開著的畫面（畫面有沒存的輸入時會自己問；使用者取消就不關）
            foreach (var page in tabs.TabPages.Cast<TabPage>().Skip(1).ToList())
            {
                if (!CloseTab(page))
                {
                    LoggedOut = false;
                    e.Cancel = true;
                    return;
                }
            }
            Audit.Write(null, null, "登出", Program.ProductName);
        };

        // 畫面上的「開啟畫面」按鈕：開在分頁裡
        ButtonRunner.OpenInShell = (def, parameters) => OpenForm(def, parameters);
        FormClosed += (s, e) => ButtonRunner.OpenInShell = null;

        LoadMenu();
    }

    /// <summary>基調顏色（選單資料夾、標題文字、分頁上的色條），跟登入畫面的橫幅顏色一樣</summary>
    private Color accent = LoginBranding.DefaultBanner;

    /// <summary>
    /// 套用登入畫面設定的企業識別：上方橫幅的底色、文字顏色、標題、標誌，以及整個畫面的基調顏色。
    /// 讀不到系統資料庫的設定時，用登入畫面快取在這台電腦的那一份
    /// </summary>
    private void ApplyBranding()
    {
        LoginBranding b;
        try { b = LoginBranding.Load(SysContext.Connection); }
        catch (Exception) { b = LoginBranding.FromJson(config.LoginBrandingCache); }

        accent = b.Banner;
        header.BackColor = accent;
        lblProduct.ForeColor = lblUser.ForeColor = b.Text;
        if (!string.IsNullOrWhiteSpace(b.Title)) Text = lblProduct.Text = b.Title.Trim();
        lblProduct.Width = Math.Max(lblProduct.Width, TextRenderer.MeasureText(lblProduct.Text, lblProduct.Font).Width + 16);
        if (b.LogoImage() is { } logo)
        {
            var pic = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Left, Width = 44, Padding = new Padding(0, 0, 8, 0) };
            header.Controls.Add(pic);
            pic.SendToBack();          // 停靠順序排最前面 = 放在標題左邊
        }
        lblUser.BringToFront();        // Fill 要最後停靠，才只佔剩下的空間，不會蓋到標題

        // 選單區的淡色背景跟著基調顏色微微帶色
        var light = Blend(accent, Color.White, 0.94);
        split.Panel1.BackColor = treeMenu.BackColor = collapsedBar.BackColor = light;
        lblMenuCaption.ForeColor = btnCollapse.ForeColor = btnExpand.ForeColor = lblWelcome.ForeColor = accent;
    }

    private static Color Blend(Color a, Color b, double toB) =>
        Color.FromArgb((int)(a.R + (b.R - a.R) * toB), (int)(a.G + (b.G - a.G) * toB), (int)(a.B + (b.B - a.B) * toB));

    /// <summary>Ctrl+W 關閉目前的分頁、Ctrl+F 搜尋功能</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.W) && tabs.SelectedIndex > 0) { CloseTab(tabs.SelectedTab); return true; }
        if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); txtSearch.SelectAll(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ================= 選單 =================

    private void LoadMenu()
    {
        try
        {
            menu = SysDb.LoadMenu(SysContext.Connection);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取選單失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        FillTree();
    }

    /// <summary>
    /// 依權限與搜尋文字建立選單樹：沒有開啟權限的畫面不顯示，裡面沒有畫面的資料夾不顯示；
    /// 有搜尋文字時，只顯示標題符合的畫面（連同它所在的資料夾）
    /// </summary>
    private void FillTree()
    {
        string keyword = txtSearch.Text.Trim();
        treeMenu.BeginUpdate();
        treeMenu.Nodes.Clear();
        void Add(TreeNodeCollection target, int? parentId)
        {
            foreach (var n in menu.Where(n => n.ParentId == parentId))
            {
                if (n.IsFolder)
                {
                    var folder = new TreeNode(n.Title) { Tag = n, NodeFont = new Font(treeMenu.Font, FontStyle.Bold), ForeColor = accent };
                    Add(folder.Nodes, n.Id);
                    if (folder.Nodes.Count > 0) target.Add(folder);
                }
                else if (SysContext.PermissionFor(n.FormKey).Open && (keyword == "" || n.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    target.Add(new TreeNode("　" + n.Title) { Tag = n });
                }
            }
        }
        Add(treeMenu.Nodes, null);
        treeMenu.ExpandAll();
        if (treeMenu.Nodes.Count == 0)
            treeMenu.Nodes.Add(new TreeNode(keyword == "" ? "（沒有可以使用的功能，請洽系統管理者）" : "（找不到符合的功能）") { ForeColor = SystemColors.GrayText });
        treeMenu.EndUpdate();
    }

    private static IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes) =>
        nodes.Cast<TreeNode>().SelectMany(n => new[] { n }.Concat(AllNodes(n.Nodes)));

    /// <summary>收起 / 展開左邊的功能選單：收起時選單區隱藏，左邊留一條有 >> 的窄條</summary>
    private void SetMenuCollapsed(bool collapsed)
    {
        split.Panel1Collapsed = collapsed;
        collapsedBar.Visible = collapsed;
        if (!collapsed) txtSearch.Focus();
    }

    private void OpenNode(TreeNode node)
    {
        if (node?.Tag is not MenuNode { IsFolder: false } item) return;
        // 同一個畫面已經開著：切過去
        var existing = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Tag as string == item.FormKey);
        if (existing != null)
        {
            tabs.SelectedTab = existing;
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            var def = FormStore.LoadForm(item.FormKey, null);
            if (!string.IsNullOrWhiteSpace(item.Title)) def.Title = item.Title;      // 分頁標題用選單上的名稱
            OpenForm(def, null);
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

    // ================= 分頁 =================

    /// <summary>把畫面開在新的分頁裡（畫面本身是 RuntimeForm，嵌進分頁、去掉外框）</summary>
    private void OpenForm(FormDefinition def, IDictionary<string, object> parameters)
    {
        if (!SysContext.PermissionFor(def.FormKey).Open)
        {
            MessageBox.Show($"你沒有開啟「{def.Title}」的權限，請洽系統管理者。", Text);
            return;
        }
        var form = new RuntimeForm(def, parameters, pickMode: false)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        var page = new TabPage(def.Title) { Tag = def.FormKey, ToolTipText = def.Title, BackColor = SystemColors.Control };
        page.Controls.Add(form);
        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;
        form.FormClosed += (s, e) =>
        {
            if (tabs.TabPages.Contains(page)) tabs.TabPages.Remove(page);
            page.Dispose();
        };
        form.Show();
    }

    /// <summary>關閉分頁（畫面有沒存的輸入時會先問）；回傳 true = 關掉了</summary>
    private bool CloseTab(TabPage page)
    {
        var form = page.Controls.OfType<RuntimeForm>().FirstOrDefault();
        if (form == null)
        {
            tabs.TabPages.Remove(page);
            return true;
        }
        form.Close();
        return form.IsDisposed || !tabs.TabPages.Contains(page);
    }

    private static Rectangle CloseBox(Rectangle tab) => new(tab.Right - 20, tab.Top + (tab.Height - 14) / 2, 14, 14);

    private void DrawTab(DrawItemEventArgs e)
    {
        var page = tabs.TabPages[e.Index];
        var rect = tabs.GetTabRect(e.Index);
        bool selected = e.Index == tabs.SelectedIndex;
        using (var back = new SolidBrush(selected ? Color.White : Blend(accent, Color.White, 0.9))) e.Graphics.FillRectangle(back, rect);
        if (selected) using (var bar = new SolidBrush(accent)) e.Graphics.FillRectangle(bar, rect.Left, rect.Top, rect.Width, 3);
        var textRect = new Rectangle(rect.Left + 8, rect.Top, rect.Width - (e.Index == 0 ? 16 : 30), rect.Height);
        TextRenderer.DrawText(e.Graphics, page.Text, e.Font, textRect, selected ? Color.Black : Color.DimGray,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (e.Index > 0)
            TextRenderer.DrawText(e.Graphics, "×", e.Font, CloseBox(rect), Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
