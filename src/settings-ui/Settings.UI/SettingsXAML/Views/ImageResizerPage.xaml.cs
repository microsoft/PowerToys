// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ImageResizerPage : Page, IRefreshablePage
    {
        public ImageResizerViewModel ViewModel { get; set; }

        public ImageResizerPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            Func<string, string> loader = (string name) =>
            {
                return LocalizerInstance.Instance.GetLocalizedString(name);
            };

            ViewModel = new ImageResizerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, loader);
            DataContext = ViewModel;
        }

        public async void DeleteCustomSize(object sender, RoutedEventArgs e)
        {
            Button deleteRowButton = (Button)sender;

            if (deleteRowButton != null)
            {
                ImageSize x = (ImageSize)deleteRowButton.DataContext;

                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = x.Name;
                dialog.PrimaryButtonText = LocalizerInstance.Instance.GetLocalizedString("Yes");
                dialog.CloseButtonText = LocalizerInstance.Instance.GetLocalizedString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = LocalizerInstance.Instance.GetLocalizedString("Delete_Dialog_Description") };
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    // Using InvariantCulture since this is internal and expected to be numerical
                    bool success = int.TryParse(deleteRowButton?.CommandParameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowNum);
                    if (success)
                    {
                        ViewModel.DeleteImageSize(rowNum);
                    }
                    else
                    {
                        Logger.LogError("Failed to delete custom image size.");
                    }
                };
                var result = await dialog.ShowAsync();
            }
        }

        private void AddSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.AddRow(LocalizerInstance.Instance.GetLocalizedString("ImageResizer_DefaultSize_NewSizePrefix"));
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception encountered when adding a new image size.", ex);
            }
        }

        private void ImagesSizesListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (ViewModel.IsListViewFocusRequested)
            {
                // Set focus to the last item in the ListView
                int size = ImagesSizesListView.Items.Count;
                ((ListViewItem)ImagesSizesListView.ContainerFromIndex(size - 1)).Focus(FocusState.Programmatic);

                // Reset the focus requested flag
                ViewModel.IsListViewFocusRequested = false;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
