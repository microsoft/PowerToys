// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Converts configured modifiers and validates native WM_HOTKEY messages.
/// </summary>
internal static class HotkeyWindowMessageHelper
{
    /// <summary>Maps configured modifiers to Win32 hotkey flags.</summary>
    /// <param name="key">Shortcut whose modifier keys are converted.</param>
    /// <returns>The combined Win32 modifier flags.</returns>
    public static HOT_KEY_MODIFIERS GetModifiers(HotkeySettings key) =>
        (key.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0)
        | (key.Ctrl ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0)
        | (key.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0)
        | (key.Win ? HOT_KEY_MODIFIERS.MOD_WIN : 0);

    /// <summary>Resolves a message only when its ID, virtual-key code, and modifiers match a registered binding.</summary>
    /// <param name="hotkeys">Active native bindings indexed by registration ID.</param>
    /// <param name="id">Registration ID from WPARAM.</param>
    /// <param name="data">LPARAM with modifiers in the low word and the virtual-key code in the high word.</param>
    /// <returns>The matching command ID, or <see langword="null"/> for an unknown ID or mismatched payload.</returns>
    public static string? GetCommand(IReadOnlyDictionary<int, TopLevelHotkey> hotkeys, int id, nint data)
    {
        if (hotkeys.TryGetValue(id, out var binding)
            && binding.Hotkey is { } key
            && (data & 0xFFFF) == (int)GetModifiers(key)
            && ((data >> 16) & 0xFFFF) == key.Code)
        {
            return binding.CommandId;
        }

        return null;
    }
}
