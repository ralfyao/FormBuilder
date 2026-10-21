namespace FormBuilder
{
    partial class UserAdminForm
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
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabUsers = new System.Windows.Forms.TabPage();
            this.dgvUsers = new System.Windows.Forms.DataGridView();
            this.userRight = new System.Windows.Forms.Panel();
            this.clbRoles = new System.Windows.Forms.CheckedListBox();
            this.lblRolesHint = new System.Windows.Forms.Label();
            this.userButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnNewUser = new FormBuilder.RoundButton();
            this.btnResetPwd = new FormBuilder.RoundButton();
            this.btnSaveUsers = new FormBuilder.RoundButton();
            this.tabRoles = new System.Windows.Forms.TabPage();
            this.dgvPerms = new System.Windows.Forms.DataGridView();
            this.roleLeft = new System.Windows.Forms.Panel();
            this.lstRoles = new System.Windows.Forms.ListBox();
            this.roleButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnNewRole = new FormBuilder.RoundButton();
            this.btnRenameRole = new FormBuilder.RoundButton();
            this.btnDeleteRole = new FormBuilder.RoundButton();
            this.permButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAllPerms = new FormBuilder.RoundButton();
            this.btnNoPerms = new FormBuilder.RoundButton();
            this.btnSavePerms = new FormBuilder.RoundButton();
            this.lblPermHint = new System.Windows.Forms.Label();
            this.tabs.SuspendLayout();
            this.tabUsers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvUsers)).BeginInit();
            this.userRight.SuspendLayout();
            this.userButtons.SuspendLayout();
            this.tabRoles.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPerms)).BeginInit();
            this.roleLeft.SuspendLayout();
            this.roleButtons.SuspendLayout();
            this.permButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabUsers);
            this.tabs.Controls.Add(this.tabRoles);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Name = "tabs";
            this.tabs.TabIndex = 0;
            //
            // tabUsers
            //
            this.tabUsers.Controls.Add(this.dgvUsers);
            this.tabUsers.Controls.Add(this.userRight);
            this.tabUsers.Controls.Add(this.userButtons);
            this.tabUsers.Name = "tabUsers";
            this.tabUsers.Padding = new System.Windows.Forms.Padding(6);
            this.tabUsers.Text = "使用者";
            //
            // dgvUsers
            //
            this.dgvUsers.AllowUserToAddRows = false;
            this.dgvUsers.AllowUserToDeleteRows = false;
            this.dgvUsers.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvUsers.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvUsers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvUsers.MultiSelect = false;
            this.dgvUsers.Name = "dgvUsers";
            this.dgvUsers.RowHeadersVisible = false;
            this.dgvUsers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvUsers.TabIndex = 0;
            //
            // userRight
            //
            this.userRight.Controls.Add(this.clbRoles);
            this.userRight.Controls.Add(this.lblRolesHint);
            this.userRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.userRight.Name = "userRight";
            this.userRight.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.userRight.Size = new System.Drawing.Size(230, 400);
            this.userRight.TabIndex = 1;
            //
            // clbRoles
            //
            this.clbRoles.CheckOnClick = true;
            this.clbRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clbRoles.IntegralHeight = false;
            this.clbRoles.Name = "clbRoles";
            this.clbRoles.TabIndex = 0;
            //
            // lblRolesHint
            //
            this.lblRolesHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblRolesHint.Name = "lblRolesHint";
            this.lblRolesHint.Size = new System.Drawing.Size(222, 40);
            this.lblRolesHint.Text = "選取的使用者屬於哪些角色（勾選後立即生效；管理者不受角色限制）";
            //
            // userButtons
            //
            this.userButtons.Controls.Add(this.btnNewUser);
            this.userButtons.Controls.Add(this.btnResetPwd);
            this.userButtons.Controls.Add(this.btnSaveUsers);
            this.userButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.userButtons.Name = "userButtons";
            this.userButtons.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.userButtons.Size = new System.Drawing.Size(800, 44);
            this.userButtons.TabIndex = 2;
            //
            // btnNewUser
            //
            this.btnNewUser.Name = "btnNewUser";
            this.btnNewUser.Size = new System.Drawing.Size(110, 32);
            this.btnNewUser.Text = "新增使用者…";
            //
            // btnResetPwd
            //
            this.btnResetPwd.Name = "btnResetPwd";
            this.btnResetPwd.Size = new System.Drawing.Size(130, 32);
            this.btnResetPwd.Text = "重設密碼 / 解鎖…";
            //
            // btnSaveUsers
            //
            this.btnSaveUsers.Name = "btnSaveUsers";
            this.btnSaveUsers.Size = new System.Drawing.Size(150, 32);
            this.btnSaveUsers.Text = "儲存姓名 / 管理者 / 啟用";
            //
            // tabRoles
            //
            this.tabRoles.Controls.Add(this.dgvPerms);
            this.tabRoles.Controls.Add(this.lblPermHint);
            this.tabRoles.Controls.Add(this.permButtons);
            this.tabRoles.Controls.Add(this.roleLeft);
            this.tabRoles.Name = "tabRoles";
            this.tabRoles.Padding = new System.Windows.Forms.Padding(6);
            this.tabRoles.Text = "角色與權限";
            //
            // dgvPerms
            //
            this.dgvPerms.AllowUserToAddRows = false;
            this.dgvPerms.AllowUserToDeleteRows = false;
            this.dgvPerms.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvPerms.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPerms.Name = "dgvPerms";
            this.dgvPerms.RowHeadersVisible = false;
            this.dgvPerms.TabIndex = 0;
            //
            // lblPermHint
            //
            this.lblPermHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblPermHint.Name = "lblPermHint";
            this.lblPermHint.Size = new System.Drawing.Size(560, 24);
            this.lblPermHint.Text = "左邊選角色，勾選這個角色對每個已發布畫面的權限（使用者有多個角色時取聯集）";
            //
            // permButtons
            //
            this.permButtons.Controls.Add(this.btnAllPerms);
            this.permButtons.Controls.Add(this.btnNoPerms);
            this.permButtons.Controls.Add(this.btnSavePerms);
            this.permButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.permButtons.Name = "permButtons";
            this.permButtons.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.permButtons.Size = new System.Drawing.Size(560, 44);
            this.permButtons.TabIndex = 1;
            //
            // btnAllPerms
            //
            this.btnAllPerms.Name = "btnAllPerms";
            this.btnAllPerms.Size = new System.Drawing.Size(90, 32);
            this.btnAllPerms.Text = "全部勾選";
            //
            // btnNoPerms
            //
            this.btnNoPerms.Name = "btnNoPerms";
            this.btnNoPerms.Size = new System.Drawing.Size(90, 32);
            this.btnNoPerms.Text = "全部取消";
            //
            // btnSavePerms
            //
            this.btnSavePerms.Name = "btnSavePerms";
            this.btnSavePerms.Size = new System.Drawing.Size(110, 32);
            this.btnSavePerms.Text = "儲存權限";
            //
            // roleLeft
            //
            this.roleLeft.Controls.Add(this.lstRoles);
            this.roleLeft.Controls.Add(this.roleButtons);
            this.roleLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.roleLeft.Name = "roleLeft";
            this.roleLeft.Padding = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.roleLeft.Size = new System.Drawing.Size(220, 400);
            this.roleLeft.TabIndex = 2;
            //
            // lstRoles
            //
            this.lstRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstRoles.IntegralHeight = false;
            this.lstRoles.Name = "lstRoles";
            this.lstRoles.TabIndex = 0;
            //
            // roleButtons
            //
            this.roleButtons.Controls.Add(this.btnNewRole);
            this.roleButtons.Controls.Add(this.btnRenameRole);
            this.roleButtons.Controls.Add(this.btnDeleteRole);
            this.roleButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.roleButtons.Name = "roleButtons";
            this.roleButtons.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.roleButtons.Size = new System.Drawing.Size(212, 44);
            this.roleButtons.TabIndex = 1;
            //
            // btnNewRole
            //
            this.btnNewRole.Name = "btnNewRole";
            this.btnNewRole.Size = new System.Drawing.Size(64, 32);
            this.btnNewRole.Text = "新增";
            //
            // btnRenameRole
            //
            this.btnRenameRole.Name = "btnRenameRole";
            this.btnRenameRole.Size = new System.Drawing.Size(64, 32);
            this.btnRenameRole.Text = "改名";
            //
            // btnDeleteRole
            //
            this.btnDeleteRole.Name = "btnDeleteRole";
            this.btnDeleteRole.Size = new System.Drawing.Size(64, 32);
            this.btnDeleteRole.Text = "刪除";
            //
            // UserAdminForm
            //
            this.ClientSize = new System.Drawing.Size(900, 560);
            this.Controls.Add(this.tabs);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "UserAdminForm";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "使用者、角色與權限";
            this.tabs.ResumeLayout(false);
            this.tabUsers.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvUsers)).EndInit();
            this.userRight.ResumeLayout(false);
            this.userButtons.ResumeLayout(false);
            this.tabRoles.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPerms)).EndInit();
            this.roleLeft.ResumeLayout(false);
            this.roleButtons.ResumeLayout(false);
            this.permButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabUsers;
        private System.Windows.Forms.DataGridView dgvUsers;
        private System.Windows.Forms.Panel userRight;
        private System.Windows.Forms.CheckedListBox clbRoles;
        private System.Windows.Forms.Label lblRolesHint;
        private System.Windows.Forms.FlowLayoutPanel userButtons;
        private FormBuilder.RoundButton btnNewUser;
        private FormBuilder.RoundButton btnResetPwd;
        private FormBuilder.RoundButton btnSaveUsers;
        private System.Windows.Forms.TabPage tabRoles;
        private System.Windows.Forms.DataGridView dgvPerms;
        private System.Windows.Forms.Panel roleLeft;
        private System.Windows.Forms.ListBox lstRoles;
        private System.Windows.Forms.FlowLayoutPanel roleButtons;
        private FormBuilder.RoundButton btnNewRole;
        private FormBuilder.RoundButton btnRenameRole;
        private FormBuilder.RoundButton btnDeleteRole;
        private System.Windows.Forms.FlowLayoutPanel permButtons;
        private FormBuilder.RoundButton btnAllPerms;
        private FormBuilder.RoundButton btnNoPerms;
        private FormBuilder.RoundButton btnSavePerms;
        private System.Windows.Forms.Label lblPermHint;
    }
}
