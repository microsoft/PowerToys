// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using PowerDisplay.Configuration;
using PowerDisplay.Helpers;
using WinUIEx;

namespace PowerDisplay.PowerDisplayXAML
{
    /// <summary>
    /// Interaction logic for IdentifyWindow.xaml
    /// </summary>
    public sealed partial class IdentifyWindow : WindowEx, IDisposable
    {
        private DpiSuppressor? _dpiSuppressor;

        public IdentifyWindow(string displayText)
        {
            InitializeComponent();
            NumberText.Text = displayText;
            try
            {
                this.SetIsShownInSwitchers(false);
            }
            catch (NotImplementedException)
            {
                // WinUI will throw if explorer is not running, safely ignore
            }

            // Configure window style
            ConfigureWindow();

            // Subclass WndProc to suppress WM_DPICHANGED during cross-DPI positioning
            _dpiSuppressor = new DpiSuppressor(this);

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
            // Set a preferred size in DIP. PositionOnDisplay will clamp it for the target monitor.
            this.SetWindowSize(AppConstants.UI.IdentifyWindowPreferredWidthDip, AppConstants.UI.IdentifyWindowPreferredHeightDip);
            this.IsAlwaysOnTop = true;
        }

        /// <summary>
        /// Position the window at the center of the specified display area
        /// </summary>
        public void PositionOnDisplay(DisplayArea displayArea)
        {
            var (windowWidthDip, windowHeightDip) = GetAdaptiveWindowSizeDip(displayArea);

            // Suppress WM_DPICHANGED during MoveAndResize to prevent double-scaling
            // when positioning on a monitor with different DPI than the primary.
            using (_dpiSuppressor?.Suppress() ?? default)
            {
                WindowHelper.CenterWindowOnDisplay(this, displayArea, windowWidthDip, windowHeightDip);
            }
        }

        private static (int WidthDip, int HeightDip) GetAdaptiveWindowSizeDip(DisplayArea displayArea)
        {
            var workArea = displayArea.WorkArea;
            double dpiScale = WindowHelper.GetDpiScale(displayArea);

            int maxWidthDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinWidthDip,
                WindowHelper.ScaleToDip((int)Math.Floor(workArea.Width * AppConstants.UI.IdentifyWindowMaxWorkAreaRatio), dpiScale));
            int maxHeightDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinHeightDip,
                WindowHelper.ScaleToDip((int)Math.Floor(workArea.Height * AppConstants.UI.IdentifyWindowMaxWorkAreaRatio), dpiScale));

            int widthDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinWidthDip,
                Math.Min(AppConstants.UI.IdentifyWindowPreferredWidthDip, maxWidthDip));
            int heightDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinHeightDip,
                Math.Min(AppConstants.UI.IdentifyWindowPreferredHeightDip, maxHeightDip));

            return (widthDip, heightDip);
        }

        public void Dispose()
        {
            _dpiSuppressor?.Dispose();
        }
    }
}
