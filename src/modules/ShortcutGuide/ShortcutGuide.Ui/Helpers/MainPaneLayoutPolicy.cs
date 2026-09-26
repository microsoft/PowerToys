// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Helpers;

internal static class MainPaneLayoutPolicy
{
    private const double DefaultMarginDip = 16;
    private const double IndicatorReserveDip = 8 + 46 + 16;

    internal static MainPaneMargins GetMargins(TaskbarEdge edge, bool isRightAligned)
    {
        double left = DefaultMarginDip;
        double top = DefaultMarginDip;
        double right = DefaultMarginDip;

        if (edge == TaskbarEdge.Left && !isRightAligned)
        {
            left = IndicatorReserveDip;
        }
        else if (edge == TaskbarEdge.Top)
        {
            top = IndicatorReserveDip;
        }
        else if (edge == TaskbarEdge.Right && isRightAligned)
        {
            right = IndicatorReserveDip;
        }

        return new MainPaneMargins(left, top, right, DefaultMarginDip);
    }
}

internal readonly record struct MainPaneMargins(double Left, double Top, double Right, double Bottom);
