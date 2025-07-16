// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    public static class WindowHelper
    {
        internal const string AdministratorPrefix = "Administrator: ";

        /// <summary>
        /// Sets the main window size.
        /// </summary>
        /// <param name="size">WindowSize enum</param>
        public static void SetWindowSize(IntPtr windowHandler, WindowSize size)
        {
            if (size == WindowSize.UnSpecified)
            {
                return;
            }

            int width = 0, height = 0;

            switch (size)
            {
                case WindowSize.Small:
                    width = 640;
                    height = 480;
                    break;
                case WindowSize.Small_Vertical:
                    width = 480;
                    height = 640;
                    break;
                case WindowSize.Medium:
                    width = 1024;
                    height = 768;
                    break;
                case WindowSize.Medium_Vertical:
                    width = 768;
                    height = 1024;
                    break;
                case WindowSize.Large:
                    width = 1920;
                    height = 1080;
                    break;
                case WindowSize.Large_Vertical:
                    width = 1080;
                    height = 1920;
                    break;
            }

            if (width > 0 && height > 0)
            {
                WindowHelper.SetMainWindowSize(windowHandler, width, height);
            }
        }

        /// <summary>
        /// Gets the main window center coordinates.
        /// </summary>
        /// <returns>(x, y)</returns>
        public static (int CenterX, int CenterY) GetWindowCenter(IntPtr windowHandler)
        {
            if (windowHandler == IntPtr.Zero)
            {
                return (0, 0);
            }
            else
            {
                var rect = ApiHelper.GetWindowCenter(windowHandler);
                return (rect.CenterX, rect.CenterY);
            }
        }

        /// <summary>
        /// Gets the main window center coordinates.
        /// </summary>
        /// <returns>(x, y)</returns>
        public static (int Left, int Top, int Right, int Bottom) GetWindowRect(IntPtr windowHandler)
        {
            if (windowHandler == IntPtr.Zero)
            {
                return (0, 0, 0, 0);
            }
            else
            {
                var rect = ApiHelper.GetWindowRect(windowHandler);
                return (rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
        }

        /// <summary>
        /// Gets the screen center coordinates.
        /// </summary>
        /// <returns>(x, y)</returns>
        public static (int CenterX, int CenterY) GetScreenCenter()
        {
            return ApiHelper.GetScreenCenter();
        }

        /// <summary>
        /// Sets the main window size based on Width and Height.
        /// </summary>
        /// <param name="width">the width in pixel</param>
        /// <param name="height">the height in pixel</param>
        public static void SetMainWindowSize(IntPtr windowHandler, int width, int height)
        {
            if (windowHandler == IntPtr.Zero
                || width <= 0
                || height <= 0)
            {
                return;
            }

            ApiHelper.SetWindowPos(windowHandler, IntPtr.Zero, 0, 0, width, height, ApiHelper.SetWindowPosNoZorder | ApiHelper.SetWindowPosShowWindow);

            // Wait for 1000ms after resize
            Task.Delay(1000).Wait();
        }

        /// <summary>
        /// Retrieves the color of the pixel at the specified screen coordinates.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public static Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = ApiHelper.GetDC(IntPtr.Zero);
            uint pixel = ApiHelper.GetPixel(hdc, x, y);
            _ = ApiHelper.ReleaseDC(IntPtr.Zero, hdc);

            int r = (int)(pixel & 0x000000FF);
            int g = (int)((pixel & 0x0000FF00) >> 8);
            int b = (int)((pixel & 0x00FF0000) >> 16);

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Retrieves the color of the pixel at the specified screen coordinates as a string.
        /// </summary>
        /// <param name="x">The X coordinate on the screen.</param>
        /// <param name="y">The Y coordinate on the screen.</param>
        /// <returns>The color of the pixel at the specified coordinates.</returns>
        public static string GetPixelColorString(int x, int y)
        {
            Color color = WindowHelper.GetPixelColor(x, y);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Gets the size of the display.
        /// </summary>
        /// <returns>
        /// A tuple containing the width and height of the display.
        /// </returns
        public static Tuple<int, int> GetDisplaySize()
        {
            IntPtr hdc = ApiHelper.GetDC(IntPtr.Zero);
            int screenWidth = ApiHelper.GetDeviceCaps(hdc, ApiHelper.DESKTOPHORZRES);
            int screenHeight = ApiHelper.GetDeviceCaps(hdc, ApiHelper.DESKTOPVERTRES);
            _ = ApiHelper.ReleaseDC(IntPtr.Zero, hdc);

            return Tuple.Create(screenWidth, screenHeight);
        }

        public static bool IsWindowOpen(string windowName)
        {
            var matchingWindows = ApiHelper.FindDesktopWindowHandler([windowName, AdministratorPrefix + windowName]);
            return matchingWindows.Count > 0;
        }

        internal static class ApiHelper
        {
            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            public const uint SetWindowPosNoMove = 0x0002;
            public const uint SetWindowPosNoZorder = 0x0004;
            public const uint SetWindowPosShowWindow = 0x0040;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

            // Delegate for the EnumWindows callback function
            private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            // P/Invoke declaration for EnumWindows
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            // P/Invoke declaration for GetWindowTextLength
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int GetWindowTextLength(IntPtr hWnd);

            // P/Invoke declaration for GetWindowText
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("gdi32.dll")]
            public static extern uint GetPixel(IntPtr hdc, int x, int y);

            [DllImport("gdi32.dll")]
            public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

            public const int DESKTOPHORZRES = 118;
            public const int DESKTOPVERTRES = 117;

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            // Define the Win32 RECT structure
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;    // X coordinate of the left edge of the window
                public int Top;     // Y coordinate of the top edge of the window
                public int Right;   // X coordinate of the right edge of the window
                public int Bottom;  // Y coordinate of the bottom edge of the window
            }

            // Import GetWindowRect API to retrieve window's screen coordinates
            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            public static List<(IntPtr HWnd, string Title)> FindDesktopWindowHandler(string[] matchingWindowsTitles)
            {
                var windows = new List<(IntPtr HWnd, string Title)>();

                _ = EnumWindows(
                    (hWnd, lParam) =>
                    {
                        int length = GetWindowTextLength(hWnd);
                        if (length > 0)
                        {
                            var builder = new StringBuilder(length + 1);
                            _ = GetWindowText(hWnd, builder, builder.Capacity);

                            var title = builder.ToString();
                            if (matchingWindowsTitles.Contains(title))
                            {
                                windows.Add((hWnd, title));
                            }
                        }

                        return true; // Continue enumeration
                    },
                    IntPtr.Zero);

                return windows;
            }

            /// <summary>
            /// Get the center point coordinates of a specified window (in screen coordinates)
            /// </summary>
            /// <param name="hWnd">The window handle</param>
            /// <returns>The center point (x, y)</returns>
            public static (int CenterX, int CenterY) GetWindowCenter(IntPtr hWnd)
            {
                if (hWnd == IntPtr.Zero)
                {
                    throw new ArgumentException("Invalid window handle");
                }

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    int centerX = rect.Left + (width / 2);
                    int centerY = rect.Top + (height / 2);

                    return (centerX, centerY);
                }
                else
                {
                    throw new InvalidOperationException("Failed to retrieve window coordinates");
                }
            }

            public static (int Left, int Top, int Right, int Bottom) GetWindowRect(IntPtr hWnd)
            {
                if (hWnd == IntPtr.Zero)
                {
                    throw new ArgumentException("Invalid window handle");
                }

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    return (rect.Left, rect.Top, rect.Right, rect.Bottom);
                }
                else
                {
                    throw new InvalidOperationException("Failed to retrieve window coordinates");
                }
            }

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            public enum SystemMetric
            {
                ScreenWidth = 0,            // Width of the primary screen in pixels (SM_CXSCREEN)
                ScreenHeight = 1,           // Height of the primary screen in pixels (SM_CYSCREEN)
                VirtualScreenWidth = 78,    // Width of the virtual screen that includes all monitors (SM_CXVIRTUALSCREEN)
                VirtualScreenHeight = 79,   // Height of the virtual screen that includes all monitors (SM_CYVIRTUALSCREEN)
                MonitorCount = 80,          // Number of display monitors (SM_CMONITORS, available on Windows XP+)
            }

            public static (int CenterX, int CenterY) GetScreenCenter()
            {
                int width = GetSystemMetrics((int)SystemMetric.ScreenWidth);
                int height = GetSystemMetrics((int)SystemMetric.ScreenHeight);

                return (width / 2, height / 2);
            }
        }
    }
}
