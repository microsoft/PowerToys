using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using static PeekUI.Helpers.WindowsInteropHelper;

namespace PeekUI.Helpers
{
    public static class InteropHelper
    {
        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public enum DWMWINDOWATTRIBUTE
        {
            DWMWAWINDOWCORNERPREFERENCE = 33
        }

        // The DWM_WINDOW_CORNER_PREFERENCE enum for DwmSetWindowAttribute's third parameter, which tells the function
        // what value of the enum to set.
        public enum DWMWINDOWCORNERPREFERENCE
        {
            DWMWCPDEFAULT = 0,
            DWMWCPDONOTROUND = 1,
            DWMWCPROUND = 2,
            DWMWCPROUNDSMALL = 3
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // Import dwmapi.dll and define DwmSetWindowAttribute in C# corresponding to the native function.
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern long DwmSetWindowAttribute(IntPtr hwnd,
                                                         DWMWINDOWATTRIBUTE attribute,
                                                         ref DWMWINDOWCORNERPREFERENCE pvAttribute,
                                                         uint cbAttribute);

        internal static void SetToolWindowStyle(Window win)
        {
            var hwnd = new WindowInteropHelper(win).Handle;
            _ = SetWindowLong(hwnd, GWL_EX_STYLE, GetWindowLong(hwnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}
