using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Wox.Helper
{
    public class WindowIntelopHelper
    {
        private const int GWL_STYLE = -16; //WPF's Message code for Title Bar's Style 
        private const int WS_SYSMENU = 0x80000; //WPF's Message code for System Menu
        private static IntPtr _hwnd_shell;
        private static IntPtr _hwnd_desktop;
        
        //Accessors for shell and desktop handlers
        //Will set the variables once and then will return them
        private static IntPtr HWND_SHELL
        {
            get
            {
                return _hwnd_shell != null ? _hwnd_shell : _hwnd_shell = GetShellWindow();
            }
        }
        private static IntPtr HWND_DESKTOP
        {
            get
            {
                return _hwnd_desktop != null ? _hwnd_desktop : _hwnd_desktop = GetDesktopWindow();
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowRect(IntPtr hwnd, out RECT rc);

        public static bool IsWindowFullscreen()
        {
            RECT foreWinBounds;
            Rectangle screenBounds;
            var hWnd = GetForegroundWindow();
            if (!hWnd.Equals(IntPtr.Zero))
            {
                if (!(hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL)))
                {
                    GetWindowRect(hWnd, out foreWinBounds);
                    screenBounds = Screen.FromHandle(hWnd).Bounds;
                    if ((foreWinBounds.Bottom - foreWinBounds.Top) == screenBounds.Height && (foreWinBounds.Right - foreWinBounds.Left) == screenBounds.Width)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     disable windows toolbar's control box
        ///     this will also disable system menu with Alt+Space hotkey
        /// </summary>
        public static void DisableControlBox(Window win)
        {
            var hwnd = new WindowInteropHelper(win).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}