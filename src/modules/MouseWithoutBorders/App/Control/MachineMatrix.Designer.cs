namespace MouseWithoutBorders
{
    partial class MachineMatrix
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
            this.SingleRowRadioButton = new MouseWithoutBorders.ImageRadioButton();
            this.TwoRowsRadioButton = new MouseWithoutBorders.ImageRadioButton();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.SingleRowRadioButton);
            this.panel1.Controls.Add(this.TwoRowsRadioButton);
            this.panel1.Location = new System.Drawing.Point(0, 7);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(51, 56);
            this.panel1.TabIndex = 6;
            // 
            // SingleRowRadioButton
            // 
            this.SingleRowRadioButton.Checked = true;
            this.SingleRowRadioButton.CheckedImage = global::MouseWithoutBorders.Properties.Images.one_row_button_checked;
            this.SingleRowRadioButton.ImageLocation = new System.Drawing.Point(0, 0);
            this.SingleRowRadioButton.Location = new System.Drawing.Point(0, 0);
            this.SingleRowRadioButton.Margin = new System.Windows.Forms.Padding(0);
            this.SingleRowRadioButton.Name = "SingleRowRadioButton";
            this.SingleRowRadioButton.Size = new System.Drawing.Size(51, 24);
            this.SingleRowRadioButton.TabIndex = 4;
            this.SingleRowRadioButton.TabStop = true;
            this.SingleRowRadioButton.TextLocation = new System.Drawing.Point(0, 0);
            this.SingleRowRadioButton.UncheckedImage = global::MouseWithoutBorders.Properties.Images.one_row_button_unchecked;
            this.SingleRowRadioButton.UseVisualStyleBackColor = true;
            this.SingleRowRadioButton.CheckedChanged += new System.EventHandler(this.SingleRowRadioButtonCheckedChanged);
            // 
            // TwoRowsRadioButton
            // 
            this.TwoRowsRadioButton.CheckedImage = global::MouseWithoutBorders.Properties.Images.two_row_button_checked;
            this.TwoRowsRadioButton.ImageLocation = new System.Drawing.Point(0, 0);
            this.TwoRowsRadioButton.Location = new System.Drawing.Point(0, 27);
            this.TwoRowsRadioButton.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.TwoRowsRadioButton.Name = "TwoRowsRadioButton";
            this.TwoRowsRadioButton.Size = new System.Drawing.Size(27, 24);
            this.TwoRowsRadioButton.TabIndex = 5;
            this.TwoRowsRadioButton.TextLocation = new System.Drawing.Point(0, 0);
            this.TwoRowsRadioButton.UncheckedImage = global::MouseWithoutBorders.Properties.Images.two_row_button_unchecked;
            this.TwoRowsRadioButton.UseVisualStyleBackColor = true;
            this.TwoRowsRadioButton.CheckedChanged += new System.EventHandler(this.TwoRowsRadioButtonCheckedChanged);
            // 
            // MachineMatrix
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Maroon;
            this.Controls.Add(this.panel1);
            this.DoubleBuffered = true;
            this.Name = "MachineMatrix";
            this.Size = new System.Drawing.Size(360, 175);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ImageRadioButton SingleRowRadioButton;
        private ImageRadioButton TwoRowsRadioButton;
        private System.Windows.Forms.Panel panel1;

    }
}
