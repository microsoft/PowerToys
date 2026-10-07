// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;

using MouseJump.Common.Capture;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Common.Helpers;

/// <summary>
/// Composes a <see cref="PreviewStyle"/> into a single flattened bitmap - border, background,
/// and per-screen bezels/screenshots all baked into one image, with each screen's content
/// supplied by <paramref name="captureProvider"/> (see <see cref="ComposePreviewAsync"/>). This is a
/// fallback for contexts that can't render the layout as native, separately-composited UI
/// elements the way the WinUI3 app's <c>PreviewWindow</c>/<c>PreviewPane</c> do -
/// currently used by the Settings UI's live style preview (against a bundled
/// fake-desktop image, via <see cref="StaticScreenshotCaptureProvider"/>) and by
/// <c>DrawingHelperTests</c> (to keep comparing against golden images captured under this same
/// flattened layout).
/// </summary>
public static class PreviewHelper
{
    /// <summary>
    /// Builds a static bitmap containing a composed view of the borders, backgrounds, bezels
    /// and screenshots for a pop-up preview. This is a static bitmap representation of the WinUI
    /// PreviewWindow form's controls, and can be used as the "live preview" in the SettingsUI
    /// and in tests to confirm the visual components all draw correctly compared to a reference image.
    /// </summary>
    public static async Task<Bitmap> ComposePreviewAsync(
        PreviewStyle previewStyle,
        IScreenshotCaptureProvider captureProvider,
        DisplayInfo displayInfo,
        SizeInfo? maximumSize = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewStyle);
        ArgumentNullException.ThrowIfNull(captureProvider);
        ArgumentNullException.ThrowIfNull(displayInfo);

        var previewLayout = LayoutHelper.GetPreviewLayout(previewStyle, displayInfo, maximumSize);
        var canvasLayout = previewLayout.CanvasLayout;

        // in the WinUI PreviewWindow, the entire visualisation is composed of the outer border
        // and an inner PreviewPane which contains the main background, bezel and screenshots.
        // the outer "window style" bounds need to be anchored to the top left corner of the
        // rendered image
        var previewWindowStyle = LayoutHelper.GetPreviewWindowStyle(previewStyle.CanvasStyle);
        var windowBounds = LayoutHelper.GetPreviewWindowBounds(new RectangleInfo(previewLayout.PreviewSize), previewWindowStyle)
            .MoveTo(new PointInfo(0, 0));

        // windowBounds is already (0,0)-normalized (see MoveTo above), so the inner ContentBounds
        // coordinates are the absolute position for the background, bezels and screenshots
        var paneOffsetX = windowBounds.ContentBounds.X;
        var paneOffsetY = windowBounds.ContentBounds.Y;

        var bounds = windowBounds.OuterBounds.ToRectangle();
        var previewImage = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(previewImage);
        graphics.Clear(Color.Transparent);

        using (var border = DrawingHelper.RenderBorder(windowBounds.BorderBounds.Size, previewWindowStyle.BorderStyle))
        {
            graphics.DrawImageUnscaled(border, (int)windowBounds.BorderBounds.X, (int)windowBounds.BorderBounds.Y);
        }

        using (var background = DrawingHelper.RenderBackground(canvasLayout.CanvasBounds.OuterBounds.Size, canvasLayout.CanvasStyle.BackgroundStyle))
        {
            graphics.DrawImageUnscaled(background, (int)paneOffsetX, (int)paneOffsetY);
        }

        foreach (var screenLayout in canvasLayout.DeviceLayouts.SelectMany(deviceLayout => deviceLayout.ScreenLayouts))
        {
            var screenBounds = screenLayout.ScreenBounds;

            using (var bezel = DrawingHelper.RenderBorder(screenBounds.BorderBounds.Size, screenLayout.ScreenStyle.BorderStyle))
            {
                graphics.DrawImageUnscaled(bezel, (int)(screenBounds.BorderBounds.X + paneOffsetX), (int)(screenBounds.BorderBounds.Y + paneOffsetY));
            }

            // the capture provider owns anti-bleed/interpolation settings for its own thumbnail
            // (see StaticScreenshotCaptureProvider/DesktopScreenshotCaptureProvider) - it always
            // hands back a bitmap already sized to screenBounds.ContentBounds.Size, so this is a
            // plain 1:1 paste, not a scaled draw.
            using var screenshotImage = await captureProvider.CaptureAsync(
                screenLayout.ScreenInfo.DisplayArea,
                screenBounds.ContentBounds.Size,
                cancellationToken).ConfigureAwait(false);
            graphics.DrawImageUnscaled(
                screenshotImage,
                (int)(paneOffsetX + screenBounds.ContentBounds.X),
                (int)(paneOffsetY + screenBounds.ContentBounds.Y));
        }

        return previewImage;
    }
}
