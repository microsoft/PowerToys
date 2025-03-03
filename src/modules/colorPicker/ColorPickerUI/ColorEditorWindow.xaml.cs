// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using ColorPicker.Helpers;
using ControlzEx.Theming;
using ManagedCommon;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Win32;
using Windows.Graphics;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for ColorEditorWindow.xaml
    /// </summary>
    public partial class ColorEditorWindow : Window
    {
        private readonly AppStateHandler _appStateHandler;

        public ColorEditorWindow(AppStateHandler appStateHandler)
        {
            InitializeComponent();

            _appStateHandler = appStateHandler;
            Closing += ColorEditorWindow_Closing;
        }

        private void ColorEditorWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            _appStateHandler.EndUserSession();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            WindowChrome.SetWindowChrome(
                this,
                new WindowChrome
                {
                CaptionHeight = 0,
                CornerRadius = default,
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = ResizeMode == ResizeMode.NoResize ? default : new Thickness(4),
                UseAeroCaptionButtons = false,
                });
            if (OSVersionHelper.IsWindows11())
            {
                // ResizeMode="NoResize" removes rounded corners. So force them to rounded.
                IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
                DWMWINDOWATTRIBUTE attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
                DWM_WINDOW_CORNER_PREFERENCE preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));
            }
            else
            {
                // On Windows10 ResizeMode="NoResize" removes the border so we add a new one.
                MainBorder.BorderThickness = new System.Windows.Thickness(0.5);
            }

            // Hide then Show with WindowStyle="None" will remove the Mica effect. So manually remove the titlebar.
            RemoveWindowTitlebarContents();

            base.OnSourceInitialized(e);
        }

        public void RemoveWindowTitlebarContents()
        {
            IntPtr handle = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int windowStyleLong = GetWindowLong(handle, GWLSTYLE);
            windowStyleLong &= ~(int)WindowStyles.WS_SYSMENU;

            IntPtr result = SetWindowLong(handle, GWLSTYLE, windowStyleLong);
            if (result.ToInt64() == 0)
            {
                int error = Marshal.GetLastWin32Error();
                Logger.LogError($"SetWindowLong error {error}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void TitleBar_MouseRightClick(object sender, MouseButtonEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Get the mouse position relative to the screen
            Point mousePosition = e.GetPosition(this);

            Point screenPoint = PointToScreen(mousePosition);

            // Display the system menu at the current mouse position
            IntPtr hMenu = GetSystemMenu(hwnd, false);
            if (hMenu != IntPtr.Zero)
            {
                int command = TrackPopupMenu(
                    hMenu,
                    TPMLEFTALIGN | TPMRETURNCMD,
                    (int)screenPoint.X,
                    (int)screenPoint.Y,
                    0,
                    hwnd,
                    IntPtr.Zero);
                if (command > 0)
                {
                    SendMessage(hwnd, WMSYSCOMMAND, new IntPtr(command), IntPtr.Zero);
                }
            }
        }

        private const int WMSYSCOMMAND = 0x0112;
        private const int TPMLEFTALIGN = 0x0000;
        private const int TPMRETURNCMD = 0x0100;

        // The enum flag for DwmSetWindowAttribute's second parameter, which tells the function what attribute to set.
        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
        }

        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3,
        }

        // Import dwmapi.dll and define DwmSetWindowAttribute in C# corresponding to the native function.
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(
            IntPtr hwnd,
            DWMWINDOWATTRIBUTE attribute,
            ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
            uint cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, int uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public const int GWLSTYLE = -16;

        [Flags]
        public enum WindowStyles : uint
        {
            WS_SYSMENU = 0x00080000, // System menu (close/maximize/minimize button area)
        }
    }
}
