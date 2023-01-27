// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.ViewModels.Flyout;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FlyoutWindow : WindowEx
    {
        private const int WindowWidth = 386;
        private const int WindowHeight = 486;
        private const int WindowMargin = 12;

        public FlyoutViewModel ViewModel { get; set; }

        public FlyoutWindow()
        {
            this.InitializeComponent();
            this.Activated += FlyoutWindow_Activated;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            double dpiScale = (float)this.GetDpiForWindow() / 96;
            double x = displayArea.WorkArea.Width - (dpiScale * (WindowWidth + WindowMargin));
            double y = displayArea.WorkArea.Height - (dpiScale * (WindowHeight + WindowMargin));
            this.MoveAndResize(x, y, WindowWidth, WindowHeight);
            ViewModel = new FlyoutViewModel();
        }

        private void FlyoutWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            PowerToysTelemetry.Log.WriteEvent(new TrayFlyoutActivatedEvent());
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                if (ViewModel.CanHide)
                {
                    FlyoutShellPage.SwitchToLaunchPage();
                    this.Hide();
                }
            }
        }
    }
}
