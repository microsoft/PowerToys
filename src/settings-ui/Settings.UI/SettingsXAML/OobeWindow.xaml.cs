// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using Windows.Graphics;
using WinUIEx;
using WinUIEx.Messaging;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OobeWindow : WindowEx, IDisposable
    {
        private PowerToysModules initialModule;

        private const int ExpectedWidth = 1100;
        private const int ExpectedHeight = 700;
        private const int DefaultDPI = 96;
        private int _currentDPI;
        private WindowId _windowId;
        private IntPtr _hWnd;
        private AppWindow _appWindow;
        private WindowMessageMonitor _msgMonitor;
        private bool disposedValue;

        public OobeWindow(PowerToysModules initialModule)
        {
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            this.InitializeComponent();

            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(_windowId);
            this.Activated += Window_Activated_SetIcon;

            OverlappedPresenter presenter = _appWindow.Presenter as OverlappedPresenter;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;

            var dpi = NativeMethods.GetDpiForWindow(_hWnd);
            _currentDPI = dpi;
            float scalingFactor = (float)dpi / DefaultDPI;
            int width = (int)(ExpectedWidth * scalingFactor);
            int height = (int)(ExpectedHeight * scalingFactor);

            SizeInt32 size;
            size.Width = width;
            size.Height = height;
            _appWindow.Resize(size);

            this.initialModule = initialModule;

            _msgMonitor = new WindowMessageMonitor(this);
            _msgMonitor.WindowMessageReceived += (_, e) =>
            {
                const int WM_NCLBUTTONDBLCLK = 0x00A3;
                if (e.Message.MessageId == WM_NCLBUTTONDBLCLK)
                {
                    // Disable double click on title bar to maximize window
                    e.Result = 0;
                    e.Handled = true;
                }
            };

            this.SizeChanged += OobeWindow_SizeChanged;

            var loader = Helpers.ResourceLoaderInstance.ResourceLoader;
            Title = loader.GetString("OobeWindow_Title");

            if (shellPage != null)
            {
                shellPage.NavigateToModule(this.initialModule);
            }

            OobeShellPage.SetRunSharedEventCallback(() =>
            {
                return Constants.PowerLauncherSharedEvent();
            });

            OobeShellPage.SetColorPickerSharedEventCallback(() =>
            {
                return Constants.ShowColorPickerSharedEvent();
            });

            OobeShellPage.SetOpenMainWindowCallback((Type type) =>
            {
                App.OpenSettingsWindow(type);
            });
        }

        public void SetAppWindow(PowerToysModules module)
        {
            if (shellPage != null)
            {
                shellPage.NavigateToModule(module);
            }
        }

        private void Window_Activated_SetIcon(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            _appWindow.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void OobeWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            var dpi = NativeMethods.GetDpiForWindow(_hWnd);
            if (_currentDPI != dpi)
            {
                // Reacting to a DPI change. Should not cause a resize -> sizeChanged loop.
                _currentDPI = dpi;
                float scalingFactor = (float)dpi / DefaultDPI;
                int width = (int)(ExpectedWidth * scalingFactor);
                int height = (int)(ExpectedHeight * scalingFactor);
                SizeInt32 size;
                size.Width = width;
                size.Height = height;
                _appWindow.Resize(size);
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            App.ClearOobeWindow();

            var mainWindow = App.GetSettingsWindow();
            if (mainWindow != null)
            {
                mainWindow.CloseHiddenWindow();
            }

            App.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object sender, ElementTheme theme)
        {
            WindowHelper.SetTheme(this, theme);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _msgMonitor?.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
