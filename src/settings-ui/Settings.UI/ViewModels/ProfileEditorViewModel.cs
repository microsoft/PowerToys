// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Profile Editor Dialog
    /// </summary>
    public class ProfileEditorViewModel : INotifyPropertyChanged
    {
        private string _profileName = string.Empty;
        private ObservableCollection<MonitorSelectionItem> _monitors;

        public ProfileEditorViewModel(ObservableCollection<MonitorInfo> availableMonitors, string defaultName = "")
        {
            _profileName = defaultName;
            _monitors = new ObservableCollection<MonitorSelectionItem>();

            // Initialize monitor selection items
            foreach (var monitor in availableMonitors)
            {
                var item = new MonitorSelectionItem
                {
                    SuppressAutoSelection = true,
                    Monitor = monitor,
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

        public ObservableCollection<MonitorSelectionItem> Monitors
        {
            get => _monitors;
            set
            {
                if (_monitors != value)
                {
                    _monitors = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasSelectedMonitors => _monitors?.Any(m => m.IsSelected) ?? false;

        public bool HasValidSettings => _monitors != null &&
            _monitors.Any(m => m.IsSelected) &&
            _monitors.Where(m => m.IsSelected).All(m => m.IncludeBrightness || m.IncludeContrast || m.IncludeVolume || m.IncludeColorTemperature);

        public bool CanSave => !string.IsNullOrWhiteSpace(_profileName) && HasSelectedMonitors && HasValidSettings;

        public PowerDisplayProfile CreateProfile()
        {
            var settings = _monitors
                .Where(m => m.IsSelected)
                .Select(m => new ProfileMonitorSetting(
                    m.Monitor.Id, // Monitor Id (unique identifier)
                    m.IncludeBrightness ? (int?)m.Brightness : null,
                    m.IncludeColorTemperature && m.SupportsColorTemperature ? (int?)m.ColorTemperature : null,
                    m.IncludeContrast && m.SupportsContrast ? (int?)m.Contrast : null,
                    m.IncludeVolume && m.SupportsVolume ? (int?)m.Volume : null))
                .ToList();

            return new PowerDisplayProfile(_profileName, settings);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
                e.PropertyName == nameof(MonitorSelectionItem.IncludeBrightness) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeContrast) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeVolume) ||
                e.PropertyName == nameof(MonitorSelectionItem.IncludeColorTemperature))
            {
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(HasValidSettings));
            }
        }
    }
}
