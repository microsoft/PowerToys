// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PowerDisplay.Helpers;
using Windows.Graphics;
using WinRT.Interop;

namespace PowerDisplay.PowerDisplayXAML
{
    /// <summary>
    /// Interaction logic for IdentifyWindow.xaml
    /// </summary>
    public sealed partial class IdentifyWindow : Window
    {
        // Window size in device-independent units (DIU)
        private const int WindowWidthDiu = 300;
        private const int WindowHeightDiu = 280;

        private readonly nint _hWnd;

        public IdentifyWindow(string displayText)
        {
            InitializeComponent();
            _hWnd = WindowNative.GetWindowHandle(this);
            NumberText.Text = displayText;

            // Cloak window to prevent flicker during cross-monitor DPI transitions.
            // PositionOnDisplay will uncloak after the window is correctly sized and positioned.
            WindowHelper.CloakWindow(_hWnd);

            // Configure window style
            ConfigureWindow();

            // Auto close after 3 seconds
            Task.Delay(3000).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Close();
                });
            });
        }

        private void ConfigureWindow()
        {
            // Base popup window configuration (presenter, taskbar, title bar collapse)
            WindowHelper.ConfigureAsPopupWindow(this, _hWnd, alwaysOnTop: true);

            // Note: Window size is NOT set here. PositionOnDisplay will set the
            // correct size based on the target monitor's DPI using the two-phase approach.
        }

        /// <summary>
        /// Position the window at the center of the specified display area.
        /// Uses a two-phase approach to handle cross-monitor DPI differences correctly.
        /// </summary>
        public void PositionOnDisplay(DisplayArea displayArea)
        {
            var workArea = displayArea.WorkArea;

            // Phase 1: Move to the target display to get the correct DPI.
            // The window may have been created on a different monitor (e.g., primary)
            // with a different DPI. Moving first ensures GetDpiForWindow returns
            // the target monitor's DPI, not the creation monitor's DPI.
            this.AppWindow.Move(new PointInt32(
                workArea.X + (workArea.Width / 2),
                workArea.Y + (workArea.Height / 2)));

            // Phase 2: Now on the target monitor, DPI is accurate.
            // No DPI change will occur during MoveAndResize, so no auto-scaling.
            double dpiScale = WindowHelper.GetDpiForWindow(this) / 96.0;
            int physicalWidth = (int)(WindowWidthDiu * dpiScale);
            int physicalHeight = (int)(WindowHeightDiu * dpiScale);

            // Calculate center position (WorkArea coordinates are in physical pixels)
            int x = workArea.X + ((workArea.Width - physicalWidth) / 2);
            int y = workArea.Y + ((workArea.Height - physicalHeight) / 2);

            this.AppWindow.MoveAndResize(new RectInt32(x, y, physicalWidth, physicalHeight));

            // Uncloak: window is correctly sized and positioned for the target monitor.
            // The caller's Activate() will make it visible.
            WindowHelper.UncloakWindow(_hWnd);
        }
    }
}
