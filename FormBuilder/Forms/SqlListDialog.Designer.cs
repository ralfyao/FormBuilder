namespace FormBuilder
{
    partial class SqlListDialog
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
            this.topPanel = new System.Windows.Forms.Panel();
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.lblTitle = new System.Windows.Forms.Label();
            this.sqlGroup = new System.Windows.Forms.GroupBox();
            this.txtSql = new FormBuilder.SqlTextBox();
            this.lblParams = new System.Windows.Forms.Label();
            this.btnReadColumns = new System.Windows.Forms.Button();
            this.columnGroup = new System.Windows.Forms.GroupBox();
            this.dgvColumns = new System.Windows.Forms.DataGridView();
            this.colVisible = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bottomPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.topPanel.SuspendLayout();
            this.sqlGroup.SuspendLayout();
            this.columnGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvColumns)).BeginInit();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // topPanel（標題）
            //
            this.topPanel.Controls.Add(this.txtTitle);
            this.topPanel.Controls.Add(this.lblTitle);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(8, 8, 8, 4);
            this.topPanel.Size = new System.Drawing.Size(684, 38);
            this.topPanel.TabIndex = 0;
            //
            // txtTitle
            //
            this.txtTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.TabIndex = 1;
            //
            // lblTitle
            //
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(60, 26);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "標題";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // sqlGroup（SQL）
            //
            this.sqlGroup.Controls.Add(this.txtSql);
            this.sqlGroup.Controls.Add(this.lblParams);
            this.sqlGroup.Controls.Add(this.btnReadColumns);
            this.sqlGroup.Dock = System.Windows.Forms.DockStyle.Top;
            this.sqlGroup.Name = "sqlGroup";
            this.sqlGroup.Padding = new System.Windows.Forms.Padding(8);
            this.sqlGroup.Size = new System.Drawing.Size(684, 230);
            this.sqlGroup.TabIndex = 1;
            this.sqlGroup.TabStop = false;
            this.sqlGroup.Text = "SQL（SELECT；用 @單頭欄位 取目前這筆的值，切換單頭時自動重查，例如 WHERE 廠商編號 = @廠商編號）";
            //
            // txtSql
            //
            this.txtSql.AcceptsTab = true;
            this.txtSql.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSql.Font = new System.Drawing.Font("Consolas", 10F);
            this.txtSql.Multiline = true;
            this.txtSql.Name = "txtSql";
            this.txtSql.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtSql.TabIndex = 0;
            this.txtSql.WordWrap = false;
            //
            // lblParams
            //
            this.lblParams.AutoEllipsis = true;
            this.lblParams.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblParams.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblParams.Name = "lblParams";
            this.lblParams.Size = new System.Drawing.Size(668, 22);
            this.lblParams.TabIndex = 1;
            this.lblParams.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnReadColumns
            //
            this.btnReadColumns.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnReadColumns.Name = "btnReadColumns";
            this.btnReadColumns.Size = new System.Drawing.Size(668, 30);
            this.btnReadColumns.TabIndex = 2;
            this.btnReadColumns.Text = "讀取欄位（執行 SQL 取得欄位，@參數先當空值）";
            //
            // columnGroup（欄位）
            //
            this.columnGroup.Controls.Add(this.dgvColumns);
            this.columnGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnGroup.Name = "columnGroup";
            this.columnGroup.Padding = new System.Windows.Forms.Padding(8);
            this.columnGroup.TabIndex = 2;
            this.columnGroup.TabStop = false;
            this.columnGroup.Text = "欄位（勾選要顯示的欄位；標題、寬度可以直接改）";
            //
            // dgvColumns
            //
            this.dgvColumns.AllowUserToAddRows = false;
            this.dgvColumns.AllowUserToDeleteRows = false;
            this.dgvColumns.AutoGenerateColumns = false;
            this.dgvColumns.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colVisible,
            this.colColumn,
            this.colHeader,
            this.colWidth});
            this.dgvColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvColumns.Name = "dgvColumns";
            this.dgvColumns.RowHeadersWidth = 24;
            this.dgvColumns.TabIndex = 0;
            //
            // colVisible
            //
            this.colVisible.DataPropertyName = "Visible";
            this.colVisible.HeaderText = "顯示";
            this.colVisible.Name = "colVisible";
            this.colVisible.Width = 50;
            //
            // colColumn
            //
            this.colColumn.DataPropertyName = "Column";
            this.colColumn.HeaderText = "欄位";
            this.colColumn.Name = "colColumn";
            this.colColumn.ReadOnly = true;
            this.colColumn.Width = 200;
            //
            // colHeader
            //
            this.colHeader.DataPropertyName = "Header";
            this.colHeader.HeaderText = "標題";
            this.colHeader.Name = "colHeader";
            this.colHeader.Width = 220;
            //
            // colWidth
            //
            this.colWidth.DataPropertyName = "Width";
            this.colWidth.HeaderText = "寬度";
            this.colWidth.Name = "colWidth";
            this.colWidth.Width = 80;
            //
            // bottomPanel
            //
            this.bottomPanel.Controls.Add(this.btnCancel);
            this.bottomPanel.Controls.Add(this.btnOk);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(6);
            this.bottomPanel.Size = new System.Drawing.Size(684, 46);
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
            // SqlListDialog
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(684, 600);
            this.Controls.Add(this.columnGroup);
            this.Controls.Add(this.sqlGroup);
            this.Controls.Add(this.topPanel);
            this.Controls.Add(this.bottomPanel);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(500, 450);
            this.Name = "SqlListDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "SQL 清單設定（唯讀明細）";
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            this.sqlGroup.ResumeLayout(false);
            this.sqlGroup.PerformLayout();
            this.columnGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvColumns)).EndInit();
            this.bottomPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox sqlGroup;
        private FormBuilder.SqlTextBox txtSql;
        private System.Windows.Forms.Label lblParams;
        private System.Windows.Forms.Button btnReadColumns;
        private System.Windows.Forms.GroupBox columnGroup;
        private System.Windows.Forms.DataGridView dgvColumns;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colVisible;
        private System.Windows.Forms.DataGridViewTextBoxColumn colColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHeader;
        private System.Windows.Forms.DataGridViewTextBoxColumn colWidth;
        private System.Windows.Forms.FlowLayoutPanel bottomPanel;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
    }
}
