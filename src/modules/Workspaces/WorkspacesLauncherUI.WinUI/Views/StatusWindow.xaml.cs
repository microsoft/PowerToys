// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Status window showing workspace launch progress.
    /// Displays a list of apps with their launch state (loading/success/failed).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "WinUI Window does not support IDisposable; ViewModel is disposed on window close.")]
    public sealed partial class StatusWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public StatusWindow()
        {
            _viewModel = new MainViewModel();
            this.InitializeComponent();

            // WinUI Window is not a DependencyObject — set DataContext on root content
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }

            this.Closed += Window_Closed;

            // Configure window size and behavior to match WPF original (360x340, non-resizable, topmost)
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(360, 340));
            appWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

            // Set title from resources
            try
            {
                this.Title = ResourceLoaderInstance.ResourceLoader?.GetString("LauncherWindowTitle") ?? "Workspaces";
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to load window title resource: " + ex.Message);
                this.Title = "Workspaces";
            }

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            // Center on screen
            CenterOnScreen(appWindow);
        }

        private static void CenterOnScreen(AppWindow appWindow)
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                int centerX = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                int centerY = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelLaunch();
            Close();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _viewModel?.Dispose();
            (Application.Current as System.IDisposable)?.Dispose();
        }
    }
}
