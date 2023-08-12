// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using interop;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Peek.Common.Constants;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Native;
using Peek.UI.Telemetry.Events;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using WinUIEx;
using Logger = ManagedCommon.Logger;

namespace Peek.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    [INotifyPropertyChanged]
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly ThemeListener? themeListener;
        private bool activated;

        public MainWindow()
        {
            InitializeComponent();
            Title = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle");
            SetTitleBar(TitleBarRootContainer);
            TitleBarRootContainer.SizeChanged += TitleBarRootContainer_SizeChanged;

            try
            {
                themeListener = new ThemeListener();
                themeListener.ThemeChanged += (_) => HandleThemeChange();
            }
            catch (Exception e)
            {
                Logger.LogError($"HandleThemeChange exception. Please install .NET 4.", e);
            }

            ViewModel = App.GetService<MainWindowViewModel>();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            Activated += PeekWindow_Activated;
            AppWindow.Closing += AppWindow_Closing;
        }

        private void HandleThemeChange()
        {
            if (ThemeHelpers.GetAppTheme() == AppTheme.Light)
            {
                AppWindow.TitleBar.ButtonForegroundColor = Colors.DarkSlateGray;
            }
            else
            {
                AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            }
        }

        private void PeekWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                AppTitle_FileName.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
                var userSettings = App.GetService<IUserSettings>();
                if (userSettings.CloseAfterLosingFocus)
                {
                    Uninitialize();
                }
            }
            else
            {
                AppTitle_FileName.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
        }

        /// <summary>
        /// Handle Peek hotkey, by toggling the window visibility and querying files when necessary.
        /// </summary>
        private void OnPeekHotkey()
        {
            // First Peek activation
            if (!activated)
            {
                Activate();
                Initialize();
                activated = true;
                return;
            }

            if (!IsNewSingleSelectedItem())
            {
                Uninitialize();
            }
            else
            {
                Initialize();
            }
        }

        private void PreviousNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptPreviousNavigation();
        }

        private void NextNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptNextNavigation();
        }

        private void EscKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            Uninitialize();
        }

        private void Initialize()
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            ViewModel.Initialize();
            ViewModel.ScalingFactor = this.GetMonitorScale();

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new OpenedEvent() { FileExtension = ViewModel.CurrentItem?.Extension ?? string.Empty, HotKeyToVisibleTimeMs = bootTime.ElapsedMilliseconds });
        }

        private void Uninitialize()
        {
            this.Restore();
            this.Hide();

            ViewModel.Uninitialize();
            ViewModel.ScalingFactor = 1;
        }

        /// <summary>
        /// Handle FilePreviewerSizeChanged event to adjust window size and position accordingly.
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">PreviewSizeChangedArgs</param>
        private void FilePreviewer_PreviewSizeChanged(object sender, PreviewSizeChangedArgs e)
        {
            var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();
            var monitorSize = foregroundWindowHandle.GetMonitorSize();
            var monitorScale = foregroundWindowHandle.GetMonitorScale();

            // If no size is requested, try to fit to the monitor size.
            Size requestedSize = e.PreviewSize.MonitorSize ?? monitorSize;
            var contentScale = e.PreviewSize.UseEffectivePixels ? 1 : monitorScale;
            Size scaledRequestedSize = new(requestedSize.Width / contentScale, requestedSize.Height / contentScale);

            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge
            Size monitorMinContentSize = GetMonitorMinContentSize(monitorScale);
            Size monitorMaxContentSize = GetMonitorMaxContentSize(monitorSize, monitorScale);
            Size adjustedContentSize = scaledRequestedSize.Fit(monitorMaxContentSize, monitorMinContentSize);

            var titleBarHeight = TitleBarRootContainer.ActualHeight;
            var desiredWindowHeight = adjustedContentSize.Height + titleBarHeight + WindowConstants.WindowHeightContentPadding;
            var desiredWindowWidth = adjustedContentSize.Width + WindowConstants.WindowWidthContentPadding;

            if (!Pinned)
            {
                this.CenterOnMonitor(foregroundWindowHandle, desiredWindowWidth, desiredWindowHeight);
            }

            this.Show();
            this.BringToForeground();
        }

        private Size GetMonitorMaxContentSize(Size monitorSize, double scaling)
        {
            var titleBarHeight = TitleBarRootContainer.ActualHeight;
            var maxContentWidth = monitorSize.Width * WindowConstants.MaxWindowToMonitorRatio;
            var maxContentHeight = (monitorSize.Height - titleBarHeight) * WindowConstants.MaxWindowToMonitorRatio;
            return new Size(maxContentWidth / scaling, maxContentHeight / scaling);
        }

        private Size GetMonitorMinContentSize(double scaling)
        {
            var titleBarHeight = TitleBarRootContainer.ActualHeight;
            var minContentWidth = WindowConstants.MinWindowWidth;
            var minContentHeight = WindowConstants.MinWindowHeight - titleBarHeight;
            return new Size(minContentWidth / scaling, minContentHeight / scaling);
        }

        /// <summary>
        /// Handle AppWindow closing to prevent app termination on close.
        /// </summary>
        /// <param name="sender">AppWindow</param>
        /// <param name="args">AppWindowClosingEventArgs</param>
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            PowerToysTelemetry.Log.WriteEvent(new ClosedEvent());
            Uninitialize();
        }

        private bool IsNewSingleSelectedItem()
        {
            try
            {
                var foregroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();

                var selectedItems = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
                var selectedItemsCount = selectedItems?.GetCount() ?? 0;
                if (selectedItems == null || selectedItemsCount == 0 || selectedItemsCount > 1)
                {
                    return false;
                }

                var fileExplorerSelectedItemPath = selectedItems.GetItemAt(0).ToIFileSystemItem().Path;
                var currentItemPath = ViewModel.CurrentItem?.Path;
                if (fileExplorerSelectedItemPath == null || currentItemPath == null || fileExplorerSelectedItemPath == currentItemPath)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }

            return false;
        }

        public void Dispose()
        {
            themeListener?.Dispose();
        }

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

        public IFileSystemItem Item { get; set; }

        public int FileIndex { get; set; }

        public bool IsMultiSelection { get; set; }

        public int NumberOfFiles { get; set; }

        public void SetTitleBarToWindow(MainWindow mainWindow)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                // AppWindow appWindow = mainWindow.AppWindow;
                mainWindow.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                mainWindow.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                mainWindow.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                mainWindow.AppWindow.SetIcon("Assets/Peek/Icon.ico");

                // appWindow.TitleBar.ButtonForegroundColor = ThemeHelpers.GetAppTheme() == AppTheme.Light ? Colors.DarkSlateGray : Colors.White;
                mainWindow.SetTitleBar(TitleBarRootContainer);
            }
            else
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ThemeHelpers.SetImmersiveDarkMode(hWnd, ThemeHelpers.GetAppTheme() == AppTheme.Dark);
                TitleBarRootContainer.Visibility = Visibility.Collapsed;

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
            AppWindow appWindow = AppWindow;
            if (AppWindowTitleBar.IsCustomizationSupported() && appWindow != null && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scale = System.Windows.PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow).CompositionTarget.TransformToDevice.M11;

                var dragRectsList = new List<RectInt32>();

                RectInt32 dragRectangleLeft;
                dragRectangleLeft.X = (int)(appWindow.TitleBar.LeftInset * scale);
                dragRectangleLeft.Y = 1;
                dragRectangleLeft.Width = (int)((IconAndTitleColumn.ActualWidth + LeftDragColumn.ActualWidth) * scale);
                dragRectangleLeft.Height = (int)(TitleBarRootContainer.ActualHeight * scale);

                RectInt32 dragRectangleRight;
                dragRectangleRight.X = (int)((appWindow.TitleBar.LeftInset + IconAndTitleColumn.ActualWidth + LeftDragColumn.ActualWidth + ButtonsColumn.ActualWidth) * scale);
                dragRectangleRight.Y = 1;
                dragRectangleRight.Width = (int)(RightDragColumn.ActualWidth * scale);
                dragRectangleRight.Height = (int)(TitleBarRootContainer.ActualHeight * scale);

                dragRectsList.Add(dragRectangleLeft);
                dragRectsList.Add(dragRectangleRight);

                appWindow.TitleBar.SetDragRectangles(dragRectsList.ToArray());
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
