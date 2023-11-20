// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dashboard Settings Page.
    /// </summary>
    public sealed partial class DashboardPage : Page, IRefreshablePage
    {
        /// <summary>
        /// Gets or sets view model.
        /// </summary>
        public DashboardViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardPage"/> class.
        /// Dashboard Settings page constructor.
        /// </summary>
        public DashboardPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();

            ViewModel = new DashboardViewModel(
               SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }

        public void RefreshEnabledState()
        {
            ViewModel.ModuleEnabledChangedOnSettingsPage();
        }

        private void SWVersionButtonClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SWVersionButtonClicked();
        }

        private void DashboardListItemClick(object sender, RoutedEventArgs e)
        {
            ViewModel.DashboardListItemClick(sender);
        }
    }
}
