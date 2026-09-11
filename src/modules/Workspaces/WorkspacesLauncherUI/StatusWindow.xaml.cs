// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows;
using System.Windows.Markup;

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
            Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
            FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        private async void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            DismissButton.IsEnabled = false;
            await _mainViewModel.CancelLaunchAsync();
            Close();
        }

        private void DismissButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _mainViewModel.Dispose();
        }
    }
}
