namespace FormBuilder
{
    partial class DesignerForm
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
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnNewForm = new System.Windows.Forms.ToolStripButton();
            this.btnOpen = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnSaveAs = new System.Windows.Forms.ToolStripButton();
            this.sepFile = new System.Windows.Forms.ToolStripSeparator();
            this.btnRemove = new System.Windows.Forms.ToolStripButton();
            this.btnAddAll = new System.Windows.Forms.ToolStripButton();
            this.btnGridColumns = new System.Windows.Forms.ToolStripButton();
            this.sepEdit = new System.Windows.Forms.ToolStripSeparator();
            this.btnPreview = new System.Windows.Forms.ToolStripButton();
            this.sepAlign = new System.Windows.Forms.ToolStripSeparator();
            this.connPanel = new System.Windows.Forms.Panel();
            this.lblConn = new System.Windows.Forms.Label();
            this.btnReconnect = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lblConnCaption = new System.Windows.Forms.Label();
            this.mainSplit = new System.Windows.Forms.SplitContainer();
            this.leftSplit = new System.Windows.Forms.SplitContainer();
            this.leftTabs = new System.Windows.Forms.TabControl();
            this.tabTables = new System.Windows.Forms.TabPage();
            this.treeTables = new System.Windows.Forms.TreeView();
            this.lblTreeHint = new System.Windows.Forms.Label();
            this.txtTableFilter = new System.Windows.Forms.TextBox();
            this.tabToolbox = new System.Windows.Forms.TabPage();
            this.toolbox = new FormBuilder.ToolboxList();
            this.lblToolboxHint = new System.Windows.Forms.Label();
            this.columnGrid = new System.Windows.Forms.PropertyGrid();
            this.lblColumnProps = new System.Windows.Forms.Label();
            this.rightSplit = new System.Windows.Forms.SplitContainer();
            this.canvasHost = new System.Windows.Forms.Panel();
            this.canvas = new System.Windows.Forms.Panel();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip.SuspendLayout();
            this.connPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).BeginInit();
            this.mainSplit.Panel1.SuspendLayout();
            this.mainSplit.Panel2.SuspendLayout();
            this.mainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.leftSplit)).BeginInit();
            this.leftSplit.Panel1.SuspendLayout();
            this.leftSplit.Panel2.SuspendLayout();
            this.leftSplit.SuspendLayout();
            this.leftTabs.SuspendLayout();
            this.tabTables.SuspendLayout();
            this.tabToolbox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rightSplit)).BeginInit();
            this.rightSplit.Panel1.SuspendLayout();
            this.rightSplit.Panel2.SuspendLayout();
            this.rightSplit.SuspendLayout();
            this.canvasHost.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStrip
            //
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnNewForm,
            this.btnOpen,
            this.btnSave,
            this.btnSaveAs,
            this.sepFile,
            this.btnRemove,
            this.btnAddAll,
            this.btnGridColumns,
            this.sepEdit,
            this.btnPreview,
            this.sepAlign});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1384, 25);
            this.toolStrip.TabIndex = 2;
            //
            // btnNewForm
            //
            this.btnNewForm.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnNewForm.Name = "btnNewForm";
            this.btnNewForm.Text = "開新畫面";
            //
            // btnOpen
            //
            this.btnOpen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Text = "開啟";
            //
            // btnSave
            //
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSave.Name = "btnSave";
            this.btnSave.Text = "存檔";
            //
            // btnSaveAs
            //
            this.btnSaveAs.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSaveAs.Name = "btnSaveAs";
            this.btnSaveAs.Text = "另存新檔";
            //
            // sepFile
            //
            this.sepFile.Name = "sepFile";
            //
            // btnRemove
            //
            this.btnRemove.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Text = "移除選取欄位";
            //
            // btnAddAll
            //
            this.btnAddAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnAddAll.Name = "btnAddAll";
            this.btnAddAll.Text = "加入全部欄位";
            //
            // btnGridColumns
            //
            this.btnGridColumns.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnGridColumns.Name = "btnGridColumns";
            this.btnGridColumns.Text = "單身欄位設定";
            //
            // sepEdit
            //
            this.sepEdit.Name = "sepEdit";
            //
            // btnPreview
            //
            this.btnPreview.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Text = "預覽執行";
            //
            // sepAlign
            //
            this.sepAlign.Name = "sepAlign";
            //
            // connPanel
            //
            this.connPanel.Controls.Add(this.lblConn);
            this.connPanel.Controls.Add(this.btnReconnect);
            this.connPanel.Controls.Add(this.btnConnect);
            this.connPanel.Controls.Add(this.lblConnCaption);
            this.connPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.connPanel.Location = new System.Drawing.Point(0, 25);
            this.connPanel.Name = "connPanel";
            this.connPanel.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.connPanel.Size = new System.Drawing.Size(1384, 32);
            this.connPanel.TabIndex = 1;
            //
            // lblConn
            //
            this.lblConn.AutoEllipsis = true;
            this.lblConn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblConn.Name = "lblConn";
            this.lblConn.TabIndex = 0;
            this.lblConn.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnReconnect
            //
            this.btnReconnect.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnReconnect.Name = "btnReconnect";
            this.btnReconnect.Size = new System.Drawing.Size(90, 24);
            this.btnReconnect.TabIndex = 1;
            this.btnReconnect.Text = "重新連線";
            //
            // btnConnect
            //
            this.btnConnect.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(100, 24);
            this.btnConnect.TabIndex = 2;
            this.btnConnect.Text = "設定連線…";
            //
            // lblConnCaption
            //
            this.lblConnCaption.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblConnCaption.Name = "lblConnCaption";
            this.lblConnCaption.Size = new System.Drawing.Size(80, 24);
            this.lblConnCaption.TabIndex = 3;
            this.lblConnCaption.Text = "資料庫連線";
            this.lblConnCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // mainSplit
            //
            this.mainSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.mainSplit.Location = new System.Drawing.Point(0, 57);
            this.mainSplit.Name = "mainSplit";
            //
            // mainSplit.Panel1
            //
            this.mainSplit.Panel1.Controls.Add(this.leftSplit);
            //
            // mainSplit.Panel2
            //
            this.mainSplit.Panel2.Controls.Add(this.rightSplit);
            this.mainSplit.Size = new System.Drawing.Size(1384, 732);
            this.mainSplit.SplitterDistance = 360;
            this.mainSplit.TabIndex = 0;
            //
            // leftSplit
            //
            this.leftSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftSplit.Location = new System.Drawing.Point(0, 0);
            this.leftSplit.Name = "leftSplit";
            this.leftSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // leftSplit.Panel1
            //
            this.leftSplit.Panel1.Controls.Add(this.leftTabs);
            //
            // leftSplit.Panel2
            //
            this.leftSplit.Panel2.Controls.Add(this.columnGrid);
            this.leftSplit.Panel2.Controls.Add(this.lblColumnProps);
            this.leftSplit.Size = new System.Drawing.Size(360, 732);
            this.leftSplit.SplitterDistance = 440;
            this.leftSplit.TabIndex = 0;
            //
            // leftTabs
            //
            this.leftTabs.Controls.Add(this.tabTables);
            this.leftTabs.Controls.Add(this.tabToolbox);
            this.leftTabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.leftTabs.Location = new System.Drawing.Point(0, 0);
            this.leftTabs.Name = "leftTabs";
            this.leftTabs.SelectedIndex = 0;
            this.leftTabs.Size = new System.Drawing.Size(360, 440);
            this.leftTabs.TabIndex = 0;
            //
            // tabTables
            //
            this.tabTables.Controls.Add(this.treeTables);
            this.tabTables.Controls.Add(this.lblTreeHint);
            this.tabTables.Controls.Add(this.txtTableFilter);
            this.tabTables.Name = "tabTables";
            this.tabTables.TabIndex = 0;
            this.tabTables.Text = "資料表";
            //
            // treeTables
            //
            this.treeTables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeTables.HideSelection = false;
            this.treeTables.Name = "treeTables";
            this.treeTables.TabIndex = 0;
            //
            // lblTreeHint
            //
            this.lblTreeHint.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblTreeHint.Name = "lblTreeHint";
            this.lblTreeHint.Size = new System.Drawing.Size(352, 20);
            this.lblTreeHint.TabIndex = 1;
            this.lblTreeHint.Text = "欄位拖到畫面 = 單頭欄位；整個資料表拖到畫面 = 單身 Grid";
            //
            // txtTableFilter
            //
            this.txtTableFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtTableFilter.Name = "txtTableFilter";
            this.txtTableFilter.PlaceholderText = "篩選資料表";
            this.txtTableFilter.TabIndex = 2;
            //
            // tabToolbox
            //
            this.tabToolbox.Controls.Add(this.toolbox);
            this.tabToolbox.Controls.Add(this.lblToolboxHint);
            this.tabToolbox.Name = "tabToolbox";
            this.tabToolbox.Padding = new System.Windows.Forms.Padding(4);
            this.tabToolbox.TabIndex = 1;
            this.tabToolbox.Text = "工具箱";
            //
            // toolbox
            //
            this.toolbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolbox.Name = "toolbox";
            this.toolbox.TabIndex = 0;
            //
            // lblToolboxHint
            //
            this.lblToolboxHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblToolboxHint.Name = "lblToolboxHint";
            this.lblToolboxHint.Size = new System.Drawing.Size(344, 22);
            this.lblToolboxHint.TabIndex = 1;
            this.lblToolboxHint.Text = "拖到畫面上，或雙擊加入";
            //
            // columnGrid
            //
            this.columnGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.columnGrid.HelpVisible = false;
            this.columnGrid.Name = "columnGrid";
            this.columnGrid.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.columnGrid.TabIndex = 0;
            this.columnGrid.ToolbarVisible = false;
            //
            // lblColumnProps
            //
            this.lblColumnProps.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblColumnProps.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblColumnProps.Name = "lblColumnProps";
            this.lblColumnProps.Size = new System.Drawing.Size(360, 20);
            this.lblColumnProps.TabIndex = 1;
            this.lblColumnProps.Text = "欄位屬性";
            //
            // rightSplit
            //
            this.rightSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.rightSplit.Location = new System.Drawing.Point(0, 0);
            this.rightSplit.Name = "rightSplit";
            //
            // rightSplit.Panel1
            //
            this.rightSplit.Panel1.Controls.Add(this.canvasHost);
            //
            // rightSplit.Panel2
            //
            this.rightSplit.Panel2.Controls.Add(this.propertyGrid);
            this.rightSplit.Size = new System.Drawing.Size(1020, 732);
            this.rightSplit.SplitterDistance = 716;
            this.rightSplit.TabIndex = 0;
            //
            // canvasHost
            //
            this.canvasHost.AutoScroll = true;
            this.canvasHost.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.canvasHost.Controls.Add(this.canvas);
            this.canvasHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvasHost.Location = new System.Drawing.Point(0, 0);
            this.canvasHost.Name = "canvasHost";
            this.canvasHost.Size = new System.Drawing.Size(716, 732);
            this.canvasHost.TabIndex = 0;
            //
            // canvas
            //
            this.canvas.AllowDrop = true;
            this.canvas.BackColor = System.Drawing.SystemColors.Control;
            this.canvas.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.canvas.Location = new System.Drawing.Point(10, 10);
            this.canvas.Name = "canvas";
            this.canvas.Size = new System.Drawing.Size(800, 500);
            this.canvas.TabIndex = 0;
            //
            // propertyGrid
            //
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.TabIndex = 0;
            this.propertyGrid.ToolbarVisible = false;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 789);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1384, 22);
            this.statusStrip.TabIndex = 3;
            //
            // lblStatus
            //
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Spring = true;
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // DesignerForm
            //
            this.ClientSize = new System.Drawing.Size(1384, 811);
            this.Controls.Add(this.mainSplit);
            this.Controls.Add(this.connPanel);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.Name = "DesignerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "畫面設計（後台）";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.connPanel.ResumeLayout(false);
            this.mainSplit.Panel1.ResumeLayout(false);
            this.mainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).EndInit();
            this.mainSplit.ResumeLayout(false);
            this.leftSplit.Panel1.ResumeLayout(false);
            this.leftSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.leftSplit)).EndInit();
            this.leftSplit.ResumeLayout(false);
            this.leftTabs.ResumeLayout(false);
            this.tabTables.ResumeLayout(false);
            this.tabTables.PerformLayout();
            this.tabToolbox.ResumeLayout(false);
            this.rightSplit.Panel1.ResumeLayout(false);
            this.rightSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rightSplit)).EndInit();
            this.rightSplit.ResumeLayout(false);
            this.canvasHost.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnNewForm;
        private System.Windows.Forms.ToolStripButton btnOpen;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripButton btnSaveAs;
        private System.Windows.Forms.ToolStripSeparator sepFile;
        private System.Windows.Forms.ToolStripButton btnRemove;
        private System.Windows.Forms.ToolStripButton btnAddAll;
        private System.Windows.Forms.ToolStripButton btnGridColumns;
        private System.Windows.Forms.ToolStripSeparator sepEdit;
        private System.Windows.Forms.ToolStripButton btnPreview;
        private System.Windows.Forms.ToolStripSeparator sepAlign;
        private System.Windows.Forms.Panel connPanel;
        private System.Windows.Forms.Label lblConn;
        private System.Windows.Forms.Button btnReconnect;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Label lblConnCaption;
        private System.Windows.Forms.SplitContainer mainSplit;
        private System.Windows.Forms.SplitContainer leftSplit;
        private System.Windows.Forms.TabControl leftTabs;
        private System.Windows.Forms.TabPage tabTables;
        private System.Windows.Forms.TreeView treeTables;
        private System.Windows.Forms.Label lblTreeHint;
        private System.Windows.Forms.TextBox txtTableFilter;
        private System.Windows.Forms.TabPage tabToolbox;
        private FormBuilder.ToolboxList toolbox;
        private System.Windows.Forms.Label lblToolboxHint;
        private System.Windows.Forms.PropertyGrid columnGrid;
        private System.Windows.Forms.Label lblColumnProps;
        private System.Windows.Forms.SplitContainer rightSplit;
        private System.Windows.Forms.Panel canvasHost;
        private System.Windows.Forms.Panel canvas;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
