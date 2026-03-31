// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// A gentle breathing/pulsing glow effect with concentric color rings.
/// Multiple radial gradients at different scales create a layered
/// heartbeat-like pulse with slowly shifting colors.
/// </summary>
internal sealed class PulseGlowEffect : IBackgroundEffect
{
    private const int RingCount = 3;

    private Compositor? _compositor;
    private ContainerVisual? _root;
    private SpriteVisual[]? _rings;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _root = rootVisual;
        _size = size;

        _rings = new SpriteVisual[RingCount];

        var colors = new[]
        {
            (inner: Color.FromArgb(140, 0, 180, 240), outer: Color.FromArgb(0, 0, 80, 180)),
            (inner: Color.FromArgb(100, 120, 0, 220), outer: Color.FromArgb(0, 60, 0, 140)),
            (inner: Color.FromArgb(80, 0, 200, 160), outer: Color.FromArgb(0, 0, 100, 100)),
        };

        for (var i = 0; i < RingCount; i++)
        {
            var ring = compositor.CreateSpriteVisual();
            ring.AnchorPoint = new Vector2(0.5f, 0.5f);
            ring.Offset = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0);

            var scale = 0.6f + (i * 0.25f);
            var diam = Math.Max(size.X, size.Y) * scale;
            ring.Size = new Vector2(diam, diam);
            ring.Opacity = 0.5f;

            var brush = compositor.CreateRadialGradientBrush();
            brush.EllipseCenter = new Vector2(0.5f, 0.5f);
            brush.EllipseRadius = new Vector2(0.5f, 0.5f);

            var c = colors[i];
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, c.inner));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb((byte)(c.inner.A / 3), c.inner.R, c.inner.G, c.inner.B)));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, c.outer));

            ring.Brush = brush;
            _rings[i] = ring;
            rootVisual.Children.InsertAtTop(ring);
        }
    }

    public void Start()
    {
        if (_compositor == null || _rings == null)
        {
            return;
        }

        var breatheEasing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));

        for (var i = 0; i < _rings.Length; i++)
        {
            var ring = _rings[i];
            var period = 3.5 + (i * 1.2);

            // Staggered opacity breathe
            var opAnim = _compositor.CreateScalarKeyFrameAnimation();
            opAnim.Duration = TimeSpan.FromSeconds(period);
            opAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            opAnim.InsertKeyFrame(0f, 0.2f, breatheEasing);
            opAnim.InsertKeyFrame(0.5f, 0.65f, breatheEasing);
            opAnim.InsertKeyFrame(1f, 0.2f, breatheEasing);
            ring.StartAnimation("Opacity", opAnim);

            // Scale breathe — inner ring pumps more than outer
            var scaleMin = 0.8f + (i * 0.05f);
            var scaleMax = 1.15f - (i * 0.05f);
            var scAnim = _compositor.CreateVector2KeyFrameAnimation();
            scAnim.Duration = TimeSpan.FromSeconds(period);
            scAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            scAnim.InsertKeyFrame(0f, new Vector2(scaleMin, scaleMin), breatheEasing);
            scAnim.InsertKeyFrame(0.5f, new Vector2(scaleMax, scaleMax), breatheEasing);
            scAnim.InsertKeyFrame(1f, new Vector2(scaleMin, scaleMin), breatheEasing);
            ring.StartAnimation("Scale.XY", scAnim);

            // Slow rotation for visual interest
            var rotAnim = _compositor.CreateScalarKeyFrameAnimation();
            rotAnim.Duration = TimeSpan.FromSeconds(period * 8);
            rotAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            var dir = (i % 2 == 0) ? 360f : -360f;
            rotAnim.InsertKeyFrame(0f, 0f, _compositor.CreateLinearEasingFunction());
            rotAnim.InsertKeyFrame(1f, dir, _compositor.CreateLinearEasingFunction());
            ring.StartAnimation("RotationAngleInDegrees", rotAnim);
        }
    }

    public void Stop()
    {
        if (_rings == null)
        {
            return;
        }

        foreach (var ring in _rings)
        {
            ring.StopAnimation("Opacity");
            ring.StopAnimation("Scale.XY");
            ring.StopAnimation("RotationAngleInDegrees");
        }
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        if (_rings == null)
        {
            return;
        }

        for (var i = 0; i < _rings.Length; i++)
        {
            _rings[i].Offset = new Vector3(newSize.X * 0.5f, newSize.Y * 0.5f, 0);
            var scale = 0.6f + (i * 0.25f);
            var diam = Math.Max(newSize.X, newSize.Y) * scale;
            _rings[i].Size = new Vector2(diam, diam);
        }
    }

    public void Dispose()
    {
        Stop();
        if (_rings != null)
        {
            foreach (var ring in _rings)
            {
                ring.Dispose();
            }
        }

        _rings = null;
    }
}
