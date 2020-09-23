// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ImageResizerPage : Page
    {
        public ImageResizerViewModel ViewModel { get; set; }

        public ImageResizerPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils(new SystemIOProvider());
            ViewModel = new ImageResizerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        public void DeleteCustomSize(object sender, RoutedEventArgs e)
        {
            try
            {
                Button deleteRowButton = (Button)sender;
                int rowNum = int.Parse(deleteRowButton.CommandParameter.ToString());
                ViewModel.DeleteImageSize(rowNum);
            }
            catch
            {
            }
        }

        private void AddSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.AddRow();
            }
            catch
            {
            }
        }
    }
}
