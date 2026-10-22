namespace FormBuilder
{
    partial class MainMenuForm
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
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnOpen = new System.Windows.Forms.ToolStripButton();
            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.sepAdmin = new System.Windows.Forms.ToolStripSeparator();
            this.btnAdmin = new System.Windows.Forms.ToolStripDropDownButton();
            this.miDesigner = new System.Windows.Forms.ToolStripMenuItem();
            this.miUsers = new System.Windows.Forms.ToolStripMenuItem();
            this.miMenuEditor = new System.Windows.Forms.ToolStripMenuItem();
            this.miAudit = new System.Windows.Forms.ToolStripMenuItem();
            this.miBranding = new System.Windows.Forms.ToolStripMenuItem();
            this.miSetup = new System.Windows.Forms.ToolStripMenuItem();
            this.sepUser = new System.Windows.Forms.ToolStripSeparator();
            this.btnPassword = new System.Windows.Forms.ToolStripButton();
            this.btnLogout = new System.Windows.Forms.ToolStripButton();
            this.treeMenu = new System.Windows.Forms.TreeView();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblUser = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblVersion = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStrip
            //
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnOpen,
            this.btnRefresh,
            this.sepAdmin,
            this.btnAdmin,
            this.sepUser,
            this.btnPassword,
            this.btnLogout});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Padding = new System.Windows.Forms.Padding(4);
            this.toolStrip.Size = new System.Drawing.Size(484, 33);
            this.toolStrip.TabIndex = 0;
            //
            // btnOpen
            //
            this.btnOpen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Text = "開啟";
            //
            // btnRefresh
            //
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Text = "重新整理";
            //
            // sepAdmin
            //
            this.sepAdmin.Name = "sepAdmin";
            //
            // btnAdmin
            //
            this.btnAdmin.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnAdmin.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miDesigner,
            this.miUsers,
            this.miMenuEditor,
            this.miAudit,
            this.miBranding,
            this.miSetup});
            this.btnAdmin.Name = "btnAdmin";
            this.btnAdmin.Text = "系統管理";
            //
            // miDesigner
            //
            this.miDesigner.Name = "miDesigner";
            this.miDesigner.Text = "畫面設計（先選選單上的畫面就直接開啟它；設計完用「檔案 → 發布到系統」）";
            //
            // miUsers
            //
            this.miUsers.Name = "miUsers";
            this.miUsers.Text = "使用者、角色與權限";
            //
            // miMenuEditor
            //
            this.miMenuEditor.Name = "miMenuEditor";
            this.miMenuEditor.Text = "選單管理";
            //
            // miAudit
            //
            this.miAudit.Name = "miAudit";
            this.miAudit.Text = "稽核紀錄";
            //
            // miBranding
            //
            this.miBranding.Name = "miBranding";
            this.miBranding.Text = "登入畫面（企業識別：標題、標誌、圖示、顏色）";
            //
            // miSetup
            //
            this.miSetup.Name = "miSetup";
            this.miSetup.Text = "系統設定（資料庫連線、更新來源）";
            //
            // sepUser
            //
            this.sepUser.Name = "sepUser";
            //
            // btnPassword
            //
            this.btnPassword.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPassword.Name = "btnPassword";
            this.btnPassword.Text = "變更密碼";
            //
            // btnLogout
            //
            this.btnLogout.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Text = "登出";
            //
            // treeMenu
            //
            this.treeMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeMenu.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F);
            this.treeMenu.FullRowSelect = true;
            this.treeMenu.HideSelection = false;
            this.treeMenu.ItemHeight = 28;
            this.treeMenu.Location = new System.Drawing.Point(0, 33);
            this.treeMenu.Name = "treeMenu";
            this.treeMenu.ShowLines = false;
            this.treeMenu.Size = new System.Drawing.Size(484, 606);
            this.treeMenu.TabIndex = 1;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblUser,
            this.lblVersion});
            this.statusStrip.Location = new System.Drawing.Point(0, 639);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(484, 22);
            this.statusStrip.TabIndex = 2;
            //
            // lblUser
            //
            this.lblUser.Name = "lblUser";
            this.lblUser.Spring = true;
            this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblVersion
            //
            this.lblVersion.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblVersion.Name = "lblVersion";
            //
            // MainMenuForm
            //
            this.ClientSize = new System.Drawing.Size(484, 661);
            this.Controls.Add(this.treeMenu);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.Name = "MainMenuForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ERP Studio";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnOpen;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripSeparator sepAdmin;
        private System.Windows.Forms.ToolStripDropDownButton btnAdmin;
        private System.Windows.Forms.ToolStripMenuItem miDesigner;
        private System.Windows.Forms.ToolStripMenuItem miUsers;
        private System.Windows.Forms.ToolStripMenuItem miMenuEditor;
        private System.Windows.Forms.ToolStripMenuItem miAudit;
        private System.Windows.Forms.ToolStripMenuItem miBranding;
        private System.Windows.Forms.ToolStripMenuItem miSetup;
        private System.Windows.Forms.ToolStripSeparator sepUser;
        private System.Windows.Forms.ToolStripButton btnPassword;
        private System.Windows.Forms.ToolStripButton btnLogout;
        private System.Windows.Forms.TreeView treeMenu;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblUser;
        private System.Windows.Forms.ToolStripStatusLabel lblVersion;
    }
}
