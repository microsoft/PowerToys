namespace MouseWithoutBorders
{
    using System.Windows.Forms;

    partial class SetupPage2a
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
            this.SecurityCodeField = new MouseWithoutBorders.ColorBorderField();
            this.ComputerNameField = new MouseWithoutBorders.ColorBorderField();
            this.label3 = new System.Windows.Forms.Label();
            this.LinkButton = new MouseWithoutBorders.ImageButton();
            this.label2 = new System.Windows.Forms.Label();
            this.ExpandHelpButton = new MouseWithoutBorders.ImageButton();
            this.CollapseHelpButton = new MouseWithoutBorders.ImageButton();
            this.HelpLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.LinkButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ExpandHelpButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CollapseHelpButton)).BeginInit();
            this.SuspendLayout();
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
            // SecurityCodeField
            // 
            this.SecurityCodeField.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(177)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.SecurityCodeField.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(177)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.SecurityCodeField.BorderSize = 2;
            this.SecurityCodeField.FocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(251)))), ((int)(((byte)(176)))), ((int)(((byte)(64)))));
            this.SecurityCodeField.Font = new System.Drawing.Font(Control.DefaultFont.Name, 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SecurityCodeField.Location = new System.Drawing.Point(80, 226);
            this.SecurityCodeField.Margin = new System.Windows.Forms.Padding(0);
            this.SecurityCodeField.MaximumLength = 22;
            this.SecurityCodeField.Name = "SecurityCodeField";
            this.SecurityCodeField.Size = new System.Drawing.Size(300, 30);
            this.SecurityCodeField.TabIndex = 0;
            this.SecurityCodeField.FieldTextChanged += new System.EventHandler(this.SecurityCodeFieldFieldTextChanged);
            // 
            // ComputerNameField
            // 
            this.ComputerNameField.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(177)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.ComputerNameField.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(177)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.ComputerNameField.BorderSize = 2;
            this.ComputerNameField.FocusColor = System.Drawing.Color.FromArgb(((int)(((byte)(251)))), ((int)(((byte)(176)))), ((int)(((byte)(64)))));
            this.ComputerNameField.Font = new System.Drawing.Font(Control.DefaultFont.Name, 18F);
            this.ComputerNameField.Location = new System.Drawing.Point(80, 280);
            this.ComputerNameField.Margin = new System.Windows.Forms.Padding(0);
            this.ComputerNameField.MaximumLength = 126;
            this.ComputerNameField.Name = "ComputerNameField";
            this.ComputerNameField.Size = new System.Drawing.Size(300, 30);
            this.ComputerNameField.TabIndex = 1;
            this.ComputerNameField.FieldTextChanged += new System.EventHandler(this.ComputerNameFieldFieldTextChanged);
            // 
            // label3
            // 
            this.label3.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(208)))), ((int)(((byte)(238)))));
            this.label3.Location = new System.Drawing.Point(98, 262);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(262, 18);
            this.label3.TabIndex = 13;
            this.label3.Text = "OTHER COMPUTER\'S NAME";
            this.label3.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // LinkButton
            // 
            this.LinkButton.DisabledImage = global::MouseWithoutBorders.Properties.Images.link_button_disabled;
            this.LinkButton.DownImage = global::MouseWithoutBorders.Properties.Images.link_button_click;
            this.LinkButton.Enabled = false;
            this.LinkButton.HoverImage = global::MouseWithoutBorders.Properties.Images.link_button_hover;
            this.LinkButton.Image = global::MouseWithoutBorders.Properties.Images.link_button_normal;
            this.LinkButton.Location = new System.Drawing.Point(199, 366);
            this.LinkButton.Name = "LinkButton";
            this.LinkButton.NormalImage = global::MouseWithoutBorders.Properties.Images.link_button_normal;
            this.LinkButton.Size = new System.Drawing.Size(55, 55);
            this.LinkButton.TabIndex = 15;
            this.LinkButton.TabStop = false;
            this.LinkButton.Click += new System.EventHandler(this.LinkButtonClick);
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
            this.ExpandHelpButton.DownImage = global::MouseWithoutBorders.Properties.Images.expand_button_click;
            this.ExpandHelpButton.HoverImage = global::MouseWithoutBorders.Properties.Images.expand_button_highlight;
            this.ExpandHelpButton.Image = global::MouseWithoutBorders.Properties.Images.expand_button_normal;
            this.ExpandHelpButton.Location = new System.Drawing.Point(360, 211);
            this.ExpandHelpButton.Name = "ExpandHelpButton";
            this.ExpandHelpButton.NormalImage = global::MouseWithoutBorders.Properties.Images.expand_button_normal;
            this.ExpandHelpButton.Size = new System.Drawing.Size(11, 11);
            this.ExpandHelpButton.TabIndex = 24;
            this.ExpandHelpButton.TabStop = false;
            this.ExpandHelpButton.Click += new System.EventHandler(this.ExpandHelpButtonClick);
            // 
            // CollapseHelpButton
            // 
            this.CollapseHelpButton.DisabledImage = null;
            this.CollapseHelpButton.DownImage = global::MouseWithoutBorders.Properties.Images.collapse_button_click;
            this.CollapseHelpButton.HoverImage = global::MouseWithoutBorders.Properties.Images.collapse_button_hover;
            this.CollapseHelpButton.Image = global::MouseWithoutBorders.Properties.Images.collapse_button_normal;
            this.CollapseHelpButton.Location = new System.Drawing.Point(360, 211);
            this.CollapseHelpButton.Name = "CollapseHelpButton";
            this.CollapseHelpButton.NormalImage = global::MouseWithoutBorders.Properties.Images.collapse_button_normal;
            this.CollapseHelpButton.Size = new System.Drawing.Size(11, 11);
            this.CollapseHelpButton.TabIndex = 25;
            this.CollapseHelpButton.TabStop = false;
            this.CollapseHelpButton.Visible = false;
            this.CollapseHelpButton.Click += new System.EventHandler(this.CollapseHelpButtonClick);
            // 
            // HelpLabel
            // 
            this.HelpLabel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(159)))), ((int)(((byte)(217)))));
            this.HelpLabel.Font = new System.Drawing.Font(Control.DefaultFont.Name, 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HelpLabel.ForeColor = System.Drawing.Color.White;
            this.HelpLabel.Location = new System.Drawing.Point(101, 226);
            this.HelpLabel.Name = "HelpLabel";
            this.HelpLabel.Size = new System.Drawing.Size(250, 94);
            this.HelpLabel.TabIndex = 26;
            this.HelpLabel.Text = "The security code can be found on the computer you want to link to by right click" +
                "ing the system tray icon, selecting \"Settings\" (spaces in the key can be discarded)";
            this.HelpLabel.Visible = false;
            // 
            // SetupPage2a
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.HelpLabel);
            this.Controls.Add(this.LinkButton);
            this.Controls.Add(this.ComputerNameField);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.SecurityCodeField);
            this.Controls.Add(this.CollapseHelpButton);
            this.Controls.Add(this.ExpandHelpButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label6);
            this.DoubleBuffered = true;
            this.Name = "SetupPage2a";
            this.Size = new System.Drawing.Size(453, 438);
            this.Controls.SetChildIndex(this.label6, 0);
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.ExpandHelpButton, 0);
            this.Controls.SetChildIndex(this.CollapseHelpButton, 0);
            this.Controls.SetChildIndex(this.SecurityCodeField, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Controls.SetChildIndex(this.ComputerNameField, 0);
            this.Controls.SetChildIndex(this.LinkButton, 0);
            this.Controls.SetChildIndex(this.HelpLabel, 0);
            ((System.ComponentModel.ISupportInitialize)(this.LinkButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ExpandHelpButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CollapseHelpButton)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label6;
        private ColorBorderField SecurityCodeField;
        private ColorBorderField ComputerNameField;
        private System.Windows.Forms.Label label3;
        private ImageButton LinkButton;
        private System.Windows.Forms.Label label2;
        private ImageButton ExpandHelpButton;
        private ImageButton CollapseHelpButton;
        private System.Windows.Forms.Label HelpLabel;
    }
}