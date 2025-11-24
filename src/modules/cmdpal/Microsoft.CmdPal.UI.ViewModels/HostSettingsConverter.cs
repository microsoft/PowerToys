// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Provides conversion between SettingsModel and IHostSettings for extension communication.
/// </summary>
public static class HostSettingsConverter
{
    /// <summary>
    /// Converts a SettingsModel to an IHostSettings object that can be passed to extensions.
    /// </summary>
    /// <param name="settings">The settings model to convert.</param>
    /// <returns>An IHostSettings object with the current settings values.</returns>
    public static IHostSettings ToHostSettings(this SettingsModel settings)
    {
        return new HostSettings
        {
            Hotkey = settings.Hotkey?.ToString() ?? string.Empty,
            ShowAppDetails = settings.ShowAppDetails,
            HotkeyGoesHome = settings.HotkeyGoesHome,
            BackspaceGoesBack = settings.BackspaceGoesBack,
            SingleClickActivates = settings.SingleClickActivates,
            HighlightSearchOnActivate = settings.HighlightSearchOnActivate,
            ShowSystemTrayIcon = settings.ShowSystemTrayIcon,
            IgnoreShortcutWhenFullscreen = settings.IgnoreShortcutWhenFullscreen,
            DisableAnimations = settings.DisableAnimations,
            SummonOn = ConvertSummonTarget(settings.SummonOn),
        };
    }

    private static SummonTarget ConvertSummonTarget(MonitorBehavior behavior)
    {
        return behavior switch
        {
            MonitorBehavior.ToMouse => SummonTarget.ToMouse,
            MonitorBehavior.ToPrimary => SummonTarget.ToPrimary,
            MonitorBehavior.ToFocusedWindow => SummonTarget.ToFocusedWindow,
            MonitorBehavior.InPlace => SummonTarget.InPlace,
            MonitorBehavior.ToLast => SummonTarget.ToLast,
            _ => SummonTarget.ToMouse,
        };
    }
}
