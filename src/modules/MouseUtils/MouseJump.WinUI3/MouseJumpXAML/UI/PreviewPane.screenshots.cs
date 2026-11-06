// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

using MouseJump.Models.Display;
using MouseJump.Models.Layout;
using MouseJump.WinUI3.Internal.Helpers;

using Image = Microsoft.UI.Xaml.Controls.Image;

namespace MouseJump.WinUI3.UI;

/// <summary>
/// The half of <see cref="PreviewPane"/> concerned with a screen's actual pixel content -
/// backfilling a real screenshot (<see cref="SetScreenshot"/>), crossfading it in
/// (<see cref="CrossfadeContent"/>), and supplying a blurred stand-in for whatever hasn't
/// backfilled yet (<see cref="GetHistoricalPlaceholder"/>) - as distinct from
/// <c>PreviewPane.layout.cs</c>'s concern of building/positioning the bezel and placeholder
/// shapes those images sit inside.
/// </summary>
internal sealed partial class PreviewPane
{
    /// <summary>
    /// Applies the real screenshot for a single screen, replacing any placeholder fill
    /// (e.g. solid background fill or blurred cached screenshot). Only called once the
    /// capture pipeline has produced the image for the specified screen. Takes ownership
    /// of <paramref name="image"/> and is responsible for disposing it. If
    /// <paramref name="screenLayout"/> isn't part of the current <see cref="Layout"/> any
    /// more (e.g. a newer activation replaced it while this capture was still in flight),
    /// disposes <paramref name="image"/> and returns rather than throwing - a stale,
    /// superseded capture result has nowhere left to go.
    /// </summary>
    /// <remarks>
    /// Also hands the <paramref name="image"/> to the <see cref="blurPipeline"/>, which
    /// generates a blurred placeholder that can be used during the next activation if the
    /// capture isn't complete by the time the form is displayed.
    /// </remarks>
    public void SetScreenshot(ScreenLayout screenLayout, Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(screenLayout);
        ArgumentNullException.ThrowIfNull(image);

        var index = this.screenSlots.FindIndex(
            s => object.ReferenceEquals(s.ScreenLayout, screenLayout));
        if (index < 0)
        {
            image.Dispose();
            return;
        }

        this.CrossfadeContent(this.screenSlots[index], MediaHelper.ToBitmapImage(image));
        this.blurPipeline.SetScreenshot(screenLayout.ScreenInfo, image);
    }

    /// <summary>
    /// Puts the <paramref name="newSource"/> image into the given PreviewPane screenshot
    /// image control by cross-fading it from <paramref name="slot"/>'s existing content.
    /// WinUI has no built-in way to crossfade an <see cref="Image"/> between two sources,
    /// so this adds a temporary overlay <see cref="Image"/> at the same position, animates
    /// its <see cref="UIElement.Opacity"/> from 0 to 1, then (once that's done) makes
    /// <paramref name="newSource"/> the slot's own real content and discards the overlay - by
    /// then it's fully opaque, so the swap itself is invisible.
    /// </summary>
    private void CrossfadeContent(ScreenSlot slot, ImageSource newSource)
    {
        var overlay = new Image
        {
            Source = newSource,
            Stretch = Stretch.Fill,
            Opacity = 0,
            Width = slot.ContentImage.Width,
            Height = slot.ContentImage.Height,
        };
        Canvas.SetLeft(overlay, Canvas.GetLeft(slot.ContentImage));
        Canvas.SetTop(overlay, Canvas.GetTop(slot.ContentImage));
        this.ScreensCanvas.Children.Add(overlay);

        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(PreviewPane.ContentTransitionDuration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, overlay);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            slot.ContentImage.Source = newSource;
            this.ScreensCanvas.Children.Remove(overlay);
        };
        storyboard.Begin();
    }

    /// <summary>
    /// Returns this screen's last real screenshot, blurred, if <see cref="blurPipeline"/> still
    /// has one available - or <see langword="null"/> (falling back to the plain placeholder
    /// fill) if not - for a screen whose current activation hasn't captured yet. See
    /// <see cref="ScreenshotBlurPipeline.TryGet"/> for the freshness rules.
    /// </summary>
    private WriteableBitmap? GetBlurredPlaceholder(ScreenInfo screenInfo)
    {
        WriteableBitmap? result = null;
        this.blurPipeline.TryBorrow(screenInfo, blurredImage => result = MediaHelper.ToBitmapImage(blurredImage));
        return result;
    }
}
