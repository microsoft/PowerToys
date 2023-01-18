// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FlyoutWindow : WindowEx
    {
        private const int WindowWidth = 358;
        private const int WindowHeight = 468;
        private const int WindowMargin = 12;

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
        }

        private void FlyoutWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                this.Close();
            }
        }
    }
}
