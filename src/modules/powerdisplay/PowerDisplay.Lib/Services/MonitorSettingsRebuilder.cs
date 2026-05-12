// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Pure function that rebuilds the persisted list of monitor entries from the
/// currently-discovered set plus the previously-saved set, applying a retention
/// rule for monitors that are missing from current discovery.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Currently-discovered monitors are emitted with a fresh <c>LastSeenUtc</c>.</item>
///   <item>Missing-but-recent monitors are preserved (caller's per-monitor settings stay intact).</item>
///   <item>Missing-and-stale monitors (older than retention window) are dropped.</item>
///   <item><see cref="IRetainableMonitor.IsHidden"/>=true monitors are preserved unconditionally.</item>
///   <item>Pre-upgrade entries (<c>LastSeenUtc == null</c>) get stamped to "now" and treated as just-seen.</item>
/// </list>
/// <para>
/// Generic on <typeparamref name="T"/> so callers can pass a concrete persisted type
/// (e.g. <c>MonitorInfo</c> from Settings.UI.Library) and get the same concrete
/// type back, without this assembly having to reference that library.
/// </para>
/// </remarks>
public static class MonitorSettingsRebuilder
{
    public static List<T> Rebuild<T>(
        IReadOnlyList<T> currentlyDiscovered,
        IReadOnlyList<T> existing,
        ISystemClock clock,
        int retentionDays)
        where T : IRetainableMonitor
    {
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-retentionDays);
        var result = new List<T>(currentlyDiscovered.Count);

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
