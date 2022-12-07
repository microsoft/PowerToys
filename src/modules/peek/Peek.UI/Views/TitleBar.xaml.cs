// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Views
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ManagedCommon;
    using Microsoft.UI;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Peek.Common.Models;
    using Peek.UI.Helpers;
    using Windows.Storage;
    using Windows.System;
    using WinUIEx;

    [INotifyPropertyChanged]
    public sealed partial class TitleBar : UserControl
    {
        public static readonly DependencyProperty FileProperty =
        DependencyProperty.Register(
            nameof(File),
            typeof(File),
            typeof(TitleBar),
            new PropertyMetadata(null, (d, e) => ((TitleBar)d).OnFilePropertyChanged()));

        [ObservableProperty]
        private string openWithApp = "Open With";
        private string? currentDefaultApp;

        public TitleBar()
        {
            InitializeComponent();
        }

        public File File
        {
            get => (File)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        public void SetToWindow(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow window = mainWindow.GetAppWindow();
                window.TitleBar.ExtendsContentIntoTitleBar = true;
                window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                window.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[]
                {
                    new Windows.Graphics.RectInt32(0, 0, (int)TitleBarRootContainer.ActualWidth, (int)TitleBarRootContainer.ActualHeight),
                });

                mainWindow.SetTitleBar(this);
            }
            else
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ThemeHelpers.SetImmersiveDarkMode(hWnd, ThemeHelpers.GetAppTheme() == AppTheme.Dark);
                Visibility = Visibility.Collapsed;

                // Set window icon
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.SetIcon("Assets/Icon.ico");
            }
        }

        private void OnFilePropertyChanged()
        {
            // Update app name
            currentDefaultApp = DefaultAppHelper.TryGetDefaultAppName(File.Extension);
            OpenWithApp = "Open With " + currentDefaultApp;
        }

        private async void LaunchAppButton_Click(object sender, RoutedEventArgs e)
        {
            StorageFile storageFile = await File.GetStorageFileAsync();
            var options = new LauncherOptions();

            if (currentDefaultApp == null)
            {
                options.DisplayApplicationPicker = true;
            }

            await Launcher.LaunchFileAsync(storageFile, options);
        }
    }
}
