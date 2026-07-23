// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Core.Models;

namespace PowerOCR.Core.Geometry;

public static class SelectionGeometry
{
    public static PixelSelection ToPixels(
        OcrPoint firstDip,
        OcrPoint secondDip,
        double rasterizationScale,
        DisplayBounds display)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rasterizationScale, 0);

        int left = (int)Math.Round(Math.Min(firstDip.X, secondDip.X) * rasterizationScale);
        int top = (int)Math.Round(Math.Min(firstDip.Y, secondDip.Y) * rasterizationScale);
        int right = (int)Math.Round(Math.Max(firstDip.X, secondDip.X) * rasterizationScale);
        int bottom = (int)Math.Round(Math.Max(firstDip.Y, secondDip.Y) * rasterizationScale);

        left = Math.Clamp(left, 0, display.Width);
        top = Math.Clamp(top, 0, display.Height);
        right = Math.Clamp(right, left, display.Width);
        bottom = Math.Clamp(bottom, top, display.Height);

        var local = new PixelRect(left, top, right - left, bottom - top);
        var absolute = new PixelRect(display.X + left, display.Y + top, local.Width, local.Height);
        return new(local, absolute);
    }
}
