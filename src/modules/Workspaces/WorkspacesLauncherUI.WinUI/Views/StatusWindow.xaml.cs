// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Status window showing workspace launch progress.
    /// Displays a list of apps with their launch state (loading/success/failed).
    /// </summary>
    public sealed partial class StatusWindow : Window
    {
        public StatusWindow()
        {
            this.InitializeComponent();

            // Configure window size and behavior to match WPF original (360x340, non-resizable, topmost)
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(360, 340));
            appWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            App.SendIPCMessage("cancel");
            Close();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
