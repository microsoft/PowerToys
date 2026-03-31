// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Aurora / Northern Lights effect: multiple overlapping vertical gradient
/// bands slowly drift horizontally and shift colors, creating a flowing
/// curtain of light reminiscent of the aurora borealis.
/// </summary>
internal sealed class AuroraEffect : IBackgroundEffect
{
    private const int LayerCount = 5;

    private static readonly Color[][] LayerPalettes =
    [
        [
            Color.FromArgb(120, 0, 255, 140),
            Color.FromArgb(80, 0, 200, 180),
            Color.FromArgb(0, 0, 140, 160),
        ],
        [
            Color.FromArgb(100, 0, 200, 220),
            Color.FromArgb(70, 20, 120, 255),
            Color.FromArgb(0, 10, 60, 200),
        ],
        [
            Color.FromArgb(90, 140, 50, 240),
            Color.FromArgb(60, 200, 80, 180),
            Color.FromArgb(0, 160, 30, 120),
        ],
        [
            Color.FromArgb(80, 0, 240, 200),
            Color.FromArgb(50, 0, 180, 240),
            Color.FromArgb(0, 0, 100, 180),
        ],
        [
            Color.FromArgb(70, 100, 0, 255),
            Color.FromArgb(40, 180, 40, 200),
            Color.FromArgb(0, 120, 20, 140),
        ],
    ];

    private Compositor? _compositor;
    private ContainerVisual? _root;
    private SpriteVisual[]? _layers;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _root = rootVisual;
        _size = size;

        _layers = new SpriteVisual[LayerCount];

        for (var i = 0; i < LayerCount; i++)
        {
            var layer = compositor.CreateSpriteVisual();

            // Each layer is wider than the viewport for seamless scrolling
            layer.Size = new Vector2(size.X * 2.5f, size.Y);
            layer.Offset = new Vector3(-size.X * 0.4f * i, 0, 0);
            layer.Opacity = 0.45f;
            layer.RotationAngleInDegrees = -3f + (i * 1.5f);

            var gradientBrush = compositor.CreateLinearGradientBrush();
            gradientBrush.StartPoint = new Vector2(0, 0);
            gradientBrush.EndPoint = new Vector2(0, 1);

            var palette = LayerPalettes[i % LayerPalettes.Length];
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, palette[0]));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, palette[1]));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, palette[2]));

            layer.Brush = gradientBrush;
            _layers[i] = layer;
            rootVisual.Children.InsertAtTop(layer);
        }
    }

    public void Start()
    {
        if (_compositor == null || _layers == null)
        {
            return;
        }

        for (var i = 0; i < _layers.Length; i++)
        {
            AnimateLayer(_layers[i], i);
        }
    }

    public void Stop()
    {
        if (_layers == null)
        {
            return;
        }

        foreach (var layer in _layers)
        {
            layer.StopAnimation("Offset.X");
            layer.StopAnimation("Opacity");
        }
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        if (_layers == null)
        {
            return;
        }

        for (var i = 0; i < _layers.Length; i++)
        {
            _layers[i].Size = new Vector2(newSize.X * 2.5f, newSize.Y);
        }
    }

    public void Dispose()
    {
        Stop();
        if (_layers != null)
        {
            foreach (var layer in _layers)
            {
                layer.Dispose();
            }
        }

        _layers = null;
    }

    private void AnimateLayer(SpriteVisual layer, int index)
    {
        if (_compositor == null)
        {
            return;
        }

        var period = 12.0 + (index * 5.0);
        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));

        // Horizontal drift: layer slides and undulates
        var xRange = _size.X * 0.6f;
        var startX = -xRange * 0.3f;
        var xAnim = _compositor.CreateScalarKeyFrameAnimation();
        xAnim.Duration = TimeSpan.FromSeconds(period);
        xAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        xAnim.InsertKeyFrame(0f, startX - (xRange * 0.4f), easing);
        xAnim.InsertKeyFrame(0.33f, startX + (xRange * 0.3f), easing);
        xAnim.InsertKeyFrame(0.66f, startX - (xRange * 0.2f), easing);
        xAnim.InsertKeyFrame(1f, startX - (xRange * 0.4f), easing);

        layer.StartAnimation("Offset.X", xAnim);

        // Vertical shimmy — layers gently bob up and down
        var yAnim = _compositor.CreateScalarKeyFrameAnimation();
        yAnim.Duration = TimeSpan.FromSeconds(period * 0.7);
        yAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        var yRange = _size.Y * 0.06f;
        yAnim.InsertKeyFrame(0f, -yRange, easing);
        yAnim.InsertKeyFrame(0.5f, yRange, easing);
        yAnim.InsertKeyFrame(1f, -yRange, easing);

        layer.StartAnimation("Offset.Y", yAnim);

        // Opacity shimmer
        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.Duration = TimeSpan.FromSeconds(period * 0.5);
        opacityAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnim.InsertKeyFrame(0f, 0.3f, easing);
        opacityAnim.InsertKeyFrame(0.5f, 0.6f, easing);
        opacityAnim.InsertKeyFrame(1f, 0.3f, easing);

        layer.StartAnimation("Opacity", opacityAnim);
    }
}
