namespace FormBuilder
{
    partial class LauncherForm
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
            this.lstForms = new System.Windows.Forms.ListBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lstForms
            //
            this.lstForms.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstForms.IntegralHeight = false;
            this.lstForms.Location = new System.Drawing.Point(0, 0);
            this.lstForms.Name = "lstForms";
            this.lstForms.Size = new System.Drawing.Size(384, 427);
            this.lstForms.TabIndex = 0;
            //
            // btnRefresh
            //
            this.btnRefresh.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnRefresh.Location = new System.Drawing.Point(0, 427);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(384, 34);
            this.btnRefresh.TabIndex = 1;
            this.btnRefresh.Text = "重新整理";
            //
            // LauncherForm
            //
            this.ClientSize = new System.Drawing.Size(384, 461);
            this.Controls.Add(this.lstForms);
            this.Controls.Add(this.btnRefresh);
            this.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F);
            this.Name = "LauncherForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "畫面清單";
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListBox lstForms;
        private System.Windows.Forms.Button btnRefresh;
    }
}
