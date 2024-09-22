// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Interaction logic for SnapshotWindow.xaml
    /// </summary>
    public partial class StatusWindow : Window
    {
        private MainViewModel _mainViewModel;

        public StatusWindow(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.SetSnapshotWindow(this);
            this.DataContext = _mainViewModel;
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CancelLaunch();
            Close();
        }

        private void DismissButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }
    }
}
