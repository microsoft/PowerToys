namespace MouseWithoutBorders
{
    partial class SettingsPage2
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
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.MachineNameLabel = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.SecurityKeyLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(47, 112);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(310, 75);
            this.label2.TabIndex = 9;
            this.label2.Text = "You know the drill! Just keep this window open or write down the information below, then head over to your" +
                " other computer and install Mouse w/o Borders. You can finish the setup and conf" +
                "iguration over there.";
            // 
            // label4
            // 
            this.label4.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.label4.Location = new System.Drawing.Point(73, 267);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(310, 15);
            this.label4.TabIndex = 13;
            this.label4.Text = "THIS COMPUTER\'S NAME";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MachineNameLabel
            // 
            this.MachineNameLabel.Font = new System.Drawing.Font(DefaultFont.Name, 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MachineNameLabel.ForeColor = System.Drawing.Color.White;
            this.MachineNameLabel.Location = new System.Drawing.Point(50, 279);
            this.MachineNameLabel.Name = "MachineNameLabel";
            this.MachineNameLabel.Size = new System.Drawing.Size(340, 61);
            this.MachineNameLabel.TabIndex = 12;
            this.MachineNameLabel.Text = "Alan - Desktop";
            this.MachineNameLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.label6.Location = new System.Drawing.Point(73, 208);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(310, 15);
            this.label6.TabIndex = 11;
            this.label6.Text = "SECURITY CODE";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SecurityKeyLabel
            // 
            this.SecurityKeyLabel.Font = new System.Drawing.Font(DefaultFont.Name, 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SecurityKeyLabel.ForeColor = System.Drawing.Color.White;
            this.SecurityKeyLabel.Location = new System.Drawing.Point(70, 216);
            this.SecurityKeyLabel.Name = "SecurityKeyLabel";
            this.SecurityKeyLabel.Size = new System.Drawing.Size(310, 40);
            this.SecurityKeyLabel.TabIndex = 10;
            this.SecurityKeyLabel.Text = "SX1q04Wr";
            this.SecurityKeyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SettingsPage2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.label4);
            this.Controls.Add(this.MachineNameLabel);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.SecurityKeyLabel);
            this.Controls.Add(this.label2);
            this.Name = "SettingsPage2";
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.SecurityKeyLabel, 0);
            this.Controls.SetChildIndex(this.label6, 0);
            this.Controls.SetChildIndex(this.MachineNameLabel, 0);
            this.Controls.SetChildIndex(this.label4, 0);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label MachineNameLabel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label SecurityKeyLabel;

    }
}
