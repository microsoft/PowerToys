using System.Windows.Forms;

namespace MouseWithoutBorders
{
    partial class SetupPage2ab
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
            this.label6 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.ExpandHelpButton = new MouseWithoutBorders.ImageButton();
            this.CollapseHelpButton = new MouseWithoutBorders.ImageButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.ExpandHelpButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CollapseHelpButton)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font(Control.DefaultFont.Name, 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(49, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(356, 53);
            this.label1.TabIndex = 2;
            this.label1.Text = "Sorry!";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.label6.Location = new System.Drawing.Point(98, 208);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(130, 18);
            this.label6.TabIndex = 7;
            this.label6.Text = "SECURITY CODE";
            this.label6.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F);
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(61, 95);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(332, 79);
            this.label4.TabIndex = 27;
            this.label4.Text = "It looks like we were unable to connect to your computer. Make sure that all of y" +
                "our computers are on the same network and that you have the correct information " +
                "filled in below.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(213, 208);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(128, 16);
            this.label2.TabIndex = 23;
            this.label2.Text = "Where can I find this?";
            // 
            // ExpandHelpButton
            // 
            this.ExpandHelpButton.DisabledImage = null;
            this.ExpandHelpButton.DownImage = null;
            this.ExpandHelpButton.HoverImage = null;
            this.ExpandHelpButton.Location = new System.Drawing.Point(0, 0);
            this.ExpandHelpButton.Name = "ExpandHelpButton";
            this.ExpandHelpButton.NormalImage = null;
            this.ExpandHelpButton.Size = new System.Drawing.Size(80, 78);
            this.ExpandHelpButton.TabIndex = 0;
            this.ExpandHelpButton.TabStop = false;
            // 
            // CollapseHelpButton
            // 
            this.CollapseHelpButton.DisabledImage = null;
            this.CollapseHelpButton.DownImage = null;
            this.CollapseHelpButton.HoverImage = null;
            this.CollapseHelpButton.Location = new System.Drawing.Point(0, 0);
            this.CollapseHelpButton.Name = "CollapseHelpButton";
            this.CollapseHelpButton.NormalImage = null;
            this.CollapseHelpButton.Size = new System.Drawing.Size(80, 78);
            this.CollapseHelpButton.TabIndex = 0;
            this.CollapseHelpButton.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Location = new System.Drawing.Point(41, 80);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(370, 1);
            this.panel1.TabIndex = 1;
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.White;
            this.panel2.Location = new System.Drawing.Point(41, 170);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(370, 1);
            this.panel2.TabIndex = 28;
            // 
            // SetupPage2ab
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label1);
            this.Name = "SetupPage2ab";
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.panel1, 0);
            this.Controls.SetChildIndex(this.label4, 0);
            this.Controls.SetChildIndex(this.panel2, 0);
            ((System.ComponentModel.ISupportInitialize)(this.ExpandHelpButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CollapseHelpButton)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label2;
        private ImageButton ExpandHelpButton;
        private ImageButton CollapseHelpButton;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel panel2;
    }
}