// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace FancyZonesEditor.Utils
{
    internal sealed class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // PInvokes used to pull the editor window to the foreground.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        private const int GWL_EX_STYLE = -20;
        private const int GWLP_HWNDPARENT = -8;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int SW_HIDE = 0;
        private const int SW_SHOWNA = 8;

        private const int DWMWA_CLOAK = 13;

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);

        /// <summary>
        /// Hides the window from the taskbar and Alt+Tab by turning it into a tool window.
        /// </summary>
        /// <param name="hwnd">Handle of the window to restyle.</param>
        public static void SetWindowStyleToolWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            nint exStyle = GetWindowLongPtr(hwnd, GWL_EX_STYLE);
            _ = SetWindowLongPtr(hwnd, GWL_EX_STYLE, (exStyle | WS_EX_TOOLWINDOW) & ~(nint)WS_EX_APPWINDOW);
        }

        /// <summary>
        /// Makes <paramref name="hwnd"/> an owned window of <paramref name="ownerHwnd"/>.
        /// WinUI 3 has no equivalent of the WPF <c>Window.Owner</c> property, so ownership
        /// (stay-on-top-of-owner, minimize/restore together) is established through Win32.
        /// </summary>
        /// <param name="hwnd">Handle of the owned window.</param>
        /// <param name="ownerHwnd">Handle of the owner window.</param>
        public static void SetWindowOwner(IntPtr hwnd, IntPtr ownerHwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, ownerHwnd);
        }

        /// <summary>
        /// Positions a window using a DPI-unaware context so it matches the virtual coordinates
        /// coming from the FancyZones C++ backend (which uses a DPI-unaware thread).
        /// This keeps overlay positioning correct on mixed-DPI multi-monitor setups.
        /// </summary>
        /// <param name="hwnd">Handle of the window to position.</param>
        /// <param name="x">Virtual left coordinate.</param>
        /// <param name="y">Virtual top coordinate.</param>
        /// <param name="width">Virtual width.</param>
        /// <param name="height">Virtual height.</param>
        public static void SetWindowPositionDpiUnaware(IntPtr hwnd, int x, int y, int width, int height)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            // Temporarily switch to DPI-unaware context to position window.
            // This matches how the C++ backend gets coordinates via dpiUnawareThread.
            IntPtr oldContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
            try
            {
                SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            finally
            {
                SetThreadDpiAwarenessContext(oldContext);
            }
        }

        /// <summary>
        /// Raises a window to the top of the z-order without activating it.
        /// <see cref="Microsoft.UI.Xaml.Window.Activate"/> alone does not reliably lift every
        /// overlay above the windows already on its monitor, because Windows can deny the
        /// foreground change, so each overlay is raised explicitly.
        /// </summary>
        /// <param name="hwnd">Handle of the window to raise.</param>
        public static void BringWindowToTopNoActivate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Makes a window invisible while keeping XAML rendering it.
        /// A plain <c>Hide()</c> stops a WinUI 3 window from painting, so its composition
        /// surface keeps the last frame and re-showing it flashes that stale frame. Cloaking
        /// the HWND instead keeps it composed but invisible - the technique Command Palette
        /// uses. The window is hidden first so the OS hands the foreground to another app,
        /// then re-shown without activation while staying cloaked.
        /// </summary>
        /// <param name="hwnd">Handle of the window to conceal.</param>
        public static void CloakWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int cloak = 1;
            bool cloaked = DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref cloak, sizeof(int)) == 0;

            ShowWindow(hwnd, SW_HIDE);

            if (cloaked)
            {
                // Bring the HWND back so XAML keeps painting it; it stays invisible because
                // it is still cloaked. If cloaking failed, leave it plainly hidden instead.
                ShowWindow(hwnd, SW_SHOWNA);
            }
        }

        /// <summary>
        /// Reverses <see cref="CloakWindow"/>.
        /// </summary>
        /// <param name="hwnd">Handle of the window to reveal.</param>
        public static void UncloakWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int cloak = 0;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
        }
    }
}
