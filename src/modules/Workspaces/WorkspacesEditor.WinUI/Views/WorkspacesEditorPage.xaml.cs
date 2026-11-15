// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

using Windows.System;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.ViewModels;

using Application = WorkspacesEditor.Models.Application;
using Project = WorkspacesEditor.Models.Project;

namespace WorkspacesEditor.Views
{
    public sealed partial class WorkspacesEditorPage : Page
    {
        private MainViewModel _mainViewModel;

        public Project EditedProject { get; private set; }

        public WorkspacesEditorPage()
        {
            this.InitializeComponent();

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    TempProjectData.DeleteTempFile();
                    _mainViewModel?.SwitchToMainView();
                    e.Handled = true;
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is (MainViewModel vm, Project project))
            {
                _mainViewModel = vm;
                EditedProject = project;
                this.DataContext = project;
                Bindings.Update();

                // Set focus to the name field so Narrator announces the page context
                this.Loaded -= OnLoaded;
                this.Loaded += OnLoaded;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= OnLoaded;
            EditNameTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        private void SaveButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.SaveOrAddProject(EditedProject);
        }

        private void DeleteButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Application app)
            {
                app.SwitchDeletion();
            }
        }

        private async void DeleteWorkspaceButtonClicked(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is not Project project)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = ResourceLoaderInstance.ResourceLoader?.GetString("Are_You_Sure") ?? "Are you sure?",
                Content = ResourceLoaderInstance.ResourceLoader?.GetString("Are_You_Sure_Description") ?? "Are you sure you want to delete this Workspace?",
                PrimaryButtonText = ResourceLoaderInstance.ResourceLoader?.GetString("Delete") ?? "Remove",
                CloseButtonText = ResourceLoaderInstance.ResourceLoader?.GetString("Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _mainViewModel.DeleteProjectById(project.Id);
            }
        }

        private void EditNameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                if (this.DataContext is Project project && sender is TextBox textBox)
                {
                    project.Name = textBox.Text;
                }
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                if (this.DataContext is Project project)
                {
                    _mainViewModel.CancelProjectName(project);
                }
            }
        }

        private void EditNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _mainViewModel.SaveProjectName(DataContext as Project);
        }

        private void EditNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext is Project project && sender is TextBox textBox)
            {
                project.Name = textBox.Text;
            }
        }

        private void LeftTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is Application app)
            {
                app.UpdatePositionX(textBox.Text);
            }
        }

        private void TopTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is Application app)
            {
                app.UpdatePositionY(textBox.Text);
            }
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is Application app)
            {
                app.UpdatePositionWidth(textBox.Text);
            }
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is Application app)
            {
                app.UpdatePositionHeight(textBox.Text);
            }
        }

        private void CommandLineTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is Application app)
            {
                app.CommandLineTextChanged(textBox.Text);
            }
        }

        private void LaunchEditButtonClicked(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is Project project)
            {
                _ = _mainViewModel.LaunchAndEditAsync(project);
            }
        }

        private void RevertButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.RevertLaunch();
        }
    }
}
