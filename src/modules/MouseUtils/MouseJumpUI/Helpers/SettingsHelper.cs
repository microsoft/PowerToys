// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using MouseJumpUI.HotKeys;

namespace MouseJumpUI.Helpers;

internal static class SettingsHelper
{
    public static Keystroke ConvertToKeystroke(HotkeySettings shortcut)
    {
        var modifiers =
            (shortcut.Win ? KeyModifiers.Windows : KeyModifiers.None) |
            (shortcut.Ctrl ? KeyModifiers.Control : KeyModifiers.None) |
            (shortcut.Alt ? KeyModifiers.Alt : KeyModifiers.None) |
            (shortcut.Shift ? KeyModifiers.Shift : KeyModifiers.None);
        return new Keystroke(
            key: (Keys)shortcut.Code,
            modifiers: modifiers);
    }
}
