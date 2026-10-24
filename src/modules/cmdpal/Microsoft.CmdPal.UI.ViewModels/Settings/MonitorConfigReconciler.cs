// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Reconciles persisted <see cref="DockMonitorConfig"/> entries against the
/// set of currently connected monitors. Uses <see cref="MonitorInfo.StableId"/>
/// (hardware device path) for persistent identification, falls back to
/// <see cref="MonitorInfo.HardwareId"/> (EDID) when a monitor changes ports, and
/// migrates legacy GDI device names (e.g. <c>\\.\DISPLAY1</c>).
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

    private const string GdiDevicePrefix = @"\\.\";

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
                result.Add(existingConfigs[ci] with
                {
                    IsPrimary = monitor.IsPrimary,
                    LastSeen = utcNow,
                    MonitorHardwareId = monitor.HardwareId ?? existingConfigs[ci].MonitorHardwareId,
                });
                matchedMonitorStableIds.Add(monitor.StableId);
                matchedConfigIndices.Add(ci);
            }
        }

        // Hardware match: the device path is tied to the port, so plugging a monitor into a
        // different port, dock, or GPU gives it a new StableId. The EDID based HardwareId
        // follows the panel instead. We only trust it when exactly one connected monitor and
        // exactly one unmatched config share it, so twins without serials never get swapped.
        MatchByHardwareId(existingConfigs, currentMonitors, matchedMonitorStableIds, matchedConfigIndices, result, utcNow);

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
                    MonitorHardwareId = monitor.HardwareId,
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
                        MonitorHardwareId = monitor.HardwareId,
                        IsPrimary = monitor.IsPrimary,
                        LastSeen = utcNow,
                    });
                    matchedMonitorStableIds.Add(monitor.StableId);
                    matchedConfigIndices.Add(ci);
                    break;
                }
            }
        }

        // Orphan cleanup: older builds of the pin dialog saved configs under the GDI name
        // even when the monitor already had a config under its StableId. Left alone, the
        // legacy pass above would eventually hand that orphan to whichever monitor picks
        // up the number next. Fold its bands into the monitor that owns the name today.
        MergeGdiOrphans(existingConfigs, currentMonitors, matchedMonitorStableIds, matchedConfigIndices, result);

        // Create defaults for new monitors with no matching config.
        // Primary monitors inherit global bands (IsCustomized = false) for a seamless
        // upgrade path. Secondary monitors start disabled without a layout. We copy the
        // primary layout when the user first enables them, so they get the current setup
        // instead of whatever the primary looked like when the monitor was first seen.
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
                    MonitorHardwareId = monitor.HardwareId,
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
                    MonitorHardwareId = monitor.HardwareId,
                    Enabled = false,
                    IsPrimary = false,
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

        // Reserve labels for retained monitors, including disconnected ones, before
        // assigning any missing labels. Store ordinals rather than localized names
        // so labels survive reconnects and can still follow the UI language.
        var usedDisplayNumbers = new HashSet<int>();
        foreach (var config in result)
        {
            if (config.FallbackDisplayNumber > 0)
            {
                usedDisplayNumbers.Add(config.FallbackDisplayNumber);
            }
        }

        var nextDisplayNumber = 1;
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i].FallbackDisplayNumber > 0)
            {
                continue;
            }

            while (!usedDisplayNumbers.Add(nextDisplayNumber))
            {
                nextDisplayNumber++;
            }

            result[i] = result[i] with { FallbackDisplayNumber = nextDisplayNumber };
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

    private static void MatchByHardwareId(
        ImmutableList<DockMonitorConfig> existingConfigs,
        IReadOnlyList<MonitorInfo> currentMonitors,
        HashSet<string> matchedMonitorStableIds,
        HashSet<int> matchedConfigIndices,
        List<DockMonitorConfig> result,
        DateTime utcNow)
    {
        var monitorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in currentMonitors)
        {
            if (monitor.HardwareId is { } id)
            {
                monitorCounts[id] = monitorCounts.GetValueOrDefault(id) + 1;
            }
        }

        if (monitorCounts.Count == 0)
        {
            return;
        }

        var configIndexByHardwareId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ambiguousHardwareIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var ci = 0; ci < existingConfigs.Count; ci++)
        {
            if (matchedConfigIndices.Contains(ci) || existingConfigs[ci].MonitorHardwareId is not { } id)
            {
                continue;
            }

            if (!configIndexByHardwareId.TryAdd(id, ci))
            {
                ambiguousHardwareIds.Add(id);
            }
        }

        foreach (var monitor in currentMonitors)
        {
            if (monitor.HardwareId is not { } id ||
                matchedMonitorStableIds.Contains(monitor.StableId) ||
                monitorCounts[id] != 1 ||
                ambiguousHardwareIds.Contains(id) ||
                !configIndexByHardwareId.TryGetValue(id, out var ci))
            {
                continue;
            }

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

    private static void MergeGdiOrphans(
        ImmutableList<DockMonitorConfig> existingConfigs,
        IReadOnlyList<MonitorInfo> currentMonitors,
        HashSet<string> matchedMonitorStableIds,
        HashSet<int> matchedConfigIndices,
        List<DockMonitorConfig> result)
    {
        for (var ci = 0; ci < existingConfigs.Count; ci++)
        {
            var orphan = existingConfigs[ci];
            if (matchedConfigIndices.Contains(ci) ||
                !orphan.IsCustomized ||
                !orphan.MonitorDeviceId.StartsWith(GdiDevicePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MonitorInfo? owner = null;
            foreach (var monitor in currentMonitors)
            {
                if (string.Equals(monitor.DeviceId, orphan.MonitorDeviceId, StringComparison.OrdinalIgnoreCase) &&
                    matchedMonitorStableIds.Contains(monitor.StableId))
                {
                    owner = monitor;
                    break;
                }
            }

            var targetIndex = owner is null
                ? -1
                : result.FindIndex(c => string.Equals(c.MonitorDeviceId, owner.StableId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
            {
                continue;
            }

            var target = result[targetIndex];
            var seenBands = new HashSet<(string ProviderId, string CommandId)>();
            if (target.IsCustomized)
            {
                AddBandsToSet(seenBands, target.StartBands);
                AddBandsToSet(seenBands, target.CenterBands);
                AddBandsToSet(seenBands, target.EndBands);

                result[targetIndex] = target with
                {
                    StartBands = MergeBands(target.StartBands, orphan.StartBands, seenBands),
                    CenterBands = MergeBands(target.CenterBands, orphan.CenterBands, seenBands),
                    EndBands = MergeBands(target.EndBands, orphan.EndBands, seenBands),
                };
            }
            else
            {
                result[targetIndex] = target with
                {
                    IsCustomized = true,
                    StartBands = MergeBands(null, orphan.StartBands, seenBands),
                    CenterBands = MergeBands(null, orphan.CenterBands, seenBands),
                    EndBands = MergeBands(null, orphan.EndBands, seenBands),
                };
            }

            matchedConfigIndices.Add(ci);
        }
    }

    private static void AddBandsToSet(HashSet<(string ProviderId, string CommandId)> seenBands, ImmutableList<DockBandSettings>? bands)
    {
        if (bands is null)
        {
            return;
        }

        foreach (var band in bands)
        {
            seenBands.Add((band.ProviderId, band.CommandId));
        }
    }

    private static ImmutableList<DockBandSettings> MergeBands(
        ImmutableList<DockBandSettings>? target,
        ImmutableList<DockBandSettings>? source,
        HashSet<(string ProviderId, string CommandId)> seenBands)
    {
        target ??= ImmutableList<DockBandSettings>.Empty;
        if (source is null || source.Count == 0)
        {
            return target;
        }

        var builder = target.ToBuilder();
        foreach (var band in source)
        {
            if (seenBands.Add((band.ProviderId, band.CommandId)))
            {
                builder.Add(band);
            }
        }

        return builder.ToImmutable();
    }
}
