// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V1_0
{
    public class MouseJumpThumbnailSize : INotifyPropertyChanged
    {
        private int? _width;
        private int? _height;

        [JsonPropertyName("width")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Width
        {
            get => _width;
            set => this.SetField(
                field: ref _width,
                value: value.HasValue ? Math.Clamp(value.Value, 0, 99999) : null);
        }

        [JsonPropertyName("height")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Height
        {
            get => _height;
            set => this.SetField(
                field: ref _height,
                value: value.HasValue ? Math.Clamp(value.Value, 0, 99999) : null);
        }

        public MouseJumpThumbnailSize()
        {
            Width = 1600;
            Height = 1200;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }
    }
}
