// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Helpers;

internal static class IndicatorLayoutPolicy
{
    private const double MaxBodyDip = 40;
    private const double MinBodyDip = 28;
    private const double IndicatorGapDip = 4;

    internal static double GetBodySizeDip(double smallestButtonSlotPhysical, float dpiScale) =>
        System.Math.Clamp((smallestButtonSlotPhysical / dpiScale) - IndicatorGapDip, MinBodyDip, MaxBodyDip);
}
