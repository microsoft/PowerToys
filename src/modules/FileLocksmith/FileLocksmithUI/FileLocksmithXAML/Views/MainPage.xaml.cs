// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.FileLocksmithLib.Interop;
using PowerToys.FileLocksmithUI.Helpers;
using PowerToys.FileLocksmithUI.ViewModels;

namespace PowerToys.FileLocksmithUI.Views
{
    public sealed partial class MainPage : Page
    {
        private bool _isEndTaskConfirmationOpen;

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

        private async void EndTask_Click(object sender, RoutedEventArgs e)
        {
            // Read the row before awaiting: the ListView can recycle the sender while the dialog is open.
            var process = (ProcessResult)((FrameworkElement)sender).DataContext;
            if (_isEndTaskConfirmationOpen)
            {
                return;
            }

            try
            {
                _isEndTaskConfirmationOpen = true;

                var loader = ResourceLoaderInstance.ResourceLoader;
                var ownerKind = ProcessOwner.Classify(process.user, Environment.UserName);
                if (ViewModel.ConfirmOnlyForOthers && ownerKind == ProcessOwnerKind.CurrentUser)
                {
                    ViewModel.EndTaskCommand.Execute(process);
                    return;
                }

                EndTaskConfirmDialog.Title = string.Format(CultureInfo.CurrentCulture, loader.GetString("EndTaskConfirmTitle"), process.name);
                EndTaskConfirmMessage.Text = ownerKind == ProcessOwnerKind.OtherUser
                    ? string.Format(CultureInfo.CurrentCulture, loader.GetString("EndTaskConfirmOtherUserMessage"), process.user)
                    : loader.GetString("EndTaskConfirmMessage");
                EndTaskConfirmSystemWarning.Visibility = ownerKind == ProcessOwnerKind.System ? Visibility.Visible : Visibility.Collapsed;

                // The row is gone if the process exited while the dialog was open; then there is nothing to end.
                if (await EndTaskConfirmDialog.ShowAsync() == ContentDialogResult.Primary && ViewModel.Processes.Contains(process))
                {
                    ViewModel.EndTaskCommand.Execute(process);
                }
            }
            finally
            {
                _isEndTaskConfirmationOpen = false;
            }
        }
    }
}
