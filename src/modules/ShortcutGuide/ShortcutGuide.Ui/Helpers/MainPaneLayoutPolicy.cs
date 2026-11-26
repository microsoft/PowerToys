// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Helpers;

internal static class MainPaneLayoutPolicy
{
    private const double DefaultMarginDip = 16;
    private const double SideIndicatorReserveDip = 8 + 46 + 16;
    private const double HorizontalIndicatorReserveDip = 8 + 46 + 8;

    internal static MainPaneMargins GetMargins(TaskbarEdge edge, bool isRightAligned, bool reserveBottomForIndicators)
    {
        double left = DefaultMarginDip;
        double top = DefaultMarginDip;
        double right = DefaultMarginDip;
        double bottom = reserveBottomForIndicators ? HorizontalIndicatorReserveDip : DefaultMarginDip;

        if (edge == TaskbarEdge.Left && !isRightAligned)
        {
            left = SideIndicatorReserveDip;
        }
        else if (edge == TaskbarEdge.Top)
        {
            top = HorizontalIndicatorReserveDip;
        }
        else if (edge == TaskbarEdge.Right && isRightAligned)
        {
            right = SideIndicatorReserveDip;
        }

        return new MainPaneMargins(left, top, right, bottom);
    }
}

internal readonly record struct MainPaneMargins(double Left, double Top, double Right, double Bottom);
