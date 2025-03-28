// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.ViewModels.Flyout;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BugReportWindow : WindowEx
    {
        private const int WindowWidth = 286;
        private const int WindowHeight = 166;
        private const int WindowMargin = 12;

        public BugReportViewModel ViewModel { get; set; }

        public BugReportWindow()
        {
            this.InitializeComponent();

            // Remove the caption style from the window style. Windows App SDK 1.6 added it, which made the title bar and borders appear for the Flyout. This code removes it.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            windowStyle &= ~NativeMethods.WS_CAPTION;
            _ = NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, windowStyle);

            this.Activated += BugReportWindow_Activated;
            ViewModel = new BugReportViewModel(this);
            BugReportShellPage.DataContext = ViewModel;
        }

        private void BugReportWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.CodeActivated)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                double dpiScale = (float)this.GetDpiForWindow() / 96;
                double x = displayArea.WorkArea.Width - (dpiScale * (WindowWidth + WindowMargin));
                double y = displayArea.WorkArea.Height - (dpiScale * (WindowHeight + WindowMargin));
                this.MoveAndResize(x, y, WindowWidth, WindowHeight);
            }

            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                {
                    this.Hide();
                }
            }
        }
    }
}
