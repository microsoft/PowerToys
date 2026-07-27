// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Reconciles persisted <see cref="DockMonitorConfig"/> entries against the
/// set of currently connected monitors. Uses <see cref="MonitorInfo.StableId"/>
/// (hardware device path) for persistent identification, with automatic
/// migration from legacy GDI device names (e.g. <c>\\.\DISPLAY1</c>).
/// </summary>
/// <remarks>
/// All operations are pure: they return new immutable lists rather than
/// mutating input collections.
/// </remarks>
public static class MonitorConfigReconciler
{
    /// <summary>
    /// Configs whose <see cref="DockMonitorConfig.LastSeen"/> is older than this
    /// duration are pruned during reconciliation.
    /// </summary>
    internal static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(180);

    /// <summary>
    /// Reconciles persisted monitor configs against the current set of connected monitors.
    /// </summary>
    public static ImmutableList<DockMonitorConfig> Reconcile(
        ImmutableList<DockMonitorConfig>? existingConfigs,
        IReadOnlyList<MonitorInfo> currentMonitors)
    {
        // Use Date (day granularity) so the value stabilizes across multiple reconciliations
        // within the same day. This prevents infinite loops: SettingsChanged → SyncDocks →
        // Reconcile → SettingsChanged when LastSeen changes by milliseconds each call.
        return Reconcile(existingConfigs, currentMonitors, DateTime.UtcNow.Date);
    }

    /// <summary>
    /// Overload accepting an explicit <paramref name="utcNow"/> for testability.
    /// </summary>
    internal static ImmutableList<DockMonitorConfig> Reconcile(
        ImmutableList<DockMonitorConfig>? existingConfigs,
        IReadOnlyList<MonitorInfo> currentMonitors,
        DateTime utcNow)
    {
        existingConfigs ??= ImmutableList<DockMonitorConfig>.Empty;

        if (currentMonitors.Count == 0)
        {
            return existingConfigs;
        }

        // Exclude only fallback monitors that could correspond to a persisted hardware
        // path. Resolved monitors and unambiguous legacy GDI configs still reconcile.
        currentMonitors = currentMonitors
            .Where(monitor => CanSafelyReconcile(monitor, existingConfigs))
            .ToList();
        if (currentMonitors.Count == 0)
        {
            return existingConfigs;
        }

        // Build a MonitorDeviceId → index lookup for O(1) matching
        var configIndexById = new Dictionary<string, int>(existingConfigs.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < existingConfigs.Count; i++)
        {
            configIndexById.TryAdd(existingConfigs[i].MonitorDeviceId, i);
        }

        var matchedMonitorStableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedConfigIndices = new HashSet<int>();
        var result = new List<DockMonitorConfig>(currentMonitors.Count);

        // Exact match on StableId (configs already migrated to stable paths)
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (configIndexById.TryGetValue(monitor.StableId, out var ci) && !matchedConfigIndices.Contains(ci))
            {
                result.Add(existingConfigs[ci] with { IsPrimary = monitor.IsPrimary, LastSeen = utcNow });
                matchedMonitorStableIds.Add(monitor.StableId);
                matchedConfigIndices.Add(ci);
            }
        }

        // Recover a single enabled legacy config when Windows renumbered its GDI display
        // name and there is only one possible connected monitor with the same primary role.
        // This repairs settings written while stable hardware IDs were unavailable without
        // guessing when multiple monitors or enabled configs could be the intended target.
        var hasResolvableEnabledConfig = existingConfigs.Any(config =>
            config.Enabled && currentMonitors.Any(monitor => ConfigMatchesMonitor(config, monitor)));
        var staleEnabledConfigs = existingConfigs
            .Select((config, index) => (Config: config, Index: index))
            .Where(entry =>
                entry.Config.Enabled &&
                IsLegacyGdiId(entry.Config.MonitorDeviceId) &&
                !currentMonitors.Any(monitor => ConfigMatchesMonitor(entry.Config, monitor)))
            .Take(2)
            .ToList();

        if (!hasResolvableEnabledConfig && staleEnabledConfigs.Count == 1)
        {
            var staleConfig = staleEnabledConfigs[0];
            var matchingMonitors = currentMonitors
                .Where(monitor =>
                    !matchedMonitorStableIds.Contains(monitor.StableId) &&
                    monitor.IsPrimary == staleConfig.Config.IsPrimary &&
                    !string.Equals(monitor.StableId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            if (matchingMonitors.Count == 1)
            {
                var monitor = matchingMonitors[0];
                if (configIndexById.TryGetValue(monitor.DeviceId, out var directConfigIndex) &&
                    !matchedConfigIndices.Contains(directConfigIndex) &&
                    IsReplaceableLegacySecondaryDefault(existingConfigs[directConfigIndex]))
                {
                    result.Add(staleConfig.Config with
                    {
                        MonitorDeviceId = monitor.StableId,
                        IsPrimary = monitor.IsPrimary,
                        LastSeen = utcNow,
                    });
                    matchedMonitorStableIds.Add(monitor.StableId);
                    matchedConfigIndices.Add(directConfigIndex);

                    // A renumbered monitor and a disconnected monitor are indistinguishable
                    // when only a legacy GDI name was persisted. Keep the original config so
                    // its customizations survive if that physical monitor reconnects.
                }
            }
        }

        // Legacy migration: match configs that still have GDI-style IDs
        // (e.g. "\\.\DISPLAY1") by matching against the monitor's GDI DeviceId,
        // then rewrite the MonitorDeviceId to the monitor's stable hardware path.
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (matchedMonitorStableIds.Contains(monitor.StableId))
            {
                continue;
            }

            if (configIndexById.TryGetValue(monitor.DeviceId, out var ci) && !matchedConfigIndices.Contains(ci))
            {
                // Migrate: rewrite from GDI name to stable path
                result.Add(existingConfigs[ci] with
                {
                    MonitorDeviceId = monitor.StableId,
                    IsPrimary = monitor.IsPrimary,
                    LastSeen = utcNow,
                });
                matchedMonitorStableIds.Add(monitor.StableId);
                matchedConfigIndices.Add(ci);
            }
        }

        // Fuzzy match: recover primary monitor config when its ID changed.
        // Windows can reassign device paths across driver updates or cable swaps.
        // When the primary monitor's StableId no longer matches any saved config,
        // we look for an unmatched config that was previously marked as primary and
        // reassociate it. Secondary monitors are not interchangeable, so we skip them.
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (!monitor.IsPrimary || matchedMonitorStableIds.Contains(monitor.StableId))
            {
                continue;
            }

            for (var ci = 0; ci < existingConfigs.Count; ci++)
            {
                if (matchedConfigIndices.Contains(ci))
                {
                    continue;
                }

                if (existingConfigs[ci].IsPrimary)
                {
                    result.Add(existingConfigs[ci] with
                    {
                        MonitorDeviceId = monitor.StableId,
                        IsPrimary = monitor.IsPrimary,
                        LastSeen = utcNow,
                    });
                    matchedMonitorStableIds.Add(monitor.StableId);
                    matchedConfigIndices.Add(ci);
                    break;
                }
            }
        }

        // Create defaults for new monitors with no matching config.
        // Primary monitors inherit global bands (IsCustomized = false) for a seamless
        // upgrade path. Secondary monitors start disabled with empty band lists;
        // users opt-in via Settings when they want the dock on additional displays.
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (matchedMonitorStableIds.Contains(monitor.StableId))
            {
                continue;
            }

            if (monitor.IsPrimary)
            {
                result.Add(new DockMonitorConfig
                {
                    MonitorDeviceId = monitor.StableId,
                    Enabled = true,
                    IsPrimary = true,
                    LastSeen = utcNow,
                });
            }
            else
            {
                result.Add(new DockMonitorConfig
                {
                    MonitorDeviceId = monitor.StableId,
                    Enabled = false,
                    IsPrimary = false,
                    IsCustomized = true,
                    StartBands = ImmutableList<DockBandSettings>.Empty,
                    CenterBands = ImmutableList<DockBandSettings>.Empty,
                    EndBands = ImmutableList<DockBandSettings>.Empty,
                    LastSeen = utcNow,
                });
            }
        }

        // Retain disconnected monitor configs so settings survive reconnection.
        // Prune entries not seen for longer than StaleThreshold (6 months).
        for (var ci = 0; ci < existingConfigs.Count; ci++)
        {
            if (matchedConfigIndices.Contains(ci))
            {
                continue;
            }

            var config = existingConfigs[ci];
            var lastSeen = config.LastSeen ?? utcNow; // Treat legacy entries (no LastSeen) as fresh
            if ((utcNow - lastSeen) < StaleThreshold)
            {
                result.Add(config);
            }
        }

        // Return the original reference when nothing actually changed so callers
        // can use reference equality to skip no-op settings writes.
        if (result.Count == existingConfigs.Count)
        {
            var changed = false;
            for (var i = 0; i < result.Count; i++)
            {
                if (!result[i].Equals(existingConfigs[i]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return existingConfigs;
            }
        }

        return ImmutableList.CreateRange(result);
    }

    private static bool IsLegacyGdiId(string monitorDeviceId) =>
        monitorDeviceId.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase);

    private static bool ConfigMatchesMonitor(DockMonitorConfig config, MonitorInfo monitor) =>
        string.Equals(config.MonitorDeviceId, monitor.StableId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(config.MonitorDeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase);

    private static bool CanSafelyReconcile(MonitorInfo monitor, ImmutableList<DockMonitorConfig> configs)
    {
        if (!string.Equals(monitor.StableId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase) ||
            configs.Any(config => string.Equals(config.MonitorDeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !configs.Any(config =>
            !IsLegacyGdiId(config.MonitorDeviceId) &&
            config.IsPrimary == monitor.IsPrimary);
    }

    private static bool IsReplaceableLegacySecondaryDefault(DockMonitorConfig config) =>
        IsLegacyGdiId(config.MonitorDeviceId) &&
        !config.Enabled &&
        !config.IsPrimary &&
        config.IsCustomized &&
        config.Side is null &&
        (config.StartBands?.Count ?? 0) == 0 &&
        (config.CenterBands?.Count ?? 0) == 0 &&
        (config.EndBands?.Count ?? 0) == 0;
}
