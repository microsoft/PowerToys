// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
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

        private double _dpiScale = 1.0;

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
            catch (Exception)
            {
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
            _dpiScale = this.GetDpiForWindow() / 96.0;

            // Set window size scaled for DPI
            // AppWindow.Resize expects physical pixels
            int physicalWidth = (int)(WindowWidthDiu * _dpiScale);
            int physicalHeight = (int)(WindowHeightDiu * _dpiScale);
            this.AppWindow.Resize(new SizeInt32 { Width = physicalWidth, Height = physicalHeight });
            this.IsAlwaysOnTop = true;
        }

        /// <summary>
        /// Position the window at the center of the specified display area
        /// </summary>
        public void PositionOnDisplay(DisplayArea displayArea)
        {
            var workArea = displayArea.WorkArea;

            // Window size in physical pixels (already scaled for DPI)
            int physicalWidth = (int)(WindowWidthDiu * _dpiScale);
            int physicalHeight = (int)(WindowHeightDiu * _dpiScale);

            // Calculate center position (WorkArea coordinates are in physical pixels)
            int x = workArea.X + ((workArea.Width - physicalWidth) / 2);
            int y = workArea.Y + ((workArea.Height - physicalHeight) / 2);

            // Use WindowEx's AppWindow property
            this.AppWindow.Move(new PointInt32(x, y));
        }
    }
}
