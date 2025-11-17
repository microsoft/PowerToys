namespace MouseWithoutBorders
{
    partial class Machine
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.labelStatusServer = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.checkBoxEnabled = new System.Windows.Forms.CheckBox();
            this.pictureBoxLogo = new System.Windows.Forms.PictureBox();
            this.labelStatusClient = new System.Windows.Forms.Label();
            this.toolTipHelp = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // labelStatusServer
            // 
            this.labelStatusServer.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelStatusServer.Location = new System.Drawing.Point(0, 90);
            this.labelStatusServer.Name = "labelStatusServer";
            this.labelStatusServer.Size = new System.Drawing.Size(106, 16);
            this.labelStatusServer.TabIndex = 0;
            this.labelStatusServer.Text = "...";
            this.labelStatusServer.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // textBoxName
            // 
            this.textBoxName.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textBoxName.Location = new System.Drawing.Point(0, 54);
            this.textBoxName.Name = "textBoxName";
            this.textBoxName.Size = new System.Drawing.Size(106, 20);
            this.textBoxName.TabIndex = 1;
            this.textBoxName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // checkBoxEnabled
            // 
            this.checkBoxEnabled.AutoSize = true;
            this.checkBoxEnabled.Checked = true;
            this.checkBoxEnabled.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxEnabled.Location = new System.Drawing.Point(91, 40);
            this.checkBoxEnabled.Name = "checkBoxEnabled";
            this.checkBoxEnabled.Size = new System.Drawing.Size(15, 14);
            this.checkBoxEnabled.TabIndex = 3;
            this.checkBoxEnabled.UseVisualStyleBackColor = true;
            this.checkBoxEnabled.CheckedChanged += new System.EventHandler(this.CheckBoxEnabled_CheckedChanged);
            // 
            // pictureBoxLogo
            // 
            this.pictureBoxLogo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxLogo.ErrorImage = null;
            this.pictureBoxLogo.InitialImage = null;
            this.pictureBoxLogo.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxLogo.Name = "pictureBoxLogo";
            this.pictureBoxLogo.Size = new System.Drawing.Size(106, 54);
            this.pictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxLogo.TabIndex = 2;
            this.pictureBoxLogo.TabStop = false;
            this.toolTipHelp.SetToolTip(this.pictureBoxLogo, "Drag to match machine physical layout. Check the checkbox to enter machine name.");
            this.pictureBoxLogo.Paint += new System.Windows.Forms.PaintEventHandler(this.PictureBoxLogo_Paint);
            this.pictureBoxLogo.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PictureBoxLogo_MouseDown);
            // 
            // labelStatusClient
            // 
            this.labelStatusClient.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelStatusClient.Location = new System.Drawing.Point(0, 74);
            this.labelStatusClient.Name = "labelStatusClient";
            this.labelStatusClient.Size = new System.Drawing.Size(106, 16);
            this.labelStatusClient.TabIndex = 4;
            this.labelStatusClient.Text = "...";
            this.labelStatusClient.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // Machine
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.Controls.Add(this.checkBoxEnabled);
            this.Controls.Add(this.pictureBoxLogo);
            this.Controls.Add(this.textBoxName);
            this.Controls.Add(this.labelStatusClient);
            this.Controls.Add(this.labelStatusServer);
            this.Name = "Machine";
            this.Size = new System.Drawing.Size(106, 106);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelStatusServer;
        private System.Windows.Forms.TextBox textBoxName;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.CheckBox checkBoxEnabled;
        private System.Windows.Forms.Label labelStatusClient;
        private System.Windows.Forms.ToolTip toolTipHelp;
    }
}
