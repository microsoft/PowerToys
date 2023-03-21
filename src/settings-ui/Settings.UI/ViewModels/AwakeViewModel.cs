// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AwakeViewModel : Observable
    {
        public AwakeViewModel()
        {
        }

        public AwakeSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;
                    RefreshModuleSettings();
                    RefreshEnabledState();
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
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

        public bool IsExpirationConfigurationEnabled
        {
            get => ModuleSettings.Properties.Mode == AwakeMode.EXPIRABLE && IsEnabled;
        }

        public bool IsTimeConfigurationEnabled
        {
            get => ModuleSettings.Properties.Mode == AwakeMode.TIMED && IsEnabled;
        }

        public bool IsScreenConfigurationPossibleEnabled
        {
            get => ModuleSettings.Properties.Mode != AwakeMode.PASSIVE && IsEnabled;
        }

        public AwakeMode Mode
        {
            get => ModuleSettings.Properties.Mode;
            set
            {
                if (ModuleSettings.Properties.Mode != value)
                {
                    ModuleSettings.Properties.Mode = value;

                    OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
                    OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
                    OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));

                    NotifyPropertyChanged();
                }
            }
        }

        public bool KeepDisplayOn
        {
            get => ModuleSettings.Properties.KeepDisplayOn;
            set
            {
                if (ModuleSettings.Properties.KeepDisplayOn != value)
                {
                    ModuleSettings.Properties.KeepDisplayOn = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint IntervalHours
        {
            get => ModuleSettings.Properties.IntervalHours;
            set
            {
                if (ModuleSettings.Properties.IntervalHours != value)
                {
                    ModuleSettings.Properties.IntervalHours = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint IntervalMinutes
        {
            get => ModuleSettings.Properties.IntervalMinutes;
            set
            {
                if (ModuleSettings.Properties.IntervalMinutes != value)
                {
                    ModuleSettings.Properties.IntervalMinutes = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTimeOffset ExpirationDateTime
        {
            get => ModuleSettings.Properties.ExpirationDateTime;
            set
            {
                if (ModuleSettings.Properties.ExpirationDateTime != value)
                {
                    ModuleSettings.Properties.ExpirationDateTime = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan ExpirationTime
        {
            get => ExpirationDateTime.TimeOfDay;
            set
            {
                if (ExpirationDateTime.TimeOfDay != value)
                {
                    ExpirationDateTime = new DateTime(ExpirationDateTime.Year, ExpirationDateTime.Month, ExpirationDateTime.Day, value.Hours, value.Minutes, value.Seconds);
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
            OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
            OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
            OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));
        }

        public void RefreshModuleSettings()
        {
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(KeepDisplayOn));
            OnPropertyChanged(nameof(IntervalHours));
            OnPropertyChanged(nameof(IntervalMinutes));
            OnPropertyChanged(nameof(ExpirationDateTime));
        }

        private bool _enabledStateIsGPOConfigured;
        private AwakeSettings _moduleSettings;
        private bool _isEnabled;
    }
}
