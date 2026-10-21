namespace FormBuilder
{
    partial class PickListDialog
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
            this.txtFilter = new System.Windows.Forms.TextBox();
            this.lstItems = new System.Windows.Forms.CheckedListBox();
            this.bottom = new System.Windows.Forms.Panel();
            this.btnOk = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // txtFilter
            //
            this.txtFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.PlaceholderText = "篩選";
            this.txtFilter.TabIndex = 0;
            //
            // lstItems
            //
            this.lstItems.CheckOnClick = true;
            this.lstItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstItems.IntegralHeight = false;
            this.lstItems.Name = "lstItems";
            this.lstItems.TabIndex = 1;
            //
            // bottom
            //
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Name = "bottom";
            this.bottom.Size = new System.Drawing.Size(396, 44);
            this.bottom.TabIndex = 2;
            //
            // btnOk
            //
            this.btnOk.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnOk.Location = new System.Drawing.Point(206, 8);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 30);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(302, 8);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 30);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            //
            // PickListDialog
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(420, 480);
            this.Controls.Add(this.lstItems);
            this.Controls.Add(this.txtFilter);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "PickListDialog";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.CheckedListBox lstItems;
        private System.Windows.Forms.Panel bottom;
        private FormBuilder.RoundButton btnOk;
        private FormBuilder.RoundButton btnCancel;
    }
}
