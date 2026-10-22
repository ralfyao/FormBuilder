namespace ERPClient
{
    partial class ErpShellForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.header = new System.Windows.Forms.Panel();
            this.lblProduct = new System.Windows.Forms.Label();
            this.lblUser = new System.Windows.Forms.Label();
            this.btnPassword = new FormBuilder.RoundButton();
            this.btnLogout = new FormBuilder.RoundButton();
            this.split = new System.Windows.Forms.SplitContainer();
            this.treeMenu = new System.Windows.Forms.TreeView();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lblMenuCaption = new System.Windows.Forms.Label();
            this.menuCaptionBar = new System.Windows.Forms.Panel();
            this.btnCollapse = new System.Windows.Forms.Button();
            this.collapsedBar = new System.Windows.Forms.Panel();
            this.btnExpand = new System.Windows.Forms.Button();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabHome = new System.Windows.Forms.TabPage();
            this.lblWelcome = new System.Windows.Forms.Label();
            this.lblHint = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblServer = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblVersion = new System.Windows.Forms.ToolStripStatusLabel();
            this.header.SuspendLayout();
            this.menuCaptionBar.SuspendLayout();
            this.collapsedBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabHome.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // header（上方標題列）
            //
            this.header.BackColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.header.Controls.Add(this.lblProduct);
            this.header.Controls.Add(this.lblUser);
            this.header.Controls.Add(this.btnPassword);
            this.header.Controls.Add(this.btnLogout);
            this.header.Dock = System.Windows.Forms.DockStyle.Top;
            this.header.Name = "header";
            this.header.Padding = new System.Windows.Forms.Padding(14, 8, 14, 8);
            this.header.Size = new System.Drawing.Size(1280, 52);
            this.header.TabIndex = 0;
            //
            // lblProduct
            //
            this.lblProduct.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblProduct.Font = new System.Drawing.Font("Microsoft JhengHei UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblProduct.ForeColor = System.Drawing.Color.White;
            this.lblProduct.Name = "lblProduct";
            this.lblProduct.Size = new System.Drawing.Size(300, 36);
            this.lblProduct.Text = "ERP 系統";
            this.lblProduct.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblUser
            //
            this.lblUser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUser.ForeColor = System.Drawing.Color.White;
            this.lblUser.Name = "lblUser";
            this.lblUser.Padding = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // btnPassword
            //
            this.btnPassword.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnPassword.Name = "btnPassword";
            this.btnPassword.Size = new System.Drawing.Size(96, 36);
            this.btnPassword.TabStop = false;
            this.btnPassword.Text = "變更密碼";
            //
            // btnLogout
            //
            this.btnLogout.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Size = new System.Drawing.Size(80, 36);
            this.btnLogout.TabStop = false;
            this.btnLogout.Text = "登出";
            //
            // split（左：選單；右：分頁）
            //
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.split.Name = "split";
            //
            // split.Panel1
            //
            this.split.Panel1.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            this.split.Panel1.Controls.Add(this.treeMenu);
            this.split.Panel1.Controls.Add(this.txtSearch);
            this.split.Panel1.Controls.Add(this.menuCaptionBar);
            this.split.Panel1.Padding = new System.Windows.Forms.Padding(8);
            //
            // split.Panel2
            //
            this.split.Panel2.Controls.Add(this.tabs);
            this.split.Size = new System.Drawing.Size(1280, 670);
            this.split.SplitterDistance = 260;
            this.split.TabIndex = 1;
            //
            // treeMenu
            //
            this.treeMenu.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            this.treeMenu.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeMenu.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10.5F);
            this.treeMenu.FullRowSelect = true;
            this.treeMenu.HideSelection = false;
            this.treeMenu.ItemHeight = 30;
            this.treeMenu.Name = "treeMenu";
            this.treeMenu.ShowLines = false;
            this.treeMenu.TabIndex = 1;
            //
            // txtSearch
            //
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.PlaceholderText = "搜尋功能…";
            this.txtSearch.TabIndex = 0;
            //
            // menuCaptionBar（「功能選單」標題 + 右邊的 << 收起按鈕）
            //
            this.menuCaptionBar.Controls.Add(this.lblMenuCaption);
            this.menuCaptionBar.Controls.Add(this.btnCollapse);
            this.menuCaptionBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.menuCaptionBar.Name = "menuCaptionBar";
            this.menuCaptionBar.Size = new System.Drawing.Size(244, 28);
            //
            // btnCollapse
            //
            this.btnCollapse.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCollapse.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCollapse.FlatAppearance.BorderSize = 0;
            this.btnCollapse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCollapse.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnCollapse.ForeColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.btnCollapse.Name = "btnCollapse";
            this.btnCollapse.Size = new System.Drawing.Size(32, 28);
            this.btnCollapse.TabStop = false;
            this.btnCollapse.Text = "<<";
            //
            // collapsedBar（選單收起時左邊留的窄條，按 >> 展開）
            //
            this.collapsedBar.BackColor = System.Drawing.Color.FromArgb(243, 246, 250);
            this.collapsedBar.Controls.Add(this.btnExpand);
            this.collapsedBar.Dock = System.Windows.Forms.DockStyle.Left;
            this.collapsedBar.Name = "collapsedBar";
            this.collapsedBar.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.collapsedBar.Size = new System.Drawing.Size(32, 670);
            this.collapsedBar.Visible = false;
            //
            // btnExpand
            //
            this.btnExpand.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnExpand.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnExpand.FlatAppearance.BorderSize = 0;
            this.btnExpand.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExpand.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnExpand.ForeColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.btnExpand.Name = "btnExpand";
            this.btnExpand.Size = new System.Drawing.Size(32, 28);
            this.btnExpand.TabStop = false;
            this.btnExpand.Text = ">>";
            //
            // lblMenuCaption
            //
            this.lblMenuCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMenuCaption.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblMenuCaption.ForeColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.lblMenuCaption.Name = "lblMenuCaption";
            this.lblMenuCaption.Size = new System.Drawing.Size(244, 26);
            this.lblMenuCaption.Text = "功能選單";
            this.lblMenuCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tabs（開啟的畫面；分頁右邊的 × 關閉）
            //
            this.tabs.Controls.Add(this.tabHome);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabs.ItemSize = new System.Drawing.Size(150, 28);
            this.tabs.Name = "tabs";
            this.tabs.Padding = new System.Drawing.Point(18, 4);
            this.tabs.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabs.TabIndex = 0;
            //
            // tabHome
            //
            this.tabHome.BackColor = System.Drawing.Color.White;
            this.tabHome.Controls.Add(this.lblHint);
            this.tabHome.Controls.Add(this.lblWelcome);
            this.tabHome.Name = "tabHome";
            this.tabHome.Padding = new System.Windows.Forms.Padding(40);
            this.tabHome.Text = "首頁";
            //
            // lblWelcome
            //
            this.lblWelcome.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblWelcome.Font = new System.Drawing.Font("Microsoft JhengHei UI", 20F, System.Drawing.FontStyle.Bold);
            this.lblWelcome.ForeColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.lblWelcome.Name = "lblWelcome";
            this.lblWelcome.Size = new System.Drawing.Size(900, 60);
            this.lblWelcome.Text = "歡迎";
            //
            // lblHint
            //
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHint.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.lblHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblHint.Name = "lblHint";
            this.lblHint.Text = "從左邊的功能選單雙擊開啟畫面，每個畫面會開在上方的分頁。\r\n"
                + "按分頁上的 × 或 Ctrl+W 關閉目前的畫面，Ctrl+Tab 切換分頁。\r\n"
                + "看不到需要的功能時，請洽系統管理者設定權限。";
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblServer,
            this.lblVersion});
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.TabIndex = 2;
            //
            // lblServer
            //
            this.lblServer.ForeColor = System.Drawing.Color.DimGray;
            this.lblServer.Name = "lblServer";
            this.lblServer.Spring = true;
            this.lblServer.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblVersion
            //
            this.lblVersion.ForeColor = System.Drawing.Color.DimGray;
            this.lblVersion.Name = "lblVersion";
            //
            // ErpShellForm
            //
            this.ClientSize = new System.Drawing.Size(1280, 744);
            this.Controls.Add(this.split);
            this.Controls.Add(this.collapsedBar);
            this.Controls.Add(this.header);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.KeyPreview = true;
            this.Name = "ErpShellForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ERP 系統";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.header.ResumeLayout(false);
            this.menuCaptionBar.ResumeLayout(false);
            this.collapsedBar.ResumeLayout(false);
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel1.PerformLayout();
            this.split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.tabHome.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel header;
        private System.Windows.Forms.Label lblProduct;
        private System.Windows.Forms.Label lblUser;
        private FormBuilder.RoundButton btnPassword;
        private FormBuilder.RoundButton btnLogout;
        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.TreeView treeMenu;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblMenuCaption;
        private System.Windows.Forms.Panel menuCaptionBar;
        private System.Windows.Forms.Button btnCollapse;
        private System.Windows.Forms.Panel collapsedBar;
        private System.Windows.Forms.Button btnExpand;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabHome;
        private System.Windows.Forms.Label lblWelcome;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblServer;
        private System.Windows.Forms.ToolStripStatusLabel lblVersion;
    }
}
