namespace MouseWithoutBorders
{
    partial class SettingsPage1
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
            this.AddComputerButton = new MouseWithoutBorders.ImageButton();
            this.KeyboardShortcutsButton = new MouseWithoutBorders.ImageButton();
            this.AdvancedOptionsButton = new MouseWithoutBorders.ImageButton();
            this.LinkComputerButton = new MouseWithoutBorders.ImageButton();
            this.MachineMatrix = new MouseWithoutBorders.MachineMatrix();
            ((System.ComponentModel.ISupportInitialize)(this.AddComputerButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.KeyboardShortcutsButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.AdvancedOptionsButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.LinkComputerButton)).BeginInit();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.Font = new System.Drawing.Font(DefaultFont.Name, 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(47, 106);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(310, 20);
            this.label2.TabIndex = 4;
            this.label2.Text = "DRAG COMPUTERS TO MATCH THEIR PHYSICAL LAYOUT";
            // 
            // AddComputerButton
            // 
            this.AddComputerButton.DisabledImage = null;
            this.AddComputerButton.DownImage = global::MouseWithoutBorders.Properties.Images.computer_button_click;
            this.AddComputerButton.HoverImage = global::MouseWithoutBorders.Properties.Images.computer_button_hover;
            this.AddComputerButton.Image = global::MouseWithoutBorders.Properties.Images.computer_button_normal;
            this.AddComputerButton.Location = new System.Drawing.Point(50, 317);
            this.AddComputerButton.Name = "AddComputerButton";
            this.AddComputerButton.NormalImage = global::MouseWithoutBorders.Properties.Images.computer_button_normal;
            this.AddComputerButton.Size = new System.Drawing.Size(74, 23);
            this.AddComputerButton.TabIndex = 9;
            this.AddComputerButton.TabStop = false;
            this.AddComputerButton.Click += new System.EventHandler(this.AddComputerButtonClick);
            // 
            // KeyboardShortcutsButton
            // 
            this.KeyboardShortcutsButton.DisabledImage = null;
            this.KeyboardShortcutsButton.DownImage = global::MouseWithoutBorders.Properties.Images.keyboard_button_click;
            this.KeyboardShortcutsButton.HoverImage = global::MouseWithoutBorders.Properties.Images.keyboard_button_hover;
            this.KeyboardShortcutsButton.Image = global::MouseWithoutBorders.Properties.Images.keyboard_button_normal;
            this.KeyboardShortcutsButton.Location = new System.Drawing.Point(327, 317);
            this.KeyboardShortcutsButton.Name = "KeyboardShortcutsButton";
            this.KeyboardShortcutsButton.NormalImage = global::MouseWithoutBorders.Properties.Images.keyboard_button_normal;
            this.KeyboardShortcutsButton.Size = new System.Drawing.Size(84, 23);
            this.KeyboardShortcutsButton.TabIndex = 11;
            this.KeyboardShortcutsButton.TabStop = false;
            this.KeyboardShortcutsButton.Click += new System.EventHandler(this.KeyboardShortcutsButtonClick);
            // 
            // AdvancedOptionsButton
            // 
            this.AdvancedOptionsButton.DisabledImage = null;
            this.AdvancedOptionsButton.DownImage = global::MouseWithoutBorders.Properties.Images.advanced_button_click;
            this.AdvancedOptionsButton.HoverImage = global::MouseWithoutBorders.Properties.Images.advanced_button_hover;
            this.AdvancedOptionsButton.Image = global::MouseWithoutBorders.Properties.Images.advanced_button_normal;
            this.AdvancedOptionsButton.Location = new System.Drawing.Point(244, 317);
            this.AdvancedOptionsButton.Name = "AdvancedOptionsButton";
            this.AdvancedOptionsButton.NormalImage = global::MouseWithoutBorders.Properties.Images.advanced_button_normal;
            this.AdvancedOptionsButton.Size = new System.Drawing.Size(74, 23);
            this.AdvancedOptionsButton.TabIndex = 12;
            this.AdvancedOptionsButton.TabStop = false;
            this.AdvancedOptionsButton.Click += new System.EventHandler(this.AdvancedOptionsButtonClick);
            // 
            // LinkComputerButton
            // 
            this.LinkComputerButton.DisabledImage = null;
            this.LinkComputerButton.DownImage = global::MouseWithoutBorders.Properties.Images.small_link_button_click;
            this.LinkComputerButton.HoverImage = global::MouseWithoutBorders.Properties.Images.small_link_button_hover;
            this.LinkComputerButton.Image = global::MouseWithoutBorders.Properties.Images.small_link_button_normal;
            this.LinkComputerButton.Location = new System.Drawing.Point(133, 317);
            this.LinkComputerButton.Name = "LinkComputerButton";
            this.LinkComputerButton.NormalImage = global::MouseWithoutBorders.Properties.Images.small_link_button_normal;
            this.LinkComputerButton.Size = new System.Drawing.Size(74, 23);
            this.LinkComputerButton.TabIndex = 13;
            this.LinkComputerButton.TabStop = false;
            this.LinkComputerButton.Click += new System.EventHandler(this.LinkComputersButtonClick);
            // 
            // machineMatrix1
            // 
            this.MachineMatrix.BackColor = System.Drawing.Color.Transparent;
            this.MachineMatrix.Location = new System.Drawing.Point(50, 121);
            this.MachineMatrix.Name = "MachineMatrix";
            this.MachineMatrix.Size = new System.Drawing.Size(360, 195);
            this.MachineMatrix.TabIndex = 14;
            this.MachineMatrix.TwoRows = false;
            // 
            // SettingsPage1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.Controls.Add(this.MachineMatrix);
            this.Controls.Add(this.LinkComputerButton);
            this.Controls.Add(this.AdvancedOptionsButton);
            this.Controls.Add(this.KeyboardShortcutsButton);
            this.Controls.Add(this.AddComputerButton);
            this.Controls.Add(this.label2);
            this.Name = "SettingsPage1";
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.AddComputerButton, 0);
            this.Controls.SetChildIndex(this.KeyboardShortcutsButton, 0);
            this.Controls.SetChildIndex(this.AdvancedOptionsButton, 0);
            this.Controls.SetChildIndex(this.LinkComputerButton, 0);
            this.Controls.SetChildIndex(this.MachineMatrix, 0);
            ((System.ComponentModel.ISupportInitialize)(this.AddComputerButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.KeyboardShortcutsButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.AdvancedOptionsButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.LinkComputerButton)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private ImageButton AddComputerButton;
        private ImageButton KeyboardShortcutsButton;
        private ImageButton AdvancedOptionsButton;
        private ImageButton LinkComputerButton;
        private MachineMatrix MachineMatrix;

    }
}
