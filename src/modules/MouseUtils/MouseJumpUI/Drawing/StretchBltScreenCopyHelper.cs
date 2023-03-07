// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using MouseJumpUI.Helpers;
using MouseJumpUI.NativeMethods.Core;
using MouseJumpUI.NativeWrappers;

namespace MouseJumpUI.Drawing;

public static class StretchBltScreenCopyHelper
{
    /// <summary>
    /// Captures the specified regions of the current desktop and composes them
    /// into a single screenshot. The resulting image is a scaled to fit the
    /// specified screenshot size and individual regions drawn according to
    /// their proportional positions on the main desktop.
    /// </summary>
    public static Bitmap CopyFromScreen(
        Rectangle desktopBounds,
        IEnumerable<Rectangle> desktopRegions,
        Size screenshotSize)
    {
        // note - it's faster to capture each screen individually and assemble them into
        // a single image as we don't have to transfer all the regions that are outside
        // the screen regions - e.g. the *** in the ascii art below:
        //
        // +----------------+********
        // |                |********
        // |       1        +-------+
        // |                |       |
        // +----------------+   0   |
        // *****************|       |
        // *****************+-------+
        //
        // for "jagged" monitor layouts this can be a big percentage of the entire desktop rectangle.
        var desktopHwnd = HWND.Null;
        var desktopHdc = HDC.Null;

        var screenshot = new Bitmap(screenshotSize.Width, screenshotSize.Height, PixelFormat.Format32bppArgb);
        using var screenshotGraphics = Graphics.FromImage(screenshot);
        screenshotGraphics.Clear(Color.Black);
        var screenshotHdc = new HDC(screenshotGraphics.GetHdc());

        try
        {
            desktopHwnd = User32.GetDesktopWindow();
            desktopHdc = User32.GetWindowDC(desktopHwnd);

            _ = Gdi32.SetStretchBltMode(screenshotHdc, NativeMethods.Gdi32.STRETCH_BLT_MODE.STRETCH_HALFTONE);

            var scalingFactor = LayoutHelper.GetScalingRatio(desktopBounds.Size, screenshotSize);
            foreach (var desktopRegion in desktopRegions)
            {
                var screenshotRegion = new Rectangle(
                    x: (int)((desktopRegion.X - desktopBounds.X) * scalingFactor),
                    y: (int)((desktopRegion.Y - desktopBounds.Y) * scalingFactor),
                    width: (int)(desktopRegion.Width * scalingFactor),
                    height: (int)(desktopRegion.Height * scalingFactor));

                _ = Gdi32.StretchBlt(
                    screenshotHdc,
                    screenshotRegion.X,
                    screenshotRegion.Y,
                    screenshotRegion.Width,
                    screenshotRegion.Height,
                    desktopHdc,
                    desktopRegion.X,
                    desktopRegion.Y,
                    desktopRegion.Width,
                    desktopRegion.Height,
                    NativeMethods.Gdi32.ROP_CODE.SRCCOPY);
            }

            return screenshot;
        }
        finally
        {
            if (!screenshotHdc.IsNull)
            {
                screenshotGraphics.ReleaseHdc(screenshotHdc.Value);
            }

            if (!desktopHwnd.IsNull && !desktopHdc.IsNull)
            {
                _ = User32.ReleaseDC(desktopHwnd, desktopHdc);
            }
        }
    }
}
