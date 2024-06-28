// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectsEditor.Utils
{
    internal sealed class MonitorHelper
    {
        internal static List<Rectangle> GetDpiUnawareScreenBounds()
        {
            List<Rectangle> screenBounds = new List<Rectangle>();

            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            GetDpiOnScreen(primaryScreen, out uint dpiXPrimary, out uint dpiYPrimary);

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                GetDpiOnScreen(screen, out uint dpiX, out uint dpiY);

                screenBounds.Add(new Rectangle((int)(screen.Bounds.Left * 96 / dpiXPrimary), (int)(screen.Bounds.Top * 96 / dpiYPrimary), (int)(screen.Bounds.Width * 96 / dpiX), (int)(screen.Bounds.Height * 96 / dpiY)));
            }

            return screenBounds;
        }

        private static void GetDpiOnScreen(Screen screen, out uint dpiX, out uint dpiY)
        {
            var point = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var hmonitor = NativeMethods.MonitorFromPoint(point, NativeMethods._MONITOR_DEFAULTTONEAREST);

            switch (NativeMethods.GetDpiForMonitor(hmonitor, NativeMethods.DpiType.EFFECTIVE, out dpiX, out dpiY).ToInt32())
            {
                case NativeMethods._S_OK: break;
                default:
                    dpiX = 96;
                    dpiY = 96;
                    break;
            }
        }
    }
}
