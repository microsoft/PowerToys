// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading;
using FileLocksmith.Interop;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PowerToys.FileLocksmithUI.Properties;
using PowerToys.FileLocksmithUI.Views;
using Windows.Graphics;
using WinUIEx;

namespace FileLocksmithUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
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
                ThemeHelpers.SetImmersiveDarkMode(WinRT.Interop.WindowNative.GetWindowHandle(this), ThemeHelpers.GetAppTheme() == AppTheme.Dark);
                titleBar.Visibility = Visibility.Collapsed;
            }
        }

        public void Dispose()
        {
        }
    }
}
