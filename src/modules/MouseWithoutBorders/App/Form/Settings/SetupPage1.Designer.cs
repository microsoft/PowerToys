using System.Windows.Forms;

namespace MouseWithoutBorders
{
    partial class SetupPage1
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
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.NoButton = new MouseWithoutBorders.ImageButton();
            this.YesButton = new MouseWithoutBorders.ImageButton();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.NoButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.YesButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Location = new System.Drawing.Point(41, 96);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(370, 1);
            this.panel1.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font(Control.DefaultFont.Name, 30F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(61, 102);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(330, 52);
            this.label1.TabIndex = 2;
            this.label1.Text = "Let\'s get started";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.White;
            this.panel2.Location = new System.Drawing.Point(41, 166);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(370, 1);
            this.panel2.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(61, 185);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(310, 40);
            this.label2.TabIndex = 3;
            this.label2.Text = "We need to know if you have already set up Mouse w/o Borders on the computer you " +
                "want to link to.";
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font(Control.DefaultFont.Name, 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(61, 240);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(310, 60);
            this.label3.TabIndex = 4;            
            this.label3.Text = "Have you already installed Mouse without Borders on another computer?";
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(61, 278);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(310, 20);
            this.label4.TabIndex = 5;
            this.label4.Text = "";
            // 
            // NoButton
            // 
            this.NoButton.BackColor = System.Drawing.Color.Transparent;
            this.NoButton.DisabledImage = null;
            this.NoButton.DownImage = global::MouseWithoutBorders.Properties.Images.no_button_click;
            this.NoButton.HoverImage = global::MouseWithoutBorders.Properties.Images.no_button_hover;
            this.NoButton.Image = global::MouseWithoutBorders.Properties.Images.no_button_normal;
            this.NoButton.InitialImage = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.NoButton.Location = new System.Drawing.Point(234, 366);
            this.NoButton.Name = "NoButton";
            this.NoButton.NormalImage = global::MouseWithoutBorders.Properties.Images.no_button_normal;
            this.NoButton.Size = new System.Drawing.Size(55, 55);
            this.NoButton.TabIndex = 7;
            this.NoButton.TabStop = false;
            this.NoButton.Click += new System.EventHandler(this.NoButtonClick);
            // 
            // YesButton
            // 
            this.YesButton.BackColor = System.Drawing.Color.Transparent;
            this.YesButton.DisabledImage = null;
            this.YesButton.DownImage = global::MouseWithoutBorders.Properties.Images.yes_button_click;
            this.YesButton.HoverImage = global::MouseWithoutBorders.Properties.Images.yes_button_hover;
            this.YesButton.Image = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.YesButton.InitialImage = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.YesButton.Location = new System.Drawing.Point(164, 366);
            this.YesButton.Name = "YesButton";
            this.YesButton.NormalImage = global::MouseWithoutBorders.Properties.Images.yes_button_normal;
            this.YesButton.Size = new System.Drawing.Size(55, 55);
            this.YesButton.TabIndex = 6;
            this.YesButton.TabStop = false;
            this.YesButton.Click += new System.EventHandler(this.YesButtonClick);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::MouseWithoutBorders.Properties.Images.Mouse;
            this.pictureBox1.Location = new System.Drawing.Point(206, 40);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(41, 36);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // SetupPage1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.NoButton);
            this.Controls.Add(this.YesButton);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.pictureBox1);
            this.DoubleBuffered = true;
            this.Name = "SetupPage1";
            this.Size = new System.Drawing.Size(453, 438);
            this.Controls.SetChildIndex(this.pictureBox1, 0);
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.panel2, 0);
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Controls.SetChildIndex(this.label4, 0);
            this.Controls.SetChildIndex(this.YesButton, 0);
            this.Controls.SetChildIndex(this.NoButton, 0);
            ((System.ComponentModel.ISupportInitialize)(this.NoButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.YesButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private ImageButton YesButton;
        private ImageButton NoButton;
    }
}
