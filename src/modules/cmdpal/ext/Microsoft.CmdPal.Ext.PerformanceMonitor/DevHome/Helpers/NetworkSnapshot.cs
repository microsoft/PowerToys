// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace CoreWidgetProvider.Helpers;

internal sealed record NetworkSnapshot(string Name, NetworkStats.Data Usage, GraphSample[] UtilizationHistory, GraphSample[] TrafficHistory, string? ErrorMessage = null)
{
    public static readonly NetworkSnapshot Empty = new(string.Empty, new NetworkStats.Data(), [], []);
}
