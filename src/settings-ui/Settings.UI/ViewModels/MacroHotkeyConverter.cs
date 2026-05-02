// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

/// <summary>
/// Converts between <see cref="HotkeySettings"/> (Settings.UI layer) and
/// <see cref="MacroHotkeySettings"/> (MacroCommon model layer).
/// </summary>
internal static class MacroHotkeyConverter
{
    /// <summary>
    /// Converts a <see cref="HotkeySettings"/> to a <see cref="MacroHotkeySettings"/>.
    /// Returns <c>null</c> when <paramref name="hs"/> is null or has no key assigned (Code == 0).
    /// </summary>
    public static MacroHotkeySettings? ToMacroHotkeySettings(HotkeySettings? hs)
    {
        if (hs is null || hs.Code == 0)
        {
            return null;
        }

        return new MacroHotkeySettings(hs.Win, hs.Ctrl, hs.Alt, hs.Shift, hs.Code);
    }

    /// <summary>
    /// Converts a <see cref="MacroHotkeySettings"/> to a <see cref="HotkeySettings"/>.
    /// Returns <c>null</c> when <paramref name="mhs"/> is null.
    /// </summary>
    public static HotkeySettings? ToHotkeySettings(MacroHotkeySettings? mhs)
    {
        if (mhs is null)
        {
            return null;
        }

        return new HotkeySettings(mhs.Win, mhs.Ctrl, mhs.Alt, mhs.Shift, mhs.Code);
    }
}
