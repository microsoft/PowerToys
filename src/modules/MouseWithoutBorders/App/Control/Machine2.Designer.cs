namespace MouseWithoutBorders
{
    partial class Machine2
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
            this.StatusLabel = new System.Windows.Forms.Label();
            this.NameLabel = new System.Windows.Forms.Label();
            this.SelectedPanel = new System.Windows.Forms.Panel();
            this.ComputerPictureBox = new System.Windows.Forms.PictureBox();
            this.RemoveButton = new MouseWithoutBorders.ImageButton();
            this.OnButton = new MouseWithoutBorders.ImageButton();
            this.OffButton = new MouseWithoutBorders.ImageButton();
            ((System.ComponentModel.ISupportInitialize)(this.ComputerPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.RemoveButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.OnButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.OffButton)).BeginInit();
            this.SuspendLayout();
            // 
            // StatusLabel
            // 
            this.StatusLabel.BackColor = System.Drawing.Color.Transparent;
            this.StatusLabel.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.StatusLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(159)))), ((int)(((byte)(217)))));
            this.StatusLabel.Location = new System.Drawing.Point(3, 30);
            this.StatusLabel.Name = "StatusLabel";
            this.StatusLabel.Size = new System.Drawing.Size(112, 39);
            this.StatusLabel.TabIndex = 2;
            this.StatusLabel.Text = "This Computer";
            this.StatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // NameLabel
            // 
            this.NameLabel.BackColor = System.Drawing.Color.Transparent;
            this.NameLabel.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.NameLabel.ForeColor = System.Drawing.Color.White;
            this.NameLabel.Location = new System.Drawing.Point(6, 96);
            this.NameLabel.Name = "NameLabel";
            this.NameLabel.Size = new System.Drawing.Size(109, 17);
            this.NameLabel.TabIndex = 1;
            this.NameLabel.Text = "label1";
            this.NameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SelectedPanel
            // 
            this.SelectedPanel.Location = new System.Drawing.Point(101, 225);
            this.SelectedPanel.Name = "SelectedPanel";
            this.SelectedPanel.Size = new System.Drawing.Size(200, 100);
            this.SelectedPanel.TabIndex = 8;
            // 
            // ComputerPictureBox
            // 
            this.ComputerPictureBox.Image = global::MouseWithoutBorders.Properties.Images.computer_connected;
            this.ComputerPictureBox.Location = new System.Drawing.Point(5, 5);
            this.ComputerPictureBox.Name = "ComputerPictureBox";
            this.ComputerPictureBox.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.ComputerPictureBox.Size = new System.Drawing.Size(109, 78);
            this.ComputerPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.ComputerPictureBox.TabIndex = 0;
            this.ComputerPictureBox.TabStop = false;
            // 
            // RemoveButton
            // 
            this.RemoveButton.DisabledImage = null;
            this.RemoveButton.DownImage = global::MouseWithoutBorders.Properties.Images.red_close_button_click;
            this.RemoveButton.HoverImage = global::MouseWithoutBorders.Properties.Images.red_close_button_hover;
            this.RemoveButton.Image = global::MouseWithoutBorders.Properties.Images.red_close_button_normal;
            this.RemoveButton.Location = new System.Drawing.Point(224, 15);
            this.RemoveButton.Name = "RemoveButton";
            this.RemoveButton.NormalImage = global::MouseWithoutBorders.Properties.Images.red_close_button_normal;
            this.RemoveButton.Size = new System.Drawing.Size(12, 12);
            this.RemoveButton.TabIndex = 5;
            this.RemoveButton.TabStop = false;
            this.RemoveButton.Click += new System.EventHandler(this.RemoveButtonClick);
            // 
            // OnButton
            // 
            this.OnButton.BackColor = System.Drawing.Color.Transparent;
            this.OnButton.DisabledImage = null;
            this.OnButton.DownImage = global::MouseWithoutBorders.Properties.Images.switch_on_click;
            this.OnButton.HoverImage = global::MouseWithoutBorders.Properties.Images.switch_on_hover;
            this.OnButton.Image = global::MouseWithoutBorders.Properties.Images.switch_on_normal;
            this.OnButton.Location = new System.Drawing.Point(277, 20);
            this.OnButton.Name = "OnButton";
            this.OnButton.NormalImage = global::MouseWithoutBorders.Properties.Images.switch_on_normal;
            this.OnButton.Size = new System.Drawing.Size(30, 15);
            this.OnButton.TabIndex = 3;
            this.OnButton.TabStop = false;
            this.OnButton.Click += new System.EventHandler(this.OnButtonClick);
            // 
            // OffButton
            // 
            this.OffButton.BackColor = System.Drawing.Color.Transparent;
            this.OffButton.DisabledImage = null;
            this.OffButton.DownImage = global::MouseWithoutBorders.Properties.Images.switch_off_click;
            this.OffButton.HoverImage = global::MouseWithoutBorders.Properties.Images.switch_off_hover;
            this.OffButton.Image = global::MouseWithoutBorders.Properties.Images.switch_off_normal;
            this.OffButton.Location = new System.Drawing.Point(241, 42);
            this.OffButton.Name = "OffButton";
            this.OffButton.NormalImage = global::MouseWithoutBorders.Properties.Images.switch_off_normal;
            this.OffButton.Size = new System.Drawing.Size(30, 15);
            this.OffButton.TabIndex = 4;
            this.OffButton.TabStop = false;
            this.OffButton.Click += new System.EventHandler(this.OffButtonClick);
            // 
            // Machine2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.RemoveButton);
            this.Controls.Add(this.OnButton);
            this.Controls.Add(this.OffButton);
            this.Controls.Add(this.StatusLabel);
            this.Controls.Add(this.NameLabel);
            this.Controls.Add(this.ComputerPictureBox);
            this.Controls.Add(this.SelectedPanel);
            this.DoubleBuffered = true;
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Size = new System.Drawing.Size(471, 432);
            ((System.ComponentModel.ISupportInitialize)(this.ComputerPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.RemoveButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.OnButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.OffButton)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox ComputerPictureBox;
        private System.Windows.Forms.Label StatusLabel;
        private ImageButton OffButton;
        private ImageButton OnButton;
        private ImageButton RemoveButton;
        private System.Windows.Forms.Label NameLabel;
        private System.Windows.Forms.Panel SelectedPanel;



    }
}
