// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Views
{
    using System;
    using System.Collections.Generic;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using ManagedCommon;
    using Microsoft.UI;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Peek.Common.Models;
    using Peek.UI.Extensions;
    using Peek.UI.Helpers;
    using Windows.ApplicationModel.Resources;
    using Windows.Graphics;
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

        public static readonly DependencyProperty FileIndexProperty =
            DependencyProperty.Register(
                nameof(FileIndex),
                typeof(int),
                typeof(TitleBar),
                new PropertyMetadata(-1, (d, e) => ((TitleBar)d).OnFileIndexPropertyChanged()));

        public static readonly DependencyProperty IsMultiSelectionProperty =
            DependencyProperty.Register(
                nameof(IsMultiSelection),
                typeof(bool),
                typeof(TitleBar),
                new PropertyMetadata(false));

        public static readonly DependencyProperty NumberOfFilesProperty =
            DependencyProperty.Register(
               nameof(NumberOfFiles),
               typeof(int),
               typeof(TitleBar),
               new PropertyMetadata(null, null));

        [ObservableProperty]
        private string openWithAppText = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_Text");

        [ObservableProperty]
        private string openWithAppToolTip = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWith_ToolTip");

        [ObservableProperty]
        private string? fileCountText;

        public TitleBar()
        {
            InitializeComponent();
            TitleBarRootContainer.SizeChanged += TitleBarRootContainer_SizeChanged;
        }

        public File File
        {
            get => (File)GetValue(FileProperty);
            set => SetValue(FileProperty, value);
        }

        public int FileIndex
        {
            get => (int)GetValue(FileIndexProperty);
            set => SetValue(FileIndexProperty, value);
        }

        public bool IsMultiSelection
        {
            get => (bool)GetValue(IsMultiSelectionProperty);
            set => SetValue(IsMultiSelectionProperty, value);
        }

        public int NumberOfFiles
        {
            get => (int)GetValue(NumberOfFilesProperty);
            set => SetValue(NumberOfFilesProperty, value);
        }

        private string? DefaultAppName { get; set; }

        private Window? MainWindow { get; set; }

        public void SetTitleBarToWindow(MainWindow mainWindow)
        {
            MainWindow = mainWindow;

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

        [RelayCommand]
        private async void LaunchDefaultAppButtonAsync()
        {
            StorageFile storageFile = await File.GetStorageFileAsync();
            LauncherOptions options = new ();

            if (string.IsNullOrEmpty(DefaultAppName))
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

        private void TitleBarRootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateDragRegion();
        }

        private void UpdateDragRegion()
        {
            if (MainWindow == null)
            {
                return;
            }

            var appWindow = MainWindow.GetAppWindow();
            if (AppWindowTitleBar.IsCustomizationSupported() && appWindow != null && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                var scale = MainWindow.GetMonitorScale();

                SystemRightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scale);
                SystemLeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scale);

                var dragRectsList = new List<RectInt32>();

                RectInt32 dragRectangleLeft;
                dragRectangleLeft.X = (int)(SystemLeftPaddingColumn.ActualWidth * scale);
                dragRectangleLeft.Y = 0;
                dragRectangleLeft.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                dragRectangleLeft.Width = (int)(DraggableColumn.ActualWidth * scale);

                RectInt32 dragRectangleRight;
                dragRectangleRight.X = (int)((SystemLeftPaddingColumn.ActualWidth + DraggableColumn.ActualWidth + LaunchAppButtonColumn.ActualWidth) * scale);
                dragRectangleRight.Y = 0;
                dragRectangleRight.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                dragRectangleRight.Width = (int)(AppRightPaddingColumn.ActualWidth * scale);

                dragRectsList.Add(dragRectangleLeft);
                dragRectsList.Add(dragRectangleRight);

                appWindow.TitleBar.SetDragRectangles(dragRectsList.ToArray());
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

        private void OnFileIndexPropertyChanged()
        {
            UpdateFileCountText();
        }

        private void UpdateFileCountText()
        {
            // Update file count
            if (NumberOfFiles > 1)
            {
                string fileCountTextFormat = ResourceLoader.GetForViewIndependentUse().GetString("AppTitle_FileCounts_Text");
                FileCountText = string.Format(fileCountTextFormat, FileIndex + 1, NumberOfFiles);
            }
        }

        private void UpdateDefaultAppToLaunch()
        {
            // Update the name of default app to launch
            DefaultAppName = DefaultAppHelper.TryGetDefaultAppName(File.Extension);

            string openWithAppTextFormat = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWithApp_Text");
            OpenWithAppText = string.Format(openWithAppTextFormat, DefaultAppName);

            string openWithAppToolTipFormat = ResourceLoader.GetForViewIndependentUse().GetString("LaunchAppButton_OpenWithApp_ToolTip");
            OpenWithAppToolTip = string.Format(openWithAppToolTipFormat, DefaultAppName);
        }
    }
}
