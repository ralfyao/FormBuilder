namespace FormBuilder
{
    partial class EditLayoutDialog
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
            this.center = new System.Windows.Forms.Panel();
            this.canvasHost = new System.Windows.Forms.Panel();
            this.canvas = new System.Windows.Forms.Panel();
            this.alignBar = new System.Windows.Forms.ToolStrip();
            this.effectBar = new System.Windows.Forms.ToolStrip();
            this.lblEffectCaption = new System.Windows.Forms.ToolStripLabel();
            this.btnEffectiveColumn = new System.Windows.Forms.ToolStripDropDownButton();
            this.lblPrintHint = new System.Windows.Forms.ToolStripLabel();
            this.left = new System.Windows.Forms.Panel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabColumns = new System.Windows.Forms.TabPage();
            this.lstAvailable = new System.Windows.Forms.ListBox();
            this.lblColumnsHint = new System.Windows.Forms.Label();
            this.tabToolbox = new System.Windows.Forms.TabPage();
            this.toolbox = new FormBuilder.ToolboxList();
            this.lblToolboxHint = new System.Windows.Forms.Label();
            this.right = new System.Windows.Forms.Panel();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.lblProps = new System.Windows.Forms.Label();
            this.bottom = new System.Windows.Forms.Panel();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnArrange = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.center.SuspendLayout();
            this.canvasHost.SuspendLayout();
            this.left.SuspendLayout();
            this.tabs.SuspendLayout();
            this.tabColumns.SuspendLayout();
            this.tabToolbox.SuspendLayout();
            this.right.SuspendLayout();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // center
            //
            this.center.Controls.Add(this.canvasHost);
            this.center.Controls.Add(this.alignBar);
            this.center.Controls.Add(this.effectBar);
            this.center.Dock = System.Windows.Forms.DockStyle.Fill;
            this.center.Location = new System.Drawing.Point(220, 0);
            this.center.Name = "center";
            this.center.Size = new System.Drawing.Size(564, 621);
            this.center.TabIndex = 0;
            //
            // canvasHost
            //
            this.canvasHost.AutoScroll = true;
            this.canvasHost.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.canvasHost.Controls.Add(this.canvas);
            this.canvasHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.canvasHost.Location = new System.Drawing.Point(0, 25);
            this.canvasHost.Name = "canvasHost";
            this.canvasHost.Size = new System.Drawing.Size(564, 596);
            this.canvasHost.TabIndex = 0;
            //
            // canvas
            //
            this.canvas.AllowDrop = true;
            this.canvas.BackColor = System.Drawing.SystemColors.Control;
            this.canvas.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.canvas.Location = new System.Drawing.Point(10, 10);
            this.canvas.Name = "canvas";
            this.canvas.Size = new System.Drawing.Size(500, 300);
            this.canvas.TabIndex = 0;
            //
            // alignBar
            //
            this.alignBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.alignBar.Location = new System.Drawing.Point(0, 0);
            this.alignBar.Name = "alignBar";
            this.alignBar.Size = new System.Drawing.Size(564, 25);
            this.alignBar.TabIndex = 1;
            //
            // effectBar
            //
            this.effectBar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.effectBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblEffectCaption,
            this.btnEffectiveColumn,
            this.lblPrintHint});
            this.effectBar.Location = new System.Drawing.Point(0, 0);
            this.effectBar.Name = "effectBar";
            this.effectBar.Size = new System.Drawing.Size(564, 25);
            this.effectBar.TabIndex = 2;
            //
            // lblEffectCaption
            //
            this.lblEffectCaption.Name = "lblEffectCaption";
            this.lblEffectCaption.Text = "生效 / 取消生效：";
            //
            // btnEffectiveColumn
            //
            this.btnEffectiveColumn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEffectiveColumn.Name = "btnEffectiveColumn";
            this.btnEffectiveColumn.Text = "（不使用）";
            this.btnEffectiveColumn.ToolTipText = "指定哪個欄位是生效欄位：執行時「生效」改成 1、「取消生效」改成 0";
            //
            // lblPrintHint
            //
            this.lblPrintHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblPrintHint.Name = "lblPrintHint";
            this.lblPrintHint.Text = "（列印：生效的資料才能按）";
            //
            // left
            //
            this.left.Controls.Add(this.tabs);
            this.left.Dock = System.Windows.Forms.DockStyle.Left;
            this.left.Location = new System.Drawing.Point(0, 0);
            this.left.Name = "left";
            this.left.Padding = new System.Windows.Forms.Padding(6);
            this.left.Size = new System.Drawing.Size(220, 621);
            this.left.TabIndex = 1;
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabColumns);
            this.tabs.Controls.Add(this.tabToolbox);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Location = new System.Drawing.Point(6, 6);
            this.tabs.Name = "tabs";
            this.tabs.SelectedIndex = 0;
            this.tabs.Size = new System.Drawing.Size(208, 609);
            this.tabs.TabIndex = 0;
            //
            // tabColumns
            //
            this.tabColumns.Controls.Add(this.lstAvailable);
            this.tabColumns.Controls.Add(this.lblColumnsHint);
            this.tabColumns.Name = "tabColumns";
            this.tabColumns.Padding = new System.Windows.Forms.Padding(4);
            this.tabColumns.TabIndex = 0;
            this.tabColumns.Text = "欄位";
            //
            // lstAvailable
            //
            this.lstAvailable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstAvailable.IntegralHeight = false;
            this.lstAvailable.Name = "lstAvailable";
            this.lstAvailable.TabIndex = 0;
            //
            // lblColumnsHint
            //
            this.lblColumnsHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblColumnsHint.Name = "lblColumnsHint";
            this.lblColumnsHint.Size = new System.Drawing.Size(192, 22);
            this.lblColumnsHint.TabIndex = 1;
            this.lblColumnsHint.Text = "拖到右邊版面，或雙擊加入";
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
            this.lblToolboxHint.Size = new System.Drawing.Size(192, 22);
            this.lblToolboxHint.TabIndex = 1;
            this.lblToolboxHint.Text = "拖到右邊版面，或雙擊加入";
            //
            // right
            //
            this.right.Controls.Add(this.propertyGrid);
            this.right.Controls.Add(this.lblProps);
            this.right.Dock = System.Windows.Forms.DockStyle.Right;
            this.right.Location = new System.Drawing.Point(784, 0);
            this.right.Name = "right";
            this.right.Padding = new System.Windows.Forms.Padding(6);
            this.right.Size = new System.Drawing.Size(280, 621);
            this.right.TabIndex = 2;
            //
            // propertyGrid
            //
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.propertyGrid.TabIndex = 0;
            this.propertyGrid.ToolbarVisible = false;
            //
            // lblProps
            //
            this.lblProps.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblProps.Name = "lblProps";
            this.lblProps.Size = new System.Drawing.Size(268, 36);
            this.lblProps.TabIndex = 1;
            this.lblProps.Text = "屬性（點選欄位或按鈕後修改；多選時一次改全部）";
            //
            // bottom
            //
            this.bottom.Controls.Add(this.lblHint);
            this.bottom.Controls.Add(this.btnRemove);
            this.bottom.Controls.Add(this.btnArrange);
            this.bottom.Controls.Add(this.btnOk);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Location = new System.Drawing.Point(0, 621);
            this.bottom.Name = "bottom";
            this.bottom.Padding = new System.Windows.Forms.Padding(6);
            this.bottom.Size = new System.Drawing.Size(1064, 40);
            this.bottom.TabIndex = 3;
            //
            // lblHint
            //
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHint.Name = "lblHint";
            this.lblHint.TabIndex = 0;
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnRemove
            //
            this.btnRemove.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(110, 28);
            this.btnRemove.TabIndex = 1;
            this.btnRemove.Text = "刪除選取欄位";
            //
            // btnArrange
            //
            this.btnArrange.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnArrange.Name = "btnArrange";
            this.btnArrange.Size = new System.Drawing.Size(130, 28);
            this.btnArrange.TabIndex = 2;
            this.btnArrange.Text = "重新排列成一欄";
            //
            // btnOk
            //
            this.btnOk.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 28);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "確定";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "取消";
            //
            // EditLayoutDialog
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(1064, 661);
            this.Controls.Add(this.center);
            this.Controls.Add(this.left);
            this.Controls.Add(this.right);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.KeyPreview = true;
            this.Name = "EditLayoutDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "編輯視窗版面";
            this.center.ResumeLayout(false);
            this.center.PerformLayout();
            this.canvasHost.ResumeLayout(false);
            this.left.ResumeLayout(false);
            this.tabs.ResumeLayout(false);
            this.tabColumns.ResumeLayout(false);
            this.tabToolbox.ResumeLayout(false);
            this.right.ResumeLayout(false);
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel center;
        private System.Windows.Forms.Panel canvasHost;
        private System.Windows.Forms.Panel canvas;
        private System.Windows.Forms.ToolStrip alignBar;
        private System.Windows.Forms.ToolStrip effectBar;
        private System.Windows.Forms.ToolStripLabel lblEffectCaption;
        private System.Windows.Forms.ToolStripDropDownButton btnEffectiveColumn;
        private System.Windows.Forms.ToolStripLabel lblPrintHint;
        private System.Windows.Forms.Panel left;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabColumns;
        private System.Windows.Forms.ListBox lstAvailable;
        private System.Windows.Forms.Label lblColumnsHint;
        private System.Windows.Forms.TabPage tabToolbox;
        private FormBuilder.ToolboxList toolbox;
        private System.Windows.Forms.Label lblToolboxHint;
        private System.Windows.Forms.Panel right;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Label lblProps;
        private System.Windows.Forms.Panel bottom;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnArrange;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
