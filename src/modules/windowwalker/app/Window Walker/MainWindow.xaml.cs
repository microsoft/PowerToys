// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Deployment.Application;
using System.Windows;
using System.Windows.Input;

namespace WindowWalker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void UpdateDisplayedVersionNumber()
        {
            // Since displaying version number is not critical to functionality, we don't need to do anything if it fails.
            try
            {
                Version applicationVersion = new Version("0.0.0.0");
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    applicationVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion;
                }

                if (ApplicationDeployment.CurrentDeployment.UpdateLocation.Host.Contains("develop"))
                {
                    versionDisplay.Text = "(develop) " + applicationVersion.ToString();
                }
                else
                {
                    versionDisplay.Text = applicationVersion.ToString();
                }
            }
            catch
            {
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = new ViewModels.WindowWalkerViewModel(this);
            searchBox.Focus();

            UpdateDisplayedVersionNumber();

            Left = (SystemParameters.WorkArea.Width - ActualWidth) / 2.0;
            Top = (SystemParameters.WorkArea.Height - ActualHeight) / 2.0;

            HideWindow();
        }

        private void SearchBoxKeyUp(object sender, KeyEventArgs e)
        {
            var viewModel = (ViewModels.WindowWalkerViewModel)DataContext;

            if (e.Key == Key.Escape)
            {
                if (viewModel.WindowHideCommand.CanExecute(null))
                {
                    viewModel.WindowHideCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Down)
            {
                if (viewModel.WindowNavigateToNextResultCommand.CanExecute(null))
                {
                    viewModel.WindowNavigateToNextResultCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Up)
            {
                if (viewModel.WindowNavigateToPreviousResultCommand.CanExecute(null))
                {
                    viewModel.WindowNavigateToPreviousResultCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (viewModel.SwitchToSelectedWindowCommand.CanExecute(null))
                {
                    viewModel.SwitchToSelectedWindowCommand.Execute(null);
                }
            }
        }

        private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var viewModel = (ViewModels.WindowWalkerViewModel)DataContext;

            if (viewModel.SwitchToSelectedWindowCommand.CanExecute(null))
            {
                viewModel.SwitchToSelectedWindowCommand.Execute(null);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void HideWindow()
        {
            var viewModel = (ViewModels.WindowWalkerViewModel)DataContext;
            if (viewModel.WindowHideCommand.CanExecute(null))
            {
                viewModel.WindowHideCommand.Execute(null);
            }
        }
    }
}
