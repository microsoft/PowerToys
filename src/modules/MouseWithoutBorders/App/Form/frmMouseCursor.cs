// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Forms;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders
{
    public partial class FrmMouseCursor : System.Windows.Forms.Form
    {
        public FrmMouseCursor()
        {
            InitializeComponent();
            Width = Height = 32;
        }

        private void FrmMouseCursor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
            else
            {
                Common.MouseCursorForm = null;
            }
        }

        private void FrmMouseCursor_LocationChanged(object sender, EventArgs e)
        {
            if (Setting.Values.DrawMouseEx)
            {
                const int DX = 0, DY = 2;
                IntPtr hDesktop = NativeMethods.GetDesktopWindow();
                IntPtr hdc = NativeMethods.GetWindowDC(hDesktop);

                if (hdc != IntPtr.Zero)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _ = NativeMethods.SetPixel(hdc, DX + Left + i, DY + Top + i, 0xFFFFFF);
                        _ = NativeMethods.SetPixel(hdc, DX + Left + 1, DY + Top + i, 0xFFFFFF);
                        _ = NativeMethods.SetPixel(hdc, DX + Left + i + 1, DY + Top + i, 0);
                        _ = NativeMethods.SetPixel(hdc, DX + Left, DY + Top + i, 0);
                    }

                    _ = NativeMethods.ReleaseDC(hDesktop, hdc);
                }
            }
        }
    }
}
