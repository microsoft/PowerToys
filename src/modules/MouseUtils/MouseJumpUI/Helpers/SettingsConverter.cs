// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;
using MouseJumpUI.Models.Styles;

namespace MouseJumpUI.Helpers;

internal static class SettingsConverter
{
    public static PreviewStyle ConvertToPreviewStyle(
        MouseJumpPreviewStyle? previewStyle,
        MouseJumpPreviewStyle? defaultStyle)
    {
        return new PreviewStyle(
            canvasSize: new(
                width: SettingsConverter.ConvertToInt(
                    value: previewStyle?.CanvasSize?.Width,
                    defaultStyle?.CanvasSize?.Width),
                height: SettingsConverter.ConvertToInt(
                    value: previewStyle?.CanvasSize?.Height,
                    defaultValue: defaultStyle?.CanvasSize?.Height)
            ),
            canvasStyle: new(
                marginStyle: MarginStyle.Empty,
                borderStyle: SettingsConverter.ConvertToBorderStyle(
                    borderStyle: previewStyle?.CanvasStyle?.BorderStyle,
                    defaultStyle: defaultStyle?.CanvasStyle?.BorderStyle),
                paddingStyle: new(
                    all: previewStyle?.CanvasStyle?.PaddingStyle?.Width
                        ?? defaultStyle?.CanvasStyle?.PaddingStyle?.Width
                        ?? throw new InvalidOperationException()),
                backgroundStyle: SettingsConverter.ConvertToBackgroundStyle(
                    backgroundStyle: previewStyle?.CanvasStyle?.BackgroundStyle,
                    defaultStyle: defaultStyle?.CanvasStyle?.BackgroundStyle)
            ),
            screenStyle: new(
                marginStyle: new(
                    all: previewStyle?.ScreenStyle?.MarginStyle?.Width
                        ?? defaultStyle?.ScreenStyle?.MarginStyle?.Width
                        ?? throw new InvalidOperationException()),
                borderStyle: SettingsConverter.ConvertToBorderStyle(
                    borderStyle: previewStyle?.ScreenStyle?.BorderStyle,
                    defaultStyle: defaultStyle?.ScreenStyle?.BorderStyle),
                paddingStyle: PaddingStyle.Empty,
                backgroundStyle: SettingsConverter.ConvertToBackgroundStyle(
                    backgroundStyle: previewStyle?.ScreenStyle?.BackgroundStyle,
                    defaultStyle: defaultStyle?.ScreenStyle?.BackgroundStyle)
            ));
    }

    private static int ConvertToInt(
        int? value,
        int? defaultValue)
    {
        return value ?? defaultValue ?? throw new InvalidOperationException();
    }

    private static BorderStyle ConvertToBorderStyle(
        MouseJumpBorderStyle? borderStyle,
        MouseJumpBorderStyle? defaultStyle)
    {
        return new(
            color: SettingsConverter.ConvertToColor(
                color: borderStyle?.Color,
                defaultColor: defaultStyle?.Color),
            all: SettingsConverter.ConvertToInt(
                value: borderStyle?.Width,
                defaultValue: defaultStyle?.Width),
            depth: SettingsConverter.ConvertToInt(
                value: borderStyle?.Depth,
                defaultValue: defaultStyle?.Depth)
        );
    }

    private static BackgroundStyle ConvertToBackgroundStyle(
        MouseJumpBackgroundStyle? backgroundStyle,
        MouseJumpBackgroundStyle? defaultStyle)
    {
        return new(
            color1: SettingsConverter.ConvertToColor(
                color: backgroundStyle?.Color1,
                defaultColor: defaultStyle?.Color1),
            color2: SettingsConverter.ConvertToColor(
                color: backgroundStyle?.Color2,
                defaultColor: defaultStyle?.Color2)
        );
    }

    private static Color ConvertToColor(
        Color? color,
        Color? defaultColor)
    {
        return color ?? defaultColor ?? throw new InvalidOperationException();
    }
}
