// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal static class ZoomItGeometry
{
    internal static Size FullScreenRecordingSize(Size displaySize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displaySize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(displaySize.Height);
        return new Size(
            checked(displaySize.Width + (displaySize.Width & 1)),
            checked(displaySize.Height + (displaySize.Height & 1)));
    }

    internal static bool IsTimerCentered(Rectangle inkBounds, Size displaySize)
    {
        if (inkBounds.Width <= 0 || inkBounds.Height <= 0)
        {
            return false;
        }

        // ZoomIt centers "% 2d:%02d", including a leading blank for single-digit minutes.
        // Ink bounds omit that blank and the font bearings; their offset scales with glyph size.
        var allowance = inkBounds.Height / 2.0;
        var centerX = inkBounds.Left + (inkBounds.Width / 2.0);
        var centerY = inkBounds.Top + (inkBounds.Height / 2.0);
        return Math.Abs(centerX - (displaySize.Width / 2.0)) <= allowance &&
            Math.Abs(centerY - (displaySize.Height / 2.0)) <= allowance;
    }
}
