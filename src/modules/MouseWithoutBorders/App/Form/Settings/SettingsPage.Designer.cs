namespace MouseWithoutBorders
{
    partial class SettingsPage
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.DoneButton = new MouseWithoutBorders.ImageButton();
            this.CloseButton = new MouseWithoutBorders.ImageButton();
            ((System.ComponentModel.ISupportInitialize)(this.DoneButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CloseButton)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Location = new System.Drawing.Point(41, 343);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(370, 1);
            this.panel1.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font(DefaultFont.Name, 30F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(61, 35);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(330, 52);
            this.label1.TabIndex = 2;
            this.label1.Text = "Settings";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.White;
            this.panel2.Location = new System.Drawing.Point(41, 99);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(370, 1);
            this.panel2.TabIndex = 2;
            // 
            // DoneButton
            // 
            this.DoneButton.BackColor = System.Drawing.Color.Transparent;
            this.DoneButton.DisabledImage = null;
            this.DoneButton.DownImage = global::MouseWithoutBorders.Properties.Images.done_button_click;
            this.DoneButton.HoverImage = global::MouseWithoutBorders.Properties.Images.done_button_hover;
            this.DoneButton.Image = global::MouseWithoutBorders.Properties.Images.done_button_normal;
            this.DoneButton.InitialImage = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.DoneButton.Location = new System.Drawing.Point(199, 366);
            this.DoneButton.Name = "DoneButton";
            this.DoneButton.NormalImage = global::MouseWithoutBorders.Properties.Images.done_button_normal;
            this.DoneButton.Size = new System.Drawing.Size(55, 55);
            this.DoneButton.TabIndex = 8;
            this.DoneButton.TabStop = false;
            this.DoneButton.Visible = false;
            this.DoneButton.Click += new System.EventHandler(this.DoneButtonClick);
            // 
            // CloseButton
            // 
            this.CloseButton.BackColor = System.Drawing.Color.Transparent;
            this.CloseButton.DisabledImage = null;
            this.CloseButton.DownImage = global::MouseWithoutBorders.Properties.Images.close_button_click;
            this.CloseButton.HoverImage = global::MouseWithoutBorders.Properties.Images.close_button_hover;
            this.CloseButton.Image = global::MouseWithoutBorders.Properties.Images.close_button_normal;
            this.CloseButton.InitialImage = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.CloseButton.Location = new System.Drawing.Point(199, 366);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.NormalImage = global::MouseWithoutBorders.Properties.Images.close_button_normal;
            this.CloseButton.Size = new System.Drawing.Size(55, 55);
            this.CloseButton.TabIndex = 7;
            this.CloseButton.TabStop = false;
            this.CloseButton.Click += new System.EventHandler(this.CloseButtonClick);
            // 
            // SettingsPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.DoneButton);
            this.Controls.Add(this.CloseButton);
            this.DoubleBuffered = true;
            this.Name = "SettingsPage";
            this.Size = new System.Drawing.Size(453, 438);
            this.Controls.SetChildIndex(this.CloseButton, 0);
            this.Controls.SetChildIndex(this.DoneButton, 0);
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.panel2, 0);
            ((System.ComponentModel.ISupportInitialize)(this.DoneButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CloseButton)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private ImageButton CloseButton;
        private ImageButton DoneButton;
    }
}
