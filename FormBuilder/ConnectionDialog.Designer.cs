namespace FormBuilder
{
    partial class ConnectionDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.layout = new System.Windows.Forms.TableLayoutPanel();
            this.lblKind = new System.Windows.Forms.Label();
            this.cboKind = new System.Windows.Forms.ComboBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblDatabase = new System.Windows.Forms.Label();
            this.dbRow = new System.Windows.Forms.FlowLayoutPanel();
            this.cboDatabase = new System.Windows.Forms.ComboBox();
            this.btnListDb = new System.Windows.Forms.Button();
            this.lblAuth = new System.Windows.Forms.Label();
            this.authRow = new System.Windows.Forms.FlowLayoutPanel();
            this.rbSql = new System.Windows.Forms.RadioButton();
            this.rbWindows = new System.Windows.Forms.RadioButton();
            this.lblUser = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.buttonRow = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnTest = new System.Windows.Forms.Button();
            this.layout.SuspendLayout();
            this.dbRow.SuspendLayout();
            this.authRow.SuspendLayout();
            this.buttonRow.SuspendLayout();
            this.SuspendLayout();
            //
            // layout
            //
            this.layout.AutoSize = true;
            this.layout.ColumnCount = 2;
            this.layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.layout.Controls.Add(this.lblKind, 0, 0);
            this.layout.Controls.Add(this.cboKind, 1, 0);
            this.layout.Controls.Add(this.lblServer, 0, 1);
            this.layout.Controls.Add(this.txtServer, 1, 1);
            this.layout.Controls.Add(this.lblDatabase, 0, 2);
            this.layout.Controls.Add(this.dbRow, 1, 2);
            this.layout.Controls.Add(this.lblAuth, 0, 3);
            this.layout.Controls.Add(this.authRow, 1, 3);
            this.layout.Controls.Add(this.lblUser, 0, 4);
            this.layout.Controls.Add(this.txtUser, 1, 4);
            this.layout.Controls.Add(this.lblPassword, 0, 5);
            this.layout.Controls.Add(this.txtPassword, 1, 5);
            this.layout.Controls.Add(this.buttonRow, 0, 6);
            this.layout.Location = new System.Drawing.Point(10, 10);
            this.layout.Name = "layout";
            this.layout.RowCount = 7;
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.layout.SetColumnSpan(this.buttonRow, 2);
            this.layout.Size = new System.Drawing.Size(360, 230);
            this.layout.TabIndex = 0;
            //
            // lblKind
            //
            this.lblKind.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblKind.AutoSize = true;
            this.lblKind.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblKind.Name = "lblKind";
            this.lblKind.Text = "資料庫類型";
            //
            // cboKind
            //
            this.cboKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboKind.Name = "cboKind";
            this.cboKind.Size = new System.Drawing.Size(180, 23);
            this.cboKind.TabIndex = 0;
            //
            // lblServer
            //
            this.lblServer.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblServer.AutoSize = true;
            this.lblServer.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblServer.Name = "lblServer";
            this.lblServer.Text = "伺服器";
            //
            // txtServer
            //
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(260, 23);
            this.txtServer.TabIndex = 1;
            //
            // lblDatabase
            //
            this.lblDatabase.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblDatabase.AutoSize = true;
            this.lblDatabase.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblDatabase.Name = "lblDatabase";
            this.lblDatabase.Text = "資料庫";
            //
            // dbRow
            //
            this.dbRow.AutoSize = true;
            this.dbRow.Controls.Add(this.cboDatabase);
            this.dbRow.Controls.Add(this.btnListDb);
            this.dbRow.Margin = new System.Windows.Forms.Padding(0);
            this.dbRow.Name = "dbRow";
            this.dbRow.TabIndex = 2;
            //
            // cboDatabase
            //
            this.cboDatabase.Name = "cboDatabase";
            this.cboDatabase.Size = new System.Drawing.Size(180, 23);
            this.cboDatabase.TabIndex = 0;
            //
            // btnListDb
            //
            this.btnListDb.AutoSize = true;
            this.btnListDb.Name = "btnListDb";
            this.btnListDb.TabIndex = 1;
            this.btnListDb.Text = "列出資料庫";
            //
            // lblAuth
            //
            this.lblAuth.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAuth.AutoSize = true;
            this.lblAuth.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblAuth.Name = "lblAuth";
            this.lblAuth.Text = "驗證方式";
            //
            // authRow
            //
            this.authRow.AutoSize = true;
            this.authRow.Controls.Add(this.rbSql);
            this.authRow.Controls.Add(this.rbWindows);
            this.authRow.Margin = new System.Windows.Forms.Padding(0);
            this.authRow.Name = "authRow";
            this.authRow.TabIndex = 3;
            //
            // rbSql
            //
            this.rbSql.AutoSize = true;
            this.rbSql.Checked = true;
            this.rbSql.Name = "rbSql";
            this.rbSql.TabIndex = 0;
            this.rbSql.TabStop = true;
            this.rbSql.Text = "資料庫帳號";
            //
            // rbWindows
            //
            this.rbWindows.AutoSize = true;
            this.rbWindows.Name = "rbWindows";
            this.rbWindows.TabIndex = 1;
            this.rbWindows.Text = "Windows 驗證";
            //
            // lblUser
            //
            this.lblUser.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUser.AutoSize = true;
            this.lblUser.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblUser.Name = "lblUser";
            this.lblUser.Text = "帳號";
            //
            // txtUser
            //
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(260, 23);
            this.txtUser.TabIndex = 4;
            //
            // lblPassword
            //
            this.lblPassword.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPassword.AutoSize = true;
            this.lblPassword.Margin = new System.Windows.Forms.Padding(3, 8, 10, 3);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Text = "密碼";
            //
            // txtPassword
            //
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(260, 23);
            this.txtPassword.TabIndex = 5;
            this.txtPassword.UseSystemPasswordChar = true;
            //
            // buttonRow
            //
            this.buttonRow.AutoSize = true;
            this.buttonRow.Controls.Add(this.btnCancel);
            this.buttonRow.Controls.Add(this.btnOk);
            this.buttonRow.Controls.Add(this.btnTest);
            this.buttonRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.buttonRow.Name = "buttonRow";
            this.buttonRow.TabIndex = 6;
            //
            // btnCancel
            //
            this.btnCancel.AutoSize = true;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "取消";
            //
            // btnOk
            //
            this.btnOk.AutoSize = true;
            this.btnOk.Name = "btnOk";
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "確定";
            //
            // btnTest
            //
            this.btnTest.AutoSize = true;
            this.btnTest.Name = "btnTest";
            this.btnTest.TabIndex = 0;
            this.btnTest.Text = "測試連線";
            //
            // ConnectionDialog
            //
            this.AcceptButton = this.btnOk;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 250);
            this.Controls.Add(this.layout);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConnectionDialog";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "資料庫連線設定";
            this.layout.ResumeLayout(false);
            this.layout.PerformLayout();
            this.dbRow.ResumeLayout(false);
            this.dbRow.PerformLayout();
            this.authRow.ResumeLayout(false);
            this.authRow.PerformLayout();
            this.buttonRow.ResumeLayout(false);
            this.buttonRow.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.Label lblKind;
        private System.Windows.Forms.ComboBox cboKind;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblDatabase;
        private System.Windows.Forms.FlowLayoutPanel dbRow;
        private System.Windows.Forms.ComboBox cboDatabase;
        private System.Windows.Forms.Button btnListDb;
        private System.Windows.Forms.Label lblAuth;
        private System.Windows.Forms.FlowLayoutPanel authRow;
        private System.Windows.Forms.RadioButton rbSql;
        private System.Windows.Forms.RadioButton rbWindows;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.FlowLayoutPanel buttonRow;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnTest;
    }
}
