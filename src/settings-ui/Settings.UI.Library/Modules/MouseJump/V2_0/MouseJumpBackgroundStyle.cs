// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using System.Drawing;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

/// <summary>
/// Represents the background fill style for a drawing object.
/// </summary>
public sealed class MouseJumpBackgroundStyle : INotifyPropertyChanged
{
    private Color? _color1;
    private Color? _color2;

    public MouseJumpBackgroundStyle(
        Color? color1,
        Color? color2)
    {
        this.Color1 = color1;
        this.Color2 = color2;
    }

    [JsonPropertyName("color1")]
    [JsonConverter(typeof(MouseJumpColorConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Color? Color1
    {
        get => _color1;
        set => PropertyChangedHelper.SetField(
            field: ref _color1,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("color2")]
    [JsonConverter(typeof(MouseJumpColorConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Color? Color2
    {
        get => _color2;
        set => PropertyChangedHelper.SetField(
            field: ref _color2,
            value: value,
            this.OnPropertyChanged);
    }

    public override string ToString()
    {
        return "{" +
           $"{nameof(this.Color1)}={this.Color1}," +
           $"{nameof(this.Color2)}={this.Color2}" +
           "}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
