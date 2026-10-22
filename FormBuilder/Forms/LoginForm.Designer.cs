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
            this.pnlBanner = new System.Windows.Forms.Panel();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.lblUser = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblPwd = new System.Windows.Forms.Label();
            this.txtPwd = new System.Windows.Forms.TextBox();
            this.btnLogin = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.lnkSetup = new System.Windows.Forms.LinkLabel();
            this.lblServer = new System.Windows.Forms.Label();
            this.lblFooter = new System.Windows.Forms.Label();
            this.pnlBanner.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();
            //
            // pnlBanner
            //
            this.pnlBanner.BackColor = System.Drawing.Color.FromArgb(31, 58, 96);
            this.pnlBanner.Controls.Add(this.picLogo);
            this.pnlBanner.Controls.Add(this.lblTitle);
            this.pnlBanner.Controls.Add(this.lblSubtitle);
            this.pnlBanner.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlBanner.Location = new System.Drawing.Point(0, 0);
            this.pnlBanner.Name = "pnlBanner";
            this.pnlBanner.Size = new System.Drawing.Size(380, 92);
            //
            // picLogo
            //
            this.picLogo.Location = new System.Drawing.Point(16, 14);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(64, 64);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.Visible = false;
            //
            // lblTitle
            //
            this.lblTitle.AutoEllipsis = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(16, 26);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(348, 40);
            this.lblTitle.Text = "ERP Studio";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoEllipsis = true;
            this.lblSubtitle.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.lblSubtitle.ForeColor = System.Drawing.Color.White;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 56);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(348, 24);
            this.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblSubtitle.Visible = false;
            //
            // lblUser
            //
            this.lblUser.Location = new System.Drawing.Point(30, 112);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(60, 26);
            this.lblUser.Text = "帳號";
            this.lblUser.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtUser
            //
            this.txtUser.Location = new System.Drawing.Point(98, 113);
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(240, 23);
            this.txtUser.TabIndex = 0;
            //
            // lblPwd
            //
            this.lblPwd.Location = new System.Drawing.Point(30, 150);
            this.lblPwd.Name = "lblPwd";
            this.lblPwd.Size = new System.Drawing.Size(60, 26);
            this.lblPwd.Text = "密碼";
            this.lblPwd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtPwd
            //
            this.txtPwd.Location = new System.Drawing.Point(98, 151);
            this.txtPwd.Name = "txtPwd";
            this.txtPwd.Size = new System.Drawing.Size(240, 23);
            this.txtPwd.TabIndex = 1;
            this.txtPwd.UseSystemPasswordChar = true;
            //
            // btnLogin
            //
            this.btnLogin.Location = new System.Drawing.Point(140, 194);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(96, 32);
            this.btnLogin.TabIndex = 2;
            this.btnLogin.Text = "登入";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(242, 194);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(96, 32);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "離開";
            //
            // lnkSetup
            //
            this.lnkSetup.Location = new System.Drawing.Point(28, 200);
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
            this.lblServer.Location = new System.Drawing.Point(20, 240);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(340, 20);
            this.lblServer.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // lblFooter
            //
            this.lblFooter.AutoEllipsis = true;
            this.lblFooter.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFooter.Location = new System.Drawing.Point(20, 262);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(340, 20);
            this.lblFooter.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblFooter.Visible = false;
            //
            // LoginForm
            //
            this.AcceptButton = this.btnLogin;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 270);
            this.Controls.Add(this.pnlBanner);
            this.Controls.Add(this.lblUser);
            this.Controls.Add(this.txtUser);
            this.Controls.Add(this.lblPwd);
            this.Controls.Add(this.txtPwd);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lnkSetup);
            this.Controls.Add(this.lblServer);
            this.Controls.Add(this.lblFooter);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "登入";
            this.pnlBanner.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel pnlBanner;
        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.Label lblPwd;
        private System.Windows.Forms.TextBox txtPwd;
        private FormBuilder.RoundButton btnLogin;
        private FormBuilder.RoundButton btnCancel;
        private System.Windows.Forms.LinkLabel lnkSetup;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.Label lblFooter;
    }
}
