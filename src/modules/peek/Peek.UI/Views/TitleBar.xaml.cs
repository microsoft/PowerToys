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
    using Windows.ApplicationModel.Resources;
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
        private string openWithAppText = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_Text");
        private string currentDefaultApp = string.Empty;

        public TitleBar()
        {
            InitializeComponent();
        }

        public File File
        {
            get => (File)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        public void SetTitleBarToWindow(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                UpdateTitleBarCustomization(mainWindow);
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

        public void UpdateTitleBarCustomization(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow appWindow = mainWindow.GetAppWindow();
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[]
                {
                    new Windows.Graphics.RectInt32(0, 0, (int)TitleBarRootContainer.ActualWidth, (int)TitleBarRootContainer.ActualHeight),
                });

                mainWindow.SetTitleBar(this);
            }
        }

        private void OnFilePropertyChanged()
        {
            // Update the name of default app to launch
            currentDefaultApp = DefaultAppHelper.TryGetDefaultAppName(File.Extension);
            string openWithAppTextFormat = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWithApp_Text");
            OpenWithAppText = string.Format(openWithAppTextFormat, currentDefaultApp);
        }

        private async void LaunchAppButton_Click(object sender, RoutedEventArgs e)
        {
            StorageFile storageFile = await File.GetStorageFileAsync();
            var options = new LauncherOptions();

            if (currentDefaultApp == string.Empty)
            {
                options.DisplayApplicationPicker = true;
            }

            await Launcher.LaunchFileAsync(storageFile, options);
        }
    }
}
