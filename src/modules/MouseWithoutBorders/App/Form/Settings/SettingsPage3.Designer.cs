namespace MouseWithoutBorders
{
    partial class SettingsPage3
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
            this.label6 = new System.Windows.Forms.Label();
            this.EditLink = new System.Windows.Forms.LinkLabel();
            this.ShareClipboardCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.HideOnLoginCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.EnableEasyMouseCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.WrapMouseCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.DisableCADCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.BlockScreenSaverCheckbox = new MouseWithoutBorders.ImageCheckButton();
            this.label3 = new System.Windows.Forms.Label();
            this.SecurityCodeLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.label6.Location = new System.Drawing.Point(51, 119);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(350, 15);
            this.label6.TabIndex = 12;
            this.label6.Tag = " ";
            this.label6.Text = "ADVANCED OPTIONS";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // EditLink
            // 
            this.EditLink.AutoSize = true;
            this.EditLink.LinkColor = System.Drawing.Color.White;
            this.EditLink.Location = new System.Drawing.Point(202, 146);
            this.EditLink.Name = "EditLink";
            this.EditLink.Size = new System.Drawing.Size(104, 13);
            this.EditLink.TabIndex = 15;
            this.EditLink.TabStop = true;
            this.EditLink.Text = "Generate New Code";
            this.EditLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.EditLink_LinkClicked);
            // 
            // ShareClipboardCheckbox
            // 
            this.ShareClipboardCheckbox.AutoSize = true;
            this.ShareClipboardCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.ShareClipboardCheckbox.DisabledImage = null;
            this.ShareClipboardCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F);
            this.ShareClipboardCheckbox.Location = new System.Drawing.Point(54, 188);
            this.ShareClipboardCheckbox.MixedImage = null;
            this.ShareClipboardCheckbox.Name = "ShareClipboardCheckbox";
            this.ShareClipboardCheckbox.Size = new System.Drawing.Size(128, 34);
            this.ShareClipboardCheckbox.TabIndex = 16;
            this.ShareClipboardCheckbox.Text = "Share Clipboard (Text \r\nand Image)";
            this.ShareClipboardCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.ShareClipboardCheckbox.UseVisualStyleBackColor = true;
            this.ShareClipboardCheckbox.CheckedChanged += new System.EventHandler(this.ShareClipboardCheckbox_CheckedChanged);
            // 
            // HideOnLoginCheckbox
            // 
            this.HideOnLoginCheckbox.AutoSize = true;
            this.HideOnLoginCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.HideOnLoginCheckbox.DisabledImage = null;
            this.HideOnLoginCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F);
            this.HideOnLoginCheckbox.Location = new System.Drawing.Point(54, 238);
            this.HideOnLoginCheckbox.MixedImage = null;
            this.HideOnLoginCheckbox.Name = "HideOnLoginCheckbox";
            this.HideOnLoginCheckbox.Size = new System.Drawing.Size(143, 34);
            this.HideOnLoginCheckbox.TabIndex = 17;
            this.HideOnLoginCheckbox.Text = "Hide Mouse w/o Borders \r\non the Login Desktop";
            this.HideOnLoginCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.HideOnLoginCheckbox.UseVisualStyleBackColor = true;
            this.HideOnLoginCheckbox.CheckedChanged += new System.EventHandler(this.HideOnLoginCheckbox_CheckedChanged);
            // 
            // EnableEasyMouseCheckbox
            // 
            this.EnableEasyMouseCheckbox.AutoSize = true;
            this.EnableEasyMouseCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.EnableEasyMouseCheckbox.DisabledImage = null;
            this.EnableEasyMouseCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F);
            this.EnableEasyMouseCheckbox.Location = new System.Drawing.Point(54, 288);
            this.EnableEasyMouseCheckbox.MixedImage = null;
            this.EnableEasyMouseCheckbox.Name = "EnableEasyMouseCheckbox";
            this.EnableEasyMouseCheckbox.Size = new System.Drawing.Size(114, 19);
            this.EnableEasyMouseCheckbox.TabIndex = 18;
            this.EnableEasyMouseCheckbox.Text = "Enable Easy Mouse";
            this.EnableEasyMouseCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.EnableEasyMouseCheckbox.UseVisualStyleBackColor = true;
            this.EnableEasyMouseCheckbox.CheckedChanged += new System.EventHandler(this.EnableEasyMouseCheckbox_CheckedChanged);
            // 
            // WrapMouseCheckbox
            // 
            this.WrapMouseCheckbox.AutoSize = true;
            this.WrapMouseCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.WrapMouseCheckbox.DisabledImage = null;
            this.WrapMouseCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.WrapMouseCheckbox.Location = new System.Drawing.Point(238, 288);
            this.WrapMouseCheckbox.MixedImage = null;
            this.WrapMouseCheckbox.Name = "WrapMouseCheckbox";
            this.WrapMouseCheckbox.Size = new System.Drawing.Size(85, 19);
            this.WrapMouseCheckbox.TabIndex = 19;
            this.WrapMouseCheckbox.Text = "Wrap Mouse";
            this.WrapMouseCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.WrapMouseCheckbox.UseVisualStyleBackColor = true;
            this.WrapMouseCheckbox.CheckedChanged += new System.EventHandler(this.WrapMouseCheckbox_CheckedChanged);
            // 
            // DisableCADCheckbox
            // 
            this.DisableCADCheckbox.AutoSize = true;
            this.DisableCADCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.DisableCADCheckbox.DisabledImage = null;
            this.DisableCADCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F);
            this.DisableCADCheckbox.Location = new System.Drawing.Point(238, 188);
            this.DisableCADCheckbox.MixedImage = null;
            this.DisableCADCheckbox.Name = "DisableCADCheckbox";
            this.DisableCADCheckbox.Size = new System.Drawing.Size(154, 34);
            this.DisableCADCheckbox.TabIndex = 20;
            this.DisableCADCheckbox.Text = "Disable Ctrl+Alt+Del on the \r\nLogin Screen";
            this.DisableCADCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.DisableCADCheckbox.UseVisualStyleBackColor = true;
            this.DisableCADCheckbox.CheckedChanged += new System.EventHandler(this.DisableCADCheckbox_CheckedChanged);
            // 
            // BlockScreenSaverCheckbox
            // 
            this.BlockScreenSaverCheckbox.AutoSize = true;
            this.BlockScreenSaverCheckbox.CheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_checked;
            this.BlockScreenSaverCheckbox.DisabledImage = null;
            this.BlockScreenSaverCheckbox.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F);
            this.BlockScreenSaverCheckbox.Location = new System.Drawing.Point(238, 238);
            this.BlockScreenSaverCheckbox.MixedImage = null;
            this.BlockScreenSaverCheckbox.Name = "BlockScreenSaverCheckbox";
            this.BlockScreenSaverCheckbox.Size = new System.Drawing.Size(158, 34);
            this.BlockScreenSaverCheckbox.TabIndex = 21;
            this.BlockScreenSaverCheckbox.Text = "Block Screen Saver on Other\r\nMachines";
            this.BlockScreenSaverCheckbox.UncheckedImage = global::MouseWithoutBorders.Properties.Images.checkbox_unchecked;
            this.BlockScreenSaverCheckbox.UseVisualStyleBackColor = true;
            this.BlockScreenSaverCheckbox.CheckedChanged += new System.EventHandler(this.BlockScreenSaverCheckbox_CheckedChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(51, 144);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(79, 16);
            this.label3.TabIndex = 13;
            this.label3.Text = "Security Code:";
            // 
            // SecurityCodeLabel
            // 
            this.SecurityCodeLabel.AutoSize = true;
            this.SecurityCodeLabel.BackColor = System.Drawing.Color.Transparent;
            this.SecurityCodeLabel.Font = new System.Drawing.Font(DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SecurityCodeLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.SecurityCodeLabel.Location = new System.Drawing.Point(134, 144);
            this.SecurityCodeLabel.Name = "SecurityCodeLabel";
            this.SecurityCodeLabel.Size = new System.Drawing.Size(67, 16);
            this.SecurityCodeLabel.TabIndex = 14;
            this.SecurityCodeLabel.Text = "SX1q04Wr";
            // 
            // SettingsPage3
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.BlockScreenSaverCheckbox);
            this.Controls.Add(this.DisableCADCheckbox);
            this.Controls.Add(this.WrapMouseCheckbox);
            this.Controls.Add(this.EnableEasyMouseCheckbox);
            this.Controls.Add(this.HideOnLoginCheckbox);
            this.Controls.Add(this.ShareClipboardCheckbox);
            this.Controls.Add(this.EditLink);
            this.Controls.Add(this.SecurityCodeLabel);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label6);
            this.Name = "SettingsPage3";
            this.Controls.SetChildIndex(this.label6, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Controls.SetChildIndex(this.SecurityCodeLabel, 0);
            this.Controls.SetChildIndex(this.EditLink, 0);
            this.Controls.SetChildIndex(this.ShareClipboardCheckbox, 0);
            this.Controls.SetChildIndex(this.HideOnLoginCheckbox, 0);
            this.Controls.SetChildIndex(this.EnableEasyMouseCheckbox, 0);
            this.Controls.SetChildIndex(this.WrapMouseCheckbox, 0);
            this.Controls.SetChildIndex(this.DisableCADCheckbox, 0);
            this.Controls.SetChildIndex(this.BlockScreenSaverCheckbox, 0);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.LinkLabel EditLink;
        private ImageCheckButton ShareClipboardCheckbox;
        private ImageCheckButton HideOnLoginCheckbox;
        private ImageCheckButton EnableEasyMouseCheckbox;
        private ImageCheckButton WrapMouseCheckbox;
        private ImageCheckButton DisableCADCheckbox;
        private ImageCheckButton BlockScreenSaverCheckbox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label SecurityCodeLabel;


    }
}
