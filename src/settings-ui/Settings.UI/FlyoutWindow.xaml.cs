// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.ViewModels.Flyout;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
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

        public POINT? FlyoutAppearPosition { get; set; }

        public FlyoutWindow(POINT? initialPosition)
        {
            this.InitializeComponent();
            this.Activated += FlyoutWindow_Activated;
            FlyoutAppearPosition = initialPosition;
            ViewModel = new FlyoutViewModel();
        }

        private void FlyoutWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            PowerToysTelemetry.Log.WriteEvent(new TrayFlyoutActivatedEvent());
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.CodeActivated)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                if (!FlyoutAppearPosition.HasValue)
                {
                    DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                    double dpiScale = (float)this.GetDpiForWindow() / 96;
                    double x = displayArea.WorkArea.Width - (dpiScale * (WindowWidth + WindowMargin));
                    double y = displayArea.WorkArea.Height - (dpiScale * (WindowHeight + WindowMargin));
                    this.MoveAndResize(x, y, WindowWidth, WindowHeight);
                }
                else
                {
                    DisplayArea displayArea = DisplayArea.GetFromPoint(new PointInt32(FlyoutAppearPosition.Value.X, FlyoutAppearPosition.Value.Y), DisplayAreaFallback.Nearest);

                    // Move the window to the correct screen as a little blob, so we can get the accurate dpi for the screen to calculate the best position to show it.
                    this.MoveAndResize(FlyoutAppearPosition.Value.X, FlyoutAppearPosition.Value.Y, 1, 1);
                    double dpiScale = (float)this.GetDpiForWindow() / 96;

                    // Position the window so that it's inside the display are closest to the point.
                    POINT newPosition = new POINT(FlyoutAppearPosition.Value.X - (int)(dpiScale * WindowWidth / 2), FlyoutAppearPosition.Value.Y - (int)(dpiScale * WindowHeight / 2));
                    if (newPosition.X < displayArea.WorkArea.X)
                    {
                        newPosition.X = displayArea.WorkArea.X;
                    }

                    if (newPosition.Y < displayArea.WorkArea.Y)
                    {
                        newPosition.Y = displayArea.WorkArea.Y;
                    }

                    if (newPosition.X + (dpiScale * WindowWidth) > displayArea.WorkArea.X + displayArea.WorkArea.Width)
                    {
                        newPosition.X = (int)(displayArea.WorkArea.X + displayArea.WorkArea.Width - (dpiScale * WindowWidth));
                    }

                    if (newPosition.Y + (dpiScale * WindowHeight) > displayArea.WorkArea.Y + displayArea.WorkArea.Height)
                    {
                        newPosition.Y = (int)(displayArea.WorkArea.Y + displayArea.WorkArea.Height - (dpiScale * WindowHeight));
                    }

                    this.MoveAndResize(newPosition.X, newPosition.Y, WindowWidth, WindowHeight);
                }
            }

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
