// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.PowerDisplay.UITests;

internal static class PowerDisplayInventory
{
    public static JsonElement[] CurrentBatch(IReadOnlyCollection<JsonElement> monitors)
    {
        var latest = monitors.Select(LastSeen).Max();
        return monitors.Where(monitor =>
            latest.HasValue && LastSeen(monitor) == latest &&
            !monitor.GetProperty("isHidden").GetBoolean()).ToArray();
    }

    private static DateTime? LastSeen(JsonElement monitor) =>
        monitor.TryGetProperty("lastSeenUtc", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDateTime()
            : null;
}
