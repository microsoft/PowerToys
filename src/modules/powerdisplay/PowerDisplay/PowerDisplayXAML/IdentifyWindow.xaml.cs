// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
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
            // correct size based on the target monitor's DPI.
        }

        /// <summary>
        /// Position the window at the center of the specified work area.
        /// DPI is provided by the caller (from GetDpiForMonitor), so no two-phase
        /// move is needed — single MoveAndResize call, same pattern as CmdPal.
        /// </summary>
        /// <param name="workArea">Work area bounds in physical pixels (excludes taskbar)</param>
        /// <param name="dpi">Target monitor's effective DPI (from GetDpiForMonitor)</param>
        public void PositionOnDisplay(RectInt32 workArea, int dpi)
        {
            double dpiScale = dpi / 96.0;
            int physicalWidth = (int)(WindowWidthDiu * dpiScale);
            int physicalHeight = (int)(WindowHeightDiu * dpiScale);

            // Calculate center position (WorkArea coordinates are in physical pixels)
            int x = workArea.X + ((workArea.Width - physicalWidth) / 2);
            int y = workArea.Y + ((workArea.Height - physicalHeight) / 2);

            // Single call — DPI is already known, no need for two-phase move
            this.AppWindow.MoveAndResize(new RectInt32(x, y, physicalWidth, physicalHeight));

            // Uncloak: window is correctly sized and positioned for the target monitor.
            // The caller's Activate() will make it visible.
            WindowHelper.UncloakWindow(_hWnd);
        }
    }
}
