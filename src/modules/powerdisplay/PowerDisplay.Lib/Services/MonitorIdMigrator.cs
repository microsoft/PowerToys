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

        // Group currently-discovered monitors by EdidId so a single legacy entry
        // can broadcast its preferences across identical monitors.
        var discoveredByEdid = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in currentlyDiscovered)
        {
            var edid = MonitorIdentity.EdidIdFromMonitorId(monitor.Id);
            if (string.IsNullOrEmpty(edid))
            {
                continue;
            }

            if (!discoveredByEdid.TryGetValue(edid, out var list))
            {
                list = new List<T>();
                discoveredByEdid[edid] = list;
            }

            list.Add(monitor);
        }

        if (discoveredByEdid.Count == 0)
        {
            return Array.Empty<T>();
        }

        var consumed = new List<T>();
        foreach (var legacy in existing)
        {
            var edid = MonitorIdentity.LegacyEdidId(legacy.Id);
            if (string.IsNullOrEmpty(edid))
            {
                continue;
            }

            if (!discoveredByEdid.TryGetValue(edid, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                mergeUserPreferencesInto(target, legacy);
            }

            Logger.LogInfo(
                $"[MonitorIdMigrator] Migrated legacy entry '{legacy.Id}' onto {targets.Count} " +
                $"discovered monitor(s) with EdidId '{edid}'.");
            consumed.Add(legacy);
        }

        return consumed;
    }
}
