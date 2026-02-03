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

        private readonly IEnumerable<MonitorInfo>? _monitors;
        private ObservableCollection<VcpValueItem> _availableValues = new();
        private byte _selectedVcpCode;
        private int _selectedValue;
        private string _customName = string.Empty;
        private bool _canSave;
        private bool _showCustomValueInput;
        private int _customValueParsed;

        public CustomVcpMappingEditorDialog()
            : this(null)
        {
        }

        public CustomVcpMappingEditorDialog(IEnumerable<MonitorInfo>? monitors)
        {
            _monitors = monitors;
            this.InitializeComponent();

            // Set localized strings for ContentDialog
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_CustomMappingEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Save");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel");

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
        public Visibility ShowCustomValueInput
        {
            get => _showCustomValueInput ? Visibility.Visible : Visibility.Collapsed;
            private set
            {
                var newValue = value == Visibility.Visible;
                if (_showCustomValueInput != newValue)
                {
                    _showCustomValueInput = newValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Pre-fill the dialog with existing mapping data for editing
        /// </summary>
        public void PreFillMapping(CustomVcpValueMapping mapping)
        {
            if (mapping == null)
            {
                return;
            }

            // Select the VCP code
            VcpCodeComboBox.SelectedIndex = mapping.VcpCode == 0x14 ? 0 : 1;

            // Populate values for the selected VCP code
            PopulateValuesForVcpCode(mapping.VcpCode);

            // Try to select the value in the ComboBox
            bool foundInList = false;
            foreach (var item in AvailableValues)
            {
                if (!item.IsCustomOption && item.Value == mapping.Value)
                {
                    ValueComboBox.SelectedItem = item;
                    foundInList = true;
                    break;
                }
            }

            // If value not found in list, select "Custom value" option and fill the TextBox
            if (!foundInList)
            {
                // Select the "Custom value" option (last item)
                var customOption = AvailableValues.FirstOrDefault(v => v.IsCustomOption);
                if (customOption != null)
                {
                    ValueComboBox.SelectedItem = customOption;
                }

                CustomValueTextBox.Text = $"0x{mapping.Value:X2}";
                _customValueParsed = mapping.Value;
            }

            // Set the custom name
            CustomNameTextBox.Text = mapping.CustomName;
            _customName = mapping.CustomName;

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
            if (_monitors != null)
            {
                foreach (var monitor in _monitors)
                {
                    if (monitor.VcpCodesFormatted == null)
                    {
                        continue;
                    }

                    // Find the VCP code entry
                    var vcpEntry = monitor.VcpCodesFormatted.FirstOrDefault(v =>
                        !string.IsNullOrEmpty(v.Code) &&
                        TryParseHexCode(v.Code, out int code) &&
                        code == vcpCode);

                    if (vcpEntry?.ValueList == null)
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

            // If no values found from monitors, fall back to built-in values
            if (values.Count == 0)
            {
                Dictionary<int, string> builtInValues = vcpCode switch
                {
                    0x14 => GetColorTemperatureValues(),
                    0x60 => GetInputSourceValues(),
                    _ => new Dictionary<int, string>(),
                };

                foreach (var kvp in builtInValues)
                {
                    values.Add(new VcpValueItem
                    {
                        Value = kvp.Key,
                        DisplayName = $"{kvp.Value} (0x{kvp.Key:X2})",
                    });
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

        private static Dictionary<int, string> GetColorTemperatureValues()
        {
            return new Dictionary<int, string>
            {
                { 0x01, "sRGB" },
                { 0x02, "Display Native" },
                { 0x03, "4000K" },
                { 0x04, "5000K" },
                { 0x05, "6500K" },
                { 0x06, "7500K" },
                { 0x07, "8200K" },
                { 0x08, "9300K" },
                { 0x09, "10000K" },
                { 0x0A, "11500K" },
                { 0x0B, "User 1" },
                { 0x0C, "User 2" },
                { 0x0D, "User 3" },
            };
        }

        private static Dictionary<int, string> GetInputSourceValues()
        {
            return new Dictionary<int, string>
            {
                { 0x01, "VGA-1" },
                { 0x02, "VGA-2" },
                { 0x03, "DVI-1" },
                { 0x04, "DVI-2" },
                { 0x05, "Composite Video 1" },
                { 0x06, "Composite Video 2" },
                { 0x07, "S-Video-1" },
                { 0x08, "S-Video-2" },
                { 0x09, "Tuner-1" },
                { 0x0A, "Tuner-2" },
                { 0x0B, "Tuner-3" },
                { 0x0C, "Component Video 1" },
                { 0x0D, "Component Video 2" },
                { 0x0E, "Component Video 3" },
                { 0x0F, "DisplayPort-1" },
                { 0x10, "DisplayPort-2" },
                { 0x11, "HDMI-1" },
                { 0x12, "HDMI-2" },
                { 0x1B, "USB-C" },
            };
        }

        private void ValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ValueComboBox.SelectedItem is VcpValueItem selectedItem)
            {
                if (selectedItem.IsCustomOption)
                {
                    // Show custom value input
                    ShowCustomValueInput = Visibility.Visible;
                    _selectedValue = 0; // Will be set from TextBox
                }
                else
                {
                    // Hide custom value input and use selected value
                    ShowCustomValueInput = Visibility.Collapsed;
                    _selectedValue = selectedItem.Value;
                }

                UpdateCanSave();
            }
        }

        private void CustomValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = CustomValueTextBox.Text?.Trim() ?? string.Empty;
            if (TryParseHexCode(text, out int parsed))
            {
                _customValueParsed = parsed;
            }
            else
            {
                _customValueParsed = 0;
            }

            UpdateCanSave();
        }

        private void CustomNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _customName = CustomNameTextBox.Text?.Trim() ?? string.Empty;
            UpdateCanSave();
        }

        private void UpdateCanSave()
        {
            bool hasValidValue;
            if (_showCustomValueInput)
            {
                hasValidValue = _customValueParsed > 0;
            }
            else
            {
                hasValidValue = ValueComboBox.SelectedItem is VcpValueItem item && !item.IsCustomOption;
            }

            CanSave = _selectedVcpCode > 0 &&
                      hasValidValue &&
                      !string.IsNullOrWhiteSpace(_customName);
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
                };
            }
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ResultMapping = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
