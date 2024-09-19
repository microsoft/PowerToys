// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for SnapshotWindow.xaml
    /// </summary>
    public partial class SnapshotWindow : Window
    {
        private MainViewModel _mainViewModel;

        public SnapshotWindow(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
            _mainViewModel.CancelSnapshot();
        }

        private void SnapshotButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
            _mainViewModel.SnapWorkspace();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _mainViewModel.CancelSnapshot();
        }
    }
}
