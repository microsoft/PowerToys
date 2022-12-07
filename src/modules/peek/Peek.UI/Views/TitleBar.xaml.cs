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

        public static readonly DependencyProperty NumberOfFilesProperty =
        DependencyProperty.Register(
           nameof(NumberOfFiles),
           typeof(int),
           typeof(TitleBar),
           new PropertyMetadata(null, null));

        [ObservableProperty]
        private string openWithAppText = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_Text");
        private string currentDefaultApp = string.Empty;

        [ObservableProperty]
        private string? fileName;

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
            // Update file name
            if (NumberOfFiles > 1)
            {
                // TODO: Update the hardcoded fileIndex when the NFQ PR gets merged
                FileName = string.Format("{0}/{1} {2}", 1, NumberOfFiles, File.FileName);
            }
            else
            {
                FileName = File.FileName;
            }

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
