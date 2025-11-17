namespace MouseWithoutBorders
{
    partial class SettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.closeWindowButton = new MouseWithoutBorders.ImageButton();
            this.contentPanel = new System.Windows.Forms.Panel();
            this.toolTipManual = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.closeWindowButton)).BeginInit();
            this.SuspendLayout();
            // 
            // closeWindowButton
            // 
            this.closeWindowButton.BackColor = System.Drawing.Color.Transparent;
            this.closeWindowButton.DisabledImage = null;
            this.closeWindowButton.DownImage = global::MouseWithoutBorders.Properties.Images.close_window_click;
            this.closeWindowButton.HoverImage = global::MouseWithoutBorders.Properties.Images.close_window_hover;
            this.closeWindowButton.Location = new System.Drawing.Point(454, 6);
            this.closeWindowButton.Name = "closeWindowButton";
            this.closeWindowButton.NormalImage = null;
            this.closeWindowButton.Size = new System.Drawing.Size(16, 16);
            this.closeWindowButton.TabIndex = 1;
            this.closeWindowButton.TabStop = false;
            this.closeWindowButton.Click += new System.EventHandler(this.CloseWindowButtonClick);
            // 
            // contentPanel
            // 
            this.contentPanel.BackColor = System.Drawing.Color.Transparent;
            this.contentPanel.Location = new System.Drawing.Point(12, 26);
            this.contentPanel.Name = "contentPanel";
            this.contentPanel.Size = new System.Drawing.Size(453, 438);
            this.contentPanel.TabIndex = 2;
            this.contentPanel.Visible = false;
            // 
            // toolTipManual
            // 
            this.toolTipManual.ToolTipTitle = "Microsoft® Visual Studio® 2010";
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = global::MouseWithoutBorders.Properties.Images.dialog_background;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(477, 476);
            this.Controls.Add(this.closeWindowButton);
            this.Controls.Add(this.contentPanel);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SettingsForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmSettings";
            this.TopMost = true;
            this.TransparencyKey = System.Drawing.Color.Magenta;
            ((System.ComponentModel.ISupportInitialize)(this.closeWindowButton)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private ImageButton closeWindowButton;
        private System.Windows.Forms.Panel contentPanel;
        private System.Windows.Forms.ToolTip toolTipManual;
    }
}