// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

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
               new PropertyMetadata(null, (d, e) => ((TitleBar)d).OnNumberOfFilesPropertyChanged()));

        [ObservableProperty]
        private string openWithAppText = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWith_Text");

        [ObservableProperty]
        private string openWithAppToolTip = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWith_ToolTip");

        [ObservableProperty]
        private string? fileCountText;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string defaultAppName = string.Empty;

        [ObservableProperty]
        private bool pinned = false;

        public TitleBar()
        {
            InitializeComponent();
            TitleBarRootContainer.SizeChanged += TitleBarRootContainer_SizeChanged;

            LaunchAppButton.RegisterPropertyChangedCallback(VisibilityProperty, LaunchAppButtonVisibilityChangedCallback);
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

                // Ensure the drag region of the title bar is updated on first Peek activation
                UpdateDragRegion();
            }
            else
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ThemeHelpers.SetImmersiveDarkMode(hWnd, ThemeHelpers.GetAppTheme() == AppTheme.Dark);
                Visibility = Visibility.Collapsed;
            }
        }

        public Visibility IsLaunchDefaultAppButtonVisible(string appName)
        {
            return string.IsNullOrEmpty(appName) ? Visibility.Collapsed : Visibility.Visible;
        }

        [RelayCommand]
        private async Task LaunchDefaultAppButtonAsync()
        {
            if (Item is not FileItem fileItem)
            {
                return;
            }

            StorageFile? storageFile = await fileItem.GetStorageFileAsync();
            LauncherOptions options = new();

            PowerToysTelemetry.Log.WriteEvent(new OpenWithEvent() { App = DefaultAppName ?? string.Empty });

            // StorageFile objects can't represent files that are ".lnk", ".url", or ".wsh" file types.
            // https://learn.microsoft.com/uwp/api/windows.storage.storagefile?view=winrt-22621
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
            return pinned ? "\xE77A" : "\xE718";
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

                SystemLeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scale);
                SystemRightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scale);

                var dragRectsList = new List<RectInt32>();
                RectInt32 dragRectangleLeft;
                RectInt32 dragRectangleRight;

                dragRectangleLeft.X = (int)(SystemLeftPaddingColumn.ActualWidth * scale);
                dragRectangleLeft.Y = 0;
                dragRectangleLeft.Width = (int)(DraggableColumn.ActualWidth * scale);
                dragRectangleLeft.Height = (int)(TitleBarRootContainer.ActualHeight * scale);

                dragRectangleRight.X = (int)((SystemLeftPaddingColumn.ActualWidth + DraggableColumn.ActualWidth + LaunchAppButtonColumn.ActualWidth) * scale);
                dragRectangleRight.Y = 0;
                dragRectangleRight.Width = (int)(AppRightPaddingColumn.ActualWidth * scale);
                dragRectangleRight.Height = (int)(TitleBarRootContainer.ActualHeight * scale);

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
                mainWindow.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonForegroundColor = ThemeHelpers.GetAppTheme() == AppTheme.Light ? Colors.DarkSlateGray : Colors.White;

                mainWindow.SetTitleBar(this);
            }
        }

        private void OnFilePropertyChanged()
        {
            UpdateFileCountText();
            UpdateFilename();
            UpdateDefaultAppToLaunch();
        }

        private void UpdateFilename()
        {
            FileName = Item?.Name ?? string.Empty;
        }

        private void OnFileIndexPropertyChanged()
        {
            UpdateFileCountText();
        }

        private void OnNumberOfFilesPropertyChanged()
        {
            UpdateFileCountText();
        }

        /// <summary>
        /// Respond to a change in the current file being previewed or the number of files available.
        /// </summary>
        private void UpdateFileCountText()
        {
            if (NumberOfFiles >= 1)
            {
                string fileCountTextFormat = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle_FileCounts_Text");
                FileCountText = string.Format(CultureInfo.InvariantCulture, fileCountTextFormat, FileIndex + 1, NumberOfFiles);
            }
            else
            {
                FileCountText = string.Empty;
            }
        }

        private void UpdateDefaultAppToLaunch()
        {
            if (Item is FileItem)
            {
                // Update the name of default app to launch
                DefaultAppName = DefaultAppHelper.TryGetDefaultAppName(Item.Extension);

                string openWithAppTextFormat = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWithApp_Text");
                OpenWithAppText = string.Format(CultureInfo.InvariantCulture, openWithAppTextFormat, DefaultAppName);

                string openWithAppToolTipFormat = ResourceLoaderInstance.ResourceLoader.GetString("LaunchAppButton_OpenWithApp_ToolTip");
                OpenWithAppToolTip = string.Format(CultureInfo.InvariantCulture, openWithAppToolTipFormat, DefaultAppName);
            }
            else
            {
                DefaultAppName = string.Empty;
                OpenWithAppText = string.Empty;
                OpenWithAppToolTip = string.Empty;
            }
        }

        /// <summary>
        /// Ensure the drag region of the title bar is updated when the visibility of the launch app button changes.
        /// </summary>
        private async void LaunchAppButtonVisibilityChangedCallback(DependencyObject sender, DependencyProperty dp)
        {
            // Ensure the ActualWidth is updated
            await Task.Delay(100);

            UpdateDragRegion();
        }
    }
}
