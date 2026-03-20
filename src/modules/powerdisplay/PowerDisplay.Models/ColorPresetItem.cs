// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Represents a color temperature preset item for VCP code 0x14.
    /// Used to display available color temperature presets in UI components.
    /// </summary>
    public partial class ColorPresetItem : INotifyPropertyChanged
    {
        private int _vcpValue;
        private string _displayName = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPresetItem"/> class.
        /// </summary>
        public ColorPresetItem()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPresetItem"/> class.
        /// </summary>
        /// <param name="vcpValue">The VCP value for the color temperature preset.</param>
        /// <param name="displayName">The display name for UI.</param>
        public ColorPresetItem(int vcpValue, string displayName)
        {
            _vcpValue = vcpValue;
            _displayName = displayName;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the VCP value for this color temperature preset.
        /// </summary>
        [JsonPropertyName("vcpValue")]
        public int VcpValue
        {
            get => _vcpValue;
            set
            {
                if (_vcpValue != value)
                {
                    _vcpValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the display name for UI.
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
