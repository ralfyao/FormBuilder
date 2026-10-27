namespace FormBuilder
{
    partial class TableColumnsDialog
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
            this.lblHint = new System.Windows.Forms.Label();
            this.lstColumns = new System.Windows.Forms.CheckedListBox();
            this.sidePanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnAll = new System.Windows.Forms.Button();
            this.btnNone = new System.Windows.Forms.Button();
            this.bottomPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.sidePanel.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // lblHint
            //
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblHint.Name = "lblHint";
            this.lblHint.Padding = new System.Windows.Forms.Padding(8, 8, 8, 0);
            this.lblHint.Size = new System.Drawing.Size(384, 48);
            this.lblHint.TabIndex = 0;
            this.lblHint.Text = "勾選明細表格要印的欄位；用「上移 / 下移」調整順序（由上到下 = 表格由左到右）";
            //
            // lstColumns
            //
            this.lstColumns.CheckOnClick = true;
            this.lstColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstColumns.IntegralHeight = false;
            this.lstColumns.Name = "lstColumns";
            this.lstColumns.TabIndex = 1;
            //
            // sidePanel
            //
            this.sidePanel.Controls.Add(this.btnUp);
            this.sidePanel.Controls.Add(this.btnDown);
            this.sidePanel.Controls.Add(this.btnAll);
            this.sidePanel.Controls.Add(this.btnNone);
            this.sidePanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.sidePanel.Name = "sidePanel";
            this.sidePanel.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.sidePanel.Size = new System.Drawing.Size(100, 360);
            this.sidePanel.TabIndex = 2;
            //
            // btnUp
            //
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(84, 30);
            this.btnUp.TabIndex = 0;
            this.btnUp.Text = "上移";
            //
            // btnDown
            //
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(84, 30);
            this.btnDown.TabIndex = 1;
            this.btnDown.Text = "下移";
            //
            // btnAll
            //
            this.btnAll.Margin = new System.Windows.Forms.Padding(3, 18, 3, 3);
            this.btnAll.Name = "btnAll";
            this.btnAll.Size = new System.Drawing.Size(84, 30);
            this.btnAll.TabIndex = 2;
            this.btnAll.Text = "全選";
            //
            // btnNone
            //
            this.btnNone.Name = "btnNone";
            this.btnNone.Size = new System.Drawing.Size(84, 30);
            this.btnNone.TabIndex = 3;
            this.btnNone.Text = "全不選";
            //
            // bottomPanel
            //
            this.bottomPanel.Controls.Add(this.btnCancel);
            this.bottomPanel.Controls.Add(this.btnOk);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(6);
            this.bottomPanel.Size = new System.Drawing.Size(384, 46);
            this.bottomPanel.TabIndex = 3;
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(84, 30);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            //
            // btnOk
            //
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(84, 30);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "確定";
            //
            // TableColumnsDialog
            //
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 454);
            this.Controls.Add(this.lstColumns);
            this.Controls.Add(this.sidePanel);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.lblHint);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(320, 300);
            this.Name = "TableColumnsDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "明細表格的欄位";
            this.sidePanel.ResumeLayout(false);
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.CheckedListBox lstColumns;
        private System.Windows.Forms.FlowLayoutPanel sidePanel;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnAll;
        private System.Windows.Forms.Button btnNone;
        private System.Windows.Forms.FlowLayoutPanel bottomPanel;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
    }
}
