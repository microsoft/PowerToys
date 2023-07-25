// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MouseJumpUI.HotKeys;
using MouseJumpUI.Models.Styles;

namespace MouseJumpUI.Models.Settings.V1;

internal static class SettingsConverter
{
    public static AppSettings ParseAppSettings(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };
        var appConfig = JsonSerializer.Deserialize<AppConfig>(json, options)
            ?? throw new InvalidOperationException();
        var hotkey = SettingsConverter.ConvertToKeystroke(appConfig.Properties?.ActivationShortcut);
        var previewStyle = SettingsConverter.ConvertToPreviewStyle(appConfig.Properties?.ThumbnailSize);
        var appSettings = new AppSettings(hotkey, previewStyle);
        return appSettings;
    }

    public static Keystroke ConvertToKeystroke(ActivationShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return AppSettings.DefaultSettings.Hotkey;
        }

        var key = (Keys)(shortcut.Code ?? (int)AppSettings.DefaultSettings.Hotkey.Key);
        var modifiers =
            (shortcut.Win ?? false ? KeyModifiers.Windows : KeyModifiers.None) |
            (shortcut.Ctrl ?? false ? KeyModifiers.Control : KeyModifiers.None) |
            (shortcut.Alt ?? false ? KeyModifiers.Alt : KeyModifiers.None) |
            (shortcut.Shift ?? false ? KeyModifiers.Shift : KeyModifiers.None);
        return new Keystroke(
            key: key,
            modifiers: modifiers);
    }

    public static PreviewStyle ConvertToPreviewStyle(CanvasSizeSettings? thumbnailSize)
    {
        return thumbnailSize is null
            ? AppSettings.DefaultSettings.PreviewStyle
            : new PreviewStyle(
                canvasSize: new(
                width: thumbnailSize.Width,
                height: thumbnailSize.Height));
    }
}
