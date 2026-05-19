// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// One-shot upgrade helper: given a legacy <c>"{Source}_{EdidId}_{N}"</c> Id
/// (pre-PR #47712), find a currently-discovered DevicePath-based Monitor.Id with
/// the same EdidId so callers can rewrite the key or copy preferences onto it.
/// </summary>
/// <remarks>
/// Behavior is intentionally minimal — first EdidId match wins, no broadcasting and
/// no preference merging. A user with two identical monitors may need to re-toggle
/// one Enable* checkbox after upgrade; that one-time cost buys a much smaller
/// migration surface (and no follow-up edge cases to maintain).
/// </remarks>
public static class MonitorIdMigrator
{
    /// <summary>
    /// Returns the first currently-discovered Id whose EdidId matches the legacy entry,
    /// or null if there is no match, the input is not a legacy Id, or its EdidId is the
    /// <c>"Unknown"</c> placeholder.
    /// </summary>
    public static string? MatchNewId(string? legacyId, IEnumerable<string> currentlyDiscoveredIds)
    {
        ArgumentNullException.ThrowIfNull(currentlyDiscoveredIds);

        var edid = MonitorIdentity.LegacyEdidId(legacyId);
        if (string.IsNullOrEmpty(edid))
        {
            return null;
        }

        foreach (var id in currentlyDiscoveredIds)
        {
            if (string.Equals(MonitorIdentity.EdidIdFromMonitorId(id), edid, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }
}
