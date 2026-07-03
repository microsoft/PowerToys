// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.Views
{
    /// <summary>
    /// Page hosting the workspace launch progress content.
    /// Displays a list of apps with their launch state (loading/success/failed).
    /// Hosted inside <see cref="StatusWindow"/> so the content can use x:Bind.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "WinUI Page does not support IDisposable; ViewModel is disposed by the hosting window on close.")]
    public sealed partial class StatusPage : Page
    {
        public MainViewModel ViewModel { get; }

        /// <summary>
        /// Raised when the user clicks Cancel or Dismiss and the hosting window should close.
        /// </summary>
        public event EventHandler CloseRequested;

        public StatusPage()
        {
            ViewModel = new MainViewModel();
            this.InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
