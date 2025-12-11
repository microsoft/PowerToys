// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
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

        private AppWindow? _appWindow;
        private double _dpiScale = 1.0;

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForWindow(IntPtr hwnd);

        public IdentifyWindow(string displayText)
        {
            InitializeComponent();
            NumberText.Text = displayText;

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
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Get DPI scale for this window
            _dpiScale = GetDpiForWindow(hwnd) / 96.0;

            if (_appWindow != null)
            {
                // Remove title bar using OverlappedPresenter
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                }

                // Set window size scaled for DPI
                // AppWindow.Resize expects physical pixels
                int physicalWidth = (int)(WindowWidthDiu * _dpiScale);
                int physicalHeight = (int)(WindowHeightDiu * _dpiScale);
                _appWindow.Resize(new SizeInt32 { Width = physicalWidth, Height = physicalHeight });
            }

            // Set window topmost and hide from taskbar
            WindowHelper.SetWindowTopmost(hwnd, true);
            WindowHelper.HideFromTaskbar(hwnd);
            WindowHelper.DisableWindowMovingAndResizing(hwnd);
        }

        /// <summary>
        /// Position the window at the center of the specified display area
        /// </summary>
        public void PositionOnDisplay(DisplayArea displayArea)
        {
            if (_appWindow == null)
            {
                return;
            }

            var workArea = displayArea.WorkArea;

            // Window size in physical pixels (already scaled for DPI)
            int physicalWidth = (int)(WindowWidthDiu * _dpiScale);
            int physicalHeight = (int)(WindowHeightDiu * _dpiScale);

            // Calculate center position (WorkArea coordinates are in physical pixels)
            int x = workArea.X + ((workArea.Width - physicalWidth) / 2);
            int y = workArea.Y + ((workArea.Height - physicalHeight) / 2);

            _appWindow.Move(new PointInt32(x, y));
        }
    }
}
