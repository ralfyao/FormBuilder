namespace FormBuilder
{
    partial class JoinDialog
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
            this.tablePanel = new System.Windows.Forms.Panel();
            this.lstTables = new System.Windows.Forms.ListBox();
            this.txtFilter = new System.Windows.Forms.TextBox();
            this.lblTables = new System.Windows.Forms.Label();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.pickGroup = new System.Windows.Forms.GroupBox();
            this.dgvPick = new System.Windows.Forms.DataGridView();
            this.colPick = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.linkGroup = new System.Windows.Forms.GroupBox();
            this.dgvLinks = new System.Windows.Forms.DataGridView();
            this.colLocal = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colRemote = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.bottom = new System.Windows.Forms.Panel();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tablePanel.SuspendLayout();
            this.rightPanel.SuspendLayout();
            this.pickGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPick)).BeginInit();
            this.linkGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLinks)).BeginInit();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // tablePanel
            //
            this.tablePanel.Controls.Add(this.lstTables);
            this.tablePanel.Controls.Add(this.txtFilter);
            this.tablePanel.Controls.Add(this.lblTables);
            this.tablePanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.tablePanel.Location = new System.Drawing.Point(0, 0);
            this.tablePanel.Name = "tablePanel";
            this.tablePanel.Padding = new System.Windows.Forms.Padding(6);
            this.tablePanel.Size = new System.Drawing.Size(250, 521);
            this.tablePanel.TabIndex = 0;
            //
            // lstTables
            //
            this.lstTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstTables.IntegralHeight = false;
            this.lstTables.Name = "lstTables";
            this.lstTables.TabIndex = 0;
            //
            // txtFilter
            //
            this.txtFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.PlaceholderText = "輸入關鍵字篩選資料表";
            this.txtFilter.TabIndex = 1;
            //
            // lblTables
            //
            this.lblTables.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTables.Name = "lblTables";
            this.lblTables.Size = new System.Drawing.Size(238, 22);
            this.lblTables.TabIndex = 2;
            this.lblTables.Text = "1. 選擇要 JOIN 的資料表";
            //
            // rightPanel
            //
            this.rightPanel.Controls.Add(this.pickGroup);
            this.rightPanel.Controls.Add(this.linkGroup);
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightPanel.Location = new System.Drawing.Point(250, 0);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Size = new System.Drawing.Size(534, 521);
            this.rightPanel.TabIndex = 1;
            //
            // pickGroup
            //
            this.pickGroup.Controls.Add(this.dgvPick);
            this.pickGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pickGroup.Name = "pickGroup";
            this.pickGroup.Padding = new System.Windows.Forms.Padding(6);
            this.pickGroup.TabIndex = 0;
            this.pickGroup.TabStop = false;
            this.pickGroup.Text = "3. 勾選要帶進 Grid 的欄位（都是唯讀，不會寫回資料庫）";
            //
            // dgvPick
            //
            this.dgvPick.AllowUserToAddRows = false;
            this.dgvPick.AllowUserToDeleteRows = false;
            this.dgvPick.AutoGenerateColumns = false;
            this.dgvPick.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPick,
            this.colName,
            this.colType,
            this.colHeader});
            this.dgvPick.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPick.Name = "dgvPick";
            this.dgvPick.RowHeadersVisible = false;
            this.dgvPick.TabIndex = 0;
            //
            // colPick
            //
            this.colPick.DataPropertyName = "Pick";
            this.colPick.HeaderText = "帶入";
            this.colPick.Name = "colPick";
            this.colPick.Width = 50;
            //
            // colName
            //
            this.colName.DataPropertyName = "Name";
            this.colName.HeaderText = "欄位";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            this.colName.Width = 140;
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
            // linkGroup
            //
            this.linkGroup.Controls.Add(this.dgvLinks);
            this.linkGroup.Dock = System.Windows.Forms.DockStyle.Top;
            this.linkGroup.Name = "linkGroup";
            this.linkGroup.Padding = new System.Windows.Forms.Padding(6);
            this.linkGroup.Size = new System.Drawing.Size(534, 150);
            this.linkGroup.TabIndex = 1;
            this.linkGroup.TabStop = false;
            this.linkGroup.Text = "2. 關聯條件（Grid 的欄位 = 關聯資料表的欄位）";
            //
            // dgvLinks
            //
            this.dgvLinks.AutoGenerateColumns = false;
            this.dgvLinks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLocal,
            this.colRemote});
            this.dgvLinks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLinks.Name = "dgvLinks";
            this.dgvLinks.RowHeadersWidth = 24;
            this.dgvLinks.TabIndex = 0;
            //
            // colLocal
            //
            this.colLocal.DataPropertyName = "Local";
            this.colLocal.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colLocal.HeaderText = "Grid 欄位";
            this.colLocal.Name = "colLocal";
            this.colLocal.Width = 220;
            //
            // colRemote
            //
            this.colRemote.DataPropertyName = "Remote";
            this.colRemote.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colRemote.HeaderText = "關聯資料表欄位";
            this.colRemote.Name = "colRemote";
            this.colRemote.Width = 220;
            //
            // bottom
            //
            this.bottom.Controls.Add(this.lblHint);
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(0, 521);
            this.bottom.Name = "bottom";
            this.bottom.Padding = new System.Windows.Forms.Padding(6);
            this.bottom.Size = new System.Drawing.Size(784, 40);
            this.bottom.TabIndex = 2;
            //
            // lblHint
            //
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHint.Name = "lblHint";
            this.lblHint.TabIndex = 0;
            this.lblHint.Text = "LEFT JOIN：Grid 的每一列都會顯示，對不到關聯資料的欄位是空白";
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnOk
            //
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "取消";
            //
            // JoinDialog
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.rightPanel);
            this.Controls.Add(this.tablePanel);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "JoinDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "JOIN 其他資料表";
            this.tablePanel.ResumeLayout(false);
            this.tablePanel.PerformLayout();
            this.rightPanel.ResumeLayout(false);
            this.pickGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPick)).EndInit();
            this.linkGroup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLinks)).EndInit();
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.pickGroup.PerformLayout();
            this.linkGroup.PerformLayout();
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel tablePanel;
        private System.Windows.Forms.ListBox lstTables;
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.Label lblTables;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.GroupBox pickGroup;
        private System.Windows.Forms.DataGridView dgvPick;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colPick;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHeader;
        private System.Windows.Forms.GroupBox linkGroup;
        private System.Windows.Forms.DataGridView dgvLinks;
        private System.Windows.Forms.DataGridViewComboBoxColumn colLocal;
        private System.Windows.Forms.DataGridViewComboBoxColumn colRemote;
        private System.Windows.Forms.Panel bottom;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
