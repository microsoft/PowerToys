// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

internal sealed class LineChartSeries
{
    public string? Legend { get; init; }

    public string? Color { get; init; }

    public IReadOnlyList<LineChartPoint> Points { get; init; } = [];
}
