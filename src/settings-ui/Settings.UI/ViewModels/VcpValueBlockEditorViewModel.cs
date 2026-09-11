// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Edits a snapshot of one monitor's disabled options without changing its settings until saved.
    /// </summary>
    public class VcpValueBlockEditorViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<byte, HashSet<int>> _originalBlocks;
        private VcpCodeBlockItem? _selectedCode;

        public VcpValueBlockEditorViewModel(MonitorInfo monitor)
        {
            MonitorName = monitor.DisplayName;
            _originalBlocks = monitor.DisabledVcpValues
                .Where(block => block != null)
                .GroupBy(block => block.VcpCode)
                .ToDictionary(group => group.Key, group => group.SelectMany(block => block.Values).ToHashSet());

            var codes = new List<VcpCodeBlockItem>();
            foreach (var entry in monitor.VcpCodesFormatted)
            {
                if (entry is null || !TryParseHex(entry.Code, byte.MaxValue, out int code) || entry.ValueList is null)
                {
                    continue;
                }

                byte vcpCode = (byte)code;
                var values = new List<VcpValueBlockItem>();
                var seenValues = new HashSet<int>();
                foreach (var valueInfo in entry.ValueList)
                {
                    if (valueInfo is null || !TryParseHex(valueInfo.Value, ushort.MaxValue, out int value) || !seenValues.Add(value))
                    {
                        continue;
                    }

                    bool isUserDisabled = _originalBlocks.TryGetValue(vcpCode, out var blockedValues) && blockedValues.Contains(value);
                    values.Add(new VcpValueBlockItem(
                        value,
                        valueInfo.Name,
                        isUserDisabled,
                        VcpValueRestrictions.IsBlockedByHardware(monitor.Id, vcpCode, value),
                        VcpValueRestrictions.GetHardwareBlockReason(monitor.Id, vcpCode, value)));
                }

                if (values.Count > 0)
                {
                    codes.Add(new VcpCodeBlockItem(vcpCode, entry.Title, values));
                }
            }

            AvailableCodes = codes;
            _selectedCode = codes.FirstOrDefault();
        }

        public string MonitorName { get; }

        public IReadOnlyList<VcpCodeBlockItem> AvailableCodes { get; }

        public bool HasOptions => AvailableCodes.Count > 0;

        public VcpCodeBlockItem? SelectedCode
        {
            get => _selectedCode;
            set
            {
                if (_selectedCode != value)
                {
                    _selectedCode = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCode)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableValues)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHardwareBlocks)));
                }
            }
        }

        public IReadOnlyList<VcpValueBlockItem> AvailableValues => SelectedCode?.Values ?? Array.Empty<VcpValueBlockItem>();

        public bool HasHardwareBlocks => AvailableValues.Any(value => value.IsHardwareBlocked);

        /// <summary>
        /// Creates numeric settings, retaining rules that are absent from the current capabilities.
        /// Hardware restrictions remain independent of user settings.
        /// </summary>
        public List<VcpValueBlock> CreateValueBlocks()
        {
            var result = _originalBlocks.ToDictionary(pair => pair.Key, pair => new HashSet<int>(pair.Value));
            foreach (var code in AvailableCodes)
            {
                if (!result.TryGetValue(code.Code, out var values))
                {
                    values = new HashSet<int>();
                    result.Add(code.Code, values);
                }

                foreach (var option in code.Values.Where(value => !value.IsHardwareBlocked))
                {
                    if (option.IsDisabled)
                    {
                        values.Add(option.Value);
                    }
                    else
                    {
                        values.Remove(option.Value);
                    }
                }
            }

            return result
                .Where(pair => pair.Value.Count > 0)
                .OrderBy(pair => pair.Key)
                .Select(pair => new VcpValueBlock { VcpCode = pair.Key, Values = pair.Value.OrderBy(value => value).ToList() })
                .ToList();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static bool TryParseHex(string? text, int maximum, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= maximum;
        }
    }
}
