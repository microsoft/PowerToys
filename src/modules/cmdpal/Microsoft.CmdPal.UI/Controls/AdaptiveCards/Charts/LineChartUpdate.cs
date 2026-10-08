// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

internal static class LineChartUpdate
{
    /// <summary>
    /// Returns whether <paramref name="next"/> is <paramref name="previous"/> moved left by one
    /// sample, which is how live telemetry arrives. The chart animates that case as a scroll.
    /// </summary>
    public static bool IsScrolledByOne(LineChartModel previous, LineChartModel next)
    {
        if (previous.Series.Count == 0 || previous.Series.Count != next.Series.Count)
        {
            return false;
        }

        for (var s = 0; s < previous.Series.Count; s++)
        {
            var before = previous.Series[s].Points;
            var after = next.Series[s].Points;
            if (before.Count != after.Count || before.Count < 2)
            {
                return false;
            }

            for (var i = 0; i < after.Count - 1; i++)
            {
                if (after[i].Y != before[i + 1].Y)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
