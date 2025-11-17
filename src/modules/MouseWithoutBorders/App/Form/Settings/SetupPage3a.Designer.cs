using System.Windows.Forms;

namespace MouseWithoutBorders
{
    partial class SetupPage3a
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
            this.MessageLabel = new System.Windows.Forms.Label();
            this.ExamplePicture = new System.Windows.Forms.PictureBox();
            this.labelStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.ExamplePicture)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font(Control.DefaultFont.Name, 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(65, 56);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(330, 116);
            this.label1.TabIndex = 2;
            this.label1.Text = "Linking! Soon you will be able to...\r\n";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.White;
            this.panel2.Location = new System.Drawing.Point(41, 178);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(370, 1);
            this.panel2.TabIndex = 2;
            // 
            // MessageLabel
            // 
            this.MessageLabel.Font = new System.Drawing.Font(Control.DefaultFont.Name, 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MessageLabel.ForeColor = System.Drawing.Color.White;
            this.MessageLabel.Location = new System.Drawing.Point(71, 197);
            this.MessageLabel.Name = "MessageLabel";
            this.MessageLabel.Size = new System.Drawing.Size(310, 24);
            this.MessageLabel.TabIndex = 4;
            this.MessageLabel.Text = "Copy && paste across screens";
            this.MessageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // ExamplePicture
            // 
            this.ExamplePicture.Image = global::MouseWithoutBorders.Properties.Images.copy_paste_example;
            this.ExamplePicture.Location = new System.Drawing.Point(101, 251);
            this.ExamplePicture.Name = "ExamplePicture";
            this.ExamplePicture.Size = new System.Drawing.Size(251, 79);
            this.ExamplePicture.TabIndex = 7;
            this.ExamplePicture.TabStop = false;
            // 
            // labelStatus
            // 
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelStatus.Font = new System.Drawing.Font(Control.DefaultFont.Name, 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelStatus.ForeColor = System.Drawing.Color.White;
            this.labelStatus.Location = new System.Drawing.Point(0, 418);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(453, 20);
            this.labelStatus.TabIndex = 8;
            this.labelStatus.Text = "Connecting...";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SetupPage3a
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.ExamplePicture);
            this.Controls.Add(this.MessageLabel);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.label1);
            this.DoubleBuffered = true;
            this.Name = "SetupPage3a";
            this.Size = new System.Drawing.Size(453, 438);
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.panel2, 0);
            this.Controls.SetChildIndex(this.MessageLabel, 0);
            this.Controls.SetChildIndex(this.ExamplePicture, 0);
            this.Controls.SetChildIndex(this.labelStatus, 0);
            ((System.ComponentModel.ISupportInitialize)(this.ExamplePicture)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label MessageLabel;
        private System.Windows.Forms.PictureBox ExamplePicture;
        private System.Windows.Forms.Label labelStatus;
    }
}