namespace MouseWithoutBorders
{
    partial class SettingsFormPage
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
            this.buttonSkip = new System.Windows.Forms.Button();
            this.BackButton = new MouseWithoutBorders.ImageButton();
            ((System.ComponentModel.ISupportInitialize)(this.BackButton)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonSkip
            // 
            this.buttonSkip.FlatAppearance.BorderSize = 0;
            this.buttonSkip.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(128)))));
            this.buttonSkip.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.buttonSkip.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSkip.ForeColor = System.Drawing.Color.White;
            this.buttonSkip.Location = new System.Drawing.Point(0, 36);
            this.buttonSkip.Name = "buttonSkip";
            this.buttonSkip.Size = new System.Drawing.Size(40, 23);
            this.buttonSkip.TabIndex = 1;
            this.buttonSkip.Text = "&Skip";
            this.buttonSkip.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSkip.UseVisualStyleBackColor = true;
            this.buttonSkip.Click += new System.EventHandler(this.ButtonSkip_Click);
            // 
            // BackButton
            // 
            this.BackButton.DisabledImage = null;
            this.BackButton.DownImage = global::MouseWithoutBorders.Properties.Images.back_button_click;
            this.BackButton.HoverImage = global::MouseWithoutBorders.Properties.Images.back_button_hover;
            this.BackButton.Image = global::MouseWithoutBorders.Properties.Images.back_button_normal;
            this.BackButton.Location = new System.Drawing.Point(6, 6);
            this.BackButton.Name = "BackButton";
            this.BackButton.NormalImage = global::MouseWithoutBorders.Properties.Images.back_button_normal;
            this.BackButton.Size = new System.Drawing.Size(24, 24);
            this.BackButton.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.BackButton.TabIndex = 0;
            this.BackButton.TabStop = false;
            this.BackButton.Visible = false;
            this.BackButton.Click += new System.EventHandler(this.BackButton_Click);
            // 
            // SettingsFormPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DeepSkyBlue;
            this.Controls.Add(this.buttonSkip);
            this.Controls.Add(this.BackButton);
            this.Name = "SettingsFormPage";
            this.Size = new System.Drawing.Size(396, 345);
            ((System.ComponentModel.ISupportInitialize)(this.BackButton)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private ImageButton BackButton;
        private System.Windows.Forms.Button buttonSkip;
    }
}
