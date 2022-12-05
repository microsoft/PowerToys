// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    using interop;
    using Microsoft.UI.Windowing;
    using Peek.FilePreviewer.Models;
    using Peek.UI.Extensions;
    using Peek.UI.Native;
    using Windows.Foundation;
    using WinUIEx;

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainWindowViewModel();

            NativeEventWaiter.WaitForEventLoop(Constants.ShowPeekEvent(), OnPeekHotkey);

            TitleBarControl.SetToWindow(this);

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
                this.Hide();
                ViewModel.ClearFileData();
            }
            else
            {
                ViewModel.InitializeFileData();
            }
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
            var maxContentSize = new Size(monitorSize.Width * 0.8, (monitorSize.Height - titleBarHeight) * 0.8);
            var minContentSize = new Size(500, 500 - titleBarHeight);

            var adjustedContentSize = requestedSize.Fit(maxContentSize, minContentSize);

            // TODO: Only re-center if window has not been resized by user (or use design-defined logic).
            // TODO: Investigate why portrait images do not perfectly fit edge-to-edge
            this.CenterOnScreen(adjustedContentSize.Width, adjustedContentSize.Height + titleBarHeight);
            this.Show();
            this.BringToFront();
        }

        /// <summary>
        /// Handle AppWindow closing to prevent app termination on close.
        /// </summary>
        /// <param name="sender">AppWindow</param>
        /// <param name="args">AppWindowClosingEventArgs</param>
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            this.Hide();
            ViewModel.ClearFileData();
        }
    }
}
