// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.NativeMethods;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.Imaging;

/// <summary>
/// Implements an IImageRegionCopyService that uses the current desktop window as the copy source.
/// This is used during the main application runtime to generate preview images of the desktop.
/// </summary>
internal sealed class DesktopImageRegionCopyService : IImageRegionCopyService
{
    /// <summary>
    /// Copies the source region from the current desktop window
    /// to the target region on the specified Graphics object.
    /// </summary>
    public void CopyImageRegion(
        Graphics targetGraphics,
        RectangleInfo sourceBounds,
        RectangleInfo targetBounds)
    {
        var stopwatch = Stopwatch.StartNew();
        var (desktopHwnd, desktopHdc) = DesktopImageRegionCopyService.GetDesktopDeviceContext();
        var previewHdc = DesktopImageRegionCopyService.GetGraphicsDeviceContext(
            targetGraphics, Gdi32.STRETCH_BLT_MODE.STRETCH_HALFTONE);
        stopwatch.Stop();

        var source = sourceBounds.ToRectangle();
        var target = targetBounds.ToRectangle();
        var result = Gdi32.StretchBlt(
            previewHdc,
            target.X,
            target.Y,
            target.Width,
            target.Height,
            desktopHdc,
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

        // we need to release the graphics device context handle before anything
        // else tries to use the Graphics object otherwise it'll give an error
        // from GDI saying "Object is currently in use elsewhere"
        DesktopImageRegionCopyService.FreeGraphicsDeviceContext(targetGraphics, ref previewHdc);

        DesktopImageRegionCopyService.FreeDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
    }

    private static (HWND DesktopHwnd, HDC DesktopHdc) GetDesktopDeviceContext()
    {
        var desktopHwnd = User32.GetDesktopWindow();
        var desktopHdc = User32.GetWindowDC(desktopHwnd);
        if (desktopHdc.IsNull)
        {
            throw new InvalidOperationException(
                $"{nameof(User32.GetWindowDC)} returned null");
        }

        return (desktopHwnd, desktopHdc);
    }

    private static void FreeDesktopDeviceContext(ref HWND desktopHwnd, ref HDC desktopHdc)
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
    /// Checks if the target device context handle exists, and creates a new one from the
    /// specified Graphics object if not.
    /// </summary>
    private static HDC GetGraphicsDeviceContext(Graphics graphics, Gdi32.STRETCH_BLT_MODE mode)
    {
        var graphicsHdc = (HDC)graphics.GetHdc();

        var result = Gdi32.SetStretchBltMode(graphicsHdc, mode);
        if (result == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(Gdi32.SetStretchBltMode)} returned {result}");
        }

        return graphicsHdc;
    }

    /// <summary>
    /// Free the specified device context handle if it exists.
    /// </summary>
    private static void FreeGraphicsDeviceContext(Graphics graphics, ref HDC graphicsHdc)
    {
        if (graphicsHdc.IsNull)
        {
            return;
        }

        graphics.ReleaseHdc(graphicsHdc.Value);
        graphicsHdc = HDC.Null;
    }
}
