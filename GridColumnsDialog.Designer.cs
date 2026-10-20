namespace FormBuilder
{
    partial class GridColumnsDialog
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
            this.components = new System.ComponentModel.Container();
            this.columnGroup = new System.Windows.Forms.GroupBox();
            this.dgvColumns = new System.Windows.Forms.DataGridView();
            this.colVisible = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIsKey = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colNoEntry = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ctxJoin = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRemoveJoin = new System.Windows.Forms.ToolStripMenuItem();
            this.linkGroup = new System.Windows.Forms.GroupBox();
            this.dgvLinks = new System.Windows.Forms.DataGridView();
            this.colMaster = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colDetail = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.bottom = new System.Windows.Forms.Panel();
            this.btnLayout = new System.Windows.Forms.Button();
            this.btnJoin = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnNone = new System.Windows.Forms.Button();
            this.btnAll = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.columnGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvColumns)).BeginInit();
            this.linkGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLinks)).BeginInit();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // columnGroup
            //
            this.columnGroup.Controls.Add(this.dgvColumns);
            this.columnGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnGroup.Location = new System.Drawing.Point(0, 150);
            this.columnGroup.Name = "columnGroup";
            this.columnGroup.Padding = new System.Windows.Forms.Padding(6);
            this.columnGroup.Size = new System.Drawing.Size(744, 413);
            this.columnGroup.TabIndex = 0;
            this.columnGroup.TabStop = false;
            this.columnGroup.Text = "Grid 欄位（勾選要顯示的欄位；主鍵用來修改、刪除明細）";
            //
            // dgvColumns
            //
            this.dgvColumns.AllowUserToAddRows = false;
            this.dgvColumns.AllowUserToDeleteRows = false;
            this.dgvColumns.AutoGenerateColumns = false;
            this.dgvColumns.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colVisible,
            this.colColumn,
            this.colType,
            this.colHeader,
            this.colWidth,
            this.colIsKey,
            this.colNoEntry,
            this.colSource});
            this.dgvColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvColumns.Name = "dgvColumns";
            this.dgvColumns.RowHeadersVisible = false;
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
            this.colColumn.Width = 140;
            //
            // colType
            //
            this.colType.DataPropertyName = "Type";
            this.colType.HeaderText = "型別";
            this.colType.Name = "colType";
            this.colType.ReadOnly = true;
            this.colType.Width = 110;
            //
            // colHeader
            //
            this.colHeader.DataPropertyName = "Header";
            this.colHeader.HeaderText = "標題";
            this.colHeader.Name = "colHeader";
            this.colHeader.Width = 180;
            //
            // colWidth
            //
            this.colWidth.DataPropertyName = "Width";
            this.colWidth.HeaderText = "寬度";
            this.colWidth.Name = "colWidth";
            this.colWidth.Width = 60;
            //
            // colIsKey
            //
            this.colIsKey.DataPropertyName = "IsKey";
            this.colIsKey.HeaderText = "主鍵";
            this.colIsKey.Name = "colIsKey";
            this.colIsKey.Width = 50;
            //
            // colNoEntry
            //
            this.colNoEntry.DataPropertyName = "NoEntry";
            this.colNoEntry.HeaderText = "只顯示";
            this.colNoEntry.Name = "colNoEntry";
            this.colNoEntry.Width = 60;
            //
            // colSource
            //
            this.colSource.DataPropertyName = "Source";
            this.colSource.HeaderText = "來源（JOIN）";
            this.colSource.Name = "colSource";
            this.colSource.ReadOnly = true;
            this.colSource.Width = 200;
            //
            // ctxJoin
            //
            this.ctxJoin.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miRemoveJoin});
            this.ctxJoin.Name = "ctxJoin";
            //
            // miRemoveJoin
            //
            this.miRemoveJoin.Name = "miRemoveJoin";
            this.miRemoveJoin.Text = "移除這個 JOIN（連同它帶進來的欄位）";
            //
            // linkGroup
            //
            this.linkGroup.Controls.Add(this.dgvLinks);
            this.linkGroup.Dock = System.Windows.Forms.DockStyle.Top;
            this.linkGroup.Location = new System.Drawing.Point(0, 0);
            this.linkGroup.Name = "linkGroup";
            this.linkGroup.Padding = new System.Windows.Forms.Padding(6);
            this.linkGroup.Size = new System.Drawing.Size(744, 150);
            this.linkGroup.TabIndex = 1;
            this.linkGroup.TabStop = false;
            this.linkGroup.Text = "關聯欄位（單身只列出「單身欄位 = 目前單頭欄位值」的資料；新增明細時自動帶入）";
            //
            // dgvLinks
            //
            this.dgvLinks.AutoGenerateColumns = false;
            this.dgvLinks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMaster,
            this.colDetail});
            this.dgvLinks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLinks.Name = "dgvLinks";
            this.dgvLinks.RowHeadersWidth = 24;
            this.dgvLinks.TabIndex = 0;
            //
            // colMaster
            //
            this.colMaster.DataPropertyName = "Master";
            this.colMaster.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colMaster.HeaderText = "單頭欄位";
            this.colMaster.Name = "colMaster";
            this.colMaster.Width = 250;
            //
            // colDetail
            //
            this.colDetail.DataPropertyName = "Detail";
            this.colDetail.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colDetail.HeaderText = "單身欄位";
            this.colDetail.Name = "colDetail";
            this.colDetail.Width = 250;
            //
            // bottom
            //
            this.bottom.Controls.Add(this.btnLayout);
            this.bottom.Controls.Add(this.btnJoin);
            this.bottom.Controls.Add(this.btnDown);
            this.bottom.Controls.Add(this.btnUp);
            this.bottom.Controls.Add(this.btnNone);
            this.bottom.Controls.Add(this.btnAll);
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(0, 563);
            this.bottom.Name = "bottom";
            this.bottom.Padding = new System.Windows.Forms.Padding(6);
            this.bottom.Size = new System.Drawing.Size(744, 38);
            this.bottom.TabIndex = 2;
            //
            // btnLayout
            //
            this.btnLayout.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnLayout.Name = "btnLayout";
            this.btnLayout.Size = new System.Drawing.Size(120, 26);
            this.btnLayout.TabIndex = 0;
            this.btnLayout.Text = "編輯視窗版面…";
            //
            // btnJoin
            //
            this.btnJoin.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnJoin.Name = "btnJoin";
            this.btnJoin.Size = new System.Drawing.Size(140, 26);
            this.btnJoin.TabIndex = 5;
            this.btnJoin.Text = "JOIN 其他資料表…";
            //
            // btnUp
            //
            this.btnUp.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(70, 26);
            this.btnUp.TabIndex = 6;
            this.btnUp.Text = "▲ 上移";
            //
            // btnDown
            //
            this.btnDown.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(70, 26);
            this.btnDown.TabIndex = 7;
            this.btnDown.Text = "▼ 下移";
            //
            // btnNone
            //
            this.btnNone.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnNone.Name = "btnNone";
            this.btnNone.Size = new System.Drawing.Size(70, 26);
            this.btnNone.TabIndex = 1;
            this.btnNone.Text = "全不選";
            //
            // btnAll
            //
            this.btnAll.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnAll.Name = "btnAll";
            this.btnAll.Size = new System.Drawing.Size(70, 26);
            this.btnAll.TabIndex = 2;
            this.btnAll.Text = "全選";
            //
            // btnOk
            //
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 26);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 26);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "取消";
            //
            // GridColumnsDialog
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(744, 601);
            this.Controls.Add(this.columnGroup);
            this.Controls.Add(this.linkGroup);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "GridColumnsDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "單身欄位設定";
            this.columnGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvColumns)).EndInit();
            this.linkGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLinks)).EndInit();
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.columnGroup.PerformLayout();
            this.linkGroup.PerformLayout();
            this.bottom.PerformLayout();
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.GroupBox columnGroup;
        private System.Windows.Forms.DataGridView dgvColumns;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colVisible;
        private System.Windows.Forms.DataGridViewTextBoxColumn colColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHeader;
        private System.Windows.Forms.DataGridViewTextBoxColumn colWidth;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colIsKey;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colNoEntry;
        private System.Windows.Forms.GroupBox linkGroup;
        private System.Windows.Forms.DataGridView dgvLinks;
        private System.Windows.Forms.DataGridViewComboBoxColumn colMaster;
        private System.Windows.Forms.DataGridViewComboBoxColumn colDetail;
        private System.Windows.Forms.Panel bottom;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSource;
        private System.Windows.Forms.ContextMenuStrip ctxJoin;
        private System.Windows.Forms.ToolStripMenuItem miRemoveJoin;
        private System.Windows.Forms.Button btnJoin;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnLayout;
        private System.Windows.Forms.Button btnNone;
        private System.Windows.Forms.Button btnAll;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
