// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.ModuleServices;
using Common.UI;

namespace PowerToysExtension.Helpers;

internal static class AwakeStatusService
{
    internal static string GetStatusSubtitle()
    {
        var state = AwakeService.Instance.GetCurrentState();
        if (!state.IsRunning)
        {
            return "Awake is idle";
        }

        if (state.Mode == AwakeStateMode.Passive)
        {
            // When the PowerToys Awake module is enabled, the Awake process stays resident
            // even in passive mode. In that case "idle" is correct. If the module is disabled,
            // a running process implies a standalone/session keep-awake, so report as active.
            return ModuleEnablementService.IsModuleEnabled(SettingsDeepLink.SettingsWindow.Awake)
                ? "Awake is idle"
                : "Active - session running";
        }

        return state.Mode switch
        {
            AwakeStateMode.Indefinite => "Active - indefinite",
            AwakeStateMode.Timed => state.Duration is { } span
                ? $"Active - timer {FormatDuration(span)}"
                : "Active - timer",
            AwakeStateMode.Expirable => state.Expiration is { } expiry
                ? $"Active - until {expiry.ToLocalTime():t}"
                : "Active - scheduled",
            _ => "Awake is running",
        };
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(span.TotalHours);
            var minutes = span.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        if (span.TotalMinutes >= 1)
        {
            return $"{(int)Math.Round(span.TotalMinutes)}m";
        }

        return span.TotalSeconds >= 1 ? $"{(int)Math.Round(span.TotalSeconds)}s" : "\u2014";
    }
}
