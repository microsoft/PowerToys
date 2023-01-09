// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Controls;
using PowerToys.FileLocksmithUI.ViewModels;

namespace PowerToys.FileLocksmithUI.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        private async void ShowSelectedPathsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await SelectedFilesListDialog.ShowAsync();
        }
    }
}
