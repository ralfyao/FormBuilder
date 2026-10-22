namespace FormBuilder
{
    partial class BrandingForm
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
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.txtSubtitle = new System.Windows.Forms.TextBox();
            this.lblFooter = new System.Windows.Forms.Label();
            this.txtFooter = new System.Windows.Forms.TextBox();
            this.lblLogo = new System.Windows.Forms.Label();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.btnLogo = new FormBuilder.RoundButton();
            this.btnClearLogo = new FormBuilder.RoundButton();
            this.lblAppIcon = new System.Windows.Forms.Label();
            this.picAppIcon = new System.Windows.Forms.PictureBox();
            this.btnAppIcon = new FormBuilder.RoundButton();
            this.btnClearAppIcon = new FormBuilder.RoundButton();
            this.lblFavicon = new System.Windows.Forms.Label();
            this.picFavicon = new System.Windows.Forms.PictureBox();
            this.btnFavicon = new FormBuilder.RoundButton();
            this.btnClearFavicon = new FormBuilder.RoundButton();
            this.lblBanner = new System.Windows.Forms.Label();
            this.btnBanner = new System.Windows.Forms.Button();
            this.lblText = new System.Windows.Forms.Label();
            this.btnText = new System.Windows.Forms.Button();
            this.lblButton = new System.Windows.Forms.Label();
            this.btnButton = new System.Windows.Forms.Button();
            this.btnDefaultColors = new FormBuilder.RoundButton();
            this.btnPreview = new FormBuilder.RoundButton();
            this.btnSave = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picFavicon)).BeginInit();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(16, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(84, 23);
            this.lblTitle.Text = "標題";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtTitle
            //
            this.txtTitle.Location = new System.Drawing.Point(106, 20);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.PlaceholderText = "空白 = 顯示產品名稱（ERP 系統 / ERP Studio）";
            this.txtTitle.Size = new System.Drawing.Size(334, 23);
            this.txtTitle.TabIndex = 0;
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(16, 56);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(84, 23);
            this.lblSubtitle.Text = "副標題";
            this.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtSubtitle
            //
            this.txtSubtitle.Location = new System.Drawing.Point(106, 56);
            this.txtSubtitle.Name = "txtSubtitle";
            this.txtSubtitle.PlaceholderText = "例如：公司英文名稱、標語";
            this.txtSubtitle.Size = new System.Drawing.Size(334, 23);
            this.txtSubtitle.TabIndex = 1;
            //
            // lblFooter
            //
            this.lblFooter.Location = new System.Drawing.Point(16, 92);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(84, 23);
            this.lblFooter.Text = "頁尾文字";
            this.lblFooter.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // txtFooter
            //
            this.txtFooter.Location = new System.Drawing.Point(106, 92);
            this.txtFooter.Name = "txtFooter";
            this.txtFooter.PlaceholderText = "例如：© 公司名稱　系統問題請洽資訊室分機 123";
            this.txtFooter.Size = new System.Drawing.Size(334, 23);
            this.txtFooter.TabIndex = 2;
            //
            // lblLogo
            //
            this.lblLogo.Location = new System.Drawing.Point(16, 128);
            this.lblLogo.Name = "lblLogo";
            this.lblLogo.Size = new System.Drawing.Size(84, 23);
            this.lblLogo.Text = "標誌";
            this.lblLogo.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // picLogo
            //
            this.picLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picLogo.Location = new System.Drawing.Point(106, 128);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(64, 64);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            //
            // btnLogo
            //
            this.btnLogo.Location = new System.Drawing.Point(182, 128);
            this.btnLogo.Name = "btnLogo";
            this.btnLogo.Size = new System.Drawing.Size(110, 28);
            this.btnLogo.TabIndex = 3;
            this.btnLogo.Text = "選擇圖片…";
            //
            // btnClearLogo
            //
            this.btnClearLogo.Location = new System.Drawing.Point(182, 164);
            this.btnClearLogo.Name = "btnClearLogo";
            this.btnClearLogo.Size = new System.Drawing.Size(110, 28);
            this.btnClearLogo.TabIndex = 4;
            this.btnClearLogo.Text = "清除標誌";
            //
            // lblAppIcon
            //
            this.lblAppIcon.Location = new System.Drawing.Point(16, 206);
            this.lblAppIcon.Name = "lblAppIcon";
            this.lblAppIcon.Size = new System.Drawing.Size(84, 32);
            this.lblAppIcon.Text = "視窗圖示";
            this.lblAppIcon.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // picAppIcon
            //
            this.picAppIcon.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picAppIcon.Location = new System.Drawing.Point(106, 206);
            this.picAppIcon.Name = "picAppIcon";
            this.picAppIcon.Size = new System.Drawing.Size(32, 32);
            this.picAppIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            //
            // btnAppIcon
            //
            this.btnAppIcon.Location = new System.Drawing.Point(182, 208);
            this.btnAppIcon.Name = "btnAppIcon";
            this.btnAppIcon.Size = new System.Drawing.Size(110, 28);
            this.btnAppIcon.TabIndex = 5;
            this.btnAppIcon.Text = "選擇圖示…";
            //
            // btnClearAppIcon
            //
            this.btnClearAppIcon.Location = new System.Drawing.Point(298, 208);
            this.btnClearAppIcon.Name = "btnClearAppIcon";
            this.btnClearAppIcon.Size = new System.Drawing.Size(110, 28);
            this.btnClearAppIcon.TabIndex = 6;
            this.btnClearAppIcon.Text = "清除圖示";
            //
            // lblFavicon
            //
            this.lblFavicon.Location = new System.Drawing.Point(16, 248);
            this.lblFavicon.Name = "lblFavicon";
            this.lblFavicon.Size = new System.Drawing.Size(84, 32);
            this.lblFavicon.Text = "小圖示";
            this.lblFavicon.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // picFavicon
            //
            this.picFavicon.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picFavicon.Location = new System.Drawing.Point(106, 248);
            this.picFavicon.Name = "picFavicon";
            this.picFavicon.Size = new System.Drawing.Size(32, 32);
            this.picFavicon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            //
            // btnFavicon
            //
            this.btnFavicon.Location = new System.Drawing.Point(182, 250);
            this.btnFavicon.Name = "btnFavicon";
            this.btnFavicon.Size = new System.Drawing.Size(110, 28);
            this.btnFavicon.TabIndex = 7;
            this.btnFavicon.Text = "選擇圖示…";
            //
            // btnClearFavicon
            //
            this.btnClearFavicon.Location = new System.Drawing.Point(298, 250);
            this.btnClearFavicon.Name = "btnClearFavicon";
            this.btnClearFavicon.Size = new System.Drawing.Size(110, 28);
            this.btnClearFavicon.TabIndex = 8;
            this.btnClearFavicon.Text = "清除小圖示";
            //
            // lblBanner
            //
            this.lblBanner.Location = new System.Drawing.Point(16, 292);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(84, 26);
            this.lblBanner.Text = "橫幅底色";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // btnBanner
            //
            this.btnBanner.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBanner.Location = new System.Drawing.Point(106, 292);
            this.btnBanner.Name = "btnBanner";
            this.btnBanner.Size = new System.Drawing.Size(64, 26);
            this.btnBanner.TabIndex = 9;
            //
            // lblText
            //
            this.lblText.Location = new System.Drawing.Point(182, 292);
            this.lblText.Name = "lblText";
            this.lblText.Size = new System.Drawing.Size(84, 26);
            this.lblText.Text = "標題文字";
            this.lblText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // btnText
            //
            this.btnText.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnText.Location = new System.Drawing.Point(272, 292);
            this.btnText.Name = "btnText";
            this.btnText.Size = new System.Drawing.Size(64, 26);
            this.btnText.TabIndex = 10;
            //
            // lblButton
            //
            this.lblButton.Location = new System.Drawing.Point(16, 328);
            this.lblButton.Name = "lblButton";
            this.lblButton.Size = new System.Drawing.Size(84, 26);
            this.lblButton.Text = "登入按鈕";
            this.lblButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // btnButton
            //
            this.btnButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnButton.Location = new System.Drawing.Point(106, 328);
            this.btnButton.Name = "btnButton";
            this.btnButton.Size = new System.Drawing.Size(64, 26);
            this.btnButton.TabIndex = 11;
            //
            // btnDefaultColors
            //
            this.btnDefaultColors.Location = new System.Drawing.Point(182, 327);
            this.btnDefaultColors.Name = "btnDefaultColors";
            this.btnDefaultColors.Size = new System.Drawing.Size(154, 28);
            this.btnDefaultColors.TabIndex = 12;
            this.btnDefaultColors.Text = "顏色恢復預設";
            //
            // btnPreview
            //
            this.btnPreview.Location = new System.Drawing.Point(16, 378);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(96, 32);
            this.btnPreview.TabIndex = 13;
            this.btnPreview.Text = "預覽";
            //
            // btnSave
            //
            this.btnSave.Location = new System.Drawing.Point(242, 378);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(96, 32);
            this.btnSave.TabIndex = 14;
            this.btnSave.Text = "儲存";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(344, 378);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(96, 32);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "取消";
            //
            // BrandingForm
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(456, 426);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.txtTitle);
            this.Controls.Add(this.lblSubtitle);
            this.Controls.Add(this.txtSubtitle);
            this.Controls.Add(this.lblFooter);
            this.Controls.Add(this.txtFooter);
            this.Controls.Add(this.lblLogo);
            this.Controls.Add(this.picLogo);
            this.Controls.Add(this.btnLogo);
            this.Controls.Add(this.btnClearLogo);
            this.Controls.Add(this.lblAppIcon);
            this.Controls.Add(this.picAppIcon);
            this.Controls.Add(this.btnAppIcon);
            this.Controls.Add(this.btnClearAppIcon);
            this.Controls.Add(this.lblFavicon);
            this.Controls.Add(this.picFavicon);
            this.Controls.Add(this.btnFavicon);
            this.Controls.Add(this.btnClearFavicon);
            this.Controls.Add(this.lblBanner);
            this.Controls.Add(this.btnBanner);
            this.Controls.Add(this.lblText);
            this.Controls.Add(this.btnText);
            this.Controls.Add(this.lblButton);
            this.Controls.Add(this.btnButton);
            this.Controls.Add(this.btnDefaultColors);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BrandingForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "登入畫面（企業識別）";
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picAppIcon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picFavicon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.TextBox txtSubtitle;
        private System.Windows.Forms.Label lblFooter;
        private System.Windows.Forms.TextBox txtFooter;
        private System.Windows.Forms.Label lblLogo;
        private System.Windows.Forms.PictureBox picLogo;
        private FormBuilder.RoundButton btnLogo;
        private FormBuilder.RoundButton btnClearLogo;
        private System.Windows.Forms.Label lblAppIcon;
        private System.Windows.Forms.PictureBox picAppIcon;
        private FormBuilder.RoundButton btnAppIcon;
        private FormBuilder.RoundButton btnClearAppIcon;
        private System.Windows.Forms.Label lblFavicon;
        private System.Windows.Forms.PictureBox picFavicon;
        private FormBuilder.RoundButton btnFavicon;
        private FormBuilder.RoundButton btnClearFavicon;
        private System.Windows.Forms.Label lblBanner;
        private System.Windows.Forms.Button btnBanner;
        private System.Windows.Forms.Label lblText;
        private System.Windows.Forms.Button btnText;
        private System.Windows.Forms.Label lblButton;
        private System.Windows.Forms.Button btnButton;
        private FormBuilder.RoundButton btnDefaultColors;
        private FormBuilder.RoundButton btnPreview;
        private FormBuilder.RoundButton btnSave;
        private FormBuilder.RoundButton btnCancel;
    }
}
