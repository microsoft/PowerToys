// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class VcpValueBlockItem : INotifyPropertyChanged
    {
        private bool _isDisabled;

        public VcpValueBlockItem(int value, string name, bool isUserDisabled, bool isHardwareBlocked, string? hardwareBlockReason)
        {
            Value = value;
            var hexValue = $"0x{value:X2}";
            DisplayName = string.IsNullOrWhiteSpace(name) || string.Equals(name, hexValue, StringComparison.OrdinalIgnoreCase)
                ? hexValue
                : $"{name} ({hexValue})";
            IsHardwareBlocked = isHardwareBlocked;
            HardwareBlockReason = hardwareBlockReason ?? string.Empty;
            _isDisabled = isUserDisabled || isHardwareBlocked;
        }

        public int Value { get; }

        public string DisplayName { get; }

        public bool IsHardwareBlocked { get; }

        public string HardwareBlockReason { get; }

        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                if (!IsHardwareBlocked && _isDisabled != value)
                {
                    _isDisabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDisabled)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
