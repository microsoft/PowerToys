// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using MouseJump.Common.Helpers;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
using MouseJump.Models.Styles;
using MouseJump.WinUI3.Internal.Helpers;

using Image = Microsoft.UI.Xaml.Controls.Image;

namespace MouseJump.WinUI3.UI;

public sealed partial class PreviewPane
{
    private static void OnLayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((PreviewPane)sender).ApplyLayout((PreviewLayout?)e.NewValue);

    private void ApplyLayout(PreviewLayout? layout)
    {
        if (layout is null)
        {
            this.ScreensCanvas.Children.Clear();
            this.screenSlots = [];
            this.Width = 0;
            this.Height = 0;
            this.BackgroundImage.Source = null;
            return;
        }

        var scale = this.GetRasterizationScale();
        var bounds = layout.CanvasLayout.CanvasBounds.OuterBounds;
        this.Width = (double)bounds.Width / scale;
        this.Height = (double)bounds.Height / scale;

        using var backgroundBitmap = DrawingHelper.RenderBackground(bounds.Size, layout.CanvasLayout.CanvasStyle.BackgroundStyle);
        this.BackgroundImage.Source = MediaHelper.ToBitmapImage(backgroundBitmap);

        var newScreenLayouts = layout.CanvasLayout.DeviceLayouts
            .SelectMany(deviceLayout => deviceLayout.ScreenLayouts)
            .ToList();

        // a new activation - tell blurPipeline which physical screens currently exist, before
        // consulting it for placeholders below, so it only ever hands back blurred images for
        // screens that are still connected
        this.blurPipeline.SetActiveScreens(
            newScreenLayouts.Select(screenLayout => screenLayout.ScreenInfo).ToList());

        // check screen slots from the previous activation and reuse any that are
        // still valid so we can avoid needing to redraw the bezel. where the monitor
        // at an index is now different (e.g. due to a monitor being connected or
        // disconnected) invalidate the existing slot and force a redraw.
        var newSlots = new List<ScreenSlot>(newScreenLayouts.Count);
        for (var i = 0; i < newScreenLayouts.Count; i++)
        {
            var screenLayout = newScreenLayouts[i];
            var previousSlot = (i < this.screenSlots.Count) ? this.screenSlots[i] : null;

            if (previousSlot is not null && PreviewPane.CanReuse(previousSlot.ScreenLayout, screenLayout))
            {
                // pixel-identical bezel/placeholder to what's already on screen at this
                // position - keep the visuals, just point the slot at the new ScreenLayout
                // instance (SetScreenshot looks slots up by reference) and clear the content
                // image (falling back to this screen's blurred stand-in, if any - see
                // GetBlurredPlaceholder) since the screenshot it was showing is now stale
                // desktop state regardless of whether the bezel itself changed
                previousSlot.ContentImage.Source = this.GetBlurredPlaceholder(screenLayout.ScreenInfo);

                // CanReuse only guarantees the *physical-pixel* bounds are unchanged - the
                // DIP-space Width/Height/Canvas.Left/Top these elements were last positioned
                // at also depend on scale (see GetRasterizationScale), which can differ from
                // last time if this activation's window landed on a different-DPI monitor
                // than the previous one. Reapplying position/size here is cheap (a handful of
                // property writes, no re-render) and keeps that in sync even though the
                // visuals themselves are being reused as-is.
                var screenBounds = screenLayout.ScreenBounds;
                PreviewPane.PositionElement(previousSlot.BezelImage, screenBounds.BorderBounds, scale);
                if (previousSlot.PlaceholderRectangle is not null)
                {
                    PreviewPane.PositionElement(previousSlot.PlaceholderRectangle, screenBounds.PaddingBounds, scale);
                }

                PreviewPane.PositionElement(previousSlot.ContentImage, screenBounds.ContentBounds, scale);

                newSlots.Add(new ScreenSlot(
                    screenLayout, previousSlot.BezelImage, previousSlot.PlaceholderRectangle, previousSlot.ContentImage));
            }
            else
            {
                this.RemoveScreenSlot(previousSlot);
                newSlots.Add(this.CreateScreenSlot(screenLayout, scale));
            }
        }

        // trim any surplus slots left over from a layout that had more screens than
        // this one does (e.g. a monitor was unplugged) - everything up to
        // newScreenLayouts.Count has already been handled (reused or replaced)
        for (var i = newScreenLayouts.Count; i < this.screenSlots.Count; i++)
        {
            this.RemoveScreenSlot(this.screenSlots[i]);
        }

        this.screenSlots = newSlots;
    }

    /// <summary>
    /// Reports whether <paramref name="current"/> would produce a pixel-identical bezel and
    /// placeholder to <paramref name="previous"/> - if so, <see cref="ApplyLayout"/> reuses the
    /// existing visuals instead of re-rendering them. Deliberately narrow: only the fields that
    /// actually feed the bezel rendering process are compared (the three bounds rectangles
    /// used for positioning, the border style used for the bezel, and the background color used
    /// for the placeholder). Margin, padding and the rest of <see cref="BoxStyle"/> don't need
    /// to be compared as the relevant values have already been calculated into the bezel size.
    /// </summary>
    private static bool CanReuse(ScreenLayout previous, ScreenLayout current)
        => PreviewPane.CanReuse(previous.ScreenBounds, current.ScreenBounds)
        && PreviewPane.CanReuse(previous.ScreenStyle.BorderStyle, current.ScreenStyle.BorderStyle)
        && previous.ScreenStyle.BackgroundStyle.Color1 == current.ScreenStyle.BackgroundStyle.Color1;

    private static bool CanReuse(BoxBounds previous, BoxBounds current)
        => PreviewPane.CanReuse(previous.BorderBounds, current.BorderBounds)
        && PreviewPane.CanReuse(previous.PaddingBounds, current.PaddingBounds)
        && PreviewPane.CanReuse(previous.ContentBounds, current.ContentBounds);

    private static bool CanReuse(RectangleInfo previous, RectangleInfo current)
        => previous.X == current.X
        && previous.Y == current.Y
        && previous.Width == current.Width
        && previous.Height == current.Height;

    private static bool CanReuse(BorderStyle previous, BorderStyle current)
        => previous.Color == current.Color
        && previous.Left == current.Left
        && previous.Top == current.Top
        && previous.Right == current.Right
        && previous.Bottom == current.Bottom
        && previous.Depth == current.Depth;

    private void RemoveScreenSlot(ScreenSlot? slot)
    {
        if (slot is null)
        {
            return;
        }

        this.ScreensCanvas.Children.Remove(slot.BezelImage);
        if (slot.PlaceholderRectangle is not null)
        {
            this.ScreensCanvas.Children.Remove(slot.PlaceholderRectangle);
        }

        this.ScreensCanvas.Children.Remove(slot.ContentImage);
    }

    /// <summary>
    /// Creates the visual elements for a single screen - a bezel (rendered the same way as the
    /// host's own outer border, see <see cref="DrawingHelper.RenderBorder"/>), a placeholder
    /// fill shown until the real screenshot arrives, and a content <see cref="Image"/> that
    /// starts either blank or showing this screen's last real screenshot, blurred (see
    /// <see cref="GetBlurredPlaceholder"/>), until it's populated for real via
    /// <see cref="SetScreenshot"/>. The bezel and placeholder/content don't overlap - a screen's
    /// border ring and its content area are disjoint (see <see cref="Models.Drawing.BoxBounds"/>)
    /// - so paint order between them doesn't matter.
    /// </summary>
    private ScreenSlot CreateScreenSlot(ScreenLayout screenLayout, double scale)
    {
        var screenBounds = screenLayout.ScreenBounds;

        Image bezelImage;
        using (var bezelBitmap = DrawingHelper.RenderBorder(
            screenBounds.BorderBounds.Size, screenLayout.ScreenStyle.BorderStyle))
        {
            bezelImage = new Image
            {
                Source = MediaHelper.ToBitmapImage(bezelBitmap),
                Stretch = Stretch.Fill,
            };
            PreviewPane.PositionElement(bezelImage, screenBounds.BorderBounds, scale);
            this.ScreensCanvas.Children.Add(bezelImage);
        }

        Microsoft.UI.Xaml.Shapes.Rectangle? placeholder = null;
        var placeholderColor = screenLayout.ScreenStyle.BackgroundStyle.Color1;
        if (placeholderColor is not null)
        {
            placeholder = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(PreviewPane.ToWindowsColor(placeholderColor.Value)),
            };
            PreviewPane.PositionElement(placeholder, screenBounds.PaddingBounds, scale);
            this.ScreensCanvas.Children.Add(placeholder);
        }

        var contentImage = new Image
        {
            Source = this.GetBlurredPlaceholder(screenLayout.ScreenInfo),
            Stretch = Stretch.Fill,
        };
        PreviewPane.PositionElement(contentImage, screenBounds.ContentBounds, scale);
        this.ScreensCanvas.Children.Add(contentImage);

        return new ScreenSlot(screenLayout, bezelImage, placeholder, contentImage);
    }

    private double GetRasterizationScale()
        => this.XamlRoot?.RasterizationScale ?? 1.0;

    private static void PositionElement(FrameworkElement element, RectangleInfo bounds, double scale)
    {
        element.Width = (double)bounds.Width / scale;
        element.Height = (double)bounds.Height / scale;
        Canvas.SetLeft(element, (double)bounds.X / scale);
        Canvas.SetTop(element, (double)bounds.Y / scale);
    }

    private static Windows.UI.Color ToWindowsColor(Color color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
}
