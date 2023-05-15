/*
 * File name: frmLogon.cs (not used)
 * History:
 *  Truong Do (ductdo) 2008-10-XX Created/Updated
 * */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    internal partial class frmLogon : Form
    {
        internal System.Windows.Forms.Label LabelDesktop
        {
            get { return labelDesktop; }
            set { labelDesktop = value; }
        }

        internal frmLogon()
        {
            InitializeComponent();
        }

        private void frmLogon_Load(object sender, EventArgs e)
        {
            Left = 10;
            Top = 10;
            Width = labelDesktop.Width + 2;
            Height = labelDesktop.Height + 2;            
        }

        private void frmLogon_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
            }
        }

        private void frmLogon_Shown(object sender, EventArgs e)
        {
            //Common.SetForegroundWindow(Common.GetDesktopWindow());
        }
    }
}
