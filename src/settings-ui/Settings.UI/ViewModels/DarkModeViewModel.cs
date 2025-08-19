// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DarkModeViewModel : Observable
    {
        public DarkModeViewModel(DarkModeSettings initialSettings = null)
        {
            _moduleSettings = initialSettings ?? new DarkModeSettings();
        }

        public DarkModeSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;

                    OnPropertyChanged(nameof(ModuleSettings));
                    RefreshModuleSettings();
                    RefreshEnabledState();
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return _enabledGPOConfiguration;
                }
                else
                {
                    return _isEnabled;
                }
            }

            set
            {
                if (_isEnabled != value)
                {
                    if (_enabledStateIsGPOConfigured)
                    {
                        // If it's GPO configured, shouldn't be able to change this state.
                        return;
                    }

                    _isEnabled = value;

                    RefreshEnabledState();

                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
            set
            {
                if (_enabledStateIsGPOConfigured != value)
                {
                    _enabledStateIsGPOConfigured = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnabledGPOConfiguration
        {
            get => _enabledGPOConfiguration;
            set
            {
                if (_enabledGPOConfiguration != value)
                {
                    _enabledGPOConfiguration = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsUseLocationEnabled => ModuleSettings.Properties.UseLocation.Value && IsEnabled;

        public bool UseLocation
        {
            get => ModuleSettings.Properties.UseLocation.Value;
            set
            {
                if (ModuleSettings.Properties.UseLocation.Value != value)
                {
                    ModuleSettings.Properties.UseLocation.Value = value;
                    OnPropertyChanged(nameof(UseLocation));
                    OnPropertyChanged(nameof(IsUseLocationEnabled));
                }
            }
        }

        public bool ChangeSystem
        {
            get => ModuleSettings.Properties.ChangeSystem.Value;
            set
            {
                if (ModuleSettings.Properties.ChangeSystem.Value != value)
                {
                    ModuleSettings.Properties.ChangeSystem.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ChangeApps
        {
            get => ModuleSettings.Properties.ChangeApps.Value;
            set
            {
                if (ModuleSettings.Properties.ChangeApps.Value != value)
                {
                    ModuleSettings.Properties.ChangeApps.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int LightTime
        {
            get => ModuleSettings.Properties.LightTime.Value;
            set
            {
                if (ModuleSettings.Properties.LightTime.Value != value)
                {
                    ModuleSettings.Properties.LightTime.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int DarkTime
        {
            get => ModuleSettings.Properties.DarkTime.Value;
            set
            {
                if (ModuleSettings.Properties.DarkTime.Value != value)
                {
                    ModuleSettings.Properties.DarkTime.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Latitude
        {
            get => ModuleSettings.Properties.Latitude.Value;
            set
            {
                if (ModuleSettings.Properties.Latitude.Value != value)
                {
                    ModuleSettings.Properties.Latitude.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Longitude
        {
            get => ModuleSettings.Properties.Longitude.Value;
            set
            {
                if (ModuleSettings.Properties.Longitude.Value != value)
                {
                    ModuleSettings.Properties.Longitude.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Logger.LogInfo($"Changed the property {propertyName}");
            OnPropertyChanged(propertyName);
        }

        public void RefreshEnabledState()
        {
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsUseLocationEnabled));
        }

        public void RefreshModuleSettings()
        {
            OnPropertyChanged(nameof(ChangeSystem));
            OnPropertyChanged(nameof(ChangeApps));
            OnPropertyChanged(nameof(LightTime));
            OnPropertyChanged(nameof(DarkTime));
            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(UseLocation));
            OnPropertyChanged(nameof(IsUseLocationEnabled));
        }

        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private DarkModeSettings _moduleSettings;
        private bool _isEnabled;
    }
}
