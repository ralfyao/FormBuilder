namespace FormBuilder
{
    partial class SysSetupForm
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
            this.lblIntro = new System.Windows.Forms.Label();
            this.lblConnCaption = new System.Windows.Forms.Label();
            this.txtConn = new System.Windows.Forms.TextBox();
            this.btnPickConn = new FormBuilder.RoundButton();
            this.grpAdmin = new System.Windows.Forms.GroupBox();
            this.lblAdminId = new System.Windows.Forms.Label();
            this.txtAdminId = new System.Windows.Forms.TextBox();
            this.lblAdminName = new System.Windows.Forms.Label();
            this.txtAdminName = new System.Windows.Forms.TextBox();
            this.lblPwd = new System.Windows.Forms.Label();
            this.txtPwd = new System.Windows.Forms.TextBox();
            this.lblPwd2 = new System.Windows.Forms.Label();
            this.txtPwd2 = new System.Windows.Forms.TextBox();
            this.lblUpdate = new System.Windows.Forms.Label();
            this.txtUpdate = new System.Windows.Forms.TextBox();
            this.btnOk = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.grpAdmin.SuspendLayout();
            this.SuspendLayout();
            //
            // lblIntro
            //
            this.lblIntro.Location = new System.Drawing.Point(16, 12);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(528, 62);
            this.lblIntro.Text = "第一次使用請設定「系統資料庫」：存放使用者、角色、權限、選單、已發布的畫面與稽核紀錄（目前只支援 SQL Server）。"
                + "請先在 SQL Server 建一個空的資料庫（例如 ERPStudio_SYS），選擇後會自動建立需要的資料表。";
            //
            // lblConnCaption
            //
            this.lblConnCaption.Location = new System.Drawing.Point(16, 84);
            this.lblConnCaption.Name = "lblConnCaption";
            this.lblConnCaption.Size = new System.Drawing.Size(90, 26);
            this.lblConnCaption.Text = "系統資料庫";
            this.lblConnCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtConn
            //
            this.txtConn.Location = new System.Drawing.Point(108, 85);
            this.txtConn.Name = "txtConn";
            this.txtConn.ReadOnly = true;
            this.txtConn.Size = new System.Drawing.Size(320, 23);
            this.txtConn.TabIndex = 0;
            this.txtConn.PlaceholderText = "（尚未設定）";
            //
            // btnPickConn
            //
            this.btnPickConn.Location = new System.Drawing.Point(436, 82);
            this.btnPickConn.Name = "btnPickConn";
            this.btnPickConn.Size = new System.Drawing.Size(108, 30);
            this.btnPickConn.TabIndex = 1;
            this.btnPickConn.Text = "設定連線…";
            //
            // grpAdmin
            //
            this.grpAdmin.Controls.Add(this.lblAdminId);
            this.grpAdmin.Controls.Add(this.txtAdminId);
            this.grpAdmin.Controls.Add(this.lblAdminName);
            this.grpAdmin.Controls.Add(this.txtAdminName);
            this.grpAdmin.Controls.Add(this.lblPwd);
            this.grpAdmin.Controls.Add(this.txtPwd);
            this.grpAdmin.Controls.Add(this.lblPwd2);
            this.grpAdmin.Controls.Add(this.txtPwd2);
            this.grpAdmin.Location = new System.Drawing.Point(16, 124);
            this.grpAdmin.Name = "grpAdmin";
            this.grpAdmin.Size = new System.Drawing.Size(528, 150);
            this.grpAdmin.TabIndex = 2;
            this.grpAdmin.TabStop = false;
            this.grpAdmin.Text = "建立系統管理者帳號（系統資料庫還沒有任何帳號時才需要）";
            //
            // lblAdminId
            //
            this.lblAdminId.Location = new System.Drawing.Point(12, 28);
            this.lblAdminId.Name = "lblAdminId";
            this.lblAdminId.Size = new System.Drawing.Size(80, 23);
            this.lblAdminId.Text = "帳號";
            this.lblAdminId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtAdminId
            //
            this.txtAdminId.Location = new System.Drawing.Point(98, 28);
            this.txtAdminId.Name = "txtAdminId";
            this.txtAdminId.Size = new System.Drawing.Size(160, 23);
            this.txtAdminId.TabIndex = 0;
            this.txtAdminId.Text = "admin";
            //
            // lblAdminName
            //
            this.lblAdminName.Location = new System.Drawing.Point(264, 28);
            this.lblAdminName.Name = "lblAdminName";
            this.lblAdminName.Size = new System.Drawing.Size(60, 23);
            this.lblAdminName.Text = "姓名";
            this.lblAdminName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtAdminName
            //
            this.txtAdminName.Location = new System.Drawing.Point(330, 28);
            this.txtAdminName.Name = "txtAdminName";
            this.txtAdminName.Size = new System.Drawing.Size(180, 23);
            this.txtAdminName.TabIndex = 1;
            this.txtAdminName.Text = "系統管理者";
            //
            // lblPwd
            //
            this.lblPwd.Location = new System.Drawing.Point(12, 66);
            this.lblPwd.Name = "lblPwd";
            this.lblPwd.Size = new System.Drawing.Size(80, 23);
            this.lblPwd.Text = "密碼";
            this.lblPwd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtPwd
            //
            this.txtPwd.Location = new System.Drawing.Point(98, 66);
            this.txtPwd.Name = "txtPwd";
            this.txtPwd.Size = new System.Drawing.Size(160, 23);
            this.txtPwd.TabIndex = 2;
            this.txtPwd.UseSystemPasswordChar = true;
            //
            // lblPwd2
            //
            this.lblPwd2.Location = new System.Drawing.Point(264, 66);
            this.lblPwd2.Name = "lblPwd2";
            this.lblPwd2.Size = new System.Drawing.Size(60, 23);
            this.lblPwd2.Text = "確認密碼";
            this.lblPwd2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtPwd2
            //
            this.txtPwd2.Location = new System.Drawing.Point(330, 66);
            this.txtPwd2.Name = "txtPwd2";
            this.txtPwd2.Size = new System.Drawing.Size(180, 23);
            this.txtPwd2.TabIndex = 3;
            this.txtPwd2.UseSystemPasswordChar = true;
            //
            // lblUpdate
            //
            this.lblUpdate.Location = new System.Drawing.Point(16, 288);
            this.lblUpdate.Name = "lblUpdate";
            this.lblUpdate.Size = new System.Drawing.Size(140, 23);
            this.lblUpdate.Text = "更新來源（可空白）";
            this.lblUpdate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtUpdate
            //
            this.txtUpdate.Location = new System.Drawing.Point(160, 288);
            this.txtUpdate.Name = "txtUpdate";
            this.txtUpdate.PlaceholderText = @"例如 \\server\ERPStudio\release，啟動時自動檢查新版";
            this.txtUpdate.Size = new System.Drawing.Size(384, 23);
            this.txtUpdate.TabIndex = 3;
            //
            // btnOk
            //
            this.btnOk.Location = new System.Drawing.Point(338, 330);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(100, 32);
            this.btnOk.TabIndex = 4;
            this.btnOk.Text = "完成設定";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(444, 330);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "取消";
            //
            // SysSetupForm
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(560, 378);
            this.Controls.Add(this.lblIntro);
            this.Controls.Add(this.lblConnCaption);
            this.Controls.Add(this.txtConn);
            this.Controls.Add(this.btnPickConn);
            this.Controls.Add(this.grpAdmin);
            this.Controls.Add(this.lblUpdate);
            this.Controls.Add(this.txtUpdate);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SysSetupForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ERP Studio 系統設定";
            this.grpAdmin.ResumeLayout(false);
            this.grpAdmin.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.Label lblConnCaption;
        private System.Windows.Forms.TextBox txtConn;
        private FormBuilder.RoundButton btnPickConn;
        private System.Windows.Forms.GroupBox grpAdmin;
        private System.Windows.Forms.Label lblAdminId;
        private System.Windows.Forms.TextBox txtAdminId;
        private System.Windows.Forms.Label lblAdminName;
        private System.Windows.Forms.TextBox txtAdminName;
        private System.Windows.Forms.Label lblPwd;
        private System.Windows.Forms.TextBox txtPwd;
        private System.Windows.Forms.Label lblPwd2;
        private System.Windows.Forms.TextBox txtPwd2;
        private System.Windows.Forms.Label lblUpdate;
        private System.Windows.Forms.TextBox txtUpdate;
        private FormBuilder.RoundButton btnOk;
        private FormBuilder.RoundButton btnCancel;
    }
}
