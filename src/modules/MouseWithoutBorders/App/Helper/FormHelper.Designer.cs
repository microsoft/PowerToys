namespace MouseWithoutBorders
{
    partial class FormHelper
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormHelper));
            this.timerHelper = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // timerHelper
            // 
            this.timerHelper.Interval = 2000;
            this.timerHelper.Tick += new System.EventHandler(this.TimerHelper_Tick);
            // 
            // FormHelper
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(60, 60);
            this.ControlBox = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "FormHelper";
            this.Opacity = 0.11D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Mouse without Borders Helper";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormHelper_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormHelper_FormClosed);
            this.Shown += new System.EventHandler(this.FormHelper_Shown);
            this.LocationChanged += new System.EventHandler(this.FormHelper_LocationChanged);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.FormHelper_DragEnter);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormHelper_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FormHelper_KeyUp);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.FormHelper_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.FormHelper_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.FormHelper_MouseUp);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timerHelper;
    }
}