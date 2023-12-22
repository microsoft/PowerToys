// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

/// <summary>
/// Represents the border style for a drawing object.
/// </summary>
public sealed class MouseJumpBorderStyle : INotifyPropertyChanged
{
    private Color? _color;
    private int? _width;
    private int? _depth;

    public MouseJumpBorderStyle(Color? color, int? width, int? depth)
    {
        this.Color = color;
        this.Width = width;
        this.Depth = depth;
    }

    [JsonPropertyName("color")]
    [JsonConverter(typeof(MouseJumpColorConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Color? Color
    {
        get => _color;
        set => PropertyChangedHelper.SetField(
            field: ref _color,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width
    {
        get => _width;
        set => PropertyChangedHelper.SetField(
            field: ref _width,
            value: value.HasValue ? Math.Clamp(value.Value, 0, 99) : null,
            this.OnPropertyChanged);
    }

    /// <summary>
    /// Gets or sets the "depth" of the 3d highlight and shadow effect on the border.
    /// </summary>
    [JsonPropertyName("depth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth
    {
        get => _depth;
        set => PropertyChangedHelper.SetField(
            field: ref _depth,
            value: value.HasValue ? Math.Clamp(value.Value, 0, 99) : null,
            this.OnPropertyChanged);
    }

    public override string ToString()
    {
        return "{" +
           $"{nameof(this.Color)}={this.Color}," +
           $"{nameof(this.Width)}={this.Width}," +
           $"{nameof(this.Depth)}={this.Depth}" +
           "}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
