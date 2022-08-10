// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using PowerAccent.Core.Services;

namespace PowerAccent.UI.SettingsPage
{
    /// <summary>
    /// Logique d'interaction pour OptionsPage.xaml
    /// </summary>
    public partial class OptionsPage : Page
    {
        private readonly SettingsService _settingService = new SettingsService();

        public OptionsPage()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            IsUseCaretPosition.IsOn = _settingService.UseCaretPosition;
            IsSpaceBarActive.IsOn = _settingService.IsSpaceBarActive;
        }

        private void UseCaretPosition_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.UseCaretPosition = ((ToggleSwitch)sender).IsOn;
            (Application.Current.MainWindow as Selector).RefreshSettings();
        }

        private void SpaceBarActive_Checked(object sender, RoutedEventArgs e)
        {
            _settingService.IsSpaceBarActive = ((ToggleSwitch)sender).IsOn;
            (Application.Current.MainWindow as Selector).RefreshSettings();
        }
    }
}
