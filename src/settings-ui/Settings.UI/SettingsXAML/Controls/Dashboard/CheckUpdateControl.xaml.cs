// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class CheckUpdateControl : UserControl, INotifyPropertyChanged
    {
        private bool _updateAvailable;
        private UpdatingSettings _updateSettingsConfig;

        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set
            {
                if (_updateAvailable != value)
                {
                    _updateAvailable = value;
                    OnPropertyChanged();
                }
            }
        }

        public UpdatingSettings UpdateSettingsConfig
        {
            get => _updateSettingsConfig;
            set
            {
                if (_updateSettingsConfig != value)
                {
                    _updateSettingsConfig = value;
                    OnPropertyChanged();
                }
            }
        }

        public CheckUpdateControl()
        {
            InitializeComponent();
            UpdateSettingsConfig = UpdatingSettings.LoadSettings();
            UpdateAvailable = UpdateSettingsConfig != null && (UpdateSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToInstall || UpdateSettingsConfig.State == UpdatingSettings.UpdatingState.ReadyToDownload);
        }

        private void SWVersionButtonClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            NavigationService.Navigate(typeof(GeneralPage));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
