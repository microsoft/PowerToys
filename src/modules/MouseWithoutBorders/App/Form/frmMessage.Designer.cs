namespace MouseWithoutBorders
{
    partial class FrmMessage
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmMessage));
            this.labelTitle = new System.Windows.Forms.Label();
            this.textExtraInfo = new System.Windows.Forms.TextBox();
            this.timerLife = new System.Windows.Forms.Timer(this.components);
            this.labelLifeTime = new System.Windows.Forms.Label();
            this.textBoxMessage = new System.Windows.Forms.TextBox();
            this.pictureBoxIcon = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelTitle.Location = new System.Drawing.Point(21, 0);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(168, 16);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "Mouse Without Borders";
            // 
            // textExtraInfo
            // 
            this.textExtraInfo.BackColor = System.Drawing.SystemColors.Info;
            this.textExtraInfo.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textExtraInfo.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textExtraInfo.Enabled = false;
            this.textExtraInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 63.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textExtraInfo.Location = new System.Drawing.Point(0, 161);
            this.textExtraInfo.Margin = new System.Windows.Forms.Padding(20);
            this.textExtraInfo.Multiline = true;
            this.textExtraInfo.Name = "textExtraInfo";
            this.textExtraInfo.ReadOnly = true;
            this.textExtraInfo.Size = new System.Drawing.Size(720, 239);
            this.textExtraInfo.TabIndex = 3;
            this.textExtraInfo.Text = "???\r\nLLL";
            this.textExtraInfo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // timerLife
            // 
            this.timerLife.Enabled = true;
            this.timerLife.Interval = 1000;
            this.timerLife.Tick += new System.EventHandler(this.TimerLife_Tick);
            // 
            // labelLifeTime
            // 
            this.labelLifeTime.AutoSize = true;
            this.labelLifeTime.Dock = System.Windows.Forms.DockStyle.Right;
            this.labelLifeTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLifeTime.Location = new System.Drawing.Point(720, 0);
            this.labelLifeTime.Name = "labelLifeTime";
            this.labelLifeTime.Size = new System.Drawing.Size(0, 16);
            this.labelLifeTime.TabIndex = 4;
            // 
            // textBoxMessage
            // 
            this.textBoxMessage.BackColor = System.Drawing.SystemColors.Info;
            this.textBoxMessage.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxMessage.Enabled = false;
            this.textBoxMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxMessage.Location = new System.Drawing.Point(0, 0);
            this.textBoxMessage.Margin = new System.Windows.Forms.Padding(20);
            this.textBoxMessage.Multiline = true;
            this.textBoxMessage.Name = "textBoxMessage";
            this.textBoxMessage.ReadOnly = true;
            this.textBoxMessage.Size = new System.Drawing.Size(720, 400);
            this.textBoxMessage.TabIndex = 2;
            this.textBoxMessage.Text = "Message Text";
            this.textBoxMessage.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // pictureBoxIcon
            // 
            this.pictureBoxIcon.Image = ((System.Drawing.Image)(resources.GetObject("pictureBoxIcon.Image")));
            this.pictureBoxIcon.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxIcon.Name = "pictureBoxIcon";
            this.pictureBoxIcon.Size = new System.Drawing.Size(16, 16);
            this.pictureBoxIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureBoxIcon.TabIndex = 5;
            this.pictureBoxIcon.TabStop = false;
            // 
            // frmMessage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(231)))));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(720, 400);
            this.Controls.Add(this.pictureBoxIcon);
            this.Controls.Add(this.labelLifeTime);
            this.Controls.Add(this.textExtraInfo);
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.textBoxMessage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmMessage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "frmMessage";
            this.TopMost = true;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FrmMessage_FormClosed);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.TextBox textExtraInfo;
        private System.Windows.Forms.Timer timerLife;
        private System.Windows.Forms.Label labelLifeTime;
        private System.Windows.Forms.TextBox textBoxMessage;
        private System.Windows.Forms.PictureBox pictureBoxIcon;
    }
}