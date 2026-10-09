namespace MouseWithoutBorders
{
    partial class ColorBorderField
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
            this.InnerField = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // InnerField
            // 
            this.InnerField.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.InnerField.Location = new System.Drawing.Point(3, 3);
            this.InnerField.Name = "InnerField";
            this.InnerField.Size = new System.Drawing.Size(100, 13);
            this.InnerField.TabIndex = 0;
            this.InnerField.WordWrap = false;
            // 
            // ColorBorderField
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.InnerField);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ColorBorderField";
            this.Size = new System.Drawing.Size(134, 36);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox InnerField;
    }
}
