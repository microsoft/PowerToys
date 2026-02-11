// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class CmdNotFoundPage : NavigablePage
    {
        private CmdNotFoundViewModel ViewModel { get; set; }

        public CmdNotFoundPage()
        {
            ViewModel = new CmdNotFoundViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }

        private void UninstallButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.UninstallModuleEventHandler?.Execute(null);
        }

        private void InstallButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.InstallModuleEventHandler?.Execute(null);
        }

        private void CheckRequirementsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.CheckRequirementsEventHandler?.Execute(null);
        }

        private void InstallPowerShell7Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.InstallPowerShell7EventHandler?.Execute(null);
        }

        private void InstallWinGetButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.InstallWinGetClientModuleEventHandler?.Execute(null);
        }
    }
}
