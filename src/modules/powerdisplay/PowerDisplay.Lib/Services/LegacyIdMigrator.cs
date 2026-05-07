// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Migrates monitor identifiers from the legacy "{Source}_{EdidId}_{MonitorNumber}"
/// format (e.g., "DDC_DELD1A8_1") to the new DevicePath-derived form (e.g.,
/// "\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1"). Pure mapping logic only — file I/O lives elsewhere.
/// </summary>
public static partial class LegacyIdMigrator
{
    [GeneratedRegex(@"^(?:DDC|WMI)_(?<edid>[A-Za-z0-9]+)_(?<num>\d+)$", RegexOptions.Compiled)]
    private static partial Regex LegacyIdRegex();

    /// <summary>
    /// Try to parse a legacy-format monitor Id. Returns false if the input is null,
    /// empty, or does not match the legacy pattern (e.g., already in new format).
    /// </summary>
    public static bool TryParseLegacyId(string? id, out string edidId, out int monitorNumber)
    {
        edidId = string.Empty;
        monitorNumber = 0;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var match = LegacyIdRegex().Match(id);
        if (!match.Success)
        {
            return false;
        }

        edidId = match.Groups["edid"].Value;
        return int.TryParse(match.Groups["num"].Value, out monitorNumber);
    }

    /// <summary>
    /// Try to map a legacy-format Id to its new-format equivalent using a
    /// (EdidId, MonitorNumber) → newId lookup. Returns false (and leaves <paramref name="newId"/>
    /// equal to the input) when the input is already in new format or the lookup misses.
    /// </summary>
    public static bool TryMapLegacyId(
        string oldId,
        IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> lookup,
        out string newId)
    {
        newId = oldId;
        if (!TryParseLegacyId(oldId, out var edid, out var num))
        {
            return false;
        }

        if (lookup.TryGetValue((edid, num), out var mapped))
        {
            newId = mapped;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Build a (EdidId, MonitorNumber) → newId lookup from the currently-discovered monitors.
    /// Skips monitors whose Id is still in legacy format (e.g., during a transition window).
    /// </summary>
    public static IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> BuildLookup(
        IReadOnlyList<PowerDisplay.Common.Models.Monitor> currentMonitors)
    {
        var dict = new Dictionary<(string, int), string>(currentMonitors.Count);
        foreach (var m in currentMonitors)
        {
            if (PowerDisplay.Common.Models.MonitorIdentity.TryGetEdidId(m.Id, out var edid))
            {
                dict[(edid, m.MonitorNumber)] = m.Id;
            }
        }

        return dict;
    }

    /// <summary>
    /// Run a single migration pass across all PowerDisplay persistence surfaces.
    /// Idempotent — safe to call after every discovery cycle.
    /// </summary>
    /// <param name="currentMonitors">Currently-discovered monitors (with new-format Ids).</param>
    /// <param name="settingsUtils">Settings persistence (used to read/write settings.json).</param>
    /// <param name="settingsSerializer">Function that serializes <see cref="Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings"/> to JSON. Caller provides this with their source-generated context.</param>
    /// <param name="stateManager">Monitor state manager (state persistence).</param>
    /// <returns>Total number of entries rewritten across all files.</returns>
    public static int Migrate(
        IReadOnlyList<PowerDisplay.Common.Models.Monitor> currentMonitors,
        Microsoft.PowerToys.Settings.UI.Library.SettingsUtils settingsUtils,
        System.Func<Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings, string> settingsSerializer,
        MonitorStateManager stateManager)
    {
        var lookup = BuildLookup(currentMonitors);
        if (lookup.Count == 0)
        {
            return 0;
        }

        int total = 0;
        total += MigrateSettings(settingsUtils, settingsSerializer, lookup);
        total += MigrateMonitorState(stateManager, lookup);
        total += MigrateProfiles(lookup);

        if (total > 0)
        {
            ManagedCommon.Logger.LogInfo($"[LegacyIdMigrator] Migrated {total} entries to new Id format");
        }

        return total;
    }

    private static int MigrateSettings(
        Microsoft.PowerToys.Settings.UI.Library.SettingsUtils settingsUtils,
        System.Func<Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings, string> settingsSerializer,
        IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> lookup)
    {
        var settings = settingsUtils.GetSettingsOrDefault<Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings>(
            Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings.ModuleName);

        int n = 0;
        foreach (var monitor in settings.Properties.Monitors)
        {
            if (TryMapLegacyId(monitor.Id, lookup, out var newId))
            {
                monitor.Id = newId;
                n++;
            }
        }

        if (settings.Properties.CustomVcpMappings != null)
        {
            foreach (var mapping in settings.Properties.CustomVcpMappings)
            {
                if (TryMapLegacyId(mapping.TargetMonitorId ?? string.Empty, lookup, out var newId))
                {
                    mapping.TargetMonitorId = newId;
                    n++;
                }
            }
        }

        if (n > 0)
        {
            settingsUtils.SaveSettings(
                settingsSerializer(settings),
                Microsoft.PowerToys.Settings.UI.Library.PowerDisplaySettings.ModuleName);
        }

        return n;
    }

    private static int MigrateMonitorState(
        MonitorStateManager stateManager,
        IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> lookup)
    {
        var rewrites = new Dictionary<string, string>();
        foreach (var oldKey in stateManager.GetAllMonitorIds())
        {
            if (TryMapLegacyId(oldKey, lookup, out var newKey) && newKey != oldKey)
            {
                rewrites[oldKey] = newKey;
            }
        }

        return rewrites.Count == 0 ? 0 : stateManager.MigrateMonitorIds(rewrites);
    }

    private static int MigrateProfiles(
        IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> lookup)
    {
        var profiles = ProfileService.LoadProfiles();
        int n = 0;
        foreach (var profile in profiles.Profiles)
        {
            foreach (var setting in profile.MonitorSettings)
            {
                if (TryMapLegacyId(setting.MonitorId, lookup, out var newId))
                {
                    setting.MonitorId = newId;
                    n++;
                }
            }
        }

        if (n > 0)
        {
            ProfileService.SaveProfiles(profiles);
        }

        return n;
    }
}
