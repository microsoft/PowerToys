// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;

using MouseJump.Common.Interop;
using MouseJump.Models.Drawing;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace MouseJump.Common.Imaging;

/// <summary>
/// Implements an IImageRegionCopyService that uses the current desktop window as the copy source.
/// This is used during the main application runtime to generate preview images of the desktop.
/// </summary>
public sealed class DesktopImageRegionCopyService : IImageRegionCopyService
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
            targetGraphics, STRETCH_BLT_MODE.STRETCH_HALFTONE);
        stopwatch.Stop();

        var source = sourceBounds.ToRectangle();
        var target = targetBounds.ToRectangle();
        var result = PInvoke.StretchBlt(
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
            ROP_CODE.SRCCOPY);
        ResultHandler.ThrowIfZero(result, getLastError: false, memberName: nameof(PInvoke.StretchBlt));

        // we need to release the graphics device context handle before anything
        // else tries to use the Graphics object - otherwise it'll give an error
        // from GDI saying "Object is currently in use elsewhere"
        DesktopImageRegionCopyService.FreeGraphicsDeviceContext(targetGraphics, ref previewHdc);

        DesktopImageRegionCopyService.FreeDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
    }

    private static (HWND DesktopHwnd, HDC DesktopHdc) GetDesktopDeviceContext()
    {
        var desktopHwnd = PInvoke.GetDesktopWindow();
        var desktopHdc = PInvoke.GetWindowDC(desktopHwnd);
        ResultHandler.HandleResult(desktopHdc, !desktopHdc.IsNull, getLastError: false, memberName: nameof(PInvoke.GetWindowDC));
        return (desktopHwnd, desktopHdc);
    }

    private static void FreeDesktopDeviceContext(ref HWND desktopHwnd, ref HDC desktopHdc)
    {
        if (!desktopHwnd.IsNull && !desktopHdc.IsNull)
        {
            var result = PInvoke.ReleaseDC(desktopHwnd, desktopHdc);
            ResultHandler.ThrowIfZero(result, getLastError: false, memberName: nameof(PInvoke.ReleaseDC));
        }

        desktopHwnd = HWND.Null;
        desktopHdc = HDC.Null;
    }

    /// <summary>
    /// Checks if the target device context handle exists, and creates a new one from the
    /// specified Graphics object if not.
    /// </summary>
    private static HDC GetGraphicsDeviceContext(Graphics graphics, STRETCH_BLT_MODE mode)
    {
        var graphicsHdc = (HDC)graphics.GetHdc();
        var result = PInvoke.SetStretchBltMode(graphicsHdc, mode);
        ResultHandler.ThrowIfZero(result, getLastError: false, memberName: nameof(PInvoke.SetStretchBltMode));
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

        graphics.ReleaseHdc(graphicsHdc);
        graphicsHdc = HDC.Null;
    }
}
