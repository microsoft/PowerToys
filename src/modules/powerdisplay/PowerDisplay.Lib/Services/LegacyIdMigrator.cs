// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Migrates monitor identifiers from the legacy "{Source}_{EdidId}_{MonitorNumber}"
/// format (e.g., "DDC_DELD1A8_1") to the new DevicePath-derived form (e.g.,
/// "\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1"). Pure mapping logic only — file I/O lives elsewhere.
/// </summary>
public static partial class LegacyIdMigrator
{
    [GeneratedRegex(@"^(?:DDC|WMI)_(?<edid>[A-Za-z0-9]+)_(?<num>\d+)$", RegexOptions.Compiled)]
    private static partial Regex LegacyIdRegex();

    /// <summary>
    /// Try to parse a legacy-format monitor Id. Returns false if the input is null,
    /// empty, or does not match the legacy pattern (e.g., already in new format).
    /// </summary>
    public static bool TryParseLegacyId(string? id, out string edidId, out int monitorNumber)
    {
        edidId = string.Empty;
        monitorNumber = 0;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var match = LegacyIdRegex().Match(id);
        if (!match.Success)
        {
            return false;
        }

        edidId = match.Groups["edid"].Value;
        return int.TryParse(match.Groups["num"].Value, out monitorNumber);
    }

    /// <summary>
    /// Try to map a legacy-format Id to its new-format equivalent using a
    /// (EdidId, MonitorNumber) → newId lookup. Returns false (and leaves <paramref name="newId"/>
    /// equal to the input) when the input is already in new format or the lookup misses.
    /// </summary>
    public static bool TryMapLegacyId(
        string oldId,
        IReadOnlyDictionary<(string EdidId, int MonitorNumber), string> lookup,
        out string newId)
    {
        newId = oldId;
        if (!TryParseLegacyId(oldId, out var edid, out var num))
        {
            return false;
        }

        if (lookup.TryGetValue((edid, num), out var mapped))
        {
            newId = mapped;
            return true;
        }

        return false;
    }
}
