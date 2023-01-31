// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ImageResizerPage : Page, IRefreshablePage
    {
        public ImageResizerViewModel ViewModel { get; set; }

        public ImageResizerPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            var resourceLoader = ResourceLoader.GetForViewIndependentUse();
            Func<string, string> loader = (string name) =>
            {
                return resourceLoader.GetString(name);
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
                ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = RootPage.XamlRoot;
                dialog.Title = x.Name;
                dialog.PrimaryButtonText = resourceLoader.GetString("Yes");
                dialog.CloseButtonText = resourceLoader.GetString("No");
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") };
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
                ViewModel.AddRow(ResourceLoader.GetForViewIndependentUse().GetString("ImageResizer_DefaultSize_NewSizePrefix"));
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
