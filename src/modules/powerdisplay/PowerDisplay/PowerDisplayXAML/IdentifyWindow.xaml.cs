// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Common.UI.Controls.Flyout;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using PowerDisplay.Configuration;
using WinUIEx;

namespace PowerDisplay.PowerDisplayXAML
{
    /// <summary>
    /// Interaction logic for IdentifyWindow.xaml
    /// </summary>
    public sealed partial class IdentifyWindow : WindowEx, IDisposable
    {
        private DispatcherQueueTimer? _autoCloseTimer;
        private bool _disposed;

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

            // Dispose timer when window closes
            this.Closed += (_, _) => Dispose();

            // Auto close after 3 seconds. DispatcherQueueTimer runs on the UI thread
            // and can be deterministically cancelled on Dispose, unlike a detached Task.Delay.
            _autoCloseTimer = DispatcherQueue.CreateTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(3);
            _autoCloseTimer.IsRepeating = false;
            _autoCloseTimer.Tick += (_, _) =>
            {
                if (!_disposed)
                {
                    Close();
                }
            };
            _autoCloseTimer.Start();
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

            // FlyoutWindowHelper handles cross-monitor DPI internally via a 1×1 teleport
            // before the final move, so no WM_DPICHANGED suppression is required here.
            FlyoutWindowHelper.CenterWindowOnDisplay(this, displayArea, windowWidthDip, windowHeightDip);
        }

        private static (int WidthDip, int HeightDip) GetAdaptiveWindowSizeDip(DisplayArea displayArea)
        {
            var workArea = displayArea.WorkArea;
            double dpiScale = FlyoutWindowHelper.GetDpiScale(displayArea);

            int maxWidthDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinWidthDip,
                FlyoutWindowHelper.ScaleToDip((int)Math.Floor(workArea.Width * AppConstants.UI.IdentifyWindowMaxWorkAreaRatio), dpiScale));
            int maxHeightDip = Math.Max(
                AppConstants.UI.IdentifyWindowMinHeightDip,
                FlyoutWindowHelper.ScaleToDip((int)Math.Floor(workArea.Height * AppConstants.UI.IdentifyWindowMaxWorkAreaRatio), dpiScale));

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
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
        }
    }
}
