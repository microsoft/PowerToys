// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace MouseJumpUI;

partial class MainForm
{

    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        panel1 = new Panel();
        Thumbnail = new PictureBox();
        panel1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)Thumbnail).BeginInit();
        SuspendLayout();
        // 
        // panel1
        // 
        panel1.BackColor = System.Drawing.SystemColors.Highlight;
        panel1.Controls.Add(Thumbnail);
        panel1.Dock = DockStyle.Fill;
        panel1.Location = new System.Drawing.Point(0, 0);
        panel1.Name = "panel1";
        panel1.Size = new System.Drawing.Size(800, 450);
        panel1.TabIndex = 1;
        // 
        // Thumbnail
        // 
        Thumbnail.BackColor = System.Drawing.SystemColors.Control;
        Thumbnail.Dock = DockStyle.Fill;
        Thumbnail.Location = new System.Drawing.Point(5, 5);
        Thumbnail.Name = "Thumbnail";
        Thumbnail.Size = new System.Drawing.Size(800, 450);
        Thumbnail.SizeMode = PictureBoxSizeMode.Normal;
        Thumbnail.TabIndex = 1;
        Thumbnail.TabStop = false;
        Thumbnail.Click += Thumbnail_Click;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(800, 450);
        Controls.Add(panel1);
        FormBorderStyle = FormBorderStyle.None;
        Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
        KeyPreview = true;
        Name = "MainForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "MouseJump";
        TopMost = true;
        Deactivate += MainForm_Deactivate;
        Load += MainForm_Load;
        KeyDown += MainForm_KeyDown;
        panel1.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)Thumbnail).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private Panel panel1;
    private PictureBox Thumbnail;

}
