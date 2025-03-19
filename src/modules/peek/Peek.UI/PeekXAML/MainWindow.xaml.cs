// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Peek.Common.Constants;
using Peek.Common.Extensions;
using Peek.FilePreviewer.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Telemetry.Events;
using Windows.Foundation;
using WinUIEx;

namespace Peek.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly ThemeListener? themeListener;

        /// <summary>
        /// Whether the delete confirmation dialog is currently open. Used to ensure only one
        /// dialog is open at a time.
        /// </summary>
        private bool _isDeleteInProgress;

        public MainWindow()
        {
            InitializeComponent();
            this.Activated += PeekWindow_Activated;

            try
            {
                themeListener = new ThemeListener();
                themeListener.ThemeChanged += (_) => HandleThemeChange();
            }
            catch (Exception e)
            {
                Logger.LogError($"HandleThemeChange exception. Please install .NET 4.", e);
            }

            ViewModel = Application.Current.GetService<MainWindowViewModel>();

            TitleBarControl.SetTitleBarToWindow(this);
            ExtendsContentIntoTitleBar = true;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/Peek/Icon.ico");

            AppWindow.Closing += AppWindow_Closing;
        }

        private async void Content_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                e.Handled = true;
                await DeleteItem();
            }
        }

        private async Task DeleteItem()
        {
            if (ViewModel.CurrentItem == null || _isDeleteInProgress)
            {
                return;
            }

            try
            {
                _isDeleteInProgress = true;

                if (Application.Current.GetService<IUserSettings>().ConfirmFileDelete)
                {
                    if (await ShowDeleteConfirmationDialogAsync() == ContentDialogResult.Primary)
                    {
                        // Delete after asking for confirmation. Persist the "Don't warn again" choice if set.
                        ViewModel.DeleteItem(DeleteDontWarnCheckbox.IsChecked, this.GetWindowHandle());
                    }
                }
                else
                {
                    // Delete without confirmation.
                    ViewModel.DeleteItem(true, this.GetWindowHandle());
                }
            }
            finally
            {
                _isDeleteInProgress = false;
            }
        }

        private async Task<ContentDialogResult> ShowDeleteConfirmationDialogAsync()
        {
            DeleteDontWarnCheckbox.IsChecked = false;
            DeleteConfirmationDialog.XamlRoot = Content.XamlRoot;

            return await DeleteConfirmationDialog.ShowAsync();
        }

        /// <summary>
        /// Toggling the window visibility and querying files when necessary.
        /// </summary>
        public void Toggle(bool firstActivation, Windows.Win32.Foundation.HWND foregroundWindowHandle)
        {
            if (firstActivation)
            {
                Activate();
                Initialize(foregroundWindowHandle);
                return;
            }

            if (DeleteConfirmationDialog.Visibility == Visibility.Visible)
            {
                DeleteConfirmationDialog.Hide();
            }

            if (AppWindow.IsVisible)
            {
                if (IsNewSingleSelectedItem(foregroundWindowHandle))
                {
                    Initialize(foregroundWindowHandle);
                    Activate(); // Brings existing window into focus in case it was previously minimized
                }
                else
                {
                    Uninitialize();
                }
            }
            else
            {
                Initialize(foregroundWindowHandle);
            }
        }

        private void HandleThemeChange()
        {
            AppWindow appWindow = this.AppWindow;

            appWindow.TitleBar.ButtonForegroundColor = ThemeHelpers.GetAppTheme() == AppTheme.Light ? Colors.DarkSlateGray : Colors.White;
        }

        private void PeekWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                var userSettings = Application.Current.GetService<IUserSettings>();
                if (userSettings.CloseAfterLosingFocus)
                {
                    Uninitialize();
                }
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

        private void CloseInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            Uninitialize();
        }

        private void Initialize(Windows.Win32.Foundation.HWND foregroundWindowHandle)
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            ViewModel.Initialize(foregroundWindowHandle);
            ViewModel.ScalingFactor = this.GetMonitorScale();
            this.Content.KeyUp += Content_KeyUp;

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new OpenedEvent() { FileExtension = ViewModel.CurrentItem?.Extension ?? string.Empty, HotKeyToVisibleTimeMs = bootTime.ElapsedMilliseconds });
        }

        private void Uninitialize()
        {
            this.Restore();
            this.Hide();

            ViewModel.Uninitialize();
            ViewModel.ScalingFactor = 1;

            this.Content.KeyUp -= Content_KeyUp;
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

            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge --> WindowHeightContentPadding can be 0 (or close to that) if custom? [Jay]
            Size monitorMinContentSize = GetMonitorMinContentSize(monitorScale);
            Size monitorMaxContentSize = GetMonitorMaxContentSize(monitorSize, monitorScale);
            Size adjustedContentSize = scaledRequestedSize.Fit(monitorMaxContentSize, monitorMinContentSize);

            var titleBarHeight = TitleBarControl.ActualHeight;
            var desiredWindowWidth = adjustedContentSize.Width;
            var desiredWindowHeight = adjustedContentSize.Height + titleBarHeight;

            if (!TitleBarControl.Pinned)
            {
                this.CenterOnMonitor(foregroundWindowHandle, desiredWindowWidth, desiredWindowHeight);
            }

            this.Show();
            WindowHelpers.BringToForeground(this.GetWindowHandle());
        }

        private Size GetMonitorMaxContentSize(Size monitorSize, double scaling)
        {
            var titleBarHeight = TitleBarControl.ActualHeight;
            var maxContentWidth = monitorSize.Width * WindowConstants.MaxWindowToMonitorRatio;
            var maxContentHeight = (monitorSize.Height - titleBarHeight) * WindowConstants.MaxWindowToMonitorRatio;
            return new Size(maxContentWidth / scaling, maxContentHeight / scaling);
        }

        private Size GetMonitorMinContentSize(double scaling)
        {
            var titleBarHeight = TitleBarControl.ActualHeight;
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

        private bool IsNewSingleSelectedItem(Windows.Win32.Foundation.HWND foregroundWindowHandle)
        {
            try
            {
                var selectedItems = FileExplorerHelper.GetSelectedItems(foregroundWindowHandle);
                var selectedItemsCount = selectedItems?.GetCount() ?? 0;
                if (selectedItems == null || selectedItemsCount == 0 || selectedItemsCount > 1)
                {
                    return false;
                }

                var fileExplorerSelectedItemPath = selectedItems.GetItemAt(0).ToIFileSystemItem().Path;
                var currentItemPath = ViewModel.CurrentItem?.Path;
                return fileExplorerSelectedItemPath != null && currentItemPath != null && fileExplorerSelectedItemPath != currentItemPath;
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
    }
}
