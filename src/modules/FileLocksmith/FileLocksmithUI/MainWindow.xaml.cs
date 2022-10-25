// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace FileLocksmithUI
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow(bool isElevated)
        {
            InitializeComponent();
            mainPage.ViewModel.IsElevated = isElevated;
            SetTitleBar();
        }

        private void SetTitleBar()
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow window = this.GetAppWindow();
                window.TitleBar.ExtendsContentIntoTitleBar = true;
                window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                SetTitleBar(titleBar);
            }
            else
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ThemeHelpers.SetImmersiveDarkMode(hWnd, ThemeHelpers.GetAppTheme() == AppTheme.Dark);
                titleBar.Visibility = Visibility.Collapsed;

                // Set window icon
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.SetIcon("icon.ico");
            }
        }

        public void Dispose()
        {
        }
    }
}
