// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog for creating/editing custom VCP value name mappings
    /// </summary>
    public sealed partial class CustomVcpMappingEditorDialog : ContentDialog, INotifyPropertyChanged
    {
        /// <summary>
        /// Special value to indicate "Custom value" option in the ComboBox
        /// </summary>
        private const int CustomValueMarker = -1;

        /// <summary>
        /// Represents a selectable VCP value item in the Value ComboBox
        /// </summary>
        public class VcpValueItem
        {
            public int Value { get; set; }

            public string DisplayName { get; set; } = string.Empty;

            public bool IsCustomOption => Value == CustomValueMarker;
        }

        /// <summary>
        /// Represents a selectable monitor item in the Monitor ComboBox
        /// </summary>
        public class MonitorItem
        {
            public string Id { get; set; } = string.Empty;

            public string DisplayName { get; set; } = string.Empty;
        }

        private readonly IEnumerable<MonitorInfo>? _monitors;
        private ObservableCollection<VcpValueItem> _availableValues = new();
        private ObservableCollection<MonitorItem> _availableMonitors = new();
        private byte _selectedVcpCode;
        private int _selectedValue;
        private string _customName = string.Empty;
        private bool _canSave;
        private bool _showCustomValueInput;
        private bool _showMonitorSelector;
        private int _customValueParsed;
        private bool _applyToAll = true;
        private string _selectedMonitorId = string.Empty;
        private string _selectedMonitorName = string.Empty;

        public CustomVcpMappingEditorDialog(IEnumerable<MonitorInfo>? monitors)
        {
            _monitors = monitors;
            this.InitializeComponent();

            // Set localized strings for ContentDialog
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_CustomMappingEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Save");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel");

            // Set VCP code ComboBox items content dynamically using localized names
            VcpCodeItem_0x14.Content = GetFormattedVcpCodeName(resourceLoader, 0x14);
            VcpCodeItem_0x60.Content = GetFormattedVcpCodeName(resourceLoader, 0x60);

            // Populate monitor list
            PopulateMonitorList();

            // Default to Color Temperature (0x14)
            VcpCodeComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Gets the result mapping after dialog closes with Primary button
        /// </summary>
        public CustomVcpValueMapping? ResultMapping { get; private set; }

        /// <summary>
        /// Gets the available values for the selected VCP code
        /// </summary>
        public ObservableCollection<VcpValueItem> AvailableValues
        {
            get => _availableValues;
            private set
            {
                _availableValues = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the available monitors for selection
        /// </summary>
        public ObservableCollection<MonitorItem> AvailableMonitors
        {
            get => _availableMonitors;
            private set
            {
                _availableMonitors = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the dialog can be saved
        /// </summary>
        public bool CanSave
        {
            get => _canSave;
            private set
            {
                if (_canSave != value)
                {
                    _canSave = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether to show the custom value input TextBox
        /// </summary>
        public Visibility ShowCustomValueInput => _showCustomValueInput ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Gets a value indicating whether to show the monitor selector ComboBox
        /// </summary>
        public Visibility ShowMonitorSelector => _showMonitorSelector ? Visibility.Visible : Visibility.Collapsed;

        private void SetShowCustomValueInput(bool value)
        {
            if (_showCustomValueInput != value)
            {
                _showCustomValueInput = value;
                OnPropertyChanged(nameof(ShowCustomValueInput));
            }
        }

        private void SetShowMonitorSelector(bool value)
        {
            if (_showMonitorSelector != value)
            {
                _showMonitorSelector = value;
                OnPropertyChanged(nameof(ShowMonitorSelector));
            }
        }

        private void PopulateMonitorList()
        {
            AvailableMonitors = new ObservableCollection<MonitorItem>(
                _monitors?.Select(m => new MonitorItem { Id = m.Id, DisplayName = m.DisplayName })
                ?? Enumerable.Empty<MonitorItem>());

            if (AvailableMonitors.Count > 0)
            {
                MonitorComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Pre-fill the dialog with existing mapping data for editing
        /// </summary>
        public void PreFillMapping(CustomVcpValueMapping mapping)
        {
            if (mapping is null)
            {
                return;
            }

            // Select the VCP code
            VcpCodeComboBox.SelectedIndex = mapping.VcpCode == 0x14 ? 0 : 1;

            // Populate values for the selected VCP code
            PopulateValuesForVcpCode(mapping.VcpCode);

            // Try to select the value in the ComboBox
            var matchingItem = AvailableValues.FirstOrDefault(v => !v.IsCustomOption && v.Value == mapping.Value);
            if (matchingItem is not null)
            {
                ValueComboBox.SelectedItem = matchingItem;
            }
            else
            {
                // Value not found in list, select "Custom value" option and fill the TextBox
                ValueComboBox.SelectedItem = AvailableValues.FirstOrDefault(v => v.IsCustomOption);
                CustomValueTextBox.Text = $"0x{mapping.Value:X2}";
                _customValueParsed = mapping.Value;
            }

            // Set the custom name
            CustomNameTextBox.Text = mapping.CustomName;
            _customName = mapping.CustomName;

            // Set apply scope
            _applyToAll = mapping.ApplyToAll;
            ApplyToAllToggle.IsOn = mapping.ApplyToAll;
            SetShowMonitorSelector(!mapping.ApplyToAll);

            // Select the target monitor if not applying to all
            if (!mapping.ApplyToAll && !string.IsNullOrEmpty(mapping.TargetMonitorId))
            {
                var targetMonitor = AvailableMonitors.FirstOrDefault(m => m.Id == mapping.TargetMonitorId);
                if (targetMonitor is not null)
                {
                    MonitorComboBox.SelectedItem = targetMonitor;
                    _selectedMonitorId = targetMonitor.Id;
                    _selectedMonitorName = targetMonitor.DisplayName;
                }
            }

            UpdateCanSave();
        }

        private void VcpCodeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VcpCodeComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string tagValue &&
                byte.TryParse(tagValue, out byte vcpCode))
            {
                _selectedVcpCode = vcpCode;
                PopulateValuesForVcpCode(vcpCode);
                UpdateCanSave();
            }
        }

        private void PopulateValuesForVcpCode(byte vcpCode)
        {
            var values = new ObservableCollection<VcpValueItem>();
            var seenValues = new HashSet<int>();

            // Collect values from all monitors
            if (_monitors is not null)
            {
                foreach (var monitor in _monitors)
                {
                    if (monitor.VcpCodesFormatted is null)
                    {
                        continue;
                    }

                    // Find the VCP code entry
                    var vcpEntry = monitor.VcpCodesFormatted.FirstOrDefault(v =>
                        !string.IsNullOrEmpty(v.Code) &&
                        TryParseHexCode(v.Code, out int code) &&
                        code == vcpCode);

                    if (vcpEntry?.ValueList is null)
                    {
                        continue;
                    }

                    // Add each value from this monitor
                    foreach (var valueInfo in vcpEntry.ValueList)
                    {
                        if (TryParseHexCode(valueInfo.Value, out int vcpValue) && !seenValues.Contains(vcpValue))
                        {
                            seenValues.Add(vcpValue);
                            var displayName = !string.IsNullOrEmpty(valueInfo.Name)
                                ? $"{valueInfo.Name} (0x{vcpValue:X2})"
                                : VcpNames.GetFormattedValueName(vcpCode, vcpValue);
                            values.Add(new VcpValueItem
                            {
                                Value = vcpValue,
                                DisplayName = displayName,
                            });
                        }
                    }
                }
            }

            // If no values found from monitors, fall back to built-in values from VcpNames
            if (values.Count == 0)
            {
                var builtInValues = VcpNames.GetValueMappings(vcpCode);
                if (builtInValues is not null)
                {
                    foreach (var kvp in builtInValues)
                    {
                        values.Add(new VcpValueItem
                        {
                            Value = kvp.Key,
                            DisplayName = $"{kvp.Value} (0x{kvp.Key:X2})",
                        });
                    }
                }
            }

            // Sort by value
            var sortedValues = new ObservableCollection<VcpValueItem>(values.OrderBy(v => v.Value));

            // Add "Custom value" option at the end
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            sortedValues.Add(new VcpValueItem
            {
                Value = CustomValueMarker,
                DisplayName = resourceLoader.GetString("PowerDisplay_CustomMappingEditor_CustomValueOption"),
            });

            AvailableValues = sortedValues;

            // Select first item if available
            if (sortedValues.Count > 0)
            {
                ValueComboBox.SelectedIndex = 0;
            }
        }

        private static bool TryParseHexCode(string? hex, out int result)
        {
            result = 0;
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            var cleanHex = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
            return int.TryParse(cleanHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        private static string GetFormattedVcpCodeName(Windows.ApplicationModel.Resources.ResourceLoader resourceLoader, byte vcpCode)
        {
            var resourceKey = $"PowerDisplay_VcpCode_Name_0x{vcpCode:X2}";
            var localizedName = resourceLoader.GetString(resourceKey);
            var name = string.IsNullOrEmpty(localizedName) ? VcpNames.GetCodeName(vcpCode) : localizedName;
            return $"{name} (0x{vcpCode:X2})";
        }

        private void ValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ValueComboBox.SelectedItem is VcpValueItem selectedItem)
            {
                SetShowCustomValueInput(selectedItem.IsCustomOption);
                _selectedValue = selectedItem.IsCustomOption ? 0 : selectedItem.Value;
                UpdateCanSave();
            }
        }

        private void CustomValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _customValueParsed = TryParseHexCode(CustomValueTextBox.Text?.Trim(), out int parsed) ? parsed : 0;
            UpdateCanSave();
        }

        private void CustomNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _customName = CustomNameTextBox.Text?.Trim() ?? string.Empty;
            UpdateCanSave();
        }

        private void ApplyToAllToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _applyToAll = ApplyToAllToggle.IsOn;
            SetShowMonitorSelector(!_applyToAll);
            UpdateCanSave();
        }

        private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonitorComboBox.SelectedItem is MonitorItem selectedMonitor)
            {
                _selectedMonitorId = selectedMonitor.Id;
                _selectedMonitorName = selectedMonitor.DisplayName;
                UpdateCanSave();
            }
        }

        private void UpdateCanSave()
        {
            var hasValidValue = _showCustomValueInput
                ? _customValueParsed > 0
                : ValueComboBox.SelectedItem is VcpValueItem item && !item.IsCustomOption;

            CanSave = _selectedVcpCode > 0 &&
                      hasValidValue &&
                      !string.IsNullOrWhiteSpace(_customName) &&
                      (_applyToAll || !string.IsNullOrEmpty(_selectedMonitorId));
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (CanSave)
            {
                int finalValue = _showCustomValueInput ? _customValueParsed : _selectedValue;
                ResultMapping = new CustomVcpValueMapping
                {
                    VcpCode = _selectedVcpCode,
                    Value = finalValue,
                    CustomName = _customName,
                    ApplyToAll = _applyToAll,
                    TargetMonitorId = _applyToAll ? string.Empty : _selectedMonitorId,
                    TargetMonitorName = _applyToAll ? string.Empty : _selectedMonitorName,
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
