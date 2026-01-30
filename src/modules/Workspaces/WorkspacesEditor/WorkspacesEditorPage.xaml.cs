// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for ProjectEditor.xaml
    /// </summary>
    public partial class ProjectEditor : Page
    {
        private const double ScrollSpeed = 15;
        private MainViewModel _mainViewModel;

        public ProjectEditor(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            InitializeComponent();
        }

        private void SaveButtonClicked(object sender, RoutedEventArgs e)
        {
            Project projectToSave = this.DataContext as Project;
            projectToSave.CloseExpanders();

            if (_mainViewModel.Workspaces.Any(x => x.Id == projectToSave.Id))
            {
                _mainViewModel.SaveProject(projectToSave);
            }
            else
            {
                _mainViewModel.AddNewProject(projectToSave);
            }

            _mainViewModel.SwitchToMainView();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            // delete the temp file created by the snapshot tool
            TempProjectData.DeleteTempFile();

            _mainViewModel.SwitchToMainView();
        }

        private void DeleteButtonClicked(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Models.Application app = button.DataContext as Models.Application;
            app.SwitchDeletion();
        }

        private void EditNameTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Project project = this.DataContext as Project;
                TextBox textBox = sender as TextBox;
                project.Name = textBox.Text;
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Project project = this.DataContext as Project;
                _mainViewModel.CancelProjectName(project);
            }
        }

        private void EditNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _mainViewModel.SaveProjectName(DataContext as Project);
        }

        private void AppBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            Models.Application app = border.DataContext as Models.Application;
            app.IsHighlighted = true;
            Project project = app.Parent;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void AppBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;
            Models.Application app = border.DataContext as Models.Application;
            if (app == null)
            {
                return;
            }

            app.IsHighlighted = false;
            Project project = app.Parent;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void EditNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Project project = this.DataContext as Project;
            TextBox textBox = sender as TextBox;
            project.Name = textBox.Text;
            project.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Project.CanBeSaved)));
        }

        private void LeftTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            Models.Application application = textBox.DataContext as Models.Application;
            int newPos;
            if (!int.TryParse(textBox.Text, out newPos))
            {
                newPos = 0;
            }

            application.Position = new Models.Application.WindowPosition() { X = newPos, Y = application.Position.Y, Width = application.Position.Width, Height = application.Position.Height };
            Project project = application.Parent;
            project.IsPositionChangedManually = true;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void TopTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            Models.Application application = textBox.DataContext as Models.Application;
            int newPos;
            if (!int.TryParse(textBox.Text, out newPos))
            {
                newPos = 0;
            }

            application.Position = new Models.Application.WindowPosition() { X = application.Position.X, Y = newPos, Width = application.Position.Width, Height = application.Position.Height };
            Project project = application.Parent;
            project.IsPositionChangedManually = true;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            Models.Application application = textBox.DataContext as Models.Application;
            int newPos;
            if (!int.TryParse(textBox.Text, out newPos))
            {
                newPos = 0;
            }

            application.Position = new Models.Application.WindowPosition() { X = application.Position.X, Y = application.Position.Y, Width = newPos, Height = application.Position.Height };
            Project project = application.Parent;
            project.IsPositionChangedManually = true;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            Models.Application application = textBox.DataContext as Models.Application;
            int newPos;
            if (!int.TryParse(textBox.Text, out newPos))
            {
                newPos = 0;
            }

            application.Position = new Models.Application.WindowPosition() { X = application.Position.X, Y = application.Position.Y, Width = application.Position.Width, Height = newPos };
            Project project = application.Parent;
            project.IsPositionChangedManually = true;
            project.Initialize(App.ThemeManager.GetCurrentTheme());
        }

        private void CommandLineTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            Models.Application application = textBox.DataContext as Models.Application;
            application.CommandLineTextChanged(textBox.Text);
        }

        private void LaunchEditButtonClicked(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Project project = button.DataContext as Project;
            _mainViewModel.LaunchAndEdit(project);
        }

        private void RevertButtonClicked(object sender, RoutedEventArgs e)
        {
            _mainViewModel.RevertLaunch();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            double scrollAmount = Math.Sign(e.Delta) * ScrollSpeed;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
            e.Handled = true;
        }
    }
}
