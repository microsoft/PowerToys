using System.Windows.Forms;

namespace MouseWithoutBorders
{
    partial class SetupPage4
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
            this.label1 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.ExamplePicture = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.NextButton = new MouseWithoutBorders.ImageButton();
            this.label4 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.ExamplePicture)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.NextButton)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font(Control.DefaultFont.Name, 30F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(65, 58);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(330, 52);
            this.label1.TabIndex = 2;
            this.label1.Text = "Success! You\'re";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.White;
            this.panel2.Location = new System.Drawing.Point(41, 175);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(370, 1);
            this.panel2.TabIndex = 2;
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font(Control.DefaultFont.Name, 30F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.Color.White;
            this.label5.Location = new System.Drawing.Point(57, 108);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(344, 52);
            this.label5.TabIndex = 6;
            this.label5.Text = "almost done here...";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // ExamplePicture
            // 
            this.ExamplePicture.Image = global::MouseWithoutBorders.Properties.Images.combined_example;
            this.ExamplePicture.Location = new System.Drawing.Point(75, 283);
            this.ExamplePicture.Name = "ExamplePicture";
            this.ExamplePicture.Size = new System.Drawing.Size(307, 25);
            this.ExamplePicture.TabIndex = 7;
            this.ExamplePicture.TabStop = false;
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(61, 200);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(310, 25);
            this.label2.TabIndex = 8;
            this.label2.Text = "Now you can do the magic of Mouse w/o Borders.";
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(71, 242);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(88, 38);
            this.label3.TabIndex = 9;
            this.label3.Text = "Copy && Paste\r\nacross screens";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Location = new System.Drawing.Point(41, 345);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(370, 1);
            this.panel1.TabIndex = 3;
            // 
            // NextButton
            // 
            this.NextButton.DisabledImage = null;
            this.NextButton.DownImage = global::MouseWithoutBorders.Properties.Images.next_button_click;
            this.NextButton.HoverImage = global::MouseWithoutBorders.Properties.Images.next_button_hover;
            this.NextButton.Image = global::MouseWithoutBorders.Properties.Images.next_button_normal;
            this.NextButton.Location = new System.Drawing.Point(199, 366);
            this.NextButton.Name = "NextButton";
            this.NextButton.NormalImage = global::MouseWithoutBorders.Properties.Images.next_button_normal;
            this.NextButton.Size = new System.Drawing.Size(55, 55);
            this.NextButton.TabIndex = 16;
            this.NextButton.TabStop = false;
            this.NextButton.Click += new System.EventHandler(this.NextButtonClick);
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(185, 242);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(88, 38);
            this.label4.TabIndex = 17;
            this.label4.Text = "Drag files\r\nacross screens";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label6.ForeColor = System.Drawing.Color.White;
            this.label6.Location = new System.Drawing.Point(294, 242);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(97, 38);
            this.label6.TabIndex = 18;
            this.label6.Text = "Share keyboard\r\nacross screens";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SetupPage4
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.NextButton);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.ExamplePicture);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.label1);
            this.DoubleBuffered = true;
            this.Name = "SetupPage4";
            this.Size = new System.Drawing.Size(453, 438);
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.panel2, 0);
            this.Controls.SetChildIndex(this.label5, 0);
            this.Controls.SetChildIndex(this.ExamplePicture, 0);
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.NextButton, 0);
            this.Controls.SetChildIndex(this.label4, 0);
            this.Controls.SetChildIndex(this.label6, 0);
            ((System.ComponentModel.ISupportInitialize)(this.ExamplePicture)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.NextButton)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.PictureBox ExamplePicture;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel1;
        private ImageButton NextButton;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
    }
}