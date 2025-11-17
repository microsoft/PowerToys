// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.FileLocksmithLib.Interop;
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

        private async void ShowSelectedPathsButton_Click(object sender, RoutedEventArgs e)
        {
            await SelectedFilesListDialog.ShowAsync();
        }

        private async void ShowProcessFiles_Click(object sender, RoutedEventArgs e)
        {
            var processResult = (ProcessResult)((FrameworkElement)sender).DataContext;
            ProcessFilesListDialogTextBlock.Text = string.Join(Environment.NewLine, processResult.files);

            await ProcessFilesListDialog.ShowAsync();
        }
    }
}
