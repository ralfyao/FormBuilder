namespace FormBuilder
{
    partial class PrintDesignerForm
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
            this.btnFileMenu = new System.Windows.Forms.ToolStripDropDownButton();
            this.miSave = new System.Windows.Forms.ToolStripMenuItem();
            this.miSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.btnEditMenu = new System.Windows.Forms.ToolStripDropDownButton();
            this.miUndo = new System.Windows.Forms.ToolStripMenuItem();
            this.miRedo = new System.Windows.Forms.ToolStripMenuItem();
            this.miRemove = new System.Windows.Forms.ToolStripMenuItem();
            this.sepEditAlign = new System.Windows.Forms.ToolStripSeparator();
            this.sepMenus = new System.Windows.Forms.ToolStripSeparator();
            this.btnPreview = new System.Windows.Forms.ToolStripButton();
            this.lblSnapHint = new System.Windows.Forms.ToolStripLabel();
            this.left = new System.Windows.Forms.Panel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabColumns = new System.Windows.Forms.TabPage();
            this.lstColumns = new System.Windows.Forms.ListBox();
            this.btnJoin = new System.Windows.Forms.Button();
            this.lblColumnsHint = new System.Windows.Forms.Label();
            this.tabTools = new System.Windows.Forms.TabPage();
            this.lstTools = new System.Windows.Forms.ListBox();
            this.lblToolsHint = new System.Windows.Forms.Label();
            this.right = new System.Windows.Forms.Panel();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.lblProps = new System.Windows.Forms.Label();
            this.canvasHost = new System.Windows.Forms.Panel();
            this.canvas = new System.Windows.Forms.Panel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip.SuspendLayout();
            this.left.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabColumns.SuspendLayout();
            this.tabTools.SuspendLayout();
            this.right.SuspendLayout();
            this.canvasHost.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // toolStrip
            //
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnFileMenu,
            this.btnEditMenu,
            this.sepMenus,
            this.btnPreview,
            this.lblSnapHint});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1284, 25);
            this.toolStrip.TabIndex = 0;
            //
            // btnFileMenu
            //
            this.btnFileMenu.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnFileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miSave,
            this.miSaveAs});
            this.btnFileMenu.Name = "btnFileMenu";
            this.btnFileMenu.Text = "檔案";
            //
            // miSave
            //
            this.miSave.Name = "miSave";
            this.miSave.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            this.miSave.Text = "儲存";
            //
            // miSaveAs
            //
            this.miSaveAs.Name = "miSaveAs";
            this.miSaveAs.Text = "另存新檔…";
            //
            // btnEditMenu（水平 / 垂直 / 大小的對齊選單由 AlignHelper.AddAlignMenu 在執行時加入）
            //
            this.btnEditMenu.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEditMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miUndo,
            this.miRedo,
            this.miRemove,
            this.sepEditAlign});
            this.btnEditMenu.Name = "btnEditMenu";
            this.btnEditMenu.Text = "編輯";
            //
            // miUndo
            //
            this.miUndo.Name = "miUndo";
            this.miUndo.ShortcutKeyDisplayString = "Ctrl+Z";
            this.miUndo.Text = "復原";
            //
            // miRedo
            //
            this.miRedo.Name = "miRedo";
            this.miRedo.ShortcutKeyDisplayString = "Ctrl+Y";
            this.miRedo.Text = "取消復原";
            //
            // miRemove
            //
            this.miRemove.Name = "miRemove";
            this.miRemove.ShortcutKeyDisplayString = "Delete";
            this.miRemove.Text = "刪除選取的元件";
            //
            // sepEditAlign
            //
            this.sepEditAlign.Name = "sepEditAlign";
            //
            // sepMenus
            //
            this.sepMenus.Name = "sepMenus";
            //
            // btnPreview
            //
            this.btnPreview.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Text = "預覽列印（第一筆資料）";
            //
            // lblSnapHint
            //
            this.lblSnapHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblSnapHint.Name = "lblSnapHint";
            this.lblSnapHint.Text = "　欄位、工具箱拖到紙張上；拖曳時靠近其他元件會自動貼齊（按住 Alt 不貼齊）";
            //
            // left
            //
            this.left.Controls.Add(this.tabs);
            this.left.Dock = System.Windows.Forms.DockStyle.Left;
            this.left.Location = new System.Drawing.Point(0, 25);
            this.left.Name = "left";
            this.left.Padding = new System.Windows.Forms.Padding(6);
            this.left.Size = new System.Drawing.Size(230, 714);
            this.left.TabIndex = 1;
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabColumns);
            this.tabs.Controls.Add(this.tabTools);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Location = new System.Drawing.Point(6, 6);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(218, 702);
            this.tabs.TabIndex = 0;
            //
            // tabColumns
            //
            this.tabColumns.Controls.Add(this.lstColumns);
            this.tabColumns.Controls.Add(this.btnJoin);
            this.tabColumns.Controls.Add(this.lblColumnsHint);
            this.tabColumns.Name = "tabColumns";
            this.tabColumns.Padding = new System.Windows.Forms.Padding(4);
            this.tabColumns.TabIndex = 0;
            this.tabColumns.Text = "欄位";
            //
            // lstColumns
            //
            this.lstColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstColumns.IntegralHeight = false;
            this.lstColumns.Name = "lstColumns";
            this.lstColumns.TabIndex = 0;
            //
            // btnJoin
            //
            this.btnJoin.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnJoin.Name = "btnJoin";
            this.btnJoin.Size = new System.Drawing.Size(202, 30);
            this.btnJoin.TabIndex = 1;
            this.btnJoin.Text = "JOIN 其他資料表…";
            //
            // lblColumnsHint
            //
            this.lblColumnsHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblColumnsHint.Name = "lblColumnsHint";
            this.lblColumnsHint.Size = new System.Drawing.Size(202, 36);
            this.lblColumnsHint.TabIndex = 2;
            this.lblColumnsHint.Text = "拖到紙張上，或雙擊加入；JOIN 的欄位排在最後（右鍵可移除 JOIN）";
            //
            // tabTools
            //
            this.tabTools.Controls.Add(this.lstTools);
            this.tabTools.Controls.Add(this.lblToolsHint);
            this.tabTools.Name = "tabTools";
            this.tabTools.Padding = new System.Windows.Forms.Padding(4);
            this.tabTools.TabIndex = 1;
            this.tabTools.Text = "工具箱";
            //
            // lstTools
            //
            this.lstTools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstTools.IntegralHeight = false;
            this.lstTools.ItemHeight = 24;
            this.lstTools.Name = "lstTools";
            this.lstTools.TabIndex = 0;
            //
            // lblToolsHint
            //
            this.lblToolsHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblToolsHint.Name = "lblToolsHint";
            this.lblToolsHint.Size = new System.Drawing.Size(202, 22);
            this.lblToolsHint.TabIndex = 1;
            this.lblToolsHint.Text = "拖到紙張上，或雙擊加入";
            //
            // right
            //
            this.right.Controls.Add(this.propertyGrid);
            this.right.Controls.Add(this.lblProps);
            this.right.Dock = System.Windows.Forms.DockStyle.Right;
            this.right.Location = new System.Drawing.Point(994, 25);
            this.right.Name = "right";
            this.right.Padding = new System.Windows.Forms.Padding(6);
            this.right.Size = new System.Drawing.Size(290, 714);
            this.right.TabIndex = 2;
            //
            // propertyGrid
            //
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.TabIndex = 0;
            this.propertyGrid.ToolbarVisible = false;
            //
            // lblProps
            //
            this.lblProps.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblProps.Name = "lblProps";
            this.lblProps.Size = new System.Drawing.Size(278, 22);
            this.lblProps.TabIndex = 1;
            this.lblProps.Text = "屬性（點空白處 = 紙張與資料設定）";
            //
            // canvasHost
            //
            this.canvasHost.AutoScroll = true;
            this.canvasHost.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.canvasHost.Controls.Add(this.canvas);
            this.canvasHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvasHost.Location = new System.Drawing.Point(230, 25);
            this.canvasHost.Name = "canvasHost";
            this.canvasHost.Size = new System.Drawing.Size(764, 714);
            this.canvasHost.TabIndex = 3;
            //
            // canvas（紙張）
            //
            this.canvas.AllowDrop = true;
            this.canvas.BackColor = System.Drawing.Color.White;
            this.canvas.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.canvas.Location = new System.Drawing.Point(20, 20);
            this.canvas.Name = "canvas";
            this.canvas.Size = new System.Drawing.Size(794, 1123);
            this.canvas.TabIndex = 0;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 739);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1284, 22);
            this.statusStrip.TabIndex = 4;
            //
            // lblStatus
            //
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Spring = true;
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // PrintDesignerForm
            //
            this.ClientSize = new System.Drawing.Size(1284, 761);
            this.Controls.Add(this.canvasHost);
            this.Controls.Add(this.right);
            this.Controls.Add(this.left);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.KeyPreview = true;
            this.MinimizeBox = false;
            this.Name = "PrintDesignerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "列印樣板設計";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.left.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.tabColumns.ResumeLayout(false);
            this.tabTools.ResumeLayout(false);
            this.right.ResumeLayout(false);
            this.canvasHost.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripDropDownButton btnFileMenu;
        private System.Windows.Forms.ToolStripMenuItem miSave;
        private System.Windows.Forms.ToolStripMenuItem miSaveAs;
        private System.Windows.Forms.ToolStripDropDownButton btnEditMenu;
        private System.Windows.Forms.ToolStripMenuItem miUndo;
        private System.Windows.Forms.ToolStripMenuItem miRedo;
        private System.Windows.Forms.ToolStripMenuItem miRemove;
        private System.Windows.Forms.ToolStripSeparator sepEditAlign;
        private System.Windows.Forms.ToolStripSeparator sepMenus;
        private System.Windows.Forms.ToolStripButton btnPreview;
        private System.Windows.Forms.ToolStripLabel lblSnapHint;
        private System.Windows.Forms.Panel left;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabColumns;
        private System.Windows.Forms.ListBox lstColumns;
        private System.Windows.Forms.Button btnJoin;
        private System.Windows.Forms.Label lblColumnsHint;
        private System.Windows.Forms.TabPage tabTools;
        private System.Windows.Forms.ListBox lstTools;
        private System.Windows.Forms.Label lblToolsHint;
        private System.Windows.Forms.Panel right;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Label lblProps;
        private System.Windows.Forms.Panel canvasHost;
        private System.Windows.Forms.Panel canvas;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
