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

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

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
        private const int WS_EX_TRANSPARENT = 0x00000020;
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
        /// Shows a blocking system message box. Used where a <c>ContentDialog</c> cannot be:
        /// before any XAML surface exists, and while reporting an unhandled exception, which can
        /// reach us on any thread and with the process already on its way down. This is what the
        /// WPF editor's <c>MessageBox.Show</c> did.
        /// </summary>
        /// <param name="text">Body of the message.</param>
        /// <param name="caption">Title of the message box.</param>
        public static void ShowMessageBox(string text, string caption)
        {
            const uint MB_OK = 0x0;
            const uint MB_ICONERROR = 0x10;
            const uint MB_TASKMODAL = 0x2000;

            _ = MessageBox(IntPtr.Zero, text ?? string.Empty, caption ?? string.Empty, MB_OK | MB_ICONERROR | MB_TASKMODAL);
        }

        /// <summary>
        /// Returns the DIP-to-pixel scale of the monitor a window is on. Usable before the window
        /// has been activated, unlike <c>XamlRoot.RasterizationScale</c>.
        /// </summary>
        /// <param name="hwnd">Handle of the window.</param>
        /// <returns>The scale factor, or 1.0 when it cannot be determined.</returns>
        public static double GetWindowScale(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return 1.0;
            }

            uint dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? 1.0 : dpi / 96.0;
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
                // Cloaking removes the window from composition, but not from hit testing. The
                // concealed picker overlaps the full-screen zone editor, so leave mouse input to
                // the editor while the picker stays shown for XAML rendering.
                SetWindowClickThrough(hwnd, true);

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
            if (DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref cloak, sizeof(int)) == 0)
            {
                // The picker is interactive again once it is visible. If uncloaking fails, keep
                // the invisible HWND click-through so it cannot become an input blocker.
                SetWindowClickThrough(hwnd, false);
            }
        }

        private static void SetWindowClickThrough(IntPtr hwnd, bool enabled)
        {
            nint exStyle = GetWindowLongPtr(hwnd, GWL_EX_STYLE);
            nint updatedStyle = enabled
                ? exStyle | WS_EX_TRANSPARENT
                : exStyle & ~(nint)WS_EX_TRANSPARENT;

            if (updatedStyle != exStyle)
            {
                _ = SetWindowLongPtr(hwnd, GWL_EX_STYLE, updatedStyle);
            }
        }
    }
}
