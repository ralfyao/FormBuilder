namespace FormBuilder
{
    partial class RecordNavigatorDialog
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
            this.body = new System.Windows.Forms.Panel();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnInsert = new System.Windows.Forms.ToolStripButton();
            this.btnModify = new System.Windows.Forms.ToolStripButton();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.sepNavigate = new System.Windows.Forms.ToolStripSeparator();
            this.btnFirst = new System.Windows.Forms.ToolStripButton();
            this.btnPrevious = new System.Windows.Forms.ToolStripButton();
            this.btnNext = new System.Windows.Forms.ToolStripButton();
            this.btnLast = new System.Windows.Forms.ToolStripButton();
            this.sepEffective = new System.Windows.Forms.ToolStripSeparator();
            this.btnEffective = new System.Windows.Forms.ToolStripButton();
            this.btnUneffective = new System.Windows.Forms.ToolStripButton();
            this.btnPrint = new System.Windows.Forms.ToolStripButton();
            this.bottom = new System.Windows.Forms.Panel();
            this.lblPosition = new System.Windows.Forms.Label();
            this.btnOk = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.btnClose = new FormBuilder.RoundButton();
            this.toolStrip.SuspendLayout();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // body
            //
            this.body.AutoScroll = true;
            this.body.Dock = System.Windows.Forms.DockStyle.Fill;
            this.body.Location = new System.Drawing.Point(0, 39);
            this.body.Name = "body";
            this.body.Size = new System.Drawing.Size(560, 282);
            this.body.TabIndex = 0;
            //
            // toolStrip
            //
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnInsert,
            this.btnModify,
            this.btnDelete,
            this.sepNavigate,
            this.btnFirst,
            this.btnPrevious,
            this.btnNext,
            this.btnLast,
            this.sepEffective,
            this.btnEffective,
            this.btnUneffective,
            this.btnPrint});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Padding = new System.Windows.Forms.Padding(4);
            this.toolStrip.Size = new System.Drawing.Size(560, 39);
            this.toolStrip.TabIndex = 1;
            //
            // btnInsert
            //
            this.btnInsert.AutoSize = false;
            this.btnInsert.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnInsert.Name = "btnInsert";
            this.btnInsert.Size = new System.Drawing.Size(64, 28);
            this.btnInsert.Text = "新增";
            //
            // btnModify
            //
            this.btnModify.AutoSize = false;
            this.btnModify.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnModify.Name = "btnModify";
            this.btnModify.Size = new System.Drawing.Size(64, 28);
            this.btnModify.Text = "修改";
            //
            // btnDelete
            //
            this.btnDelete.AutoSize = false;
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(64, 28);
            this.btnDelete.Text = "刪除";
            //
            // sepNavigate
            //
            this.sepNavigate.Name = "sepNavigate";
            //
            // btnFirst
            //
            this.btnFirst.AutoSize = false;
            this.btnFirst.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnFirst.Name = "btnFirst";
            this.btnFirst.Size = new System.Drawing.Size(64, 28);
            this.btnFirst.Text = "首筆";
            //
            // btnPrevious
            //
            this.btnPrevious.AutoSize = false;
            this.btnPrevious.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.Size = new System.Drawing.Size(64, 28);
            this.btnPrevious.Text = "上筆";
            //
            // btnNext
            //
            this.btnNext.AutoSize = false;
            this.btnNext.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(64, 28);
            this.btnNext.Text = "下筆";
            //
            // btnLast
            //
            this.btnLast.AutoSize = false;
            this.btnLast.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLast.Name = "btnLast";
            this.btnLast.Size = new System.Drawing.Size(64, 28);
            this.btnLast.Text = "末筆";
            //
            // sepEffective
            //
            this.sepEffective.Name = "sepEffective";
            //
            // btnEffective
            //
            this.btnEffective.AutoSize = false;
            this.btnEffective.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEffective.Name = "btnEffective";
            this.btnEffective.Size = new System.Drawing.Size(64, 28);
            this.btnEffective.Text = "生效";
            //
            // btnUneffective
            //
            this.btnUneffective.AutoSize = false;
            this.btnUneffective.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnUneffective.Name = "btnUneffective";
            this.btnUneffective.Size = new System.Drawing.Size(72, 28);
            this.btnUneffective.Text = "取消生效";
            //
            // btnPrint
            //
            this.btnPrint.AutoSize = false;
            this.btnPrint.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(64, 28);
            this.btnPrint.Text = "列印";
            //
            // bottom
            //
            this.bottom.Controls.Add(this.lblPosition);
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Controls.Add(this.btnClose);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(0, 321);
            this.bottom.Name = "bottom";
            this.bottom.Padding = new System.Windows.Forms.Padding(8);
            this.bottom.Size = new System.Drawing.Size(560, 40);
            this.bottom.TabIndex = 2;
            //
            // lblPosition
            //
            this.lblPosition.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPosition.Name = "lblPosition";
            this.lblPosition.TabIndex = 0;
            this.lblPosition.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnOk
            //
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 24);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 24);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "取消";
            //
            // btnClose
            //
            this.btnClose.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 24);
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "關閉";
            //
            // RecordNavigatorDialog
            //
            this.ClientSize = new System.Drawing.Size(560, 361);
            this.Controls.Add(this.body);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "RecordNavigatorDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "編輯";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        /// <summary>欄位區：欄位與按鈕依定義動態產生到這裡</summary>
        private System.Windows.Forms.Panel body;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnInsert;
        private System.Windows.Forms.ToolStripButton btnModify;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.ToolStripSeparator sepNavigate;
        private System.Windows.Forms.ToolStripButton btnFirst;
        private System.Windows.Forms.ToolStripButton btnPrevious;
        private System.Windows.Forms.ToolStripButton btnNext;
        private System.Windows.Forms.ToolStripButton btnLast;
        private System.Windows.Forms.ToolStripSeparator sepEffective;
        private System.Windows.Forms.ToolStripButton btnEffective;
        private System.Windows.Forms.ToolStripButton btnUneffective;
        private System.Windows.Forms.ToolStripButton btnPrint;
        private System.Windows.Forms.Panel bottom;
        private System.Windows.Forms.Label lblPosition;
        private FormBuilder.RoundButton btnOk;
        private FormBuilder.RoundButton btnCancel;
        private FormBuilder.RoundButton btnClose;
    }
}
