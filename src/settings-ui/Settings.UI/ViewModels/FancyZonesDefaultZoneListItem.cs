// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// One row of the FancyZones "default zone" settings list: the fallback zone (or zone range) a
    /// newly created window with no zone history falls back to on a specific layout. There is one
    /// of these per built-in template and per custom layout.
    /// </summary>
    public sealed class FancyZonesDefaultZoneListItem : INotifyPropertyChanged
    {
        private readonly System.Action<FancyZonesDefaultZoneListItem> _onChanged;

        private bool _isEnabled;
        private string _value = string.Empty;
        private bool _hasError;
        private string _errorMessage = string.Empty;

        public FancyZonesDefaultZoneListItem(string layoutId, bool isCustomLayout, string displayName, int zoneCount, List<int> initialZoneIndexSet, System.Action<FancyZonesDefaultZoneListItem> onChanged)
        {
            LayoutId = layoutId;
            IsCustomLayout = isCustomLayout;
            DisplayName = displayName;
            ZoneCount = zoneCount;
            _onChanged = onChanged;

            if (initialZoneIndexSet != null && initialZoneIndexSet.Count > 0)
            {
                _isEnabled = true;
                _value = FormatZoneIndexSet(initialZoneIndexSet);
                ZoneIndexSet = initialZoneIndexSet;
            }
        }

        // Template "type" (e.g. "grid") for a built-in layout, or the layout uuid for a custom one.
        public string LayoutId { get; }

        public bool IsCustomLayout { get; }

        public string DisplayName { get; set; }

        public int ZoneCount { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    Validate();
                }
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    Validate();
                }
            }
        }

        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The zero-based zone-index-set to persist, or null when the feature is off for this
        /// layout, or the current text does not validate. Never persisted while invalid.
        /// </summary>
        public List<int> ZoneIndexSet { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static string FormatZoneIndexSet(List<int> zoneIndexSet)
        {
            int first = zoneIndexSet[0] + 1;
            int last = zoneIndexSet[^1] + 1;
            return first == last ? first.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{first}-{last}";
        }

        private void Validate()
        {
            if (!IsEnabled)
            {
                HasError = false;
                ErrorMessage = string.Empty;
                ZoneIndexSet = null;
                _onChanged?.Invoke(this);
                return;
            }

            ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var result = FancyZonesDefaultZoneValidator.TryParse(Value, ZoneCount, out List<int> parsed);
            switch (result)
            {
                case FancyZonesDefaultZoneValidator.ValidationResult.Valid:
                    HasError = false;
                    ErrorMessage = string.Empty;
                    ZoneIndexSet = parsed;
                    break;
                case FancyZonesDefaultZoneValidator.ValidationResult.Empty:
                    // Toggled on but nothing typed yet: not an error, just not saved until filled in.
                    HasError = false;
                    ErrorMessage = string.Empty;
                    ZoneIndexSet = null;
                    break;
                case FancyZonesDefaultZoneValidator.ValidationResult.ReversedRange:
                    HasError = true;
                    ErrorMessage = resourceLoader.GetString("FancyZones_DefaultZone_Error_ReversedRange");
                    ZoneIndexSet = null;
                    break;
                case FancyZonesDefaultZoneValidator.ValidationResult.OutOfRange:
                    ErrorMessage = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        resourceLoader.GetString("FancyZones_DefaultZone_Error_OutOfRange"),
                        ZoneCount);
                    HasError = true;
                    ZoneIndexSet = null;
                    break;
                case FancyZonesDefaultZoneValidator.ValidationResult.NotANumber:
                default:
                    HasError = true;
                    ErrorMessage = resourceLoader.GetString("FancyZones_DefaultZone_Error_NotANumber");
                    ZoneIndexSet = null;
                    break;
            }

            _onChanged?.Invoke(this);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
