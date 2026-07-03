// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.UI.Xaml;

using WinUIEx;
using WorkspacesLauncherUI.Views;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Status window showing workspace launch progress.
    /// Hosts <see cref="StatusPage"/> which owns the ViewModel and renders the app list.
    /// </summary>
    public sealed partial class StatusWindow : WindowEx
    {
        public StatusWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

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

            this.CenterOnScreen();
        }

        private void StatusPage_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            StatusPageView.ViewModel?.Dispose();
            (Application.Current as IDisposable)?.Dispose();
        }
    }
}
