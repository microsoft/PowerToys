// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Common.UI;

namespace PowerToysExtension.Helpers;

internal static class AwakeStatusService
{
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    internal static AwakeStatusSnapshot GetSnapshot()
    {
        var isRunning = IsAwakeProcessRunning();
        var settings = ReadSettings();

        if (settings is null)
        {
            return new AwakeStatusSnapshot(isRunning, AwakeMode.Passive, false, null, null);
        }

        var mode = Enum.IsDefined(typeof(AwakeMode), settings.Properties.Mode)
            ? (AwakeMode)settings.Properties.Mode
            : AwakeMode.Passive;

        TimeSpan? duration = null;
        DateTimeOffset? expiration = null;

        switch (mode)
        {
            case AwakeMode.Timed:
                duration = TimeSpan.FromHours(settings.Properties.IntervalHours) + TimeSpan.FromMinutes(settings.Properties.IntervalMinutes);
                break;
            case AwakeMode.Expirable:
                expiration = settings.Properties.ExpirationDateTime;
                break;
        }

        return new AwakeStatusSnapshot(isRunning, mode, settings.Properties.KeepDisplayOn, duration, expiration);
    }

    internal static string GetStatusSubtitle()
    {
        var snapshot = GetSnapshot();
        if (!snapshot.IsRunning)
        {
            return "Awake is idle";
        }

        if (snapshot.Mode == AwakeMode.Passive)
        {
            // When the PowerToys Awake module is enabled, the Awake process stays resident
            // even in passive mode. In that case "idle" is correct. If the module is disabled,
            // a running process implies a standalone/session keep-awake, so report as active.
            return ModuleEnablementService.IsModuleEnabled(SettingsDeepLink.SettingsWindow.Awake)
                ? "Awake is idle"
                : "Active - session running";
        }

        return snapshot.Mode switch
        {
            AwakeMode.Indefinite => snapshot.KeepDisplayOn ? "Active - indefinite (display on)" : "Active - indefinite",
            AwakeMode.Timed => snapshot.Duration is { } span
                ? $"Active - timer {FormatDuration(span)}"
                : "Active - timer",
            AwakeMode.Expirable => snapshot.Expiration is { } expiry
                ? $"Active - until {expiry.ToLocalTime():t}"
                : "Active - scheduled",
            _ => "Awake is running",
        };
    }

    internal static IReadOnlyList<AwakeCustomPreset> ReadCustomPresets()
    {
        var settings = ReadSettings();
        if (settings?.Properties?.CustomTrayTimes is null || settings.Properties.CustomTrayTimes.Count == 0)
        {
            return Array.Empty<AwakeCustomPreset>();
        }

        var presets = new List<AwakeCustomPreset>();
        foreach (var kvp in settings.Properties.CustomTrayTimes)
        {
            try
            {
                var minutes = kvp.Value;
                if (minutes <= 0)
                {
                    continue;
                }

                var span = TimeSpan.FromMinutes(minutes);
                presets.Add(new AwakeCustomPreset(kvp.Key, span));
            }
            catch
            {
                // Ignore malformed entries
            }
        }

        return presets;
    }

    private static bool IsAwakeProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("PowerToys.Awake").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static AwakeSettingsDocument? ReadSettings()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                return null;
            }

            var path = Path.Combine(appData, "Microsoft", "PowerToys", "Awake", SettingsFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, PowerToysJsonContext.Default.AwakeSettingsDocument);
        }
        catch
        {
            return null;
        }
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

internal enum AwakeMode
{
    Passive = 0,
    Indefinite = 1,
    Timed = 2,
    Expirable = 3,
}

internal readonly record struct AwakeStatusSnapshot(bool IsRunning, AwakeMode Mode, bool KeepDisplayOn, TimeSpan? Duration, DateTimeOffset? Expiration);

internal readonly record struct AwakeCustomPreset(string Name, TimeSpan Duration);
