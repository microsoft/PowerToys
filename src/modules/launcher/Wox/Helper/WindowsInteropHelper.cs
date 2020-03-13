using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Wox.Helper
{
    public class WindowsInteropHelper
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
                return _hwnd_shell != IntPtr.Zero ? _hwnd_shell : _hwnd_shell = GetShellWindow();
            }
        }
        private static IntPtr HWND_DESKTOP
        {
            get
            {
                return _hwnd_desktop != IntPtr.Zero ? _hwnd_desktop : _hwnd_desktop = GetDesktopWindow();
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

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.DLL")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);


        const string WINDOW_CLASS_CONSOLE = "ConsoleWindowClass";
        const string WINDOW_CLASS_WINTAB = "Flip3D";
        const string WINDOW_CLASS_PROGMAN = "Progman";
        const string WINDOW_CLASS_WORKERW = "WorkerW";

        public static bool IsWindowFullscreen()
        {
            //get current active window
            IntPtr hWnd = GetForegroundWindow();

            if (hWnd != null && !hWnd.Equals(IntPtr.Zero))
            {
                //if current active window is NOT desktop or shell
                if (!(hWnd.Equals(HWND_DESKTOP) || hWnd.Equals(HWND_SHELL)))
                {
                    StringBuilder sb = new StringBuilder(256);
                    GetClassName(hWnd, sb, sb.Capacity);
                    string windowClass = sb.ToString();

                    //for Win+Tab (Flip3D)
                    if (windowClass == WINDOW_CLASS_WINTAB)
                    {
                        return false;
                    }

                    RECT appBounds;
                    GetWindowRect(hWnd, out appBounds);

                    //for console (ConsoleWindowClass), we have to check for negative dimensions
                    if (windowClass == WINDOW_CLASS_CONSOLE)
                    {
                        return appBounds.Top < 0 && appBounds.Bottom < 0;
                    }

                    //for desktop (Progman or WorkerW, depends on the system), we have to check 
                    if (windowClass == WINDOW_CLASS_PROGMAN || windowClass == WINDOW_CLASS_WORKERW)
                    {
                        IntPtr hWndDesktop = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        hWndDesktop = FindWindowEx(hWndDesktop, IntPtr.Zero, "SysListView32", "FolderView");
                        if (hWndDesktop != null && !hWndDesktop.Equals(IntPtr.Zero))
                        {
                            return false;
                        }
                    }

                    Rectangle screenBounds = Screen.FromHandle(hWnd).Bounds;
                    if ((appBounds.Bottom - appBounds.Top) == screenBounds.Height && (appBounds.Right - appBounds.Left) == screenBounds.Width)
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

        /// <summary>
        /// Transforms pixels to Device Independent Pixels used by WPF
        /// </summary>
        /// <param name="visual">current window, required to get presentation source</param>
        /// <param name="unitX">horizontal position in pixels</param>
        /// <param name="unitY">vertical position in pixels</param>
        /// <returns>point containing device independent pixels</returns>
        public static Point TransformPixelsToDIP(Visual visual, double unitX, double unitY)
        {
            Matrix matrix;
            var source = PresentationSource.FromVisual(visual);
            if (source != null)
            {
                matrix = source.CompositionTarget.TransformFromDevice;
            }
            else
            {
                using (var src = new HwndSource(new HwndSourceParameters()))
                {
                    matrix = src.CompositionTarget.TransformFromDevice;
                }
            }
            return new Point((int)(matrix.M11 * unitX), (int)(matrix.M22 * unitY));
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