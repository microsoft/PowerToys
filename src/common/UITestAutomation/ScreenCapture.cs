// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides methods for capturing the screen with the mouse cursor.
    /// </summary>
    internal static class ScreenCapture
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        /// <summary>
        /// Represents a point with X and Y coordinates.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Contains information about the cursor.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            /// <summary>
            /// Gets or sets the size of the structure.
            /// </summary>
            public int CbSize;

            /// <summary>
            /// Gets or sets the cursor state.
            /// </summary>
            public int Flags;

            /// <summary>
            /// Gets or sets the handle to the cursor.
            /// </summary>
            public IntPtr HCursor;

            /// <summary>
            /// Gets or sets the screen position of the cursor.
            /// </summary>
            public POINT PTScreenPos;
        }

        private const int CURSORSHOWING = 0x00000001;
        private const int DESKTOPHORZRES = 118;
        private const int DESKTOPVERTRES = 117;
        private const int DINORMAL = 0x0003;

        /// <summary>
        /// Captures the screen with the mouse cursor and saves it to the specified file path.
        /// </summary>
        /// <param name="filePath">The file path to save the captured image.</param>
        private static void CaptureScreenWithMouse(string filePath)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int screenWidth = GetDeviceCaps(hdc, DESKTOPHORZRES);
            int screenHeight = GetDeviceCaps(hdc, DESKTOPVERTRES);
            ReleaseDC(IntPtr.Zero, hdc);

            Rectangle bounds = new Rectangle(0, 0, screenWidth, screenHeight);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    CURSORINFO cursorInfo;
                    cursorInfo.CbSize = Marshal.SizeOf<CURSORINFO>();
                    if (GetCursorInfo(out cursorInfo) && cursorInfo.Flags == CURSORSHOWING)
                    {
                        using (System.Drawing.Graphics gIcon = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            IntPtr hdcDest = gIcon.GetHdc();
                            DrawIconEx(hdcDest, cursorInfo.PTScreenPos.X, cursorInfo.PTScreenPos.Y, cursorInfo.HCursor, 0, 0, 0, IntPtr.Zero, DINORMAL);
                            gIcon.ReleaseHdc(hdcDest);
                        }
                    }
                }

                bitmap.Save(filePath, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Captures a screenshot and saves it to the specified directory.
        /// </summary>
        /// <param name="directory">The directory to save the screenshot.</param>
        private static void CaptureScreenshot(string directory)
        {
            string filePath = Path.Combine(directory, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
            CaptureScreenWithMouse(filePath);
        }

        /// <summary>
        /// Timer callback method to capture a screenshot.
        /// </summary>
        /// <param name="state">The state object passed to the callback method.</param>
        public static void TimerCallback(object? state)
        {
            string directory = (string)state!;
            CaptureScreenshot(directory);
        }
    }
}
