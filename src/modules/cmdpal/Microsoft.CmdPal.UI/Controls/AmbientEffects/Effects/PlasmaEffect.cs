// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// WMP "Geiss" / plasma inspired effect — multiple large overlapping radial
/// gradient blobs with vivid psychedelic colors drift, rotate and scale
/// continuously. A heavy gaussian blur merges them into swirling plasma.
/// </summary>
internal sealed class PlasmaEffect : IBackgroundEffect
{
    private const int BlobCount = 6;
    private const float BlurAmount = 60f;

    private static readonly Color[] PlasmaColors =
    [
        Color.FromArgb(230, 255, 0, 80),
        Color.FromArgb(230, 0, 80, 255),
        Color.FromArgb(220, 220, 0, 255),
        Color.FromArgb(210, 0, 255, 120),
        Color.FromArgb(220, 255, 200, 0),
        Color.FromArgb(200, 255, 0, 200),
    ];

    private Compositor? _compositor;
    private SpriteVisual? _blurHost;
    private ContainerVisual? _blobContainer;
    private SpriteVisual[]? _blobs;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        _blobContainer = compositor.CreateContainerVisual();
        _blobContainer.Size = size;

        _blobs = new SpriteVisual[BlobCount];
        var blobDiam = Math.Max(size.X, size.Y) * 0.7f;

        for (var i = 0; i < BlobCount; i++)
        {
            var blob = compositor.CreateSpriteVisual();
            blob.Size = new Vector2(blobDiam, blobDiam);
            blob.AnchorPoint = new Vector2(0.5f, 0.5f);
            blob.Offset = new Vector3(
                size.X * (0.2f + (0.6f * (((i * 3) % BlobCount) / (float)BlobCount))),
                size.Y * (0.2f + (0.6f * (((i * 7) % BlobCount) / (float)BlobCount))),
                0);
            blob.Opacity = 0.8f;

            var brush = compositor.CreateRadialGradientBrush();
            brush.EllipseCenter = new Vector2(0.5f, 0.5f);
            brush.EllipseRadius = new Vector2(0.5f, 0.5f);

            var c = PlasmaColors[i % PlasmaColors.Length];
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, c));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0.45f, Color.FromArgb((byte)(c.A / 2), c.R, c.G, c.B)));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, c.R, c.G, c.B)));

            blob.Brush = brush;
            _blobs[i] = blob;
            _blobContainer.Children.InsertAtTop(blob);
        }

        // Blur the entire blob container into swirling plasma
        var blurEffect = new GaussianBlurEffect
        {
            Name = "Blur",
            BlurAmount = BlurAmount,
            BorderMode = EffectBorderMode.Soft,
            Source = new CompositionEffectSourceParameter("Source"),
        };

        var factory = compositor.CreateEffectFactory(blurEffect);
        var effectBrush = factory.CreateBrush();

        var surface = compositor.CreateVisualSurface();
        surface.SourceVisual = _blobContainer;
        surface.SourceSize = size;

        var surfaceBrush = compositor.CreateSurfaceBrush(surface);
        surfaceBrush.Stretch = CompositionStretch.Fill;
        effectBrush.SetSourceParameter("Source", surfaceBrush);

        _blurHost = compositor.CreateSpriteVisual();
        _blurHost.Size = size;
        _blurHost.Brush = effectBrush;
        _blurHost.Opacity = 0.75f;

        rootVisual.Children.InsertAtTop(_blurHost);
    }

    public void Start()
    {
        if (_compositor == null || _blobs == null)
        {
            return;
        }

        var smoothEasing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.45f, 0.05f), new Vector2(0.55f, 0.95f));
        var linearEasing = _compositor.CreateLinearEasingFunction();

        for (var i = 0; i < _blobs.Length; i++)
        {
            var blob = _blobs[i];

            // Lissajous-style X motion — faster
            var xPeriod = 5.0 + (i * 2.5);
            var xRange = _size.X * 0.45f;
            var cx = _size.X * 0.5f;
            var xPhase = i * 0.2f;

            var xAnim = _compositor.CreateScalarKeyFrameAnimation();
            xAnim.Duration = TimeSpan.FromSeconds(xPeriod);
            xAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            xAnim.InsertKeyFrame(0f, cx + (xRange * MathF.Cos(MathF.PI * 2 * xPhase)), smoothEasing);
            xAnim.InsertKeyFrame(0.25f, cx + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.25f))), smoothEasing);
            xAnim.InsertKeyFrame(0.5f, cx + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.5f))), smoothEasing);
            xAnim.InsertKeyFrame(0.75f, cx + (xRange * MathF.Cos(MathF.PI * 2 * (xPhase + 0.75f))), smoothEasing);
            xAnim.InsertKeyFrame(1f, cx + (xRange * MathF.Cos(MathF.PI * 2 * xPhase)), smoothEasing);
            blob.StartAnimation("Offset.X", xAnim);

            // Y motion — offset ratio for tighter swirl patterns
            var yPeriod = xPeriod * 1.3;
            var yRange = _size.Y * 0.45f;
            var cy = _size.Y * 0.5f;
            var yPhase = i * 0.35f;

            var yAnim = _compositor.CreateScalarKeyFrameAnimation();
            yAnim.Duration = TimeSpan.FromSeconds(yPeriod);
            yAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            yAnim.InsertKeyFrame(0f, cy + (yRange * MathF.Sin(MathF.PI * 2 * yPhase)), smoothEasing);
            yAnim.InsertKeyFrame(0.25f, cy + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.25f))), smoothEasing);
            yAnim.InsertKeyFrame(0.5f, cy + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.5f))), smoothEasing);
            yAnim.InsertKeyFrame(0.75f, cy + (yRange * MathF.Sin(MathF.PI * 2 * (yPhase + 0.75f))), smoothEasing);
            yAnim.InsertKeyFrame(1f, cy + (yRange * MathF.Sin(MathF.PI * 2 * yPhase)), smoothEasing);
            blob.StartAnimation("Offset.Y", yAnim);

            // Faster rotation for psychedelic turbulence
            var rotAnim = _compositor.CreateScalarKeyFrameAnimation();
            rotAnim.Duration = TimeSpan.FromSeconds(xPeriod * 1.5);
            rotAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            rotAnim.InsertKeyFrame(0f, 0f, linearEasing);
            rotAnim.InsertKeyFrame(1f, (i % 2 == 0) ? 360f : -360f, linearEasing);
            blob.StartAnimation("RotationAngleInDegrees", rotAnim);

            // Dramatic scale pulse
            var scaleAnim = _compositor.CreateVector2KeyFrameAnimation();
            scaleAnim.Duration = TimeSpan.FromSeconds(4 + (i * 1.5));
            scaleAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            scaleAnim.InsertKeyFrame(0f, new Vector2(0.7f, 0.7f), smoothEasing);
            scaleAnim.InsertKeyFrame(0.5f, new Vector2(1.4f, 1.4f), smoothEasing);
            scaleAnim.InsertKeyFrame(1f, new Vector2(0.7f, 0.7f), smoothEasing);
            blob.StartAnimation("Scale.XY", scaleAnim);
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
            blob.StopAnimation("RotationAngleInDegrees");
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
            if (_blurHost.Brush is CompositionEffectBrush effectBrush && _compositor != null && _blobContainer != null)
            {
                var surface = _compositor.CreateVisualSurface();
                surface.SourceVisual = _blobContainer;
                surface.SourceSize = newSize;
                var surfaceBrush = _compositor.CreateSurfaceBrush(surface);
                surfaceBrush.Stretch = CompositionStretch.Fill;
                effectBrush.SetSourceParameter("Source", surfaceBrush);
            }
        }

        if (_blobs != null)
        {
            var blobDiam = Math.Max(newSize.X, newSize.Y) * 0.7f;
            foreach (var blob in _blobs)
            {
                blob.Size = new Vector2(blobDiam, blobDiam);
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
}
