// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Pure function that rebuilds the persisted list of <see cref="MonitorInfo"/> entries
/// from the currently-discovered set plus the previously-saved set.
/// Implements the 30-day retention rule:
///   - Currently-discovered monitors are emitted with a fresh <c>LastSeenUtc</c>.
///   - Missing-but-recent monitors are preserved with their saved Enable* flags.
///   - Missing-and-stale monitors (older than retention window) are dropped.
///   - <see cref="MonitorInfo.IsHidden"/>=true monitors are preserved unconditionally.
///   - Pre-upgrade entries (<c>LastSeenUtc == null</c>) get stamped to "now" and treated as just-seen.
/// </summary>
public static class MonitorSettingsRebuilder
{
    public static List<MonitorInfo> Rebuild(
        IReadOnlyList<MonitorInfo> currentlyDiscovered,
        IReadOnlyList<MonitorInfo> existing,
        ISystemClock clock,
        int retentionDays)
    {
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-retentionDays);
        var result = new List<MonitorInfo>(currentlyDiscovered.Count);

        // Stamp currently-discovered monitors with a fresh timestamp.
        foreach (var info in currentlyDiscovered)
        {
            info.LastSeenUtc = now;
            result.Add(info);
        }

        // Walk previously-saved entries; keep ones we haven't already emitted.
        foreach (var existingMonitor in existing)
        {
            if (string.IsNullOrEmpty(existingMonitor.Id))
            {
                continue;
            }

            if (result.Any(m => m.Id == existingMonitor.Id))
            {
                continue;
            }

            if (existingMonitor.LastSeenUtc == null)
            {
                // Pre-upgrade entry — start the retention clock now.
                existingMonitor.LastSeenUtc = now;
            }

            bool keep = existingMonitor.IsHidden || existingMonitor.LastSeenUtc.Value > cutoff;

            if (keep)
            {
                result.Add(existingMonitor);
            }
            else
            {
                Logger.LogInfo(
                    $"[MonitorSettingsRebuilder] Dropping stale entry '{existingMonitor.Id}' " +
                    $"(lastSeen={existingMonitor.LastSeenUtc:o})");
            }
        }

        return result;
    }
}
