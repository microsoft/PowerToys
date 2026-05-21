// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// One-shot upgrade helper: given a legacy <c>"{Source}_{EdidId}_{N}"</c> Id
/// (pre-PR #47712), find a currently-discovered DevicePath-based Monitor.Id
/// that shares both the EdidId and the Windows DISPLAY number, so callers can
/// rewrite the key or copy preferences onto it.
/// </summary>
/// <remarks>
/// Strict matching on the pair (EdidId, MonitorNumber): if no exact match is
/// found we return null and the caller drops the legacy entry. Mis-attributing
/// preferences (e.g. turning on a power-state toggle on the wrong monitor) is
/// worse than asking the user to re-toggle one checkbox, so there is no fallback.
/// </remarks>
public static class MonitorIdMigrator
{
    /// <summary>
    /// Returns the currently-discovered Id whose EdidId AND MonitorNumber match
    /// the legacy entry, or null if no such match exists, the input is not a
    /// legacy Id, or its EdidId is the <c>"Unknown"</c> placeholder.
    /// </summary>
    public static string? MatchNewId(
        string? legacyId,
        IEnumerable<(string Id, int MonitorNumber)> currentlyDiscovered)
    {
        ArgumentNullException.ThrowIfNull(currentlyDiscovered);

        var edid = MonitorIdentity.LegacyEdidId(legacyId);
        if (string.IsNullOrEmpty(edid))
        {
            return null;
        }

        var legacyNumber = MonitorIdentity.LegacyMonitorNumber(legacyId);
        if (legacyNumber <= 0)
        {
            return null;
        }

        foreach (var (id, number) in currentlyDiscovered)
        {
            if (number == legacyNumber
                && string.Equals(MonitorIdentity.EdidIdFromMonitorId(id), edid, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }
}
