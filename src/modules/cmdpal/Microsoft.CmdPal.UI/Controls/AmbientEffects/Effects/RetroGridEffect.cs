// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Retro 80s / Tron / synthwave inspired effect. A neon grid recedes towards
/// a glowing horizon, with horizontal scanlines sweeping upward and pulsing
/// neon accent bars at the top and bottom edges. Full synthwave vibes.
/// </summary>
internal sealed class RetroGridEffect : IBackgroundEffect
{
    private const int HorizontalLineCount = 8;
    private const int VerticalLineCount = 12;
    private const int ScanlineCount = 3;

    private static readonly Color NeonPink = Color.FromArgb(200, 255, 0, 200);
    private static readonly Color NeonCyan = Color.FromArgb(180, 0, 240, 255);
    private static readonly Color NeonPurple = Color.FromArgb(160, 180, 0, 255);
    private static readonly Color DarkBase = Color.FromArgb(60, 20, 0, 40);

    private Compositor? _compositor;
    private SpriteVisual? _horizonGlow;
    private SpriteVisual? _topEdgeGlow;
    private SpriteVisual? _bottomEdgeGlow;
    private SpriteVisual[]? _hLines;
    private SpriteVisual[]? _vLines;
    private SpriteVisual[]? _scanlines;
    private Vector2 _size;

    private AudioLoopbackService? _audioService;
    private DispatcherQueueTimer? _updateTimer;
    private float[]? _levelBuffer;
    private bool _audioAvailable;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        // Dark purple/black base tint
        var baseTint = compositor.CreateSpriteVisual();
        baseTint.Size = size;
        baseTint.Brush = compositor.CreateColorBrush(DarkBase);
        rootVisual.Children.InsertAtTop(baseTint);

        // Horizon glow — a wide gradient band at ~60% height
        var horizonY = size.Y * 0.55f;
        _horizonGlow = compositor.CreateSpriteVisual();
        _horizonGlow.Size = new Vector2(size.X * 1.5f, size.Y * 0.35f);
        _horizonGlow.AnchorPoint = new Vector2(0.5f, 0.5f);
        _horizonGlow.Offset = new Vector3(size.X * 0.5f, horizonY, 0);

        var horizonBrush = compositor.CreateRadialGradientBrush();
        horizonBrush.EllipseCenter = new Vector2(0.5f, 0.5f);
        horizonBrush.EllipseRadius = new Vector2(0.5f, 0.5f);
        horizonBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(140, 255, 80, 200)));
        horizonBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.4f, Color.FromArgb(60, 200, 0, 180)));
        horizonBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 100, 0, 80)));
        _horizonGlow.Brush = horizonBrush;
        rootVisual.Children.InsertAtTop(_horizonGlow);

        // Horizontal grid lines — converge towards horizon using perspective
        _hLines = new SpriteVisual[HorizontalLineCount];
        for (var i = 0; i < HorizontalLineCount; i++)
        {
            var line = compositor.CreateSpriteVisual();
            var t = (i + 1f) / HorizontalLineCount;
            var yPos = horizonY + ((size.Y - horizonY) * t * t);
            var thickness = 1f + (t * 2f);
            line.Size = new Vector2(size.X, thickness);
            line.Offset = new Vector3(0, yPos, 0);
            line.Opacity = 0.3f + (0.5f * t);

            var lineBrush = compositor.CreateLinearGradientBrush();
            lineBrush.StartPoint = new Vector2(0, 0.5f);
            lineBrush.EndPoint = new Vector2(1, 0.5f);
            lineBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, NeonCyan.R, NeonCyan.G, NeonCyan.B)));
            lineBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.2f, NeonCyan));
            lineBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.8f, NeonCyan));
            lineBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, NeonCyan.R, NeonCyan.G, NeonCyan.B)));

            line.Brush = lineBrush;
            _hLines[i] = line;
            rootVisual.Children.InsertAtTop(line);
        }

        // Vertical grid lines — converge towards center at the horizon
        _vLines = new SpriteVisual[VerticalLineCount];
        var gridBottom = size.Y;
        for (var i = 0; i < VerticalLineCount; i++)
        {
            var line = compositor.CreateSpriteVisual();
            var fraction = (i - (VerticalLineCount / 2f)) / (VerticalLineCount / 2f);
            var topX = (size.X * 0.5f) + (size.X * 0.08f * fraction);
            var bottomX = (size.X * 0.5f) + (size.X * 0.55f * fraction);

            // Approximate a perspective line with a thin tall rect + rotation
            var dx = bottomX - topX;
            var dy = gridBottom - horizonY;
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            var angle = MathF.Atan2(dx, dy) * (180f / MathF.PI);

            line.Size = new Vector2(1.5f, length);
            line.AnchorPoint = new Vector2(0.5f, 0f);
            line.Offset = new Vector3(topX, horizonY, 0);
            line.RotationAngleInDegrees = -angle;
            line.Opacity = 0.2f + (0.3f * (1f - MathF.Abs(fraction)));

            line.Brush = compositor.CreateColorBrush(NeonPurple);
            _vLines[i] = line;
            rootVisual.Children.InsertAtTop(line);
        }

        // Sweeping scanlines
        _scanlines = new SpriteVisual[ScanlineCount];
        for (var i = 0; i < ScanlineCount; i++)
        {
            var scanline = compositor.CreateSpriteVisual();
            scanline.Size = new Vector2(size.X, 2f);
            scanline.Offset = new Vector3(0, -10f, 0);
            scanline.Opacity = 0f;

            var scanBrush = compositor.CreateLinearGradientBrush();
            scanBrush.StartPoint = new Vector2(0, 0.5f);
            scanBrush.EndPoint = new Vector2(1, 0.5f);
            scanBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, NeonPink.R, NeonPink.G, NeonPink.B)));
            scanBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.3f, NeonPink));
            scanBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.7f, NeonPink));
            scanBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, NeonPink.R, NeonPink.G, NeonPink.B)));

            scanline.Brush = scanBrush;
            _scanlines[i] = scanline;
            rootVisual.Children.InsertAtTop(scanline);
        }

        // Top neon edge glow
        _topEdgeGlow = CreateEdgeGlow(compositor, size, true);
        rootVisual.Children.InsertAtTop(_topEdgeGlow);

        // Bottom neon edge glow
        _bottomEdgeGlow = CreateEdgeGlow(compositor, size, false);
        rootVisual.Children.InsertAtTop(_bottomEdgeGlow);
    }

    public void Start()
    {
        if (_compositor == null)
        {
            return;
        }

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.3f, 0f), new Vector2(0.7f, 1f));
        var linearEasing = _compositor.CreateLinearEasingFunction();

        // Horizon glow pulse
        if (_horizonGlow != null)
        {
            var pulseAnim = _compositor.CreateScalarKeyFrameAnimation();
            pulseAnim.Duration = TimeSpan.FromSeconds(4);
            pulseAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            pulseAnim.InsertKeyFrame(0f, 0.6f, easing);
            pulseAnim.InsertKeyFrame(0.5f, 1f, easing);
            pulseAnim.InsertKeyFrame(1f, 0.6f, easing);
            _horizonGlow.StartAnimation("Opacity", pulseAnim);
        }

        // Grid line pulse
        if (_hLines != null)
        {
            for (var i = 0; i < _hLines.Length; i++)
            {
                var lineAnim = _compositor.CreateScalarKeyFrameAnimation();
                lineAnim.Duration = TimeSpan.FromSeconds(2.5 + (i * 0.3));
                lineAnim.IterationBehavior = AnimationIterationBehavior.Forever;
                var baseOp = _hLines[i].Opacity;
                lineAnim.InsertKeyFrame(0f, baseOp * 0.6f, easing);
                lineAnim.InsertKeyFrame(0.5f, Math.Min(baseOp * 1.3f, 1f), easing);
                lineAnim.InsertKeyFrame(1f, baseOp * 0.6f, easing);
                _hLines[i].StartAnimation("Opacity", lineAnim);
            }
        }

        // Scanline sweep — each scanline sweeps up at staggered intervals
        if (_scanlines != null)
        {
            for (var i = 0; i < _scanlines.Length; i++)
            {
                AnimateScanline(_scanlines[i], i);
            }
        }

        // Edge glow pulse
        AnimateEdgeGlow(_topEdgeGlow, 3.0);
        AnimateEdgeGlow(_bottomEdgeGlow, 3.5);

        // Start audio reactivity
        _levelBuffer = new float[HorizontalLineCount + 4];
        _audioService = new AudioLoopbackService(_levelBuffer.Length);
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
            _updateTimer.Tick += OnAudioTick;
            _updateTimer.Start();
        }
    }

    public void Stop()
    {
        _updateTimer?.Stop();
        if (_updateTimer != null)
        {
            _updateTimer.Tick -= OnAudioTick;
        }

        _updateTimer = null;
        _audioService?.Dispose();
        _audioService = null;

        _horizonGlow?.StopAnimation("Opacity");

        if (_hLines != null)
        {
            foreach (var line in _hLines)
            {
                line.StopAnimation("Opacity");
            }
        }

        if (_scanlines != null)
        {
            foreach (var scanline in _scanlines)
            {
                scanline.StopAnimation("Offset.Y");
                scanline.StopAnimation("Opacity");
            }
        }

        _topEdgeGlow?.StopAnimation("Opacity");
        _bottomEdgeGlow?.StopAnimation("Opacity");
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;

        // Full re-layout would be complex; the key visuals will still render
        if (_horizonGlow != null)
        {
            _horizonGlow.Size = new Vector2(newSize.X * 1.5f, newSize.Y * 0.35f);
            _horizonGlow.Offset = new Vector3(newSize.X * 0.5f, newSize.Y * 0.55f, 0);
        }

        if (_topEdgeGlow != null)
        {
            _topEdgeGlow.Size = new Vector2(newSize.X, 6f);
        }

        if (_bottomEdgeGlow != null)
        {
            _bottomEdgeGlow.Size = new Vector2(newSize.X, 6f);
            _bottomEdgeGlow.Offset = new Vector3(0, newSize.Y - 6f, 0);
        }

        if (_scanlines != null)
        {
            foreach (var scanline in _scanlines)
            {
                scanline.Size = new Vector2(newSize.X, 2f);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _horizonGlow?.Dispose();
        _topEdgeGlow?.Dispose();
        _bottomEdgeGlow?.Dispose();

        DisposeArray(_hLines);
        DisposeArray(_vLines);
        DisposeArray(_scanlines);

        _hLines = null;
        _vLines = null;
        _scanlines = null;
        _horizonGlow = null;
        _topEdgeGlow = null;
        _bottomEdgeGlow = null;
    }

    private void OnAudioTick(DispatcherQueueTimer sender, object args)
    {
        if (_levelBuffer == null)
        {
            return;
        }

        if (_audioAvailable && _audioService != null)
        {
            _audioService.GetBandLevels(_levelBuffer);
        }
        else
        {
            var time = (float)(Environment.TickCount64 / 1000.0);
            for (var i = 0; i < _levelBuffer.Length; i++)
            {
                _levelBuffer[i] = 0.3f + (0.1f * MathF.Sin((time * 2f) + (i * 0.5f)));
            }
        }

        // Bass drives horizon glow intensity
        var bass = (_levelBuffer.Length > 2) ? (_levelBuffer[0] + _levelBuffer[1]) * 0.5f : 0.3f;
        if (_horizonGlow != null)
        {
            _horizonGlow.Opacity = 0.4f + (0.6f * bass);
            var scale = 0.9f + (0.3f * bass);
            _horizonGlow.Scale = new Vector3(scale, scale, 1f);
        }

        // Each horizontal grid line reacts to a frequency band
        if (_hLines != null)
        {
            for (var i = 0; i < _hLines.Length && i < _levelBuffer.Length; i++)
            {
                _hLines[i].Opacity = 0.2f + (0.8f * _levelBuffer[i]);
            }
        }

        // Edge glows react to bass
        if (_topEdgeGlow != null)
        {
            _topEdgeGlow.Opacity = 0.3f + (0.7f * bass);
        }

        if (_bottomEdgeGlow != null)
        {
            _bottomEdgeGlow.Opacity = 0.3f + (0.7f * bass);
        }
    }

    private static void DisposeArray(SpriteVisual[]? visuals)
    {
        if (visuals == null)
        {
            return;
        }

        foreach (var v in visuals)
        {
            v.Dispose();
        }
    }

    private static SpriteVisual CreateEdgeGlow(Compositor compositor, Vector2 size, bool isTop)
    {
        var glow = compositor.CreateSpriteVisual();
        glow.Size = new Vector2(size.X, 6f);
        glow.Offset = isTop ? new Vector3(0, 0, 0) : new Vector3(0, size.Y - 6f, 0);
        glow.Opacity = 0.7f;

        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 0.5f);
        brush.EndPoint = new Vector2(1, 0.5f);

        var color = isTop ? NeonCyan : NeonPink;
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.15f, color));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.85f, color));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

        glow.Brush = brush;
        return glow;
    }

    private void AnimateScanline(SpriteVisual scanline, int index)
    {
        if (_compositor == null)
        {
            return;
        }

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.4f, 0f), new Vector2(0.6f, 1f));

        var period = 3.0 + (index * 2.0);

        // Scanlines only sweep within the grid area: bottom → horizon
        // They travel "into" the grid, fading out as they approach the vanishing point
        var horizonY = _size.Y * 0.55f;
        var bottomY = _size.Y;

        var yAnim = _compositor.CreateScalarKeyFrameAnimation();
        yAnim.Duration = TimeSpan.FromSeconds(period);
        yAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        yAnim.InsertKeyFrame(0f, bottomY, easing);
        yAnim.InsertKeyFrame(1f, horizonY, easing);
        scanline.StartAnimation("Offset.Y", yAnim);

        // Bright at the bottom (near us), fades to invisible at the horizon
        var opAnim = _compositor.CreateScalarKeyFrameAnimation();
        opAnim.Duration = TimeSpan.FromSeconds(period);
        opAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        opAnim.InsertKeyFrame(0f, 0f, easing);
        opAnim.InsertKeyFrame(0.1f, 0.6f, easing);
        opAnim.InsertKeyFrame(0.7f, 0.3f, easing);
        opAnim.InsertKeyFrame(1f, 0f, easing);
        scanline.StartAnimation("Opacity", opAnim);
    }

    private void AnimateEdgeGlow(SpriteVisual? glow, double period)
    {
        if (_compositor == null || glow == null)
        {
            return;
        }

        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));

        var anim = _compositor.CreateScalarKeyFrameAnimation();
        anim.Duration = TimeSpan.FromSeconds(period);
        anim.IterationBehavior = AnimationIterationBehavior.Forever;
        anim.InsertKeyFrame(0f, 0.4f, easing);
        anim.InsertKeyFrame(0.5f, 0.9f, easing);
        anim.InsertKeyFrame(1f, 0.4f, easing);
        glow.StartAnimation("Opacity", anim);
    }
}
