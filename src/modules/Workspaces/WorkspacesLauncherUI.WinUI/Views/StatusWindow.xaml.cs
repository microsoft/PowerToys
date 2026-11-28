// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using WorkspacesLauncherUI.ViewModels;
using WorkspacesLauncherUI.Views;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Status window showing workspace launch progress.
    /// Hosts <see cref="StatusPage"/> which owns the ViewModel and renders the app list.
    /// </summary>
    public sealed partial class StatusWindow : Window
    {
        public StatusWindow(MainViewModel mainViewModel)
        {
            this.InitializeComponent();
            StatusPageView.Initialize(mainViewModel);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Configure window size and behavior
            var appWindow = this.AppWindow;
            var dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
            var scale = (dpi == 0 ? 96 : dpi) / 96.0;
            var width = (int)(360 * scale);
            var height = (int)(360 * scale);
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                width = Math.Min(width, displayArea.WorkArea.Width);
                height = Math.Min(height, displayArea.WorkArea.Height);
            }

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            appWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            // Set title from resources
            string title;
            try
            {
                title = ResourceLoaderInstance.ResourceLoader?.GetString("LauncherWindowTitle") ?? "Workspaces";
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load window title resource: " + ex.Message);
                title = "Workspaces";
            }

            this.Title = title;
            AppTitleBar.Title = title;

            StatusPageView.CloseRequested += StatusPage_CloseRequested;

            this.Closed += Window_Closed;

            // Center on screen
            CenterOnScreen(appWindow);
        }

        private static void CenterOnScreen(AppWindow appWindow)
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                int centerX = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
                int centerY = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - appWindow.Size.Height) / 2);
                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }
        }

        private void StatusPage_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            StatusPageView.ViewModel?.Dispose();
            (Application.Current as App)?.OnMainWindowClosed();
        }

        [LibraryImport("user32.dll")]
        private static partial uint GetDpiForWindow(IntPtr window);
    }
}
