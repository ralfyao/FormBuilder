namespace FormBuilder
{
    partial class PromptDialog
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
            this.fields = new System.Windows.Forms.TableLayoutPanel();
            this.bottom = new System.Windows.Forms.Panel();
            this.btnOk = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // fields（欄位依呼叫時的定義動態加入：每列「標籤 + 輸入框」）
            //
            this.fields.AutoSize = true;
            this.fields.ColumnCount = 2;
            this.fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.fields.Dock = System.Windows.Forms.DockStyle.Top;
            this.fields.Location = new System.Drawing.Point(12, 12);
            this.fields.Name = "fields";
            this.fields.Padding = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.fields.Size = new System.Drawing.Size(376, 8);
            this.fields.TabIndex = 0;
            //
            // bottom
            //
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(12, 112);
            this.bottom.Name = "bottom";
            this.bottom.Size = new System.Drawing.Size(376, 40);
            this.bottom.TabIndex = 1;
            //
            // btnOk
            //
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnOk.Location = new System.Drawing.Point(186, 6);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 30);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(284, 6);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 30);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            //
            // PromptDialog
            //
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(400, 164);
            this.Controls.Add(this.fields);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PromptDialog";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel fields;
        private System.Windows.Forms.Panel bottom;
        private FormBuilder.RoundButton btnOk;
        private FormBuilder.RoundButton btnCancel;
    }
}
