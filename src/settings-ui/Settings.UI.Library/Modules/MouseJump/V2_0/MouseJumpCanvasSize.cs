// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

public sealed class MouseJumpCanvasSize : INotifyPropertyChanged
{
    private int? _width;
    private int? _height;

    public MouseJumpCanvasSize()
    {
    }

    public MouseJumpCanvasSize(
        int? width,
        int? height)
    {
        this.Width = width;
        this.Height = height;
    }

    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width
    {
        get => _width;
        set => PropertyChangedHelper.SetField(
            field: ref _width,
            value: value.HasValue ? Math.Clamp(value.Value, 50, 99999) : null,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height
    {
        get => _height;
        set => PropertyChangedHelper.SetField(
            field: ref _height,
            value: value.HasValue ? Math.Clamp(value.Value, 50, 99999) : null,
            this.OnPropertyChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
