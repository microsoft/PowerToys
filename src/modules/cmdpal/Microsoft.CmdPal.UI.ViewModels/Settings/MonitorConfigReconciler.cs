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
/// set of currently connected monitors. Handles stale device IDs that may change
/// across reboots by using the <see cref="DockMonitorConfig.IsPrimary"/> flag as
/// a secondary matching key.
/// </summary>
/// <remarks>
/// All operations are pure — they return new immutable lists rather than
/// mutating input collections.
/// </remarks>
public static class MonitorConfigReconciler
{
    /// <summary>
    /// Reconciles persisted monitor configs against the current set of connected monitors.
    /// <para>
    /// <b>Phase 1</b>: Exact DeviceId matching — keep IsPrimary up-to-date.<br/>
    /// <b>Phase 2</b>: Fuzzy matching — reassociate unmatched configs by IsPrimary flag.<br/>
    /// <b>Phase 3</b>: Create default configs for monitors that have no matching config.<br/>
    /// <b>Phase 4</b>: Remove orphaned configs whose DeviceIds don't match any connected monitor.
    /// </para>
    /// </summary>
    public static ImmutableList<DockMonitorConfig> Reconcile(
        ImmutableList<DockMonitorConfig> existingConfigs,
        IReadOnlyList<MonitorInfo> currentMonitors)
    {
        if (currentMonitors.Count == 0)
        {
            return existingConfigs;
        }

        // Build sets for tracking
        var matchedMonitorDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedConfigIndices = new HashSet<int>();
        var result = new List<DockMonitorConfig>(currentMonitors.Count);

        // Convert to mutable working list for easier manipulation
        var configs = new List<DockMonitorConfig>(existingConfigs);

        // Phase 1: Exact DeviceId match
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            for (var ci = 0; ci < configs.Count; ci++)
            {
                if (matchedConfigIndices.Contains(ci))
                {
                    continue;
                }

                if (string.Equals(configs[ci].MonitorDeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    // Update IsPrimary to current state
                    result.Add(configs[ci] with { IsPrimary = monitor.IsPrimary });
                    matchedMonitorDeviceIds.Add(monitor.DeviceId);
                    matchedConfigIndices.Add(ci);
                    break;
                }
            }
        }

        // Phase 2: Fuzzy match by IsPrimary for unmatched configs (primary only).
        // Non-primary monitors are not interchangeable, so we only fuzzy-match the primary.
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (!monitor.IsPrimary || matchedMonitorDeviceIds.Contains(monitor.DeviceId))
            {
                continue;
            }

            for (var ci = 0; ci < configs.Count; ci++)
            {
                if (matchedConfigIndices.Contains(ci))
                {
                    continue;
                }

                if (configs[ci].IsPrimary)
                {
                    // Reassociate: update DeviceId and IsPrimary
                    result.Add(configs[ci] with
                    {
                        MonitorDeviceId = monitor.DeviceId,
                        IsPrimary = monitor.IsPrimary,
                    });
                    matchedMonitorDeviceIds.Add(monitor.DeviceId);
                    matchedConfigIndices.Add(ci);
                    break;
                }
            }
        }

        // Phase 3: Create defaults for new monitors with no matching config
        for (var mi = 0; mi < currentMonitors.Count; mi++)
        {
            var monitor = currentMonitors[mi];
            if (matchedMonitorDeviceIds.Contains(monitor.DeviceId))
            {
                continue;
            }

            result.Add(new DockMonitorConfig
            {
                MonitorDeviceId = monitor.DeviceId,
                Enabled = true,
                IsPrimary = monitor.IsPrimary,
            });
        }

        // Phase 4: Orphaned configs are not included in result (implicitly removed)

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
}
