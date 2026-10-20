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
            toolStrip = new ToolStrip();
            btnFileMenu = new ToolStripDropDownButton();
            btnNewForm = new ToolStripMenuItem();
            btnOpen = new ToolStripMenuItem();
            btnSave = new ToolStripMenuItem();
            btnSaveAs = new ToolStripMenuItem();
            sepFile = new ToolStripSeparator();
            btnEditMenu = new ToolStripDropDownButton();
            btnRemove = new ToolStripMenuItem();
            btnAddAll = new ToolStripMenuItem();
            btnGridColumns = new ToolStripMenuItem();
            sepEdit = new ToolStripSeparator();
            btnPreview = new ToolStripButton();
            sepAlign = new ToolStripSeparator();
            connPanel = new Panel();
            lblConn = new Label();
            btnReconnect = new Button();
            btnConnect = new Button();
            lblConnCaption = new Label();
            mainSplit = new SplitContainer();
            leftSplit = new SplitContainer();
            leftTabs = new TabControl();
            tabTables = new TabPage();
            treeTables = new TreeView();
            lblTreeHint = new Label();
            txtTableFilter = new TextBox();
            tabToolbox = new TabPage();
            toolbox = new ToolboxList();
            lblToolboxHint = new Label();
            columnGrid = new PropertyGrid();
            lblColumnProps = new Label();
            rightSplit = new SplitContainer();
            canvasHost = new Panel();
            canvas = new Panel();
            propertyGrid = new PropertyGrid();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            toolStrip.SuspendLayout();
            connPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplit).BeginInit();
            mainSplit.Panel1.SuspendLayout();
            mainSplit.Panel2.SuspendLayout();
            mainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)leftSplit).BeginInit();
            leftSplit.Panel1.SuspendLayout();
            leftSplit.Panel2.SuspendLayout();
            leftSplit.SuspendLayout();
            leftTabs.SuspendLayout();
            tabTables.SuspendLayout();
            tabToolbox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)rightSplit).BeginInit();
            rightSplit.Panel1.SuspendLayout();
            rightSplit.Panel2.SuspendLayout();
            rightSplit.SuspendLayout();
            canvasHost.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // toolStrip
            // 
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Items.AddRange(new ToolStripItem[] { btnFileMenu, btnEditMenu, sepFile, btnPreview, sepAlign });
            toolStrip.Location = new Point(0, 0);
            toolStrip.Name = "toolStrip";
            toolStrip.Size = new Size(1384, 25);
            toolStrip.TabIndex = 2;
            // 
            // btnNewForm
            // 
            btnFileMenu.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnFileMenu.DropDownItems.AddRange(new ToolStripItem[] { btnNewForm, btnOpen, btnSave, btnSaveAs });
            btnFileMenu.Name = "btnFileMenu";
            btnFileMenu.Text = "檔案";
            //
            // btnNewForm
            //
            btnNewForm.Name = "btnNewForm";
            btnNewForm.ShortcutKeys = Keys.Control | Keys.N;
            btnNewForm.Text = "開新畫面";
            // 
            // btnOpen
            // 
            btnOpen.Name = "btnOpen";
            btnOpen.ShortcutKeys = Keys.Control | Keys.O;
            btnOpen.Text = "開啟";
            // 
            // btnSave
            // 
            btnSave.Name = "btnSave";
            btnSave.ShortcutKeys = Keys.Control | Keys.S;
            btnSave.Text = "存檔";
            // 
            // btnSaveAs
            // 
            btnSaveAs.Name = "btnSaveAs";
            btnSaveAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            btnSaveAs.Text = "另存新檔";
            // 
            // sepFile
            // 
            sepFile.Name = "sepFile";
            sepFile.Size = new Size(6, 25);
            // 
            // btnRemove
            // 
            btnRemove.Name = "btnRemove";
            btnRemove.ShortcutKeyDisplayString = "Delete";
            btnRemove.Text = "移除選取欄位";
            //
            // btnAddAll
            //
            btnAddAll.Name = "btnAddAll";
            btnAddAll.Text = "加入全部欄位";
            //
            // btnGridColumns
            //
            btnGridColumns.Name = "btnGridColumns";
            btnGridColumns.Text = "單身欄位設定";
            //
            // btnEditMenu（水平 / 垂直 / 大小的對齊選單由 AlignHelper.AddAlignMenu 在執行時加入）
            //
            btnEditMenu.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnEditMenu.DropDownItems.AddRange(new ToolStripItem[] { btnRemove, btnAddAll, btnGridColumns, sepEdit });
            btnEditMenu.Name = "btnEditMenu";
            btnEditMenu.Text = "編輯";
            //
            // sepEdit
            //
            sepEdit.Name = "sepEdit";
            sepEdit.Size = new Size(6, 6);
            // 
            // btnPreview
            // 
            btnPreview.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPreview.Name = "btnPreview";
            btnPreview.Size = new Size(59, 22);
            btnPreview.Text = "預覽執行";
            // 
            // sepAlign
            // 
            sepAlign.Name = "sepAlign";
            sepAlign.Size = new Size(6, 25);
            // 
            // connPanel
            // 
            connPanel.Controls.Add(lblConn);
            connPanel.Controls.Add(btnReconnect);
            connPanel.Controls.Add(btnConnect);
            connPanel.Controls.Add(lblConnCaption);
            connPanel.Dock = DockStyle.Top;
            connPanel.Location = new Point(0, 25);
            connPanel.Name = "connPanel";
            connPanel.Padding = new Padding(6, 4, 6, 4);
            connPanel.Size = new Size(1384, 32);
            connPanel.TabIndex = 1;
            // 
            // lblConn
            // 
            lblConn.AutoEllipsis = true;
            lblConn.Dock = DockStyle.Fill;
            lblConn.Location = new Point(86, 4);
            lblConn.Name = "lblConn";
            lblConn.Size = new Size(1102, 24);
            lblConn.TabIndex = 0;
            lblConn.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnReconnect
            // 
            btnReconnect.Dock = DockStyle.Right;
            btnReconnect.Location = new Point(1188, 4);
            btnReconnect.Name = "btnReconnect";
            btnReconnect.Size = new Size(90, 24);
            btnReconnect.TabIndex = 1;
            btnReconnect.Text = "重新連線";
            // 
            // btnConnect
            // 
            btnConnect.Dock = DockStyle.Right;
            btnConnect.Location = new Point(1278, 4);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(100, 24);
            btnConnect.TabIndex = 2;
            btnConnect.Text = "設定連線…";
            // 
            // lblConnCaption
            // 
            lblConnCaption.Dock = DockStyle.Left;
            lblConnCaption.Location = new Point(6, 4);
            lblConnCaption.Name = "lblConnCaption";
            lblConnCaption.Size = new Size(80, 24);
            lblConnCaption.TabIndex = 3;
            lblConnCaption.Text = "資料庫連線";
            lblConnCaption.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // mainSplit
            // 
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.FixedPanel = FixedPanel.Panel1;
            mainSplit.Location = new Point(0, 57);
            mainSplit.Name = "mainSplit";
            // 
            // mainSplit.Panel1
            // 
            mainSplit.Panel1.Controls.Add(leftSplit);
            // 
            // mainSplit.Panel2
            // 
            mainSplit.Panel2.Controls.Add(rightSplit);
            mainSplit.Size = new Size(1384, 732);
            mainSplit.SplitterDistance = 360;
            mainSplit.TabIndex = 0;
            // 
            // leftSplit
            // 
            leftSplit.Dock = DockStyle.Fill;
            leftSplit.Location = new Point(0, 0);
            leftSplit.Name = "leftSplit";
            leftSplit.Orientation = Orientation.Horizontal;
            // 
            // leftSplit.Panel1
            // 
            leftSplit.Panel1.Controls.Add(leftTabs);
            // 
            // leftSplit.Panel2
            // 
            leftSplit.Panel2.Controls.Add(columnGrid);
            leftSplit.Panel2.Controls.Add(lblColumnProps);
            leftSplit.Size = new Size(360, 732);
            leftSplit.SplitterDistance = 440;
            leftSplit.TabIndex = 0;
            // 
            // leftTabs
            // 
            leftTabs.Controls.Add(tabTables);
            leftTabs.Controls.Add(tabToolbox);
            leftTabs.Dock = DockStyle.Fill;
            leftTabs.Location = new Point(0, 0);
            leftTabs.Name = "leftTabs";
            leftTabs.SelectedIndex = 0;
            leftTabs.Size = new Size(360, 440);
            leftTabs.TabIndex = 0;
            // 
            // tabTables
            // 
            tabTables.Controls.Add(treeTables);
            tabTables.Controls.Add(lblTreeHint);
            tabTables.Controls.Add(txtTableFilter);
            tabTables.Location = new Point(4, 24);
            tabTables.Name = "tabTables";
            tabTables.Size = new Size(352, 412);
            tabTables.TabIndex = 0;
            tabTables.Text = "資料表";
            // 
            // treeTables
            // 
            treeTables.Dock = DockStyle.Fill;
            treeTables.HideSelection = false;
            treeTables.Location = new Point(0, 23);
            treeTables.Name = "treeTables";
            treeTables.Size = new Size(352, 369);
            treeTables.TabIndex = 0;
            // 
            // lblTreeHint
            // 
            lblTreeHint.Dock = DockStyle.Bottom;
            lblTreeHint.Location = new Point(0, 392);
            lblTreeHint.Name = "lblTreeHint";
            lblTreeHint.Size = new Size(352, 20);
            lblTreeHint.TabIndex = 1;
            lblTreeHint.Text = "欄位拖到畫面 = 單頭欄位；整個資料表拖到畫面 = 單身 Grid";
            // 
            // txtTableFilter
            // 
            txtTableFilter.Dock = DockStyle.Top;
            txtTableFilter.Location = new Point(0, 0);
            txtTableFilter.Name = "txtTableFilter";
            txtTableFilter.PlaceholderText = "篩選資料表";
            txtTableFilter.Size = new Size(352, 23);
            txtTableFilter.TabIndex = 2;
            // 
            // tabToolbox
            // 
            tabToolbox.Controls.Add(toolbox);
            tabToolbox.Controls.Add(lblToolboxHint);
            tabToolbox.Location = new Point(4, 24);
            tabToolbox.Name = "tabToolbox";
            tabToolbox.Padding = new Padding(4);
            tabToolbox.Size = new Size(352, 412);
            tabToolbox.TabIndex = 1;
            tabToolbox.Text = "工具箱";
            // 
            // toolbox
            // 
            toolbox.Dock = DockStyle.Fill;
            toolbox.IntegralHeight = false;
            toolbox.ItemHeight = 15;
            toolbox.Location = new Point(4, 26);
            toolbox.Name = "toolbox";
            toolbox.Size = new Size(344, 382);
            toolbox.TabIndex = 0;
            // 
            // lblToolboxHint
            // 
            lblToolboxHint.Dock = DockStyle.Top;
            lblToolboxHint.Location = new Point(4, 4);
            lblToolboxHint.Name = "lblToolboxHint";
            lblToolboxHint.Size = new Size(344, 22);
            lblToolboxHint.TabIndex = 1;
            lblToolboxHint.Text = "拖到畫面上，或雙擊加入";
            // 
            // columnGrid
            // 
            columnGrid.Dock = DockStyle.Fill;
            columnGrid.HelpVisible = false;
            columnGrid.Location = new Point(0, 20);
            columnGrid.Name = "columnGrid";
            columnGrid.PropertySort = PropertySort.NoSort;
            columnGrid.Size = new Size(360, 268);
            columnGrid.TabIndex = 0;
            columnGrid.ToolbarVisible = false;
            // 
            // lblColumnProps
            // 
            lblColumnProps.Dock = DockStyle.Top;
            lblColumnProps.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold);
            lblColumnProps.Location = new Point(0, 0);
            lblColumnProps.Name = "lblColumnProps";
            lblColumnProps.Size = new Size(360, 20);
            lblColumnProps.TabIndex = 1;
            lblColumnProps.Text = "欄位屬性";
            // 
            // rightSplit
            // 
            rightSplit.Dock = DockStyle.Fill;
            rightSplit.FixedPanel = FixedPanel.Panel2;
            rightSplit.Location = new Point(0, 0);
            rightSplit.Name = "rightSplit";
            // 
            // rightSplit.Panel1
            // 
            rightSplit.Panel1.Controls.Add(canvasHost);
            // 
            // rightSplit.Panel2
            // 
            rightSplit.Panel2.Controls.Add(propertyGrid);
            rightSplit.Size = new Size(1020, 732);
            rightSplit.SplitterDistance = 716;
            rightSplit.TabIndex = 0;
            // 
            // canvasHost
            // 
            canvasHost.AutoScroll = true;
            canvasHost.BackColor = SystemColors.AppWorkspace;
            canvasHost.Controls.Add(canvas);
            canvasHost.Dock = DockStyle.Fill;
            canvasHost.Location = new Point(0, 0);
            canvasHost.Name = "canvasHost";
            canvasHost.Size = new Size(716, 732);
            canvasHost.TabIndex = 0;
            // 
            // canvas
            // 
            canvas.AllowDrop = true;
            canvas.BackColor = SystemColors.Control;
            canvas.BorderStyle = BorderStyle.FixedSingle;
            canvas.Location = new Point(7, 6);
            canvas.Name = "canvas";
            canvas.Size = new Size(800, 500);
            canvas.TabIndex = 0;
            // 
            // propertyGrid
            // 
            propertyGrid.Dock = DockStyle.Fill;
            propertyGrid.Location = new Point(0, 0);
            propertyGrid.Name = "propertyGrid";
            propertyGrid.Size = new Size(300, 732);
            propertyGrid.TabIndex = 0;
            propertyGrid.ToolbarVisible = false;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 789);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1384, 22);
            statusStrip.TabIndex = 3;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(1369, 17);
            lblStatus.Spring = true;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // DesignerForm
            // 
            ClientSize = new Size(1384, 811);
            Controls.Add(mainSplit);
            Controls.Add(connPanel);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);
            Font = new Font("Microsoft JhengHei UI", 9F);
            Name = "DesignerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "畫面設計（後台）";
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            connPanel.ResumeLayout(false);
            mainSplit.Panel1.ResumeLayout(false);
            mainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplit).EndInit();
            mainSplit.ResumeLayout(false);
            leftSplit.Panel1.ResumeLayout(false);
            leftSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)leftSplit).EndInit();
            leftSplit.ResumeLayout(false);
            leftTabs.ResumeLayout(false);
            tabTables.ResumeLayout(false);
            tabTables.PerformLayout();
            tabToolbox.ResumeLayout(false);
            rightSplit.Panel1.ResumeLayout(false);
            rightSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)rightSplit).EndInit();
            rightSplit.ResumeLayout(false);
            canvasHost.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripDropDownButton btnFileMenu;
        private System.Windows.Forms.ToolStripMenuItem btnNewForm;
        private System.Windows.Forms.ToolStripMenuItem btnOpen;
        private System.Windows.Forms.ToolStripMenuItem btnSave;
        private System.Windows.Forms.ToolStripMenuItem btnSaveAs;
        private System.Windows.Forms.ToolStripSeparator sepFile;
        private System.Windows.Forms.ToolStripDropDownButton btnEditMenu;
        private System.Windows.Forms.ToolStripMenuItem btnRemove;
        private System.Windows.Forms.ToolStripMenuItem btnAddAll;
        private System.Windows.Forms.ToolStripMenuItem btnGridColumns;
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
