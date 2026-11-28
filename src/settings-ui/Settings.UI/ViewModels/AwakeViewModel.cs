// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class AwakeViewModel : Observable
    {
        public const uint MaxTrayTimeHours = 999;

        public AwakeViewModel()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AwakeViewModel"/> class.
        /// </summary>
        /// <param name="trayTimeFormatter">Formats a tray time shortcut duration (in seconds) as a label. Defaults to localized resources.</param>
        public AwakeViewModel(Func<uint, string> trayTimeFormatter)
        {
            _trayTimeFormatter = trayTimeFormatter ?? FormatTrayTimeFromResources;
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

        public bool AllowDisplayOffWhileLocked
        {
            get => ModuleSettings.Properties.AllowDisplayOffWhileLocked;
            set
            {
                if (ModuleSettings.Properties.AllowDisplayOffWhileLocked != value)
                {
                    ModuleSettings.Properties.AllowDisplayOffWhileLocked = value;
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

        /// <summary>
        /// Gets the time shortcuts shown in the Awake tray menu, ordered by duration.
        /// </summary>
        public ObservableCollection<AwakeTrayTimeItem> TrayTimes { get; } = new ObservableCollection<AwakeTrayTimeItem>();

        public bool CanRemoveTrayTime => TrayTimes.Count > 1;

        public double NewTrayTimeHours
        {
            get => _newTrayTimeHours;
            set
            {
                if (_newTrayTimeHours != value)
                {
                    _newTrayTimeHours = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAddTrayTime));
                }
            }
        }

        public double NewTrayTimeMinutes
        {
            get => _newTrayTimeMinutes;
            set
            {
                if (_newTrayTimeMinutes != value)
                {
                    _newTrayTimeMinutes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanAddTrayTime));
                }
            }
        }

        public bool CanAddTrayTime
        {
            get
            {
                uint? seconds = GetNewTrayTimeSeconds();
                return seconds.HasValue && !TrayTimes.Any(item => item.Seconds == seconds.Value);
            }
        }

        /// <summary>
        /// Adds a tray time shortcut using <see cref="NewTrayTimeHours"/> and <see cref="NewTrayTimeMinutes"/>.
        /// </summary>
        /// <returns>True if the shortcut was added; false if the duration is invalid or already present.</returns>
        public bool AddTrayTime()
        {
            if (!CanAddTrayTime)
            {
                return false;
            }

            uint seconds = GetNewTrayTimeSeconds().Value;
            List<AwakeTrayTimeItem> items = [.. TrayTimes, new AwakeTrayTimeItem(_trayTimeFormatter(seconds), seconds)];
            SaveTrayTimes(items);
            return true;
        }

        /// <summary>
        /// Removes a tray time shortcut. The last remaining shortcut can't be removed.
        /// </summary>
        /// <param name="item">The shortcut to remove.</param>
        /// <returns>True if the shortcut was removed.</returns>
        public bool RemoveTrayTime(AwakeTrayTimeItem item)
        {
            if (item == null || !CanRemoveTrayTime || !TrayTimes.Contains(item))
            {
                return false;
            }

            SaveTrayTimes(TrayTimes.Where(existing => existing != item).ToList());
            return true;
        }

        /// <summary>
        /// Clears the custom tray time shortcuts so that Awake falls back to its defaults.
        /// </summary>
        public void ResetTrayTimes()
        {
            if (ModuleSettings.Properties.CustomTrayTimes.Count == 0)
            {
                return;
            }

            ModuleSettings.Properties.CustomTrayTimes.Clear();
            RefreshTrayTimes();
            NotifyPropertyChanged(nameof(TrayTimes));
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
            OnPropertyChanged(nameof(AllowDisplayOffWhileLocked));
            OnPropertyChanged(nameof(IntervalHours));
            OnPropertyChanged(nameof(IntervalMinutes));
            OnPropertyChanged(nameof(ExpirationDateTime));
            RefreshTrayTimes();
        }

        private void RefreshTrayTimes()
        {
            IEnumerable<AwakeTrayTimeItem> items = ModuleSettings?.Properties.CustomTrayTimes.Count > 0
                ? ModuleSettings.Properties.CustomTrayTimes.Select(entry => new AwakeTrayTimeItem(entry.Key, entry.Value))
                : AwakeDefaults.TrayTimeSeconds.Select(seconds => new AwakeTrayTimeItem(_trayTimeFormatter(seconds), seconds));

            TrayTimes.Clear();
            foreach (AwakeTrayTimeItem item in items.OrderBy(item => item.Seconds))
            {
                TrayTimes.Add(item);
            }

            OnPropertyChanged(nameof(CanRemoveTrayTime));
            OnPropertyChanged(nameof(CanAddTrayTime));
        }

        private void SaveTrayTimes(IEnumerable<AwakeTrayTimeItem> items)
        {
            // The tray menu lists the entries in dictionary order, so keep them sorted by duration.
            Dictionary<string, uint> customTrayTimes = ModuleSettings.Properties.CustomTrayTimes;
            customTrayTimes.Clear();
            foreach (AwakeTrayTimeItem item in items.OrderBy(item => item.Seconds))
            {
                customTrayTimes.TryAdd(item.Label, item.Seconds);
            }

            RefreshTrayTimes();
            NotifyPropertyChanged(nameof(TrayTimes));
        }

        private uint? GetNewTrayTimeSeconds()
        {
            double hours = double.IsNaN(NewTrayTimeHours) ? 0 : NewTrayTimeHours;
            double minutes = double.IsNaN(NewTrayTimeMinutes) ? 0 : NewTrayTimeMinutes;

            if (hours < 0 || hours > MaxTrayTimeHours || minutes < 0 || minutes >= 60 ||
                hours != Math.Floor(hours) || minutes != Math.Floor(minutes))
            {
                return null;
            }

            uint seconds = ((uint)hours * 3600) + ((uint)minutes * 60);
            return seconds == 0 ? null : seconds;
        }

        private static string FormatTrayTimeFromResources(uint seconds)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            uint hours = seconds / 3600;
            uint minutes = (seconds % 3600) / 60;

            if (hours == 0)
            {
                string minutesFormat = resourceLoader.GetString(minutes == 1 ? "Awake_TrayTime_Minute" : "Awake_TrayTime_Minutes");
                return string.Format(CultureInfo.CurrentCulture, minutesFormat, minutes);
            }

            if (minutes == 0)
            {
                string hoursFormat = resourceLoader.GetString(hours == 1 ? "Awake_TrayTime_Hour" : "Awake_TrayTime_Hours");
                return string.Format(CultureInfo.CurrentCulture, hoursFormat, hours);
            }

            return string.Format(CultureInfo.CurrentCulture, resourceLoader.GetString("Awake_TrayTime_HoursMinutes"), hours, minutes);
        }

        private readonly Func<uint, string> _trayTimeFormatter;
        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private AwakeSettings _moduleSettings;
        private bool _isEnabled;
        private double _newTrayTimeHours = 4;
        private double _newTrayTimeMinutes;
    }
}
