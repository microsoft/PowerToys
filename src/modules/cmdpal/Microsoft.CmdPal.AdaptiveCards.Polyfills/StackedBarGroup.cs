// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>One bar of a stacked bar chart: a title and its stacked values.</summary>
internal sealed record StackedBarGroup(string? Title, IReadOnlyList<ChartDataPoint> Data)
{
    public double Total
    {
        get
        {
            var total = 0d;
            foreach (var point in Data)
            {
                total += Math.Max(0, point.Value);
            }

            return total;
        }
    }
}
