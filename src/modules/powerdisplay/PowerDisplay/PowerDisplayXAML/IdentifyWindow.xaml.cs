// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
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
    public sealed partial class IdentifyWindow : Window, IDisposable
    {
        private AppWindow? _appWindow;
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _configurationSource;
        private bool _disposed;

        public IdentifyWindow(int number)
        {
            InitializeComponent();
            NumberText.Text = number.ToString(CultureInfo.InvariantCulture);

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

                // Set window size to fit the large number (200pt font)
                _appWindow.Resize(new SizeInt32 { Width = 300, Height = 280 });
            }

            // Set window topmost and hide from taskbar
            WindowHelper.SetWindowTopmost(hwnd, true);
            WindowHelper.HideFromTaskbar(hwnd);
            WindowHelper.DisableWindowMovingAndResizing(hwnd);

            // Configure 90% transparent acrylic backdrop
            ConfigureAcrylicBackdrop();
        }

        private void ConfigureAcrylicBackdrop()
        {
            if (!DesktopAcrylicController.IsSupported())
            {
                return;
            }

            _configurationSource = new SystemBackdropConfiguration();
            _acrylicController = new DesktopAcrylicController();

            // Set 90% transparency (TintOpacity 0.1 = 10% tint = 90% transparent)
            _acrylicController.TintColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            _acrylicController.TintOpacity = 0.1f;
            _acrylicController.LuminosityOpacity = 0f;

            // Add target using WinRT cast
            var target = WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this);
            _acrylicController.AddSystemBackdropTarget(target);
            _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
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
            int windowWidth = 300;
            int windowHeight = 280;

            // Calculate center position (WorkArea coordinates are in physical pixels)
            int x = workArea.X + ((workArea.Width - windowWidth) / 2);
            int y = workArea.Y + ((workArea.Height - windowHeight) / 2);

            _appWindow.Move(new PointInt32(x, y));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _acrylicController?.Dispose();
            _acrylicController = null;
        }
    }
}
