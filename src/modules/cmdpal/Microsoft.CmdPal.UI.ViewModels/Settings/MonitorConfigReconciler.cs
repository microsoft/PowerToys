// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Reconciles saved <see cref="DockMonitorConfig"/> entries with the current
/// set of connected monitors. Windows GDI device names (e.g. "\\.\DISPLAY49")
/// are not stable across reboots, so this helper re-associates configs using
/// <see cref="DockMonitorConfig.IsPrimary"/> as a fallback matching key and
/// removes orphaned entries.
/// </summary>
public static class MonitorConfigReconciler
{
    /// <summary>
    /// Reconciles <paramref name="configs"/> against <paramref name="monitors"/>.
    /// Stale DeviceIds are updated in-place, orphaned configs are removed, and
    /// missing monitors get new default configs.
    /// </summary>
    /// <returns><see langword="true"/> if any config was added, updated, or removed.</returns>
    public static bool Reconcile(List<DockMonitorConfig> configs, IReadOnlyList<MonitorInfo> monitors)
    {
        var changed = false;

        // Build lookup of current monitor DeviceIds for fast membership checks.
        var currentIds = new HashSet<string>(monitors.Count, StringComparer.Ordinal);
        foreach (var m in monitors)
        {
            currentIds.Add(m.DeviceId);
        }

        // Phase 1: Exact DeviceId match — mark as used and keep IsPrimary up-to-date.
        var usedConfigs = new HashSet<DockMonitorConfig>(configs.Count);
        var matchedMonitors = new HashSet<string>(monitors.Count, StringComparer.Ordinal);

        foreach (var monitor in monitors)
        {
            foreach (var cfg in configs)
            {
                if (usedConfigs.Contains(cfg))
                {
                    continue;
                }

                if (string.Equals(cfg.MonitorDeviceId, monitor.DeviceId, StringComparison.Ordinal))
                {
                    usedConfigs.Add(cfg);
                    matchedMonitors.Add(monitor.DeviceId);

                    if (cfg.IsPrimary != monitor.IsPrimary)
                    {
                        cfg.IsPrimary = monitor.IsPrimary;
                        changed = true;
                    }

                    break;
                }
            }
        }

        // Phase 2: Fuzzy match — for each unmatched monitor, find an unmatched
        // config whose IsPrimary flag agrees. Update the config's DeviceId.
        foreach (var monitor in monitors)
        {
            if (matchedMonitors.Contains(monitor.DeviceId))
            {
                continue;
            }

            DockMonitorConfig? best = null;
            foreach (var cfg in configs)
            {
                if (usedConfigs.Contains(cfg))
                {
                    continue;
                }

                if (cfg.IsPrimary == monitor.IsPrimary)
                {
                    best = cfg;
                    break;
                }
            }

            if (best is not null)
            {
                best.MonitorDeviceId = monitor.DeviceId;
                best.IsPrimary = monitor.IsPrimary;
                usedConfigs.Add(best);
                matchedMonitors.Add(monitor.DeviceId);
                changed = true;
            }
        }

        // Phase 3: Create default configs for monitors that still have no match.
        foreach (var monitor in monitors)
        {
            if (matchedMonitors.Contains(monitor.DeviceId))
            {
                continue;
            }

            configs.Add(new DockMonitorConfig
            {
                MonitorDeviceId = monitor.DeviceId,
                Enabled = monitor.IsPrimary,
                IsPrimary = monitor.IsPrimary,
            });
            changed = true;
        }

        // Phase 4: Remove orphaned configs (DeviceIds that don't match any
        // current monitor after reconciliation).
        for (var i = configs.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(configs[i].MonitorDeviceId))
            {
                configs.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }
}
