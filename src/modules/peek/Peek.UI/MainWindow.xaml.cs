// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using interop;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml.Input;
    using Peek.FilePreviewer.Models;
    using Peek.UI.Extensions;
    using Peek.UI.Native;
    using Windows.Foundation;
    using Windows.System;
    using Windows.UI.Core;
    using WinUIEx;

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private const double MaxWindowToMonitorRatio = 0.80;
        private const double MinWindowHeight = 500;
        private const double MinWindowWidth = 680;
        private const double WindowWidthContentPadding = 7;
        private const double WindowHeightContentPadding = 16;

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainWindowViewModel();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            TitleBarControl.SetTitleBarToWindow(this);

            AppWindow.Closing += AppWindow_Closing;
        }

        public MainWindowViewModel ViewModel { get; }

        /// <summary>
        /// Handle Peek hotkey, by toggling the window visibility and querying files when necessary.
        /// </summary>
        private void OnPeekHotkey()
        {
            if (AppWindow.IsVisible)
            {
                Uninitialize();
            }
            else
            {
                Initialize();
            }
        }

        private void LeftNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptLeftNavigation();
        }

        private void RightNavigationInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ViewModel.AttemptRightNavigation();
        }

        private void Initialize()
        {
            ViewModel.FolderItemsQuery.Start();
        }

        private void Uninitialize()
        {
            this.Restore();
            this.Hide();

            // TODO: move into general ViewModel method when needed
            ViewModel.FolderItemsQuery.Clear();
        }

        /// <summary>
        /// Handle FilePreviewerSizeChanged event to adjust window size and position accordingly.
        /// </summary>
        /// <param name="sender">object</param>
        /// <param name="e">PreviewSizeChangedArgs</param>
        private void FilePreviewer_PreviewSizeChanged(object sender, PreviewSizeChangedArgs e)
        {
            var requestedSize = e.WindowSizeRequested;
            var monitorSize = this.GetMonitorSize();

            // TODO: Use design-defined rules for adjusted window size
            var titleBarHeight = TitleBarControl.ActualHeight;
            var maxContentSize = new Size(monitorSize.Width * MaxWindowToMonitorRatio, (monitorSize.Height - titleBarHeight) * MaxWindowToMonitorRatio);
            var minContentSize = new Size(MinWindowWidth, MinWindowHeight - titleBarHeight);

            var adjustedContentSize = requestedSize.Fit(maxContentSize, minContentSize);

            // TODO: Only re-center if window has not been resized by user (or use design-defined logic).
            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge
            var monitorScale = this.GetMonitorScale();
            var scaledWindowWidth = adjustedContentSize.Width / monitorScale;
            var scaledWindowHeight = adjustedContentSize.Height / monitorScale;

            this.CenterOnScreen(scaledWindowWidth + WindowHeightContentPadding, scaledWindowHeight + titleBarHeight + WindowWidthContentPadding);
            this.Show();
            this.BringToForeground();
        }

        /// <summary>
        /// Handle AppWindow closing to prevent app termination on close.
        /// </summary>
        /// <param name="sender">AppWindow</param>
        /// <param name="args">AppWindowClosingEventArgs</param>
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            Uninitialize();
        }
    }
}
