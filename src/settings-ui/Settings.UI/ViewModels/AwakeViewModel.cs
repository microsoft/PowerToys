// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AwakeViewModel : Observable
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

        public bool IsExpirationConfigurationEnabled => ModuleSettings.Properties.Mode == AwakeMode.EXPIRABLE && IsEnabled;

        public bool IsTimeConfigurationEnabled => ModuleSettings.Properties.Mode == AwakeMode.TIMED && IsEnabled;

        public bool IsScreenConfigurationPossibleEnabled => ModuleSettings.Properties.Mode != AwakeMode.PASSIVE && IsEnabled;

        public bool IsActivityConfigurationEnabled => ModuleSettings.Properties.Mode == AwakeMode.ACTIVITY && IsEnabled;

        public bool IsProcessConfigurationEnabled => ModuleSettings.Properties.Mode == AwakeMode.PROCESS && IsEnabled;

        public AwakeMode Mode
        {
            get => ModuleSettings.Properties.Mode;
            set
            {
                if (ModuleSettings.Properties.Mode != value)
                {
                    ModuleSettings.Properties.Mode = value;

                    if (value == AwakeMode.TIMED && IntervalMinutes == 0 && IntervalHours == 0)
                    {
                        // Handle the special case where both hours and minutes are zero.
                        // Otherwise, this will reset to passive very quickly in the UI.
                        ModuleSettings.Properties.IntervalMinutes = 1;
                        OnPropertyChanged(nameof(IntervalMinutes));
                    }
                    else if (value == AwakeMode.EXPIRABLE && ExpirationDateTime <= DateTimeOffset.Now)
                    {
                        // To make sure that we're not tracking expirable keep-awake in the past,
                        // let's make sure that every time it's enabled from the settings UI, it's
                        // five (5) minutes into the future.
                        ExpirationDateTime = DateTimeOffset.Now.AddMinutes(5);

                        // The expiration date/time is updated and will send the notification
                        // but we need to do this manually for the expiration time that is
                        // bound to the time control on the settings page.
                        OnPropertyChanged(nameof(ExpirationTime));
                    }

                    OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
                    OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
                    OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));
                    OnPropertyChanged(nameof(IsActivityConfigurationEnabled));
                    OnPropertyChanged(nameof(IsProcessConfigurationEnabled));

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

        public bool TrackUsageEnabled
        {
            get => ModuleSettings.Properties.TrackUsageEnabled;
            set
            {
                if (ModuleSettings.Properties.TrackUsageEnabled != value)
                {
                    ModuleSettings.Properties.TrackUsageEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int UsageRetentionDays
        {
            get => ModuleSettings.Properties.UsageRetentionDays;
            set
            {
                int clamped = Math.Max(1, Math.Min(365, value));
                if (ModuleSettings.Properties.UsageRetentionDays != clamped)
                {
                    ModuleSettings.Properties.UsageRetentionDays = clamped;
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
            OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
            OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
            OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));
            OnPropertyChanged(nameof(IsActivityConfigurationEnabled));
            OnPropertyChanged(nameof(IsProcessConfigurationEnabled));
        }

        public void RefreshModuleSettings()
        {
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(KeepDisplayOn));
            OnPropertyChanged(nameof(IntervalHours));
            OnPropertyChanged(nameof(IntervalMinutes));
            OnPropertyChanged(nameof(ExpirationDateTime));
            OnPropertyChanged(nameof(ActivityCpuThresholdPercent));
            OnPropertyChanged(nameof(ActivityMemoryThresholdPercent));
            OnPropertyChanged(nameof(ActivityNetworkThresholdKBps));
            OnPropertyChanged(nameof(ActivitySampleIntervalSeconds));
            OnPropertyChanged(nameof(ActivityInactivityTimeoutSeconds));
            OnPropertyChanged(nameof(ProcessMonitoringList));
            OnPropertyChanged(nameof(ProcessCheckIntervalSeconds));
            OnPropertyChanged(nameof(TrackUsageEnabled));
            OnPropertyChanged(nameof(UsageRetentionDays));
        }

        // Activity configuration bindables
        public uint ActivityCpuThresholdPercent
        {
            get => ModuleSettings.Properties.ActivityCpuThresholdPercent;
            set
            {
                if (ModuleSettings.Properties.ActivityCpuThresholdPercent != value)
                {
                    ModuleSettings.Properties.ActivityCpuThresholdPercent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint ActivityMemoryThresholdPercent
        {
            get => ModuleSettings.Properties.ActivityMemoryThresholdPercent;
            set
            {
                if (ModuleSettings.Properties.ActivityMemoryThresholdPercent != value)
                {
                    ModuleSettings.Properties.ActivityMemoryThresholdPercent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint ActivityNetworkThresholdKBps
        {
            get => ModuleSettings.Properties.ActivityNetworkThresholdKBps;
            set
            {
                if (ModuleSettings.Properties.ActivityNetworkThresholdKBps != value)
                {
                    ModuleSettings.Properties.ActivityNetworkThresholdKBps = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint ActivitySampleIntervalSeconds
        {
            get => ModuleSettings.Properties.ActivitySampleIntervalSeconds;
            set
            {
                if (ModuleSettings.Properties.ActivitySampleIntervalSeconds != value)
                {
                    ModuleSettings.Properties.ActivitySampleIntervalSeconds = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint ActivityInactivityTimeoutSeconds
        {
            get => ModuleSettings.Properties.ActivityInactivityTimeoutSeconds;
            set
            {
                if (ModuleSettings.Properties.ActivityInactivityTimeoutSeconds != value)
                {
                    ModuleSettings.Properties.ActivityInactivityTimeoutSeconds = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // Process monitoring configuration bindables
        public string ProcessMonitoringList
        {
            get => string.Join(", ", ModuleSettings.Properties.ProcessMonitoringList);
            set
            {
                var processList = string.IsNullOrWhiteSpace(value)
                    ? new List<string>()
                    : value.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

                if (!ModuleSettings.Properties.ProcessMonitoringList.SequenceEqual(processList))
                {
                    ModuleSettings.Properties.ProcessMonitoringList = processList;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint ProcessCheckIntervalSeconds
        {
            get => ModuleSettings.Properties.ProcessCheckIntervalSeconds;
            set
            {
                if (ModuleSettings.Properties.ProcessCheckIntervalSeconds != value)
                {
                    ModuleSettings.Properties.ProcessCheckIntervalSeconds = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private AwakeSettings _moduleSettings;
        private bool _isEnabled;
    }
}
