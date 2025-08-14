// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DarkModeViewModel : Observable
    {
        public DarkModeViewModel()
        {
            // Ensure ModuleSettings is never null
            _moduleSettings = new DarkModeSettings();
        }

        public DarkModeSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;

                    _moduleSettings.Properties.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ModuleSettings.Properties.UseLocation))
                        {
                            OnPropertyChanged(nameof(IsUseLocationEnabled));
                        }
                    };

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

        public bool IsUseLocationEnabled => ModuleSettings.Properties.UseLocation && IsEnabled;

        public bool UseLocation
        {
            get => ModuleSettings.Properties.UseLocation;
            set
            {
                if (ModuleSettings.Properties.UseLocation != value)
                {
                    ModuleSettings.Properties.UseLocation = value;
                    OnPropertyChanged(nameof(UseLocation));
                    OnPropertyChanged(nameof(IsUseLocationEnabled));
                }
            }
        }

        public bool ChangeSystem
        {
            get => ModuleSettings.Properties.ChangeSystem;
            set
            {
                if (ModuleSettings.Properties.ChangeSystem != value)
                {
                    ModuleSettings.Properties.ChangeSystem = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ChangeApps
        {
            get => ModuleSettings.Properties.ChangeApps;
            set
            {
                if (ModuleSettings.Properties.ChangeApps != value)
                {
                    ModuleSettings.Properties.ChangeApps = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint LightTime
        {
            get => ModuleSettings.Properties.LightTime;
            set
            {
                if (ModuleSettings.Properties.LightTime != value)
                {
                    ModuleSettings.Properties.LightTime = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint DarkTime
        {
            get => ModuleSettings.Properties.DarkTime;
            set
            {
                if (ModuleSettings.Properties.DarkTime != value)
                {
                    ModuleSettings.Properties.DarkTime = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Latitude
        {
            get => ModuleSettings.Properties.Latitude;
            set
            {
                if (ModuleSettings.Properties.Latitude != value)
                {
                    ModuleSettings.Properties.Latitude = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Longitude
        {
            get => ModuleSettings.Properties.Longitude;
            set
            {
                if (ModuleSettings.Properties.Longitude != value)
                {
                    ModuleSettings.Properties.Longitude = value;
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
