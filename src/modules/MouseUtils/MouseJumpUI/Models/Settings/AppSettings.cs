// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.HotKeys;
using MouseJumpUI.Models.Styles;
using Keys = MouseJumpUI.HotKeys.Keys;

namespace MouseJumpUI.Models.Settings;

/// <summary>
/// Represents the settings used to control application behaviour.
/// This is different to the AppConfig class that is used to
/// serialize / deserialize settings into the application config file.
/// </summary>
internal sealed class AppSettings
{
    public static readonly AppSettings DefaultSettings = new(
        hotkey: new(
            key: Keys.F,
            modifiers: KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift
        ),
        previewStyle: new(
            canvasSize: new(
                width: 1600,
                height: 1200
            )
        )
    );

    public AppSettings(
        Keystroke hotkey,
        PreviewStyle previewStyle)
    {
        this.Hotkey = hotkey ?? throw new ArgumentNullException(nameof(hotkey));
        this.PreviewStyle = previewStyle ?? throw new ArgumentNullException(nameof(previewStyle));
    }

    public Keystroke Hotkey
    {
        get;
    }

    public PreviewStyle PreviewStyle
    {
        get;
    }
}
