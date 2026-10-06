// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class DisplayProfilesPage : NavigablePage
    {
        private MonitorPowerViewModel ViewModel { get; } = new();

        public DisplayProfilesPage()
        {
            Logger.LogInfo("Creating Monitor Power Settings page.");
            DataContext = ViewModel;
            InitializeComponent();
            Logger.LogInfo("Monitor Power Settings page created.");
        }

        private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile })
            {
                ViewModel.SelectedProfile = profile;
                await ViewModel.ApplyProfileAsync();
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SaveProfileAsync();
        }

        private async void TestController_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.TestControllerAsync();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile })
            {
                ViewModel.SelectedProfile = profile;
                ViewModel.DeleteProfile();
            }
        }
    }
}
