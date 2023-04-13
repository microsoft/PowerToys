// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using MouseJumpUI.Models.Drawing;
using MouseJumpUI.NativeMethods;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.Helpers;

internal static class DrawingHelper
{
    /// <summary>
    /// Draw the gradient-filled preview background.
    /// </summary>
    public static void DrawPreviewBackground(
        Graphics previewGraphics, RectangleInfo previewBounds, IEnumerable<RectangleInfo> screenBounds)
    {
        using var backgroundBrush = new LinearGradientBrush(
            previewBounds.Location.ToPoint(),
            previewBounds.Size.ToPoint(),
            Color.FromArgb(13, 87, 210), // light blue
            Color.FromArgb(3, 68, 192)); // darker blue

        // it's faster to build a region with the screen areas excluded
        // and fill that than it is to fill the entire bounding rectangle
        var backgroundRegion = new Region(previewBounds.ToRectangle());
        foreach (var screen in screenBounds)
        {
            backgroundRegion.Exclude(screen.ToRectangle());
        }

        previewGraphics.FillRegion(backgroundBrush, backgroundRegion);
    }

    public static void EnsureDesktopDeviceContext(ref HWND desktopHwnd, ref HDC desktopHdc)
    {
        if (desktopHwnd.IsNull)
        {
            desktopHwnd = User32.GetDesktopWindow();
        }

        if (desktopHdc.IsNull)
        {
            desktopHdc = User32.GetWindowDC(desktopHwnd);
            if (desktopHdc.IsNull)
            {
                throw new InvalidOperationException(
                    $"{nameof(User32.GetWindowDC)} returned null");
            }
        }
    }

    public static void FreeDesktopDeviceContext(ref HWND desktopHwnd, ref HDC desktopHdc)
    {
        if (!desktopHwnd.IsNull && !desktopHdc.IsNull)
        {
            var result = User32.ReleaseDC(desktopHwnd, desktopHdc);
            if (result == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(User32.ReleaseDC)} returned {result}");
            }
        }

        desktopHwnd = HWND.Null;
        desktopHdc = HDC.Null;
    }

    /// <summary>
    /// Checks if the device context handle exists, and creates a new one from the
    /// specified Graphics object if not.
    /// </summary>
    public static void EnsurePreviewDeviceContext(Graphics previewGraphics, ref HDC previewHdc)
    {
        if (previewHdc.IsNull)
        {
            previewHdc = new HDC(previewGraphics.GetHdc());
            var result = Gdi32.SetStretchBltMode(previewHdc, Gdi32.STRETCH_BLT_MODE.STRETCH_HALFTONE);

            if (result == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(Gdi32.SetStretchBltMode)} returned {result}");
            }
        }
    }

    /// <summary>
    /// Free the specified device context handle if it exists.
    /// </summary>
    public static void FreePreviewDeviceContext(Graphics previewGraphics, ref HDC previewHdc)
    {
        if ((previewGraphics is not null) && !previewHdc.IsNull)
        {
            previewGraphics.ReleaseHdc(previewHdc.Value);
            previewHdc = HDC.Null;
        }
    }

    /// <summary>
    /// Draw placeholder images for any non-activated screens on the preview.
    /// Will release the specified device context handle if it needs to draw anything.
    /// </summary>
    public static void DrawPreviewScreenPlaceholders(
        Graphics previewGraphics, IEnumerable<RectangleInfo> screenBounds)
    {
        // we can exclude the activated screen because we've already draw
        // the screen capture image for that one on the preview
        if (screenBounds.Any())
        {
            var brush = Brushes.Black;
            previewGraphics.FillRectangles(brush, screenBounds.Select(screen => screen.ToRectangle()).ToArray());
        }
    }

    /// <summary>
    /// Draws a screen capture from the specified desktop handle onto the target device context.
    /// </summary>
    public static void DrawPreviewScreen(
        HDC sourceHdc,
        HDC targetHdc,
        RectangleInfo sourceBounds,
        RectangleInfo targetBounds)
    {
        var source = sourceBounds.ToRectangle();
        var target = targetBounds.ToRectangle();
        var result = Gdi32.StretchBlt(
            targetHdc,
            target.X,
            target.Y,
            target.Width,
            target.Height,
            sourceHdc,
            source.X,
            source.Y,
            source.Width,
            source.Height,
            Gdi32.ROP_CODE.SRCCOPY);
        if (!result)
        {
            throw new InvalidOperationException(
                $"{nameof(Gdi32.StretchBlt)} returned {result.Value}");
        }
    }

    /// <summary>
    /// Draws screen captures from the specified desktop handle onto the target device context.
    /// </summary>
    public static void DrawPreviewScreens(
        HDC sourceHdc,
        HDC targetHdc,
        IList<RectangleInfo> sourceBounds,
        IList<RectangleInfo> targetBounds)
    {
        for (var i = 0; i < sourceBounds.Count; i++)
        {
            var source = sourceBounds[i].ToRectangle();
            var target = targetBounds[i].ToRectangle();
            var result = Gdi32.StretchBlt(
                targetHdc,
                target.X,
                target.Y,
                target.Width,
                target.Height,
                sourceHdc,
                source.X,
                source.Y,
                source.Width,
                source.Height,
                Gdi32.ROP_CODE.SRCCOPY);
            if (!result)
            {
                throw new InvalidOperationException(
                    $"{nameof(Gdi32.StretchBlt)} returned {result.Value}");
            }
        }
    }
}
