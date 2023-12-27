// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

public class MouseJumpPreviewStyle : INotifyPropertyChanged
{
    private MouseJumpCanvasSize? _canvasSize;
    private MouseJumpCanvasStyle? _canvasStyle;
    private MouseJumpScreenStyle? _screenStyle;

    public MouseJumpPreviewStyle()
    {
    }

    public MouseJumpPreviewStyle(
        MouseJumpCanvasSize? canvasSize,
        MouseJumpCanvasStyle? canvasStyle,
        MouseJumpScreenStyle? screenStyle)
    {
        this.CanvasSize = canvasSize;
        this.CanvasStyle = canvasStyle;
        this.ScreenStyle = screenStyle;
    }

    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpCanvasSize? CanvasSize
    {
        get => _canvasSize;
        set => PropertyChangedHelper.SetField(
            field: ref _canvasSize,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("canvas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpCanvasStyle? CanvasStyle
    {
        get => _canvasStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _canvasStyle,
            value: value,
            this.OnPropertyChanged);
    }

    [JsonPropertyName("screens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpScreenStyle? ScreenStyle
    {
        get => _screenStyle;
        set => PropertyChangedHelper.SetField(
            field: ref _screenStyle,
            value: value,
            this.OnPropertyChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
