// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using interop;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OobeWindow : Window
    {
        private PowerToysModules initialModule;

        private const int ExpectedWidth = 1100;
        private const int ExpectedHeight = 700;
        private const int DefaultDPI = 96;
        private int _currentDPI;
        private WindowId _windowId;
        private IntPtr _hWnd;
        private AppWindow _appWindow;

        public OobeWindow(PowerToysModules initialModule)
        {
            this.InitializeComponent();

            // Set window icon
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(_windowId);
            _appWindow.SetIcon("icon.ico");

            OverlappedPresenter presenter = _appWindow.Presenter as OverlappedPresenter;
            presenter.IsResizable = false;
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

            this.SizeChanged += OobeWindow_SizeChanged;

            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
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
        }
    }
}
