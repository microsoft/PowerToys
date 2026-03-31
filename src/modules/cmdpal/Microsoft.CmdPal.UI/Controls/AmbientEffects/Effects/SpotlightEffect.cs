// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Spotlight effect: a large, soft radial gradient light that slowly roams
/// around the background tracing a smooth Lissajous curve. Pairs well with
/// background images as a subtle animated highlight.
/// </summary>
internal sealed class SpotlightEffect : IBackgroundEffect
{
    private Compositor? _compositor;
    private ContainerVisual? _root;
    private SpriteVisual? _spotVisual;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _root = rootVisual;
        _size = size;

        var spotDiameter = Math.Max(size.X, size.Y) * 0.9f;
        _spotVisual = compositor.CreateSpriteVisual();
        _spotVisual.Size = new Vector2(spotDiameter, spotDiameter);
        _spotVisual.AnchorPoint = new Vector2(0.5f, 0.5f);
        _spotVisual.Offset = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0);
        _spotVisual.Opacity = 0.3f;

        var gradientBrush = compositor.CreateRadialGradientBrush();
        gradientBrush.EllipseCenter = new Vector2(0.5f, 0.5f);
        gradientBrush.EllipseRadius = new Vector2(0.5f, 0.5f);

        // Warm white/soft gold light
        gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(160, 255, 245, 220)));
        gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.35f, Color.FromArgb(80, 255, 230, 190)));
        gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.7f, Color.FromArgb(20, 200, 180, 150)));
        gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 150, 130, 100)));

        _spotVisual.Brush = gradientBrush;
        rootVisual.Children.InsertAtTop(_spotVisual);
    }

    public void Start()
    {
        if (_compositor == null || _spotVisual == null)
        {
            return;
        }

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.45f, 0.05f), new Vector2(0.55f, 0.95f));

        // Lissajous X motion: period ~18s
        var xAnim = _compositor.CreateScalarKeyFrameAnimation();
        xAnim.Duration = TimeSpan.FromSeconds(18);
        xAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        var cx = _size.X * 0.5f;
        var rx = _size.X * 0.4f;
        xAnim.InsertKeyFrame(0f, cx + rx, easing);
        xAnim.InsertKeyFrame(0.25f, cx, easing);
        xAnim.InsertKeyFrame(0.5f, cx - rx, easing);
        xAnim.InsertKeyFrame(0.75f, cx, easing);
        xAnim.InsertKeyFrame(1f, cx + rx, easing);
        _spotVisual.StartAnimation("Offset.X", xAnim);

        // Lissajous Y motion: period ~24s (3:4 ratio with X for variety)
        var yAnim = _compositor.CreateScalarKeyFrameAnimation();
        yAnim.Duration = TimeSpan.FromSeconds(24);
        yAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        var cy = _size.Y * 0.5f;
        var ry = _size.Y * 0.35f;
        yAnim.InsertKeyFrame(0f, cy, easing);
        yAnim.InsertKeyFrame(0.25f, cy + ry, easing);
        yAnim.InsertKeyFrame(0.5f, cy, easing);
        yAnim.InsertKeyFrame(0.75f, cy - ry, easing);
        yAnim.InsertKeyFrame(1f, cy, easing);
        _spotVisual.StartAnimation("Offset.Y", yAnim);

        // Gentle opacity pulse
        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.Duration = TimeSpan.FromSeconds(10);
        opacityAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnim.InsertKeyFrame(0f, 0.25f, easing);
        opacityAnim.InsertKeyFrame(0.5f, 0.4f, easing);
        opacityAnim.InsertKeyFrame(1f, 0.25f, easing);
        _spotVisual.StartAnimation("Opacity", opacityAnim);
    }

    public void Stop()
    {
        _spotVisual?.StopAnimation("Offset.X");
        _spotVisual?.StopAnimation("Offset.Y");
        _spotVisual?.StopAnimation("Opacity");
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        if (_spotVisual == null)
        {
            return;
        }

        var spotDiameter = Math.Max(newSize.X, newSize.Y) * 0.9f;
        _spotVisual.Size = new Vector2(spotDiameter, spotDiameter);
    }

    public void Dispose()
    {
        Stop();
        _spotVisual?.Dispose();
        _spotVisual = null;
    }
}
