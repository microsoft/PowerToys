// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal static class CompactPositionConstraints
{
    // A normally sized expanded palette has about 420 DIPs below the center of its
    // search row. Reserving that much room also leaves space for the collapsed shelf.
    private const double MinimumExpandedSpaceBelowSearchDip = 420;

    public static double ClampPercentageFromBottom(double percentage, double workAreaHeightDip)
    {
        var minimumPercentage = GetMinimumPercentageFromBottom(workAreaHeightDip);
        return Math.Clamp(percentage, minimumPercentage, 100);
    }

    public static double GetMinimumPercentageFromBottom(double workAreaHeightDip)
    {
        if (workAreaHeightDip <= 0)
        {
            return 0;
        }

        return Math.Clamp(
            MinimumExpandedSpaceBelowSearchDip / workAreaHeightDip * 100,
            0,
            100);
    }
}
