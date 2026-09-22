// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Profile Editor Dialog
    /// </summary>
    public class ProfileEditorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly int _profileId;
        private readonly ObservableCollection<MonitorSelectionItem> _monitors;
        private string _profileName = string.Empty;
        private PowerDisplayProfile? _originalProfile;

        public ProfileEditorViewModel(
            ObservableCollection<MonitorInfo> availableMonitors,
            string defaultName = "",
            int profileId = 0)
        {
            _profileId = profileId;
            _profileName = defaultName;
            _monitors = new ObservableCollection<MonitorSelectionItem>();

            // Set TotalMonitorCount for DisplayName to show monitor numbers when multiple monitors exist
            int totalCount = availableMonitors.Count;
            foreach (var monitor in availableMonitors)
            {
                monitor.TotalMonitorCount = totalCount;
            }

            // Initialize monitor selection items
            foreach (var monitor in availableMonitors)
            {
                var item = new MonitorSelectionItem(monitor)
                {
                    SuppressAutoSelection = true,
                    IsSelected = false,
                    Brightness = monitor.CurrentBrightness,
                    Contrast = 50, // Default value (MonitorInfo doesn't store contrast)
                    Volume = 50, // Default value (MonitorInfo doesn't store volume)
                    ColorTemperature = monitor.ColorTemperatureVcp,
                };

                item.SuppressAutoSelection = false;

                // Subscribe to selection and checkbox changes
                item.PropertyChanged += OnMonitorItemPropertyChanged;

                _monitors.Add(item);
            }
        }

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public ObservableCollection<MonitorSelectionItem> Monitors => _monitors;

        public bool HasSelectedMonitors => _monitors.Any(m => m.IsSelected) || HasMissingProfileMonitors;

        public bool HasValidSettings => HasSelectedMonitors &&
            _monitors.Where(m => m.IsSelected).All(m => m.HasValidSettings);

        public bool HasPreservedSettings => HasMissingProfileMonitors ||
            _monitors.Any(m => m.IsSelected && m.HasPreservedSettings);

        public bool CanSave => !string.IsNullOrWhiteSpace(_profileName) && HasSelectedMonitors && HasValidSettings;

        private bool HasMissingProfileMonitors => _originalProfile?.MonitorSettings.Any(setting =>
            !_monitors.Any(m => MonitorIdComparer.Equal(m.Monitor.Id, setting.MonitorId))) ?? false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PowerDisplayProfile CreateProfile()
        {
            var settings = new List<ProfileMonitorSetting>();
            if (_originalProfile != null)
            {
                foreach (var original in _originalProfile.MonitorSettings)
                {
                    var item = _monitors.FirstOrDefault(m => MonitorIdComparer.Equal(m.Monitor.Id, original.MonitorId));
                    if (item == null)
                    {
                        // Unavailable monitors have no editor controls. Keep their snapshots
                        // until the user can explicitly change or remove them.
                        settings.Add(CloneMonitorSetting(original));
                    }
                    else if (item.IsSelected)
                    {
                        settings.Add(item.CreateProfileSetting(original.MonitorId));
                    }
                }
            }

            foreach (var item in _monitors.Where(m => m.IsSelected))
            {
                if (_originalProfile?.MonitorSettings.Any(setting => MonitorIdComparer.Equal(setting.MonitorId, item.Monitor.Id)) != true)
                {
                    settings.Add(item.CreateProfileSetting(item.Monitor.Id));
                }
            }

            var profile = new PowerDisplayProfile(_profileName, settings) { Id = _originalProfile?.Id ?? _profileId };
            if (_originalProfile != null)
            {
                profile.CreatedDate = _originalProfile.CreatedDate;
            }

            return profile;
        }

        /// <summary>
        /// Pre-fill a new editor with the existing profile's monitor settings.
        /// </summary>
        public void PreFillProfile(PowerDisplayProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            _originalProfile = new PowerDisplayProfile(profile.Name, profile.MonitorSettings.Select(CloneMonitorSetting).ToList())
            {
                Id = profile.Id,
                CreatedDate = profile.CreatedDate,
                LastModified = profile.LastModified,
            };
            ProfileName = profile.Name;

            foreach (var item in _monitors)
            {
                item.LoadProfileSetting(_originalProfile.MonitorSettings.FirstOrDefault(setting => MonitorIdComparer.Equal(item.Monitor.Id, setting.MonitorId)));
            }

            OnPropertyChanged(nameof(HasSelectedMonitors));
            OnPropertyChanged(nameof(HasValidSettings));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(HasPreservedSettings));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var monitor in _monitors)
                {
                    monitor.PropertyChanged -= OnMonitorItemPropertyChanged;
                    monitor.Dispose();
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static ProfileMonitorSetting CloneMonitorSetting(ProfileMonitorSetting setting)
            => new(setting.MonitorId, setting.Brightness, setting.ColorTemperatureVcp, setting.Contrast, setting.Volume);

        /// <summary>
        /// Handle property changes from monitor selection items.
        /// Centralizes validation state updates to avoid duplication.
        /// </summary>
        private void OnMonitorItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update selection-dependent properties
            if (e.PropertyName == nameof(MonitorSelectionItem.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelectedMonitors));
            }

            // Update validation state for relevant property changes
            if (e.PropertyName == nameof(MonitorSelectionItem.IsSelected) ||
                e.PropertyName == nameof(MonitorSelectionItem.HasValidSettings))
            {
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(HasValidSettings));
            }

            if (e.PropertyName == nameof(MonitorSelectionItem.IsSelected) ||
                e.PropertyName == nameof(MonitorSelectionItem.HasPreservedSettings))
            {
                OnPropertyChanged(nameof(HasPreservedSettings));
            }
        }
    }
}
