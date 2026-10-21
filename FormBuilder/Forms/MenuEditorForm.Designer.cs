namespace FormBuilder
{
    partial class MenuEditorForm
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
            this.treeMenu = new System.Windows.Forms.TreeView();
            this.buttons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAddFolder = new FormBuilder.RoundButton();
            this.btnAddForm = new FormBuilder.RoundButton();
            this.btnRename = new FormBuilder.RoundButton();
            this.btnDelete = new FormBuilder.RoundButton();
            this.btnUp = new FormBuilder.RoundButton();
            this.btnDown = new FormBuilder.RoundButton();
            this.btnOutdent = new FormBuilder.RoundButton();
            this.bottom = new System.Windows.Forms.Panel();
            this.btnSave = new FormBuilder.RoundButton();
            this.btnCancel = new FormBuilder.RoundButton();
            this.lblHint = new System.Windows.Forms.Label();
            this.buttons.SuspendLayout();
            this.bottom.SuspendLayout();
            this.SuspendLayout();
            //
            // treeMenu
            //
            this.treeMenu.AllowDrop = true;
            this.treeMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeMenu.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.treeMenu.HideSelection = false;
            this.treeMenu.ItemHeight = 24;
            this.treeMenu.LabelEdit = true;
            this.treeMenu.Name = "treeMenu";
            this.treeMenu.TabIndex = 0;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.btnAddFolder);
            this.buttons.Controls.Add(this.btnAddForm);
            this.buttons.Controls.Add(this.btnRename);
            this.buttons.Controls.Add(this.btnDelete);
            this.buttons.Controls.Add(this.btnUp);
            this.buttons.Controls.Add(this.btnDown);
            this.buttons.Controls.Add(this.btnOutdent);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.buttons.Name = "buttons";
            this.buttons.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.buttons.Size = new System.Drawing.Size(130, 400);
            this.buttons.TabIndex = 1;
            //
            // btnAddFolder
            //
            this.btnAddFolder.Name = "btnAddFolder";
            this.btnAddFolder.Size = new System.Drawing.Size(112, 32);
            this.btnAddFolder.Text = "新增資料夾…";
            //
            // btnAddForm
            //
            this.btnAddForm.Name = "btnAddForm";
            this.btnAddForm.Size = new System.Drawing.Size(112, 32);
            this.btnAddForm.Text = "加入畫面…";
            //
            // btnRename
            //
            this.btnRename.Name = "btnRename";
            this.btnRename.Size = new System.Drawing.Size(112, 32);
            this.btnRename.Text = "改名（F2）";
            //
            // btnDelete
            //
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(112, 32);
            this.btnDelete.Text = "移除";
            //
            // btnUp
            //
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(112, 32);
            this.btnUp.Text = "上移";
            //
            // btnDown
            //
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(112, 32);
            this.btnDown.Text = "下移";
            //
            // btnOutdent
            //
            this.btnOutdent.Name = "btnOutdent";
            this.btnOutdent.Size = new System.Drawing.Size(112, 32);
            this.btnOutdent.Text = "移到上一層";
            //
            // bottom
            //
            this.bottom.Controls.Add(this.lblHint);
            this.bottom.Controls.Add(this.btnSave);
            this.bottom.Controls.Add(this.btnCancel);
            this.bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottom.Name = "bottom";
            this.bottom.Size = new System.Drawing.Size(560, 44);
            this.bottom.TabIndex = 2;
            //
            // lblHint
            //
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHint.Name = "lblHint";
            this.lblHint.Text = "可以拖曳節點調整位置（拖到資料夾上 = 放進資料夾；按住 Shift = 放在它下面同一層；拖到空白處 = 最上層）";
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnSave
            //
            this.btnSave.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 44);
            this.btnSave.Text = "儲存";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 44);
            this.btnCancel.Text = "取消";
            //
            // MenuEditorForm
            //
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(580, 560);
            this.Controls.Add(this.treeMenu);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.bottom);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 9F);
            this.MinimizeBox = false;
            this.Name = "MenuEditorForm";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "選單管理";
            this.buttons.ResumeLayout(false);
            this.bottom.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TreeView treeMenu;
        private System.Windows.Forms.FlowLayoutPanel buttons;
        private FormBuilder.RoundButton btnAddFolder;
        private FormBuilder.RoundButton btnAddForm;
        private FormBuilder.RoundButton btnRename;
        private FormBuilder.RoundButton btnDelete;
        private FormBuilder.RoundButton btnUp;
        private FormBuilder.RoundButton btnDown;
        private FormBuilder.RoundButton btnOutdent;
        private System.Windows.Forms.Panel bottom;
        private FormBuilder.RoundButton btnSave;
        private FormBuilder.RoundButton btnCancel;
        private System.Windows.Forms.Label lblHint;
    }
}
