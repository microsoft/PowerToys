// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// One-shot upgrade migration: carries per-monitor user preferences from
/// legacy <c>"{Source}_{EdidId}_{MonitorNumber}"</c> Ids (pre-PR #47712) onto
/// the current set of monitor entries that use the DevicePath-based Id.
/// </summary>
/// <remarks>
/// <para>
/// Matching is done by EdidId. When a single legacy entry maps to a single
/// currently-discovered monitor with the same EdidId, the legacy preferences
/// are simply merged onto that monitor. When several legacy entries share an
/// EdidId (the user owns multiple identical-model monitors), the union of
/// their preference flags is broadcast to every currently-discovered monitor
/// of that EdidId — this preserves user opt-ins at the cost of a benign false
/// positive on the rare case where the user wanted the feature on only one of
/// two identical monitors. Re-disabling from the Settings UI is a single
/// toggle, whereas re-enabling lost opt-ins is invisible until the user
/// notices the feature is gone, so we err on the side of preserving them.
/// </para>
/// <para>
/// Legacy entries that successfully match are returned so the caller can drop
/// them from the persisted list — leaving them in place would cause the
/// retention rule in <see cref="MonitorSettingsRebuilder"/> to keep them for
/// another 30 days under their now-superseded Ids.
/// </para>
/// </remarks>
public static class MonitorIdMigrator
{
    /// <summary>
    /// Merge preferences from legacy-Id entries into currently-discovered monitors
    /// whose EdidId matches. Returns the legacy entries that were consumed.
    /// </summary>
    /// <typeparam name="T">A monitor entry type — typically <c>MonitorInfo</c>.</typeparam>
    /// <param name="currentlyDiscovered">Entries with the new DevicePath-based Id, freshly built from discovery.</param>
    /// <param name="existing">All entries loaded from <c>settings.json</c>, possibly mixing legacy and new-format Ids.</param>
    /// <param name="mergeUserPreferencesInto">
    /// Callback that merges legacy user preferences (<paramref name="mergeUserPreferencesInto"/> 2nd arg)
    /// into a currently-discovered entry (1st arg). Implementations should use union semantics for boolean
    /// flags so opt-ins are never silently lost.
    /// </param>
    /// <returns>The legacy entries whose preferences were merged onto at least one discovered monitor.</returns>
    public static IReadOnlyList<T> MergeLegacyPreferences<T>(
        IReadOnlyList<T> currentlyDiscovered,
        IReadOnlyList<T> existing,
        Action<T, T> mergeUserPreferencesInto)
        where T : IRetainableMonitor
    {
        ArgumentNullException.ThrowIfNull(currentlyDiscovered);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(mergeUserPreferencesInto);

        var discoveredByEdid = GroupIdsByEdid(currentlyDiscovered.Select(m => m.Id));
        if (discoveredByEdid.Count == 0)
        {
            return Array.Empty<T>();
        }

        var discoveredObjectsByEdid = GroupByEdid(currentlyDiscovered, m => m.Id);

        var consumed = new List<T>();
        foreach (var legacy in existing)
        {
            var edid = MonitorIdentity.LegacyEdidId(legacy.Id);
            if (string.IsNullOrEmpty(edid))
            {
                continue;
            }

            if (!discoveredObjectsByEdid.TryGetValue(edid, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                mergeUserPreferencesInto(target, legacy);
            }

            Logger.LogInfo(
                $"[MonitorIdMigrator] Migrated legacy settings entry '{legacy.Id}' onto {targets.Count} " +
                $"discovered monitor(s) with EdidId '{edid}'.");
            consumed.Add(legacy);
        }

        return consumed;
    }

    /// <summary>
    /// Rewrite legacy <c>monitorId</c> values inside every saved profile to the matching
    /// DevicePath-based Id. When a legacy Id maps to multiple identical monitors, the
    /// profile entry is duplicated once per matching new Id so each physical monitor
    /// receives the same brightness/contrast/volume/color-temperature target.
    /// </summary>
    /// <param name="profiles">The loaded profiles collection. Mutated in place.</param>
    /// <param name="currentlyDiscoveredIds">Ids of monitors currently discovered, in the new DevicePath-based format.</param>
    /// <returns><c>true</c> if any profile was modified and the file should be saved.</returns>
    /// <remarks>
    /// Unlike settings preferences (booleans → OR-merge), profile entries hold numeric
    /// targets (brightness 0–100, etc.). There is no meaningful way to merge two
    /// numeric values, so the rule is "fan out the legacy entry across all matching
    /// new Ids, preferring any already-present new-Id entry if it exists" — the user
    /// has likely re-saved the profile under the new format already if both exist.
    /// </remarks>
    public static bool MigrateProfileMonitorIds(
        PowerDisplayProfiles profiles,
        IEnumerable<string> currentlyDiscoveredIds)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(currentlyDiscoveredIds);

        var newIdsByEdid = GroupIdsByEdid(currentlyDiscoveredIds);
        if (newIdsByEdid.Count == 0 || profiles.Profiles is null || profiles.Profiles.Count == 0)
        {
            return false;
        }

        bool anyChanged = false;
        foreach (var profile in profiles.Profiles)
        {
            if (profile?.MonitorSettings is null || profile.MonitorSettings.Count == 0)
            {
                continue;
            }

            anyChanged |= MigrateProfileEntries(profile, newIdsByEdid);
        }

        return anyChanged;
    }

    /// <summary>
    /// Rewrite the keys of a per-monitor state dictionary so any legacy-format
    /// keys move onto their matching DevicePath-based equivalents. When a legacy key
    /// maps to multiple identical monitors, the value is cloned under each new key so
    /// every physical monitor starts with the last-known state.
    /// </summary>
    /// <typeparam name="TValue">Value type — typically <c>MonitorStateEntry</c> on disk, or <c>MonitorStateManager.MonitorState</c> in memory.</typeparam>
    /// <param name="states">Dictionary keyed by Monitor.Id. Mutated in place.</param>
    /// <param name="currentlyDiscoveredIds">Ids of monitors currently discovered, in the new DevicePath-based format.</param>
    /// <param name="cloneValue">
    /// Deep-clone callback for values: identical-monitor migrations duplicate the same value
    /// under multiple new keys, so callers must provide a clone to avoid aliasing.
    /// </param>
    /// <returns><c>true</c> if any key was rewritten and the dictionary should be re-persisted.</returns>
    /// <remarks>
    /// Entries that have a legacy-format key but no matching discovered EdidId are
    /// left untouched — they may belong to a temporarily disconnected monitor and
    /// dropping them would silently lose state. An existing new-format entry wins
    /// over a legacy entry that wants to overwrite it (avoids clobbering fresher data).
    /// </remarks>
    public static bool MigrateStateKeys<TValue>(
        IDictionary<string, TValue> states,
        IEnumerable<string> currentlyDiscoveredIds,
        Func<TValue, TValue> cloneValue)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(currentlyDiscoveredIds);
        ArgumentNullException.ThrowIfNull(cloneValue);

        var newIdsByEdid = GroupIdsByEdid(currentlyDiscoveredIds);
        if (newIdsByEdid.Count == 0 || states.Count == 0)
        {
            return false;
        }

        // Collect legacy keys up front; we mutate `states` inside the loop.
        var legacyKeys = states.Keys.Where(k => !string.IsNullOrEmpty(MonitorIdentity.LegacyEdidId(k))).ToList();
        if (legacyKeys.Count == 0)
        {
            return false;
        }

        bool anyChanged = false;
        foreach (var legacyKey in legacyKeys)
        {
            var edid = MonitorIdentity.LegacyEdidId(legacyKey);
            if (!newIdsByEdid.TryGetValue(edid, out var newIds))
            {
                continue;
            }

            var legacyEntry = states[legacyKey];
            foreach (var newId in newIds)
            {
                if (states.ContainsKey(newId))
                {
                    // A new-format entry already exists; it's freshly written and
                    // therefore more authoritative than the legacy snapshot.
                    continue;
                }

                states[newId] = cloneValue(legacyEntry);
            }

            states.Remove(legacyKey);
            anyChanged = true;

            Logger.LogInfo(
                $"[MonitorIdMigrator] Migrated legacy state key '{legacyKey}' onto {newIds.Count} " +
                $"discovered monitor(s) with EdidId '{edid}'.");
        }

        return anyChanged;
    }

    private static bool MigrateProfileEntries(
        PowerDisplayProfile profile,
        Dictionary<string, List<string>> newIdsByEdid)
    {
        // Snapshot the legacy entries first; we mutate the list inside the loop.
        var legacyEntries = profile.MonitorSettings
            .Where(s => !string.IsNullOrEmpty(MonitorIdentity.LegacyEdidId(s?.MonitorId)))
            .ToList();
        if (legacyEntries.Count == 0)
        {
            return false;
        }

        // Already-present new-format Ids for this profile — used to skip duplication
        // when the user has already created an entry under the new Id.
        var existingNewIds = new HashSet<string>(
            profile.MonitorSettings
                .Where(s => !string.IsNullOrEmpty(s?.MonitorId) && !MonitorIdentity.IsLegacyId(s.MonitorId))
                .Select(s => s.MonitorId),
            StringComparer.Ordinal);

        bool changed = false;
        foreach (var legacy in legacyEntries)
        {
            var edid = MonitorIdentity.LegacyEdidId(legacy.MonitorId);
            if (!newIdsByEdid.TryGetValue(edid, out var newIds))
            {
                continue;
            }

            foreach (var newId in newIds)
            {
                if (existingNewIds.Contains(newId))
                {
                    continue;
                }

                profile.MonitorSettings.Add(CloneProfileEntry(legacy, newId));
                existingNewIds.Add(newId);
            }

            profile.MonitorSettings.Remove(legacy);
            changed = true;

            Logger.LogInfo(
                $"[MonitorIdMigrator] Profile '{profile.Name}': migrated entry '{legacy.MonitorId}' onto " +
                $"{newIds.Count} discovered monitor(s) with EdidId '{edid}'.");
        }

        if (changed)
        {
            profile.Touch();
        }

        return changed;
    }

    private static ProfileMonitorSetting CloneProfileEntry(ProfileMonitorSetting source, string newMonitorId)
        => new(newMonitorId, source.Brightness, source.ColorTemperatureVcp, source.Contrast, source.Volume);

    private static Dictionary<string, List<string>> GroupIdsByEdid(IEnumerable<string> ids)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var edid = MonitorIdentity.EdidIdFromMonitorId(id);
            if (string.IsNullOrEmpty(edid))
            {
                continue;
            }

            if (!result.TryGetValue(edid, out var list))
            {
                list = new List<string>();
                result[edid] = list;
            }

            list.Add(id);
        }

        return result;
    }

    private static Dictionary<string, List<T>> GroupByEdid<T>(IEnumerable<T> items, Func<T, string?> idSelector)
    {
        var result = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var edid = MonitorIdentity.EdidIdFromMonitorId(idSelector(item));
            if (string.IsNullOrEmpty(edid))
            {
                continue;
            }

            if (!result.TryGetValue(edid, out var list))
            {
                list = new List<T>();
                result[edid] = list;
            }

            list.Add(item);
        }

        return result;
    }
}
