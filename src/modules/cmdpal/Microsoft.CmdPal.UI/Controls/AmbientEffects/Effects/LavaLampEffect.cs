// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Lava lamp effect: multiple large, softly glowing radial gradient blobs drift
/// around the background with layered sinusoidal motion. A gaussian blur
/// softens the edges so overlapping blobs appear to merge organically.
/// </summary>
internal sealed class LavaLampEffect : IBackgroundEffect
{
    private const int BlobCount = 5;
    private const float BlurAmount = 80f;

    private static readonly Color[] BlobColors =
    [
        Color.FromArgb(220, 255, 40, 100),
        Color.FromArgb(200, 180, 20, 240),
        Color.FromArgb(210, 255, 140, 20),
        Color.FromArgb(190, 20, 200, 200),
        Color.FromArgb(200, 255, 60, 200),
    ];

    private Compositor? _compositor;
    private ContainerVisual? _root;
    private SpriteVisual? _blurHost;
    private ContainerVisual? _blobContainer;
    private SpriteVisual[]? _blobs;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _root = rootVisual;
        _size = size;

        // Create a container for the blobs, which will be blurred as a group
        _blobContainer = compositor.CreateContainerVisual();
        _blobContainer.Size = size;

        _blobs = new SpriteVisual[BlobCount];
        var blobDiameter = Math.Max(size.X, size.Y) * 0.5f;

        for (var i = 0; i < BlobCount; i++)
        {
            var blob = compositor.CreateSpriteVisual();
            var scale = 0.7f + (0.6f * (i % 3) / 2f);
            blob.Size = new Vector2(blobDiameter * scale, blobDiameter * scale);
            blob.AnchorPoint = new Vector2(0.5f, 0.5f);

            // Spread blobs in a pentagon-like pattern
            var angle = (i * MathF.PI * 2) / BlobCount;
            blob.Offset = new Vector3(
                (size.X * 0.5f) + (size.X * 0.25f * MathF.Cos(angle)),
                (size.Y * 0.5f) + (size.Y * 0.25f * MathF.Sin(angle)),
                0);

            var brush = compositor.CreateRadialGradientBrush();
            brush.EllipseCenter = new Vector2(0.5f, 0.5f);
            brush.EllipseRadius = new Vector2(0.5f, 0.5f);

            var color = BlobColors[i % BlobColors.Length];
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, color));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb((byte)(color.A / 2), color.R, color.G, color.B)));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

            blob.Brush = brush;
            _blobs[i] = blob;
            _blobContainer.Children.InsertAtTop(blob);
        }

        // Apply gaussian blur to the entire blob container using an effect brush
        var blurEffect = new GaussianBlurEffect
        {
            Name = "Blur",
            BlurAmount = BlurAmount,
            BorderMode = EffectBorderMode.Soft,
            Source = new CompositionEffectSourceParameter("Source"),
        };

        var effectFactory = compositor.CreateEffectFactory(blurEffect);
        var effectBrush = effectFactory.CreateBrush();

        // Use a VisualSurface to capture the blob container
        var surface = compositor.CreateVisualSurface();
        surface.SourceVisual = _blobContainer;
        surface.SourceSize = size;

        var surfaceBrush = compositor.CreateSurfaceBrush(surface);
        surfaceBrush.Stretch = CompositionStretch.Fill;
        effectBrush.SetSourceParameter("Source", surfaceBrush);

        _blurHost = compositor.CreateSpriteVisual();
        _blurHost.Size = size;
        _blurHost.Brush = effectBrush;
        _blurHost.Opacity = 0.7f;

        rootVisual.Children.InsertAtTop(_blurHost);
    }

    public void Start()
    {
        if (_compositor == null || _blobs == null)
        {
            return;
        }

        for (var i = 0; i < _blobs.Length; i++)
        {
            AnimateBlob(_blobs[i], i);
        }
    }

    public void Stop()
    {
        if (_blobs == null)
        {
            return;
        }

        foreach (var blob in _blobs)
        {
            blob.StopAnimation("Offset.X");
            blob.StopAnimation("Offset.Y");
            blob.StopAnimation("Scale.XY");
        }
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        if (_blobContainer != null)
        {
            _blobContainer.Size = newSize;
        }

        if (_blurHost != null)
        {
            _blurHost.Size = newSize;

            // Update the visual surface source size
            if (_blurHost.Brush is CompositionEffectBrush effectBrush)
            {
                // Recreate the surface for the new size
                if (_compositor != null && _blobContainer != null)
                {
                    var surface = _compositor.CreateVisualSurface();
                    surface.SourceVisual = _blobContainer;
                    surface.SourceSize = newSize;

                    var surfaceBrush = _compositor.CreateSurfaceBrush(surface);
                    surfaceBrush.Stretch = CompositionStretch.Fill;
                    effectBrush.SetSourceParameter("Source", surfaceBrush);
                }
            }
        }

        if (_blobs != null)
        {
            var blobDiameter = Math.Max(newSize.X, newSize.Y) * 0.55f;
            foreach (var blob in _blobs)
            {
                blob.Size = new Vector2(blobDiameter, blobDiameter);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _blurHost?.Dispose();
        if (_blobs != null)
        {
            foreach (var blob in _blobs)
            {
                blob.Dispose();
            }
        }

        _blobContainer?.Dispose();
        _blurHost = null;
        _blobs = null;
        _blobContainer = null;
    }

    private void AnimateBlob(SpriteVisual blob, int index)
    {
        if (_compositor == null)
        {
            return;
        }

        // Each blob has a unique period and path so they move organically
        var basePeriod = 10.0 + (index * 3.7);
        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));

        // X oscillation — wide lazy arcs
        var xAnim = _compositor.CreateScalarKeyFrameAnimation();
        xAnim.Duration = TimeSpan.FromSeconds(basePeriod);
        xAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        var xCenter = _size.X * 0.5f;
        var xRange = _size.X * 0.4f;
        var xPhase = index * 0.2f;
        xAnim.InsertKeyFrame(0f, xCenter + (xRange * MathF.Cos(MathF.PI * 2 * xPhase)), easing);
        xAnim.InsertKeyFrame(0.25f, xCenter + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.25f))), easing);
        xAnim.InsertKeyFrame(0.5f, xCenter + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.5f))), easing);
        xAnim.InsertKeyFrame(0.75f, xCenter + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.75f))), easing);
        xAnim.InsertKeyFrame(1f, xCenter + (xRange * MathF.Cos(MathF.PI * 2 * xPhase)), easing);

        blob.StartAnimation("Offset.X", xAnim);

        // Y oscillation — slightly different ratio for figure-8-like paths
        var yPeriod = basePeriod * 1.4;
        var yAnim = _compositor.CreateScalarKeyFrameAnimation();
        yAnim.Duration = TimeSpan.FromSeconds(yPeriod);
        yAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        var yCenter = _size.Y * 0.5f;
        var yRange = _size.Y * 0.38f;
        var yPhase = index * 0.33f;
        yAnim.InsertKeyFrame(0f, yCenter + (yRange * MathF.Sin(MathF.PI * 2 * yPhase)), easing);
        yAnim.InsertKeyFrame(0.25f, yCenter + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.25f))), easing);
        yAnim.InsertKeyFrame(0.5f, yCenter + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.5f))), easing);
        yAnim.InsertKeyFrame(0.75f, yCenter + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.75f))), easing);
        yAnim.InsertKeyFrame(1f, yCenter + (yRange * MathF.Sin(MathF.PI * 2 * yPhase)), easing);

        blob.StartAnimation("Offset.Y", yAnim);

        // Organic scale pulsing — each blob breathes independently
        var scaleAnim = _compositor.CreateVector2KeyFrameAnimation();
        scaleAnim.Duration = TimeSpan.FromSeconds(basePeriod * 0.6);
        scaleAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnim.InsertKeyFrame(0f, new Vector2(0.85f, 0.95f), easing);
        scaleAnim.InsertKeyFrame(0.33f, new Vector2(1.2f, 1.05f), easing);
        scaleAnim.InsertKeyFrame(0.66f, new Vector2(0.95f, 1.2f), easing);
        scaleAnim.InsertKeyFrame(1f, new Vector2(0.85f, 0.95f), easing);

        blob.StartAnimation("Scale.XY", scaleAnim);
    }
}
