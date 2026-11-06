// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ManagedCommon;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private async void RetryCleanup_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                await WorkspacesFailureViewModel.ExecuteAsync(
                    async () =>
                    {
                        _mainViewModel.IsSourceCleanupPending = !await App.WorkspacesEditorIO.RetrySourceCleanupAsync();
                    },
                    allowRetry: false);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private MainViewModel _mainViewModel;

        public MainPage(MainViewModel mainViewModel)
        {
            InitializeComponent();
            _mainViewModel = mainViewModel;
            this.DataContext = _mainViewModel;
        }

        private /*async*/ void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.EnterSnapshotMode(false);
        }

        private void EditButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CloseAllPopups();
            Button button = sender as Button;
            Project selectedProject = button.DataContext as Project;
            _mainViewModel.EditProject(selectedProject);
        }

        private async void DeleteButtonClicked(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Button button = sender as Button;
            Project selectedProject = button.DataContext as Project;
            selectedProject.IsPopupVisible = false;

            IsEnabled = false;
            try
            {
                await _mainViewModel.DeleteProjectAsync(selectedProject);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CloseAllPopups();
            e.Handled = true;
            Button button = sender as Button;
            Project project = button.DataContext as Project;
            project.IsPopupVisible = true;
        }

        private void PopupClosed(object sender, object e)
        {
            if (sender is Popup p && p.DataContext is Project proj)
            {
                proj.IsPopupVisible = false;
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Button button = sender as Button;
            Project project = button.DataContext as Project;
            _mainViewModel.LaunchProject(project);
        }
    }
}
