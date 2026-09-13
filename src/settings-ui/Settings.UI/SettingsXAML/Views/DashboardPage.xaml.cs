// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dashboard Settings Page.
    /// </summary>
    public sealed partial class DashboardPage : NavigablePage, IRefreshablePage
    {
        /// <summary>
        /// Gets the view model.
        /// </summary>
        public DashboardViewModel ViewModel => _viewModelLifetime?.ViewModel;

        private readonly DashboardViewModelLifetime _viewModelLifetime;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardPage"/> class.
        /// Dashboard Settings page constructor.
        /// </summary>
        public DashboardPage()
        {
            InitializeComponent();
            var settingsUtils = SettingsUtils.Default;

            _viewModelLifetime = new DashboardViewModelLifetime(() => new DashboardViewModel(
               SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage));
            DataContext = ViewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModelLifetime.Load())
            {
                DataContext = ViewModel;

                // The same Page can reload; compiled bindings must follow the new model too.
                Bindings.Update();
            }

            ViewModel.OnPageLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModelLifetime.Unload();
        }

        public void RefreshEnabledState()
        {
            ViewModel.ModuleEnabledChangedOnSettingsPage();
        }

        private void WhatsNewButton_Click(object sender, RoutedEventArgs e)
        {
            ((App)App.Current)!.OpenScoobe();
        }

        private void SortAlphabetical_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DashboardSortOrder = DashboardSortOrder.Alphabetical;
            if (sender is ToggleMenuFlyoutItem item)
            {
                item.IsChecked = true;
            }
        }

        private void SortByStatus_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DashboardSortOrder = DashboardSortOrder.ByStatus;
            if (sender is ToggleMenuFlyoutItem item)
            {
                item.IsChecked = true;
            }
        }
    }
}
