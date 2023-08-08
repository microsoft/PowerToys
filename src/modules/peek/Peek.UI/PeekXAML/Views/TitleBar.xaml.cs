// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Telemetry.Events;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using WinUIEx;

namespace Peek.UI.Views
{
    [INotifyPropertyChanged]
    public sealed partial class TitleBar : UserControl
    {
        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register(
                nameof(Item),
                typeof(IFileSystemItem),
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
        private string openWithAppText = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWith_Text");

        [ObservableProperty]
        private string openWithAppToolTip = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWith_ToolTip");

        [ObservableProperty]
        private string? fileCountText;

        [ObservableProperty]
        private string defaultAppName = string.Empty;

        [ObservableProperty]
        private bool pinned = false;

        private ColumnDefinition systemLeftPaddingColumn = new() { Width = new GridLength(0) };
        private ColumnDefinition draggableColumn = new() { Width = new GridLength(1, GridUnitType.Star) };
        private ColumnDefinition launchAppButtonColumn = new() { Width = GridLength.Auto };
        private ColumnDefinition appRightPaddingColumn = new() { Width = new GridLength(65) };
        private ColumnDefinition pinButtonColumn = new() { Width = new GridLength(40) };
        private ColumnDefinition systemRightPaddingColumn = new() { Width = new GridLength(0) };

        public TitleBar()
        {
            InitializeComponent();
            TitleBarRootContainer.SizeChanged += TitleBarRootContainer_SizeChanged;
        }

        public IFileSystemItem Item
        {
            get => (IFileSystemItem)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
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
                appWindow.SetIcon("Assets/Peek/Icon.ico");
            }
        }

        public Visibility IsLaunchDefaultAppButtonVisible(string appName)
        {
            return string.IsNullOrEmpty(appName) ? Visibility.Collapsed : Visibility.Visible;
        }

        [RelayCommand]
        private async void LaunchDefaultAppButtonAsync()
        {
            if (Item is not FileItem fileItem)
            {
                return;
            }

            StorageFile? storageFile = await fileItem.GetStorageFileAsync();
            LauncherOptions options = new();

            PowerToysTelemetry.Log.WriteEvent(new OpenWithEvent() { App = DefaultAppName ?? string.Empty });

            // StorageFile objects can't represent files that are ".lnk", ".url", or ".wsh" file types.
            // https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile?view=winrt-22621
            if (storageFile == null)
            {
                options.DisplayApplicationPicker = true;
                await Launcher.LaunchUriAsync(new Uri(Item.Path), options);
            }
            else if (string.IsNullOrEmpty(DefaultAppName))
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

        public string PinGlyph(bool pinned)
        {
            return pinned ? "\xE841" : "\xE77A";
        }

        public string PinToolTip(bool pinned)
        {
            return pinned ? ResourceLoaderInstance.ResourceLoader.GetString("UnpinButton_ToolTip") : ResourceLoaderInstance.ResourceLoader.GetString("PinButton_ToolTip");
        }

        [RelayCommand]
        private void Pin()
        {
            Pinned = !Pinned;
        }

        public FlowDirection TitleBarFlowDirection
        {
            get
            {
                var direction = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft ?
                    FlowDirection.RightToLeft :
                    FlowDirection.LeftToRight;
                SetupGridColumnDefinitions(direction);
                return direction;
            }
        }

        private void SetupGridColumnDefinitions(FlowDirection direction)
        {
            TitleBarRootContainer.ColumnDefinitions.Clear();

            if (direction == FlowDirection.LeftToRight)
            {
                TitleBarRootContainer.ColumnDefinitions.Add(systemLeftPaddingColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(draggableColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(launchAppButtonColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(appRightPaddingColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(pinButtonColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(systemRightPaddingColumn);

                Grid.SetColumn(AppIconAndFileTitleContainer, 1);
                FileCountAndNameContainer.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetColumn(LaunchAppButton, 2);
                Grid.SetColumn(PinButton, 4);
            }
            else
            {
                TitleBarRootContainer.ColumnDefinitions.Add(systemRightPaddingColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(pinButtonColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(appRightPaddingColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(launchAppButtonColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(draggableColumn);
                TitleBarRootContainer.ColumnDefinitions.Add(systemLeftPaddingColumn);

                Grid.SetColumn(AppIconAndFileTitleContainer, 4);
                FileCountAndNameContainer.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(LaunchAppButton, 3);
                LaunchAppButton.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetColumn(PinButton, 1);
                PinButton.HorizontalAlignment = HorizontalAlignment.Right;
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

            var appWindow = MainWindow.AppWindow;
            if (AppWindowTitleBar.IsCustomizationSupported() && appWindow != null && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                var scale = MainWindow.GetMonitorScale();

                systemRightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scale);
                systemLeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scale);

                var dragRectsList = new List<RectInt32>();
                RectInt32 dragRectangleLeft;
                RectInt32 dragRectangleRight;

                if (TitleBarFlowDirection == FlowDirection.LeftToRight)
                {
                    dragRectangleLeft.X = (int)(systemLeftPaddingColumn.ActualWidth * scale);
                    dragRectangleLeft.Y = 0;
                    dragRectangleLeft.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                    dragRectangleLeft.Width = (int)(draggableColumn.ActualWidth * scale);

                    dragRectangleRight.X = (int)((systemLeftPaddingColumn.ActualWidth + draggableColumn.ActualWidth + launchAppButtonColumn.ActualWidth) * scale);
                    dragRectangleRight.Y = 0;
                    dragRectangleRight.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                    dragRectangleRight.Width = (int)(appRightPaddingColumn.ActualWidth * scale);
                }
                else
                {
                    dragRectangleRight.X = (int)(pinButtonColumn.ActualWidth * scale);
                    dragRectangleRight.Y = 0;
                    dragRectangleRight.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                    dragRectangleRight.Width = (int)(appRightPaddingColumn.ActualWidth * scale);

                    dragRectangleLeft.X = (int)((pinButtonColumn.ActualWidth + appRightPaddingColumn.ActualWidth + launchAppButtonColumn.ActualWidth) * scale);
                    dragRectangleLeft.Y = 0;
                    dragRectangleLeft.Height = (int)(TitleBarRootContainer.ActualHeight * scale);
                    dragRectangleLeft.Width = (int)(draggableColumn.ActualWidth * scale);
                }

                dragRectsList.Add(dragRectangleLeft);
                dragRectsList.Add(dragRectangleRight);

                appWindow.TitleBar.SetDragRectangles(dragRectsList.ToArray());
            }
        }

        private void UpdateTitleBarCustomization(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow appWindow = mainWindow.AppWindow;
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                if (ThemeHelpers.GetAppTheme() == AppTheme.Light)
                {
                    appWindow.TitleBar.ButtonForegroundColor = Colors.DarkSlateGray;
                }
                else
                {
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                }

                mainWindow.SetTitleBar(this);
            }
        }

        private void OnFilePropertyChanged()
        {
            if (Item == null)
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
                string fileCountTextFormat = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle_FileCounts_Text");
                FileCountText = string.Format(CultureInfo.InvariantCulture, fileCountTextFormat, FileIndex + 1, NumberOfFiles);
            }
        }

        private void UpdateDefaultAppToLaunch()
        {
            // Update the name of default app to launch
            DefaultAppName = DefaultAppHelper.TryGetDefaultAppName(Item.Extension);

            string openWithAppTextFormat = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWithApp_Text");
            OpenWithAppText = string.Format(CultureInfo.InvariantCulture, openWithAppTextFormat, DefaultAppName);

            string openWithAppToolTipFormat = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWithApp_ToolTip");
            OpenWithAppToolTip = string.Format(CultureInfo.InvariantCulture, openWithAppToolTipFormat, DefaultAppName);
        }
    }
}
