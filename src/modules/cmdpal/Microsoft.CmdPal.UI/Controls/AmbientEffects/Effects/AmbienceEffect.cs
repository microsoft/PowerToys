// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// WMP "Ambience" inspired visualization — dreamy flowing organic shapes that
/// morph and blend with smooth color transitions. Multiple soft radial blobs
/// drift in slow, fluid orbits. Heavy gaussian blur creates a liquid,
/// hallucinogenic look. Audio drives blob scale, opacity, and movement speed.
/// Colors cycle slowly through a warm-to-cool palette.
/// </summary>
internal sealed class AmbienceEffect : IBackgroundEffect
{
    private const int BlobCount = 7;
    private const float BlurAmount = 100f;
    private const float HueSpeed = 0.003f;

    private Compositor? _compositor;
    private SpriteVisual? _blurHost;
    private ContainerVisual? _blobContainer;
    private SpriteVisual[]? _blobs;
    private CompositionRadialGradientBrush[]? _blobBrushes;
    private Vector2 _size;

    private AudioLoopbackService? _audioService;
    private DispatcherQueueTimer? _updateTimer;
    private float[]? _levelBuffer;
    private bool _audioAvailable;
    private float _hueOffset;
    private float _time;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        _blobContainer = compositor.CreateContainerVisual();
        _blobContainer.Size = size;

        _blobs = new SpriteVisual[BlobCount];
        _blobBrushes = new CompositionRadialGradientBrush[BlobCount];

        for (var i = 0; i < BlobCount; i++)
        {
            var blob = compositor.CreateSpriteVisual();
            var scale = 0.5f + (0.5f * ((i % 3) / 2f));
            var diameter = Math.Max(size.X, size.Y) * 0.6f * scale;
            blob.Size = new Vector2(diameter, diameter);
            blob.AnchorPoint = new Vector2(0.5f, 0.5f);

            // Spread in a circular pattern
            var angle = (i * MathF.PI * 2f) / BlobCount;
            blob.Offset = new Vector3(
                (size.X * 0.5f) + (size.X * 0.2f * MathF.Cos(angle)),
                (size.Y * 0.5f) + (size.Y * 0.2f * MathF.Sin(angle)),
                0);
            blob.Opacity = 0.8f;

            var brush = compositor.CreateRadialGradientBrush();
            brush.EllipseCenter = new Vector2(0.5f, 0.5f);
            brush.EllipseRadius = new Vector2(0.5f, 0.5f);

            var color = HueToColor((float)i / BlobCount);
            SetBlobColor(compositor, brush, color);

            blob.Brush = brush;
            _blobs[i] = blob;
            _blobBrushes[i] = brush;
            _blobContainer.Children.InsertAtTop(blob);
        }

        // Heavy blur for the dreamy liquid look
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
        _blurHost.Opacity = 0.8f;

        rootVisual.Children.InsertAtTop(_blurHost);

        _levelBuffer = new float[BlobCount];
    }

    public void Start()
    {
        _audioService = new AudioLoopbackService(BlobCount);
        _audioAvailable = _audioService.Start();

        if (!_audioAvailable)
        {
            _audioService.Dispose();
            _audioService = null;
        }

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue != null)
        {
            _updateTimer = dispatcherQueue.CreateTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(33);
            _updateTimer.Tick += OnTick;
            _updateTimer.Start();
        }
    }

    public void Stop()
    {
        _updateTimer?.Stop();
        if (_updateTimer != null)
        {
            _updateTimer.Tick -= OnTick;
        }

        _updateTimer = null;
        _audioService?.Dispose();
        _audioService = null;
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
    }

    public void Dispose()
    {
        Stop();
        _blurHost?.Dispose();
        if (_blobs != null)
        {
            foreach (var b in _blobs)
            {
                b.Dispose();
            }
        }

        _blobContainer?.Dispose();
        _blurHost = null;
        _blobs = null;
        _blobBrushes = null;
        _blobContainer = null;
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_blobs == null || _blobBrushes == null || _levelBuffer == null || _compositor == null)
        {
            return;
        }

        _time += 0.033f;

        if (_audioAvailable && _audioService != null)
        {
            _audioService.GetBandLevels(_levelBuffer);
        }
        else
        {
            for (var i = 0; i < _levelBuffer.Length; i++)
            {
                _levelBuffer[i] = 0.3f + (0.15f * MathF.Sin((_time * 1.5f) + (i * 0.8f)));
            }
        }

        // Slow hue drift
        _hueOffset += HueSpeed;
        if (_hueOffset >= 1f)
        {
            _hueOffset -= 1f;
        }

        var cx = _size.X * 0.5f;
        var cy = _size.Y * 0.5f;

        for (var i = 0; i < BlobCount; i++)
        {
            var level = _levelBuffer[i];
            var blob = _blobs[i];

            // Fluid orbital motion — speed increases with audio level
            var speedMultiplier = 0.5f + (1.5f * level);
            var baseAngle = (i * MathF.PI * 2f) / BlobCount;
            var xPeriod = 8f + (i * 2.3f);
            var yPeriod = xPeriod * 1.4f;
            var orbitRadius = Math.Min(_size.X, _size.Y) * (0.15f + (0.2f * level));

            var x = cx + (orbitRadius * MathF.Cos((_time * speedMultiplier / xPeriod * MathF.PI * 2f) + baseAngle));
            var y = cy + (orbitRadius * MathF.Sin((_time * speedMultiplier / yPeriod * MathF.PI * 2f) + baseAngle));
            blob.Offset = new Vector3(x, y, 0);

            // Scale breathes with audio — blobs grow on beats
            var scale = 0.6f + (1.5f * level);
            blob.Scale = new Vector3(scale, scale, 1f);
            blob.Opacity = 0.4f + (0.6f * level);

            // Update color with slow hue rotation
            var hue = (((float)i / BlobCount) + _hueOffset) % 1f;
            var color = HueToColor(hue);
            SetBlobColor(_compositor, _blobBrushes[i], color);
        }
    }

    private static void SetBlobColor(Compositor compositor, CompositionRadialGradientBrush brush, Color c)
    {
        brush.ColorStops.Clear();
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(255, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.35f, Color.FromArgb(180, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.65f, Color.FromArgb(60, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, c.R, c.G, c.B)));
    }

    private static Color HueToColor(float hue)
    {
        hue = ((hue % 1f) + 1f) % 1f;
        var h = hue * 6f;
        var sector = (int)h;
        var frac = h - sector;

        byte full = 255;
        var rising = (byte)(255 * frac);
        var falling = (byte)(255 * (1f - frac));

        return sector switch
        {
            0 => Color.FromArgb(full, full, rising, 0),
            1 => Color.FromArgb(full, falling, full, 0),
            2 => Color.FromArgb(full, 0, full, rising),
            3 => Color.FromArgb(full, 0, falling, full),
            4 => Color.FromArgb(full, rising, 0, full),
            _ => Color.FromArgb(full, full, 0, falling),
        };
    }
}
