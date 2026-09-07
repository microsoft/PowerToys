// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;

namespace MouseJump.Kicker
{
    partial class KickerForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(KickerForm));
            ActivationHotkey = new Button();
            CloseMouseJump = new Button();
            StartWinUI3 = new Button();
            SuspendLayout();
            // 
            // ActivationHotkey
            // 
            ActivationHotkey.Location = new Point(12, 89);
            ActivationHotkey.Name = "ActivationHotkey";
            ActivationHotkey.Size = new Size(260, 70);
            ActivationHotkey.TabIndex = 1;
            ActivationHotkey.Text = "Activate Hotkey";
            ActivationHotkey.UseVisualStyleBackColor = true;
            ActivationHotkey.Click += ActivationHotkey_Click;
            // 
            // CloseMouseJump
            // 
            CloseMouseJump.Location = new Point(12, 165);
            CloseMouseJump.Name = "CloseMouseJump";
            CloseMouseJump.Size = new Size(260, 70);
            CloseMouseJump.TabIndex = 2;
            CloseMouseJump.Text = "Close MouseJump";
            CloseMouseJump.UseVisualStyleBackColor = true;
            CloseMouseJump.Click += CloseMouseJump_Click;
            // 
            // StartWinUI3
            // 
            StartWinUI3.Location = new Point(12, 12);
            StartWinUI3.Name = "StartWinUI3";
            StartWinUI3.Size = new Size(260, 70);
            StartWinUI3.TabIndex = 3;
            StartWinUI3.Text = "Start MouseJump (WinUI3)";
            StartWinUI3.UseVisualStyleBackColor = true;
            StartWinUI3.Click += StartWinUI3_Click;
            // 
            // KickerForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(281, 242);
            Controls.Add(StartWinUI3);
            Controls.Add(CloseMouseJump);
            Controls.Add(ActivationHotkey);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "KickerForm";
            Text = "MouseJump Kicker";
            ResumeLayout(false);
        }

        #endregion
        private Button ActivationHotkey;
        private Button CloseMouseJump;
        private Button StartWinUI3;
    }
}
