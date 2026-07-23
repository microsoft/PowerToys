// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Plans the conservative set of monitor-state entries to retain while a rebuilt monitor list is persisted.
/// </summary>
public static class MonitorStateRetentionPlanner
{
    /// <summary>
    /// Returns the union of the monitor Ids read from persisted settings and the Ids in the rebuilt settings.
    /// </summary>
    /// <remarks>
/// Settings persistence does not report write failures. Retaining both snapshots prevents a failed or
/// same-cycle competing settings write from deleting state that the observed settings still reference.
/// An entry removed from settings is pruned on the next reconciliation, once it is absent from both snapshots.
    /// </remarks>
    public static IReadOnlySet<string> BuildRetainedIds(
        IEnumerable<string> previouslyPersistedIds,
        IEnumerable<string> rebuiltIds)
    {
        ArgumentNullException.ThrowIfNull(previouslyPersistedIds);
        ArgumentNullException.ThrowIfNull(rebuiltIds);

        var retainedIds = new HashSet<string>(MonitorIdComparer.Instance);
        AddValidIds(retainedIds, previouslyPersistedIds);
        AddValidIds(retainedIds, rebuiltIds);
        return retainedIds;
    }

    private static void AddValidIds(HashSet<string> retainedIds, IEnumerable<string> monitorIds)
    {
        foreach (var monitorId in monitorIds)
        {
            if (!string.IsNullOrEmpty(monitorId))
            {
                retainedIds.Add(monitorId);
            }
        }
    }
}
