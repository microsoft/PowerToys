// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Plans which persisted monitor-state entries a settings reconciliation drops.
/// </summary>
public static class MonitorStateRetentionPlanner
{
    /// <summary>
    /// Returns the monitor Ids that the persisted settings snapshot listed but the rebuilt settings
    /// list no longer keeps — that is, the entries this reconciliation deliberately dropped.
    /// </summary>
    /// <remarks>
    /// State cleanup is driven by an observed drop, never by absence from the rebuilt list. A
    /// missing or corrupt settings.json makes <c>GetSettingsOrDefault</c> return — and persist — a
    /// defaults object whose monitor list is empty and is indistinguishable from a real one;
    /// pruning by absence would then delete the saved brightness, contrast, volume, color
    /// temperature and known-good VCP cache of every monitor not connected at that instant. With no
    /// observed drop there is nothing to delete.
    /// </remarks>
    /// <param name="previouslyPersistedIds">Monitor Ids read from settings.json, before the rebuild.</param>
    /// <param name="rebuiltIds">Monitor Ids about to be written back to settings.json.</param>
    /// <returns>The Ids whose persisted monitor state is no longer referenced by settings.</returns>
    public static IReadOnlySet<string> BuildDroppedIds(
        IEnumerable<string> previouslyPersistedIds,
        IEnumerable<string> rebuiltIds)
    {
        ArgumentNullException.ThrowIfNull(previouslyPersistedIds);
        ArgumentNullException.ThrowIfNull(rebuiltIds);

        var droppedIds = new HashSet<string>(MonitorIdComparer.Instance);
        foreach (var monitorId in previouslyPersistedIds)
        {
            if (!string.IsNullOrEmpty(monitorId))
            {
                droppedIds.Add(monitorId);
            }
        }

        // ExceptWith uses the set's own comparer, so Id matching stays case-insensitive.
        droppedIds.ExceptWith(rebuiltIds);
        return droppedIds;
    }
}
