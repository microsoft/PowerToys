// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows;

using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for SnapshotWindow.xaml
    /// </summary>
    public partial class SnapshotWindow : Window
    {
        private readonly Func<Task> _capture;
        private readonly Action _cancelSnapshot;
        private bool _capturing;

        public SnapshotWindow(MainViewModel mainViewModel)
            : this(mainViewModel.SnapWorkspaceAsync, mainViewModel.CancelSnapshot)
        {
        }

        internal SnapshotWindow(Func<Task> capture, Action cancelSnapshot)
        {
            _capture = capture;
            _cancelSnapshot = cancelSnapshot;
            InitializeComponent();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SnapshotButtonClicked(object sender, RoutedEventArgs e)
        {
            if (_capturing)
            {
                return;
            }

            _capturing = true;
            Topmost = false;
            SnapshotButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            SnapshotDescriptionText.Text = Properties.Resources.ResourceManager.GetString("ProtectedStorageCapturing", Properties.Resources.Culture);
            CaptureProgress.Visibility = Visibility.Visible;
            try
            {
                await _capture();
            }
            finally
            {
                _capturing = false;
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_capturing)
            {
                e.Cancel = true;
                return;
            }

            _cancelSnapshot();
        }
    }
}
