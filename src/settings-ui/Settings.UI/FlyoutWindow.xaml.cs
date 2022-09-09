// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    using global::Windows.Graphics;
    using Microsoft.UI;
    using Microsoft.UI.Windowing;
    using WinUIEx;

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FlyoutWindow : WindowEx
    {
        private const int WindowWidth = 360;
        private const int WindowHeight = 340;

        public FlyoutWindow()
        {
            this.InitializeComponent();
            this.Activated += FlyoutWindow_Activated;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            double x = displayArea.WorkArea.Width - (WindowWidth + 204); // TO DO - HARDCODED, BUT THIS SHOULD BE DPI DEPENDEND?
            double y = displayArea.WorkArea.Height - (WindowHeight + 186); // TO DO - HARDCODED, BUT THIS SHOULD BE DPI DEPENDEND?
            this.MoveAndResize(x, y, WindowWidth, WindowHeight);
        }

        private void FlyoutWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                this.Hide();
            }
        }
    }
}
