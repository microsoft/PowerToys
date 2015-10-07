using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;

namespace Wox.Helper
{
    public class WindowIntelopHelper
    {
        private const int GWL_STYLE = -16; //WPF's Message code for Title Bar's Style 
        private const int GWL_EXSTYLE = -20; //Gets the exstyle of the window
        private const int WS_EX_TOPMOST = 0x00000008; //Topmost flag
        private const int WS_SYSMENU = 0x80000; //WPF's Message code for System Menu
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        ///Checks if the foreground window is TopMost (even Wox)
        /// </summary>
        /// <returns></returns>
        public static bool IsForegroundWindowTopMost()
        {
            return (GetWindowLong(GetForegroundWindow(), GWL_EXSTYLE) & WS_EX_TOPMOST) == WS_EX_TOPMOST;
        }

        /// <summary>
        /// disable windows toolbar's control box
        /// this will also disable system menu with Alt+Space hotkey
        /// </summary>
        public static void DisableControlBox(Window win)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(win).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }
    }
}
