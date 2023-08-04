// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using interop;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Peek.Common.Constants;
using Peek.FilePreviewer.Models;
using Peek.UI.Extensions;
using Peek.UI.Helpers;
using Peek.UI.Native;
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

        private ThemeListener? themeListener;
        private bool activated;

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

            ViewModel = App.GetService<MainWindowViewModel>();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            TitleBarControl.SetTitleBarToWindow(this);

            AppWindow.Closing += AppWindow_Closing;
        }

        private void HandleThemeChange()
        {
            AppWindow appWindow = this.AppWindow;

            if (ThemeHelpers.GetAppTheme() == AppTheme.Light)
            {
                appWindow.TitleBar.ButtonForegroundColor = Colors.DarkSlateGray;
            }
            else
            {
                appWindow.TitleBar.ButtonForegroundColor = Colors.White;
            }
        }

        private void PeekWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                var userSettings = App.GetService<IUserSettings>();
                if (userSettings.CloseAfterLosingFocus)
                {
                    Uninitialize();
                }
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

            if (AppWindow.IsVisible)
            {
                if (IsNewSingleSelectedItem())
                {
                    Initialize();
                }
                else
                {
                    Uninitialize();
                }
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
            Size scaledRequestedSize = new Size(requestedSize.Width / contentScale, requestedSize.Height / contentScale);

            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge
            Size monitorMinContentSize = GetMonitorMinContentSize(monitorScale);
            Size monitorMaxContentSize = GetMonitorMaxContentSize(monitorSize, monitorScale);
            Size adjustedContentSize = scaledRequestedSize.Fit(monitorMaxContentSize, monitorMinContentSize);

            var titleBarHeight = TitleBarControl.ActualHeight;
            var desiredWindowHeight = adjustedContentSize.Height + titleBarHeight + WindowConstants.WindowWidthContentPadding;
            var desiredWindowWidth = adjustedContentSize.Width + WindowConstants.WindowHeightContentPadding;

            if (!TitleBarControl.Pinned)
            {
                this.CenterOnMonitor(foregroundWindowHandle, desiredWindowWidth, desiredWindowHeight);
            }

            this.Show();
            this.BringToForeground();
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
    }
}
