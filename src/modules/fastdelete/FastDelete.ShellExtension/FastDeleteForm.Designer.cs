namespace FastDelete.ShellExtension
{
    partial class FastDeleteForm
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.footerPanel1 = new FastDelete.ShellExtension.FooterPanel();
            this.mConfirmButton = new System.Windows.Forms.Button();
            this.mCancelButton = new System.Windows.Forms.Button();
            this.mInstructionLabel = new Sunburst.SharingManager.Controls.MainInstructionLabel();
            this.mConfirmationLabel = new System.Windows.Forms.Label();
            this.mProgressBar = new System.Windows.Forms.ProgressBar();
            this.tableLayoutPanel1.SuspendLayout();
            this.footerPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.footerPanel1, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.mInstructionLabel, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.mConfirmationLabel, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.mProgressBar, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(978, 450);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // footerPanel1
            // 
            this.footerPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.footerPanel1.AutoSize = true;
            this.footerPanel1.ColumnCount = 2;
            this.footerPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.footerPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.footerPanel1.Controls.Add(this.mConfirmButton, 0, 0);
            this.footerPanel1.Controls.Add(this.mCancelButton, 1, 0);
            this.footerPanel1.Location = new System.Drawing.Point(0, 356);
            this.footerPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.footerPanel1.Name = "footerPanel1";
            this.footerPanel1.RowCount = 1;
            this.footerPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.footerPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.footerPanel1.Size = new System.Drawing.Size(978, 94);
            this.footerPanel1.TabIndex = 0;
            // 
            // mConfirmButton
            // 
            this.mConfirmButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.mConfirmButton.Location = new System.Drawing.Point(630, 24);
            this.mConfirmButton.Margin = new System.Windows.Forms.Padding(24, 24, 0, 24);
            this.mConfirmButton.Name = "mConfirmButton";
            this.mConfirmButton.Size = new System.Drawing.Size(150, 46);
            this.mConfirmButton.TabIndex = 0;
            this.mConfirmButton.Text = "&Delete";
            this.mConfirmButton.UseVisualStyleBackColor = true;
            this.mConfirmButton.Click += new System.EventHandler(this.mConfirmButton_Click);
            // 
            // mCancelButton
            // 
            this.mCancelButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.mCancelButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.mCancelButton.Location = new System.Drawing.Point(804, 24);
            this.mCancelButton.Margin = new System.Windows.Forms.Padding(24);
            this.mCancelButton.Name = "mCancelButton";
            this.mCancelButton.Size = new System.Drawing.Size(150, 46);
            this.mCancelButton.TabIndex = 1;
            this.mCancelButton.Text = "C&ancel";
            this.mCancelButton.UseVisualStyleBackColor = true;
            this.mCancelButton.Click += new System.EventHandler(this.mCancelButton_Click);
            // 
            // mInstructionLabel
            // 
            this.mInstructionLabel.AutoSize = true;
            this.mInstructionLabel.Location = new System.Drawing.Point(24, 24);
            this.mInstructionLabel.Margin = new System.Windows.Forms.Padding(24);
            this.mInstructionLabel.Name = "mInstructionLabel";
            this.mInstructionLabel.Size = new System.Drawing.Size(566, 45);
            this.mInstructionLabel.TabIndex = 1;
            this.mInstructionLabel.Text = "Are you sure you want to delete \"%1\"?";
            // 
            // mConfirmationLabel
            // 
            this.mConfirmationLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mConfirmationLabel.AutoSize = true;
            this.mConfirmationLabel.Location = new System.Drawing.Point(24, 117);
            this.mConfirmationLabel.Margin = new System.Windows.Forms.Padding(24);
            this.mConfirmationLabel.Name = "mConfirmationLabel";
            this.mConfirmationLabel.Size = new System.Drawing.Size(930, 64);
            this.mConfirmationLabel.TabIndex = 2;
            this.mConfirmationLabel.Text = "This operation cannot be undone. The contents of this folder will not be moved to" +
    " the Recycle Bin.";
            // 
            // mProgressBar
            // 
            this.mProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mProgressBar.Location = new System.Drawing.Point(24, 229);
            this.mProgressBar.Margin = new System.Windows.Forms.Padding(24);
            this.mProgressBar.Name = "mProgressBar";
            this.mProgressBar.Size = new System.Drawing.Size(930, 46);
            this.mProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.mProgressBar.TabIndex = 3;
            this.mProgressBar.Visible = false;
            // 
            // FastDeleteForm
            // 
            this.AcceptButton = this.mConfirmButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.CancelButton = this.mCancelButton;
            this.ClientSize = new System.Drawing.Size(978, 450);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(1004, 100000);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(1004, 521);
            this.Name = "FastDeleteForm";
            this.Text = "Fast Delete";
            this.Load += new System.EventHandler(this.FastDeleteForm_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.footerPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private FooterPanel footerPanel1;
        private System.Windows.Forms.Button mConfirmButton;
        private System.Windows.Forms.Button mCancelButton;
        private Sunburst.SharingManager.Controls.MainInstructionLabel mInstructionLabel;
        private System.Windows.Forms.Label mConfirmationLabel;
        private System.Windows.Forms.ProgressBar mProgressBar;
    }
}