// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace FancyZones.UITests.Utils;

/// <summary>
/// Reads <c>app-zone-history.json</c>, the file FancyZones writes when a window is snapped into a
/// zone. Ported from the legacy <c>ZoneSwitchHelper.GetZoneIndexSetByAppName</c>.
/// </summary>
public static class ZoneHistory
{
    /// <summary>
    /// The first zone index the given executable was last snapped to, or <c>null</c> when the app has
    /// no history entry (i.e. it was never snapped).
    /// </summary>
    /// <param name="exeName">Executable name the history entry's <c>app-path</c> ends with.</param>
    /// <param name="json">Raw contents of <c>app-zone-history.json</c>.</param>
    public static string? GetZoneIndexSetByAppName(string exeName, string json)
    {
        if (string.IsNullOrEmpty(exeName) || string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("app-zone-history", out var historyArray))
        {
            return null;
        }

        foreach (var item in historyArray.EnumerateArray())
        {
            if (!item.TryGetProperty("app-path", out var appPath) ||
                appPath.GetString() is not string path ||
                !path.EndsWith(exeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.TryGetProperty("history", out var history) && history.GetArrayLength() > 0)
            {
                return history[0].GetProperty("zone-index-set")[0].GetRawText();
            }
        }

        return null;
    }
}
