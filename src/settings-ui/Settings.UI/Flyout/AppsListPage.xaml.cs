// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.ObjectModel;
using System.Threading;
using global::Windows.System;
using interop;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Microsoft.PowerToys.Settings.UI.Flyout
{
    public sealed partial class AppsListPage : Page
    {
        private AllAppsViewModel ViewModel { get; set; }

        public AppsListPage()
        {
            this.InitializeComponent();

            var settingsUtils = new SettingsUtils();
            ViewModel = new AllAppsViewModel(SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), Views.ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LaunchPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }
    }
}
