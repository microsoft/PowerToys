// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using static MouseWithoutBorders.NativeMethods;

namespace MouseWithoutBorders
{
    internal sealed partial class FormDot : System.Windows.Forms.Form
    {
        private int left;
        private int top;

        internal FormDot()
        {
            InitializeComponent();
            Cursor = CreateDotCursor();
        }

        public void SetPosition(int left, int top)
        {
            this.left = left - 2;
            this.top = top - 2;
        }

        private Cursor CreateCursor(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IconInfo tmp = default;
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            tmp.hbmColor = bmp.GetHbitmap();
            tmp.hbmMask = bmp.GetHbitmap();
            return new Cursor(NativeMethods.CreateIconIndirect(ref tmp));
        }

        private Cursor CreateDotCursor()
        {
            Bitmap bitmap = new(32, 32, PixelFormat.Format24bppRgb);
            bitmap.MakeTransparent(Color.Black);
            Graphics g = Graphics.FromImage(bitmap);
            g.DrawLine(Pens.Gray, 0, 0, 1, 1);
            Cursor c = CreateCursor(bitmap, 0, 0);
            bitmap.Dispose();
            return c;
        }

        private void FormDot_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
        }

        private void FormDot_Shown(object sender, EventArgs e)
        {
            MoveMe();
        }

        private void MoveMe()
        {
            Left = left;
            Top = top;
            Width = 4;
            Height = 4;
        }

        private void FormDot_Click(object sender, EventArgs e)
        {
            IntPtr foreGroundWindow = NativeMethods.GetForegroundWindow();
            Program.FormHelper.SendLog($"{nameof(FormDot_Click)}.Foreground window ({foreGroundWindow}) is " + (foreGroundWindow == Handle ? string.Empty : "not ") + $"the dot form: {Program.DotForm.Handle}.");
        }

        private void FormDot_VisibleChanged(object sender, EventArgs e)
        {
            MoveMe();
        }
    }
}
