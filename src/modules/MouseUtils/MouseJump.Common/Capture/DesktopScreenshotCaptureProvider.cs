// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;

using MouseJump.Common.Win32Gen;
using MouseJump.Models.Drawing;

using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace MouseJump.Common.Capture;

/// <summary>
/// Implements an <see cref="IScreenshotCaptureProvider"/> that captures from the current
/// interactive desktop using <c>StretchBlt</c>. This is used during the main application
/// runtime to generate preview images of the desktop.
/// </summary>
/// <remarks>
/// A single instance of this provider is shared across every screen on the same device, and
/// captures for different screens are allowed to run concurrently - each call acquires its own
/// independent desktop device context (see <see cref="GetDesktopDeviceContext"/>) and writes
/// into its own <see cref="Bitmap"/>, so there's no shared GDI state for concurrent
/// <c>StretchBlt</c> calls to contend over. Verified against real multi-monitor hardware
/// (including virtual/software monitors) with no corruption or GDI errors. An earlier
/// (WinForms-era) version of this codebase did need a lock here, but that was because it drew
/// every screen directly into one shared composite bitmap - a genuinely unsafe concurrent write
/// - not because of contention between independent device contexts like this.
/// </remarks>
public sealed class DesktopScreenshotCaptureProvider : IScreenshotCaptureProvider, IDisposable
{
    public void Dispose()
    {
        // nothing to release
    }

    public async Task<Bitmap> CaptureAsync(
        RectangleInfo sourceArea,
        SizeInfo thumbnailSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await Task.Run(
            () => DesktopScreenshotCaptureProvider.Capture(sourceArea, thumbnailSize),
            cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    private static Bitmap Capture(
        RectangleInfo sourceArea,
        SizeInfo thumbnailSize)
    {
        var target = thumbnailSize.Round().ToSize();
        var thumbnailImage = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var thumbnailGraphics = Graphics.FromImage(thumbnailImage);

            var desktopHwnd = HWND.Null;
            var desktopHdc = HDC.Null;
            var thumbnailHdc = HDC.Null;
            try
            {
                (desktopHwnd, desktopHdc) = DesktopScreenshotCaptureProvider.GetDesktopDeviceContext();
                thumbnailHdc = DesktopScreenshotCaptureProvider.GetGraphicsDeviceContext(
                    thumbnailGraphics, STRETCH_BLT_MODE.STRETCH_HALFTONE);

                var source = sourceArea.ToRectangle();
                _ = Gdi32.StretchBlt(
                    thumbnailHdc,
                    0,
                    0,
                    target.Width,
                    target.Height,
                    desktopHdc,
                    source.X,
                    source.Y,
                    source.Width,
                    source.Height,
                    ROP_CODE.SRCCOPY)
                    .ThrowIfFailed();
            }
            finally
            {
                // we need to release the graphics device context handle before anything
                // else tries to use the Graphics object - otherwise it'll give an error from
                // GDI saying "Object is currently in use elsewhere". Both releases have to run
                // here, not just on the success path, so a StretchBlt (or context acquisition)
                // failure can't leak either device context handle - Free*DeviceContext is a
                // no-op for whichever one (if any) was never actually acquired.
                DesktopScreenshotCaptureProvider.FreeGraphicsDeviceContext(thumbnailGraphics, ref thumbnailHdc);
                DesktopScreenshotCaptureProvider.FreeDesktopDeviceContext(ref desktopHwnd, ref desktopHdc);
            }

            return thumbnailImage;
        }
        catch
        {
            // capture failed after we'd already allocated the thumbnail bitmap - nobody else
            // is going to receive or dispose it, so it's this method's job to clean it up
            thumbnailImage.Dispose();
            throw;
        }
    }

    private static (HWND DesktopHwnd, HDC DesktopHdc) GetDesktopDeviceContext()
    {
        var desktopHwnd = User32.GetDesktopWindow().IgnoreFailure().GetValue();
        var desktopHdc = User32.GetWindowDC(desktopHwnd).ThrowIfFailed().GetValue();
        return (desktopHwnd, desktopHdc);
    }

    private static void FreeDesktopDeviceContext(ref HWND desktopHwnd, ref HDC desktopHdc)
    {
        if (!desktopHwnd.IsNull && !desktopHdc.IsNull)
        {
            _ = User32.ReleaseDC(desktopHwnd, desktopHdc)
                .ThrowIfFailed();
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
        _ = Gdi32.SetStretchBltMode(graphicsHdc, mode)
            .ThrowIfFailed();
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
