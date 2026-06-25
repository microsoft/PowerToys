// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using WorkspacesEditor.Helpers;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();

            WorkspacesHeaderBlock.Text = ResourceLoaderInstance.ResourceLoader?.GetString("Workspaces") ?? "Workspaces";
            CreateWorkspaceText.Text = ResourceLoaderInstance.ResourceLoader?.GetString("CreateWorkspace") ?? "Create Workspace";
            SortByLabel.Text = ResourceLoaderInstance.ResourceLoader?.GetString("SortBy") ?? "Sort by";
            SearchTextBox.PlaceholderText = ResourceLoaderInstance.ResourceLoader?.GetString("SearchExplanation") ?? "Search for Workspaces or apps";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is MainViewModel vm)
            {
                ViewModel = vm;
                this.DataContext = vm;
                Bindings.Update();
            }
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.EnterSnapshotMode(false);
        }

        private void EditButtonClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.CloseAllPopups();
            if (sender is FrameworkElement element && element.DataContext is Project selectedProject)
            {
                ViewModel.EditProject(selectedProject);
            }
        }

        private async void DeleteButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Project selectedProject)
            {
                selectedProject.IsPopupVisible = false;

                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = ResourceLoaderInstance.ResourceLoader?.GetString("Are_You_Sure") ?? "Are you sure?",
                    Content = ResourceLoaderInstance.ResourceLoader?.GetString("Are_You_Sure_Description") ?? "Are you sure you want to delete this Workspace?",
                    PrimaryButtonText = ResourceLoaderInstance.ResourceLoader?.GetString("Delete") ?? "Remove",
                    CloseButtonText = ResourceLoaderInstance.ResourceLoader?.GetString("Cancel") ?? "Cancel",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot,
                };

                var result = await dialog.ShowAsync();
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    ViewModel.DeleteProject(selectedProject);
                }
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Project project)
            {
                _ = ViewModel.LaunchProjectAsync(project);
            }
        }
    }
}
