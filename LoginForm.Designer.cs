namespace FormBuilder
{
    partial class LoginForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblUser = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblPwd = new System.Windows.Forms.Label();
            this.txtPwd = new System.Windows.Forms.TextBox();
            this.btnLogin = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.lnkSetup = new System.Windows.Forms.LinkLabel();
            this.lblServer = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(20, 18);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(340, 40);
            this.lblTitle.Text = "ERP Studio";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // lblUser
            //
            this.lblUser.Location = new System.Drawing.Point(30, 78);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(60, 26);
            this.lblUser.Text = "帳號";
            this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtUser
            //
            this.txtUser.Location = new System.Drawing.Point(98, 79);
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(240, 23);
            this.txtUser.TabIndex = 0;
            //
            // lblPwd
            //
            this.lblPwd.Location = new System.Drawing.Point(30, 116);
            this.lblPwd.Name = "lblPwd";
            this.lblPwd.Size = new System.Drawing.Size(60, 26);
            this.lblPwd.Text = "密碼";
            this.lblPwd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtPwd
            //
            this.txtPwd.Location = new System.Drawing.Point(98, 117);
            this.txtPwd.Name = "txtPwd";
            this.txtPwd.Size = new System.Drawing.Size(240, 23);
            this.txtPwd.TabIndex = 1;
            this.txtPwd.UseSystemPasswordChar = true;
            //
            // btnLogin
            //
            this.btnLogin.Location = new System.Drawing.Point(140, 160);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(96, 32);
            this.btnLogin.TabIndex = 2;
            this.btnLogin.Text = "登入";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(242, 160);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(96, 32);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "離開";
            //
            // lnkSetup
            //
            this.lnkSetup.Location = new System.Drawing.Point(28, 166);
            this.lnkSetup.Name = "lnkSetup";
            this.lnkSetup.Size = new System.Drawing.Size(80, 22);
            this.lnkSetup.TabIndex = 4;
            this.lnkSetup.TabStop = true;
            this.lnkSetup.Text = "系統設定…";
            //
            // lblServer
            //
            this.lblServer.AutoEllipsis = true;
            this.lblServer.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblServer.Location = new System.Drawing.Point(20, 206);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(340, 20);
            this.lblServer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // LoginForm
            //
            this.AcceptButton = this.btnLogin;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 236);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblUser);
            this.Controls.Add(this.txtUser);
            this.Controls.Add(this.lblPwd);
            this.Controls.Add(this.txtPwd);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lnkSetup);
            this.Controls.Add(this.lblServer);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "登入";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.Label lblPwd;
        private System.Windows.Forms.TextBox txtPwd;
        private FormBuilder.RoundButton btnLogin;
        private FormBuilder.RoundButton btnCancel;
        private System.Windows.Forms.LinkLabel lnkSetup;
        private System.Windows.Forms.Label lblServer;
    }
}
