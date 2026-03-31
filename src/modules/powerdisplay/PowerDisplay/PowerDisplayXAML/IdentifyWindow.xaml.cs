// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using PowerDisplay.Helpers;
using Windows.Graphics;
using WinUIEx;

namespace PowerDisplay.PowerDisplayXAML
{
    /// <summary>
    /// Interaction logic for IdentifyWindow.xaml
    /// </summary>
    public sealed partial class IdentifyWindow : WindowEx
    {
        // Window size in device-independent units (DIU)
        private const int WindowWidthDiu = 300;
        private const int WindowHeightDiu = 280;

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
            // Set an initial size in DIU. PositionOnDisplay will rescale for the target monitor.
            this.SetWindowSize(WindowWidthDiu, WindowHeightDiu);
            this.IsAlwaysOnTop = true;
        }

        /// <summary>
        /// Position the window at the center of the specified display area
        /// </summary>
        public void PositionOnDisplay(DisplayArea displayArea)
        {
            var workArea = displayArea.WorkArea;

            double dpiScale = WindowHelper.GetDpiScale(displayArea);
            int physicalWidth = WindowHelper.ScaleToPhysicalPixels(WindowWidthDiu, dpiScale);
            int physicalHeight = WindowHelper.ScaleToPhysicalPixels(WindowHeightDiu, dpiScale);

            // WorkArea coordinates are relative to the target display area.
            int x = workArea.X + ((workArea.Width - physicalWidth) / 2);
            int y = workArea.Y + ((workArea.Height - physicalHeight) / 2);

            this.AppWindow.MoveAndResize(new RectInt32(x, y, physicalWidth, physicalHeight), displayArea);
        }
    }
}
