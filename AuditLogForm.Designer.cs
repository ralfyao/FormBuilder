namespace FormBuilder
{
    partial class AuditLogForm
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
            this.filters = new System.Windows.Forms.FlowLayoutPanel();
            this.lblFrom = new System.Windows.Forms.Label();
            this.dtpFrom = new System.Windows.Forms.DateTimePicker();
            this.lblTo = new System.Windows.Forms.Label();
            this.dtpTo = new System.Windows.Forms.DateTimePicker();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.txtForm = new System.Windows.Forms.TextBox();
            this.cboAction = new System.Windows.Forms.ComboBox();
            this.btnQuery = new FormBuilder.RoundButton();
            this.btnExport = new FormBuilder.RoundButton();
            this.split = new System.Windows.Forms.SplitContainer();
            this.dgvLog = new System.Windows.Forms.DataGridView();
            this.txtDetail = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.filters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // filters
            //
            this.filters.Controls.Add(this.lblFrom);
            this.filters.Controls.Add(this.dtpFrom);
            this.filters.Controls.Add(this.lblTo);
            this.filters.Controls.Add(this.dtpTo);
            this.filters.Controls.Add(this.txtUser);
            this.filters.Controls.Add(this.txtForm);
            this.filters.Controls.Add(this.cboAction);
            this.filters.Controls.Add(this.btnQuery);
            this.filters.Controls.Add(this.btnExport);
            this.filters.Dock = System.Windows.Forms.DockStyle.Top;
            this.filters.Name = "filters";
            this.filters.Padding = new System.Windows.Forms.Padding(6);
            this.filters.Size = new System.Drawing.Size(1100, 46);
            this.filters.TabIndex = 0;
            //
            // lblFrom
            //
            this.lblFrom.Margin = new System.Windows.Forms.Padding(3, 9, 0, 0);
            this.lblFrom.Name = "lblFrom";
            this.lblFrom.Size = new System.Drawing.Size(32, 20);
            this.lblFrom.Text = "日期";
            //
            // dtpFrom
            //
            this.dtpFrom.CustomFormat = "yyyy/MM/dd";
            this.dtpFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpFrom.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.dtpFrom.Name = "dtpFrom";
            this.dtpFrom.Size = new System.Drawing.Size(110, 23);
            //
            // lblTo
            //
            this.lblTo.Margin = new System.Windows.Forms.Padding(0, 9, 0, 0);
            this.lblTo.Name = "lblTo";
            this.lblTo.Size = new System.Drawing.Size(20, 20);
            this.lblTo.Text = "～";
            //
            // dtpTo
            //
            this.dtpTo.CustomFormat = "yyyy/MM/dd";
            this.dtpTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpTo.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.dtpTo.Name = "dtpTo";
            this.dtpTo.Size = new System.Drawing.Size(110, 23);
            //
            // txtUser
            //
            this.txtUser.Margin = new System.Windows.Forms.Padding(10, 6, 3, 3);
            this.txtUser.Name = "txtUser";
            this.txtUser.PlaceholderText = "使用者";
            this.txtUser.Size = new System.Drawing.Size(110, 23);
            //
            // txtForm
            //
            this.txtForm.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.txtForm.Name = "txtForm";
            this.txtForm.PlaceholderText = "畫面";
            this.txtForm.Size = new System.Drawing.Size(140, 23);
            //
            // cboAction
            //
            this.cboAction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAction.Items.AddRange(new object[] {
            "（全部動作）", "登入", "登入失敗", "登出", "新增", "修改", "刪除", "生效", "取消生效", "單身存檔", "修改明細",
            "執行SQL", "列印", "發布", "變更密碼", "重設密碼", "新增帳號", "修改帳號", "權限", "新增角色", "刪除角色", "修改選單"});
            this.cboAction.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.cboAction.Name = "cboAction";
            this.cboAction.Size = new System.Drawing.Size(120, 23);
            //
            // btnQuery
            //
            this.btnQuery.Margin = new System.Windows.Forms.Padding(10, 3, 3, 3);
            this.btnQuery.Name = "btnQuery";
            this.btnQuery.Size = new System.Drawing.Size(80, 30);
            this.btnQuery.Text = "查詢";
            //
            // btnExport
            //
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(100, 30);
            this.btnExport.Text = "匯出 CSV…";
            //
            // split
            //
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.split.Name = "split";
            this.split.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // split.Panel1
            //
            this.split.Panel1.Controls.Add(this.dgvLog);
            //
            // split.Panel2
            //
            this.split.Panel2.Controls.Add(this.txtDetail);
            this.split.Size = new System.Drawing.Size(1100, 560);
            this.split.SplitterDistance = 430;
            this.split.TabIndex = 1;
            //
            // dgvLog
            //
            this.dgvLog.AllowUserToAddRows = false;
            this.dgvLog.AllowUserToDeleteRows = false;
            this.dgvLog.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLog.Name = "dgvLog";
            this.dgvLog.ReadOnly = true;
            this.dgvLog.RowHeadersVisible = false;
            this.dgvLog.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLog.TabIndex = 0;
            //
            // txtDetail
            //
            this.txtDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDetail.Multiline = true;
            this.txtDetail.Name = "txtDetail";
            this.txtDetail.ReadOnly = true;
            this.txtDetail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDetail.TabIndex = 0;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.lblCount });
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.TabIndex = 2;
            //
            // lblCount
            //
            this.lblCount.Name = "lblCount";
            //
            // AuditLogForm
            //
            this.ClientSize = new System.Drawing.Size(1100, 630);
            this.Controls.Add(this.split);
            this.Controls.Add(this.filters);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.Name = "AuditLogForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "稽核紀錄";
            this.filters.ResumeLayout(false);
            this.filters.PerformLayout();
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            this.split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLog)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel filters;
        private System.Windows.Forms.Label lblFrom;
        private System.Windows.Forms.DateTimePicker dtpFrom;
        private System.Windows.Forms.Label lblTo;
        private System.Windows.Forms.DateTimePicker dtpTo;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.TextBox txtForm;
        private System.Windows.Forms.ComboBox cboAction;
        private FormBuilder.RoundButton btnQuery;
        private FormBuilder.RoundButton btnExport;
        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.DataGridView dgvLog;
        private System.Windows.Forms.TextBox txtDetail;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblCount;
    }
}
