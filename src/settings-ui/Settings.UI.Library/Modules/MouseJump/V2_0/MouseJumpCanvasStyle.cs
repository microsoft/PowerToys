// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

/// <remarks>
/// Doesn't have a MarginStyle setting like the BoxStyle class does - we don't
/// support configuring this in app settings.
/// ></remarks>
public sealed class MouseJumpCanvasStyle : INotifyPropertyChanged
{
    private MouseJumpBorderStyle? _borderStyle;
    private MouseJumpPaddingStyle? _paddingStyle;
    private MouseJumpBackgroundStyle? _backgroundStyle;

    public MouseJumpCanvasStyle(
        MouseJumpBorderStyle? borderStyle,
        MouseJumpPaddingStyle? paddingStyle,
        MouseJumpBackgroundStyle? backgroundStyle)
    {
        this.BorderStyle = borderStyle;
        this.PaddingStyle = paddingStyle;
        this.BackgroundStyle = backgroundStyle;
    }

    [JsonPropertyName("border")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpBorderStyle? BorderStyle
    {
        get => _borderStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _borderStyle,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("padding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpPaddingStyle? PaddingStyle
    {
        get => _paddingStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _paddingStyle,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("background")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpBackgroundStyle? BackgroundStyle
    {
        get => _backgroundStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _backgroundStyle,
            value: value,
            this.OnPropertyChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
