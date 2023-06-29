namespace MouseWithoutBorders
{
    partial class frmLogon
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmLogon));
            this.labelDesktop = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelDesktop
            // 
            this.labelDesktop.AutoSize = true;
            this.labelDesktop.BackColor = System.Drawing.Color.White;
            this.labelDesktop.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelDesktop.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelDesktop.ForeColor = System.Drawing.Color.LimeGreen;
            this.labelDesktop.Location = new System.Drawing.Point(1, 1);
            this.labelDesktop.Name = "labelDesktop";
            this.labelDesktop.Size = new System.Drawing.Size(80, 15);
            this.labelDesktop.TabIndex = 0;
            this.labelDesktop.Text = "MouseWithoutBorders";
            // 
            // frmLogon
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(83, 17);
            this.ControlBox = false;
            this.Controls.Add(this.labelDesktop);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmLogon";
            this.Opacity = 0.5;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "MouseWithoutBorders";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.frmLogon_Load);
            this.Shown += new System.EventHandler(this.frmLogon_Shown);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmLogon_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelDesktop;
    }
}