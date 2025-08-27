// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DarkModeViewModel : Observable
    {
        private Func<string, int> SendConfigMSG { get; }

        public DarkModeViewModel(DarkModeSettings initialSettings = null, Func<string, int> ipcMSGCallBackFunc = null)
        {
            _moduleSettings = initialSettings ?? new DarkModeSettings();
            SendConfigMSG = ipcMSGCallBackFunc;

            ForceLightCommand = new RelayCommand(ForceLightNow);
            ForceDarkCommand = new RelayCommand(ForceDarkNow);

            // populate the list of modes for dropdown binding
            AvailableScheduleModes = new ObservableCollection<string>
            {
                "FixedHours",
                "SunsetToSunrise",
            };
        }

        private void ForceLightNow()
        {
            Logger.LogInfo("Sending custom action: forceLight");
            SendCustomAction("forceLight");
        }

        private void ForceDarkNow()
        {
            Logger.LogInfo("Sending custom action: forceDark");
            SendCustomAction("forceDark");
        }

        private void SendCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"DarkMode\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
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

        public string ScheduleMode
        {
            get => ModuleSettings.Properties.ScheduleMode.Value;
            set
            {
                if (ModuleSettings.Properties.ScheduleMode.Value != value)
                {
                    ModuleSettings.Properties.ScheduleMode.Value = value;
                    OnPropertyChanged(nameof(ScheduleMode));
                }
            }
        }

        // available values for the dropdown
        public ObservableCollection<string> AvailableScheduleModes { get; }

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

        public int Offset
        {
            get => ModuleSettings.Properties.Offset.Value;
            set
            {
                if (ModuleSettings.Properties.Offset.Value != value)
                {
                    ModuleSettings.Properties.Offset.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan LightTimeTimeSpan
        {
            get => TimeSpan.FromMinutes(LightTime);
            set
            {
                int minutes = (int)value.TotalMinutes;
                if (LightTime != minutes)
                {
                    LightTime = minutes;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(LightTimeTimeSpan));
                }
            }
        }

        public TimeSpan DarkTimeTimeSpan
        {
            get => TimeSpan.FromMinutes(DarkTime);
            set
            {
                int minutes = (int)value.TotalMinutes;
                if (DarkTime != minutes)
                {
                    DarkTime = minutes;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(DarkTimeTimeSpan));
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
        }

        public void RefreshModuleSettings()
        {
            OnPropertyChanged(nameof(ChangeSystem));
            OnPropertyChanged(nameof(ChangeApps));
            OnPropertyChanged(nameof(LightTime));
            OnPropertyChanged(nameof(DarkTime));
            OnPropertyChanged(nameof(Offset));
            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(ScheduleMode));
        }

        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private DarkModeSettings _moduleSettings;
        private bool _isEnabled;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
