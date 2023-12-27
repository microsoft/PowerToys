// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

/// <remarks>
/// Doesn't have a PaddingStyle setting like the BoxStyle class does - we don't
/// support configuring this in app settings.
/// ></remarks>
public sealed class MouseJumpScreenStyle : INotifyPropertyChanged
{
    private MouseJumpMarginStyle? _marginStyle;
    private MouseJumpBorderStyle? _borderStyle;
    private MouseJumpBackgroundStyle? _backgroundStyle;

    public MouseJumpScreenStyle(
        MouseJumpMarginStyle? marginStyle,
        MouseJumpBorderStyle? borderStyle,
        MouseJumpBackgroundStyle? backgroundStyle)
    {
        this.MarginStyle = marginStyle;
        this.BorderStyle = borderStyle;
        this.BackgroundStyle = backgroundStyle;
    }

    [JsonPropertyName("margin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpMarginStyle? MarginStyle
    {
        get => _marginStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _marginStyle,
            value: value,
            this.OnPropertyChanged);
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
