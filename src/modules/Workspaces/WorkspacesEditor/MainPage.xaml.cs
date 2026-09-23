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
        private async void ImportWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Workspaces (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true ||
                MessageBox.Show(Properties.Resources.ProtectedStorageReplaceConfirm, Properties.Resources.ProtectedStorageTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            IsEnabled = false;
            try
            {
                if (await ViewModels.WorkspacesFailureViewModel.ExecuteAsync(() => App.WorkspacesEditorIO.ImportAsync(dialog.FileName)))
                {
                    await ViewModels.WorkspacesFailureViewModel.ExecuteAsync(async () => await App.WorkspacesEditorIO.ParseWorkspacesAsync((ViewModels.MainViewModel)DataContext));
                }
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void ExportWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Workspaces (*.json)|*.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = true };
            if (dialog.ShowDialog() == true)
            {
                await ViewModels.WorkspacesFailureViewModel.ExecuteAsync(() => App.WorkspacesEditorIO.ExportAsync(dialog.FileName));
            }
        }

        private async void ReloadWorkspaces_Click(object sender, RoutedEventArgs e)
        {
            await ViewModels.WorkspacesFailureViewModel.ExecuteAsync(async () => await App.WorkspacesEditorIO.ParseWorkspacesAsync((ViewModels.MainViewModel)DataContext));
        }

        private async void RetryCleanup_Click(object sender, RoutedEventArgs e)
        {
            await ViewModels.WorkspacesFailureViewModel.ExecuteAsync(async () =>
            {
                if (!await App.WorkspacesEditorIO.RetrySourceCleanupAsync())
                {
                    throw new PowerToys.ProtectedStorage.ProtectedStorageException("CleanupPending");
                }
            });
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
