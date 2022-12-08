// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Views
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
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

        public static readonly DependencyProperty NumberOfFilesProperty =
        DependencyProperty.Register(
           nameof(NumberOfFiles),
           typeof(int),
           typeof(TitleBar),
           new PropertyMetadata(null, null));

        private string? defaultAppName;

        [ObservableProperty]
        private string openWithAppText = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_Text");

        [ObservableProperty]
        private string openWithAppToolTip = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_ToolTip");

        [ObservableProperty]
        private string? fileCountText;

        public TitleBar()
        {
            InitializeComponent();
        }

        public File File
        {
            get => (File)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        public int NumberOfFiles
        {
            get => (int)GetValue(NumberOfFilesProperty);
            set => SetValue(NumberOfFilesProperty, value);
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

        private void UpdateTitleBarCustomization(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow appWindow = mainWindow.GetAppWindow();
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[]
                {
                    new Windows.Graphics.RectInt32(0, 0, (int)TitleBarRootContainer.ActualWidth, (int)TitleBarRootContainer.ActualHeight),
                });

                mainWindow.SetTitleBar(this);
            }
        }

        private void OnFilePropertyChanged()
        {
            if (File == null)
            {
                return;
            }

            UpdateFileCountText();
            UpdateDefaultAppToLaunch();
        }

        private void UpdateFileCountText()
        {
            // Update file count
            if (NumberOfFiles > 1)
            {
                // TODO: Update the hardcoded fileIndex when the NFQ PR gets merged
                int currentFileIndex = 1;
                string fileCountTextFormat = ResourceLoader.GetForViewIndependentUse().GetString("AppTitle_FileCounts_Text");
                FileCountText = string.Format(fileCountTextFormat, currentFileIndex, NumberOfFiles);
            }
        }

        private void UpdateDefaultAppToLaunch()
        {
            // Update the name of default app to launch
            defaultAppName = DefaultAppHelper.TryGetDefaultAppName(File.Extension);

            string openWithAppTextFormat = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWithApp_Text");
            OpenWithAppText = string.Format(openWithAppTextFormat, defaultAppName);

            string openWithAppToolTipFormat = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWithApp_ToolTip");
            OpenWithAppToolTip = string.Format(openWithAppToolTipFormat, defaultAppName);
        }

        [RelayCommand]
        private async void LaunchDefaultAppButtonAsync()
        {
            StorageFile storageFile = await File.GetStorageFileAsync();
            LauncherOptions options = new ();

            if (string.IsNullOrEmpty(defaultAppName))
            {
                // If there's no default app found, open the App picker
                options.DisplayApplicationPicker = true;
            }
            else
            {
                // Try to launch the default app for current file format
                bool result = await Launcher.LaunchFileAsync(storageFile, options);

                if (!result)
                {
                    // If we couldn't successfully open the default app, open the App picker as a fallback
                    options.DisplayApplicationPicker = true;
                    await Launcher.LaunchFileAsync(storageFile, options);
                }
            }
        }
    }
}
