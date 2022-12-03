// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PeekUI.WASDK
{
    using System.Collections.Generic;
    using System.Linq;
    using interop;
    using Microsoft.UI.Windowing;
    using Peek.Common.Models;
    using Peek.FilePreviewer.Models;
    using PeekUI.WASDK.Helpers;
    using PeekUI.WASDK.Native;
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

        private void OnPeekHotkey()
        {
            if (AppWindow.IsVisible)
            {
                this.Hide();
                ViewModel.Files = new List<File>();
                ViewModel.CurrentFile = null;
            }
            else
            {
                var fileExplorerSelectedFiles = FileExplorerHelper.GetSelectedFileExplorerFiles();
                if (fileExplorerSelectedFiles.Count == 0)
                {
                    return;
                }

                ViewModel.Files = fileExplorerSelectedFiles;
                ViewModel.CurrentFile = fileExplorerSelectedFiles.First();
            }
        }

        private void FilePreviewer_PreviewSizeChanged(object sender, PreviewSizeChangedArgs e)
        {
            // TODO: Show window, center/resize it if necessary and bring to front.
            this.Show();
            this.CenterOnScreen(e.WindowSizeRequested.Width, e.WindowSizeRequested.Height);
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
        }
    }
}
