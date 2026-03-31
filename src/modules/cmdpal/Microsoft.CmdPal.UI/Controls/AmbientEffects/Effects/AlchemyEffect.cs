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
/// WMP "Alchemy" visualization — concentric geometric starburst patterns
/// with an inner and outer ring of rays rotating in opposite directions,
/// pulsing scale/opacity, and a bright morphing center orb. Authentic
/// spirographic kaleidoscope feel.
/// </summary>
internal sealed class AlchemyEffect : IBackgroundEffect
{
    private const int OuterRayCount = 24;
    private const int InnerRayCount = 14;

    private static readonly Color OuterColor = Color.FromArgb(180, 100, 180, 255);
    private static readonly Color InnerColor = Color.FromArgb(200, 255, 100, 220);
    private static readonly Color AccentColor = Color.FromArgb(180, 100, 255, 180);

    private Compositor? _compositor;
    private SpriteVisual[]? _outerRays;
    private SpriteVisual[]? _innerRays;
    private SpriteVisual? _centerOrb;
    private SpriteVisual? _outerRing;
    private Vector2 _size;

    private AudioLoopbackService? _audioService;
    private DispatcherQueueTimer? _updateTimer;
    private float[]? _levelBuffer;
    private bool _audioAvailable;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        var cx = size.X * 0.5f;
        var cy = size.Y * 0.5f;

        // Use smaller dimension so it fits in dock's thin aspect ratio
        var span = Math.Min(size.X, size.Y);
        var outerLen = span * 0.45f;
        var innerLen = outerLen * 0.5f;

        // If we're in a wide/thin layout (dock), extend rays further
        if (size.X > size.Y * 2.5f)
        {
            outerLen = size.Y * 0.8f;
            innerLen = outerLen * 0.5f;
        }

        // Expanding/contracting outer ring
        _outerRing = compositor.CreateSpriteVisual();
        var ringDiam = outerLen * 2.5f;
        _outerRing.Size = new Vector2(ringDiam, ringDiam);
        _outerRing.AnchorPoint = new Vector2(0.5f, 0.5f);
        _outerRing.Offset = new Vector3(cx, cy, 0);
        _outerRing.Opacity = 0.35f;

        var ringBrush = compositor.CreateRadialGradientBrush();
        ringBrush.EllipseCenter = new Vector2(0.5f, 0.5f);
        ringBrush.EllipseRadius = new Vector2(0.5f, 0.5f);
        ringBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(0, OuterColor.R, OuterColor.G, OuterColor.B)));
        ringBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.6f, Color.FromArgb(0, OuterColor.R, OuterColor.G, OuterColor.B)));
        ringBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.8f, Color.FromArgb(100, OuterColor.R, OuterColor.G, OuterColor.B)));
        ringBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.92f, Color.FromArgb(50, OuterColor.R, OuterColor.G, OuterColor.B)));
        ringBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, OuterColor.R, OuterColor.G, OuterColor.B)));
        _outerRing.Brush = ringBrush;
        rootVisual.Children.InsertAtTop(_outerRing);

        // Outer rays — long, thin, rotate clockwise
        _outerRays = new SpriteVisual[OuterRayCount];
        for (var i = 0; i < OuterRayCount; i++)
        {
            var ray = CreateRay(compositor, cx, cy, 5f, outerLen, i, OuterRayCount, OuterColor, AccentColor);
            _outerRays[i] = ray;
            rootVisual.Children.InsertAtTop(ray);
        }

        // Inner rays — shorter, slightly wider, rotate counter-clockwise
        _innerRays = new SpriteVisual[InnerRayCount];
        for (var i = 0; i < InnerRayCount; i++)
        {
            var ray = CreateRay(compositor, cx, cy, 6f, innerLen, i, InnerRayCount, InnerColor, OuterColor);
            _innerRays[i] = ray;
            rootVisual.Children.InsertAtTop(ray);
        }

        // Bright center orb — large and prominent
        _centerOrb = compositor.CreateSpriteVisual();
        var orbSize = Math.Min(size.X, size.Y) * 0.4f;
        _centerOrb.Size = new Vector2(orbSize, orbSize);
        _centerOrb.AnchorPoint = new Vector2(0.5f, 0.5f);
        _centerOrb.Offset = new Vector3(cx, cy, 0);
        _centerOrb.Opacity = 0.8f;

        var orbBrush = compositor.CreateRadialGradientBrush();
        orbBrush.EllipseCenter = new Vector2(0.5f, 0.5f);
        orbBrush.EllipseRadius = new Vector2(0.5f, 0.5f);
        orbBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(200, 255, 255, 255)));
        orbBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.15f, Color.FromArgb(160, 220, 200, 255)));
        orbBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.4f, Color.FromArgb(80, 160, 120, 255)));
        orbBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.7f, Color.FromArgb(30, 100, 80, 220)));
        orbBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 60, 40, 180)));
        _centerOrb.Brush = orbBrush;
        rootVisual.Children.InsertAtTop(_centerOrb);
    }

    public void Start()
    {
        if (_compositor == null)
        {
            return;
        }

        var smooth = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.6f, 1f));
        var linear = _compositor.CreateLinearEasingFunction();

        // Outer rays: slow clockwise, staggered pulse
        AnimateRayGroup(_outerRays, OuterRayCount, 30.0, true, smooth, linear);

        // Inner rays: faster counter-clockwise
        AnimateRayGroup(_innerRays, InnerRayCount, 18.0, false, smooth, linear);

        // Outer ring breathe
        if (_outerRing != null)
        {
            var ringScale = _compositor.CreateVector2KeyFrameAnimation();
            ringScale.Duration = TimeSpan.FromSeconds(6);
            ringScale.IterationBehavior = AnimationIterationBehavior.Forever;
            ringScale.InsertKeyFrame(0f, new Vector2(0.85f, 0.85f), smooth);
            ringScale.InsertKeyFrame(0.5f, new Vector2(1.15f, 1.15f), smooth);
            ringScale.InsertKeyFrame(1f, new Vector2(0.85f, 0.85f), smooth);
            _outerRing.StartAnimation("Scale.XY", ringScale);

            var ringOp = _compositor.CreateScalarKeyFrameAnimation();
            ringOp.Duration = TimeSpan.FromSeconds(6);
            ringOp.IterationBehavior = AnimationIterationBehavior.Forever;
            ringOp.InsertKeyFrame(0f, 0.15f, smooth);
            ringOp.InsertKeyFrame(0.5f, 0.35f, smooth);
            ringOp.InsertKeyFrame(1f, 0.15f, smooth);
            _outerRing.StartAnimation("Opacity", ringOp);
        }

        // Center orb pulse
        if (_centerOrb != null)
        {
            var orbScale = _compositor.CreateVector2KeyFrameAnimation();
            orbScale.Duration = TimeSpan.FromSeconds(2.5);
            orbScale.IterationBehavior = AnimationIterationBehavior.Forever;
            orbScale.InsertKeyFrame(0f, new Vector2(0.7f, 0.7f), smooth);
            orbScale.InsertKeyFrame(0.5f, new Vector2(1.4f, 1.4f), smooth);
            orbScale.InsertKeyFrame(1f, new Vector2(0.7f, 0.7f), smooth);
            _centerOrb.StartAnimation("Scale.XY", orbScale);

            var orbOp = _compositor.CreateScalarKeyFrameAnimation();
            orbOp.Duration = TimeSpan.FromSeconds(2.5);
            orbOp.IterationBehavior = AnimationIterationBehavior.Forever;
            orbOp.InsertKeyFrame(0f, 0.5f, smooth);
            orbOp.InsertKeyFrame(0.5f, 1f, smooth);
            orbOp.InsertKeyFrame(1f, 0.5f, smooth);
            _centerOrb.StartAnimation("Opacity", orbOp);
        }

        // Start audio reactivity
        var totalRays = OuterRayCount + InnerRayCount;
        _levelBuffer = new float[totalRays];
        _audioService = new AudioLoopbackService(totalRays);
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

        StopRays(_outerRays);
        StopRays(_innerRays);
        _outerRing?.StopAnimation("Scale.XY");
        _outerRing?.StopAnimation("Opacity");
        _centerOrb?.StopAnimation("Scale.XY");
        _centerOrb?.StopAnimation("Opacity");
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        var cx = newSize.X * 0.5f;
        var cy = newSize.Y * 0.5f;

        var span = Math.Min(newSize.X, newSize.Y);
        var outerLen = span * 0.45f;
        var innerLen = outerLen * 0.5f;

        if (newSize.X > newSize.Y * 2.5f)
        {
            outerLen = newSize.Y * 0.8f;
            innerLen = outerLen * 0.5f;
        }

        ResizeRays(_outerRays, cx, cy, outerLen);
        ResizeRays(_innerRays, cx, cy, innerLen);

        if (_outerRing != null)
        {
            var ringDiam = outerLen * 2.5f;
            _outerRing.Size = new Vector2(ringDiam, ringDiam);
            _outerRing.Offset = new Vector3(cx, cy, 0);
        }

        if (_centerOrb != null)
        {
            var orbSize = Math.Min(newSize.X, newSize.Y) * 0.4f;
            _centerOrb.Size = new Vector2(orbSize, orbSize);
            _centerOrb.Offset = new Vector3(cx, cy, 0);
        }
    }

    public void Dispose()
    {
        Stop();
        DisposeArray(_outerRays);
        DisposeArray(_innerRays);
        _outerRing?.Dispose();
        _centerOrb?.Dispose();
        _outerRays = null;
        _innerRays = null;
        _outerRing = null;
        _centerOrb = null;
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
            // Fallback pulse
            var time = (float)(Environment.TickCount64 / 1000.0);
            for (var i = 0; i < _levelBuffer.Length; i++)
            {
                _levelBuffer[i] = 0.3f + (0.15f * MathF.Sin((time * 2f) + (i * 0.3f)));
            }
        }

        // Modulate outer rays — 0 to full blast
        if (_outerRays != null)
        {
            for (var i = 0; i < _outerRays.Length && i < _levelBuffer.Length; i++)
            {
                var level = _levelBuffer[i];
                _outerRays[i].Opacity = level;
                _outerRays[i].Scale = new Vector3(
                    0.2f + (4.0f * level),
                    level * 3.0f,
                    1f);
            }
        }

        // Modulate inner rays — even more explosive
        if (_innerRays != null)
        {
            for (var i = 0; i < _innerRays.Length; i++)
            {
                var bandIdx = OuterRayCount + i;
                var level = (bandIdx < _levelBuffer.Length) ? _levelBuffer[bandIdx] : 0.3f;
                _innerRays[i].Opacity = level;
                _innerRays[i].Scale = new Vector3(
                    0.15f + (5.0f * level),
                    level * 4.0f,
                    1f);
            }
        }

        // Center orb reacts hard to bass — BIG pulsing flash
        if (_centerOrb != null && _levelBuffer.Length > 4)
        {
            var bass = (_levelBuffer[0] + _levelBuffer[1] + _levelBuffer[2] + _levelBuffer[3]) * 0.25f;
            var orbScale = 0.2f + (3.5f * bass);
            _centerOrb.Scale = new Vector3(orbScale, orbScale, 1f);
            _centerOrb.Opacity = 0.3f + (0.7f * bass);
        }

        // Outer ring reacts to overall energy
        if (_outerRing != null)
        {
            var overall = 0f;
            for (var i = 0; i < _levelBuffer.Length; i++)
            {
                overall += _levelBuffer[i];
            }

            overall /= _levelBuffer.Length;
            _outerRing.Opacity = 0.1f + (0.6f * overall);
            var ringScale = 0.85f + (0.4f * overall);
            _outerRing.Scale = new Vector3(ringScale, ringScale, 1f);
        }
    }

    private static SpriteVisual CreateRay(
        Compositor compositor,
        float cx,
        float cy,
        float width,
        float length,
        int index,
        int total,
        Color primary,
        Color secondary)
    {
        var ray = compositor.CreateSpriteVisual();
        ray.Size = new Vector2(width, length);
        ray.AnchorPoint = new Vector2(0.5f, 1f);
        ray.Offset = new Vector3(cx, cy, 0);
        ray.RotationAngleInDegrees = index * (360f / total);
        ray.Opacity = 0.7f;

        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0.5f, 1f);
        brush.EndPoint = new Vector2(0.5f, 0f);

        // Soft, diffuse glow — rays bleed into each other
        var color = (index % 2 == 0) ? primary : secondary;
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(80, 255, 255, 255)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.05f, color));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.2f, Color.FromArgb((byte)(color.A * 0.7), color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb((byte)(color.A * 0.3), color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));

        ray.Brush = brush;
        return ray;
    }

    private void AnimateRayGroup(
        SpriteVisual[]? rays,
        int count,
        double rotPeriod,
        bool clockwise,
        CompositionEasingFunction smooth,
        CompositionEasingFunction linear)
    {
        if (rays == null || _compositor == null)
        {
            return;
        }

        for (var i = 0; i < rays.Length; i++)
        {
            var baseDeg = i * (360f / count);
            var dir = clockwise ? 360f : -360f;

            // Rotation — all rays in group share period but start at different angles
            var rotAnim = _compositor.CreateScalarKeyFrameAnimation();
            rotAnim.Duration = TimeSpan.FromSeconds(rotPeriod);
            rotAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            rotAnim.InsertKeyFrame(0f, baseDeg, linear);
            rotAnim.InsertKeyFrame(1f, baseDeg + dir, linear);
            rays[i].StartAnimation("RotationAngleInDegrees", rotAnim);

            // Length pulse — staggered so rays "breathe" in a wave pattern
            var pulsePeriod = 3.0 + (i * 0.4);
            var scaleAnim = _compositor.CreateVector2KeyFrameAnimation();
            scaleAnim.Duration = TimeSpan.FromSeconds(pulsePeriod);
            scaleAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            scaleAnim.InsertKeyFrame(0f, new Vector2(1f, 0.7f), smooth);
            scaleAnim.InsertKeyFrame(0.5f, new Vector2(1.3f, 1.2f), smooth);
            scaleAnim.InsertKeyFrame(1f, new Vector2(1f, 0.7f), smooth);
            rays[i].StartAnimation("Scale.XY", scaleAnim);

            // Opacity wave
            var opAnim = _compositor.CreateScalarKeyFrameAnimation();
            opAnim.Duration = TimeSpan.FromSeconds(pulsePeriod * 0.8);
            opAnim.IterationBehavior = AnimationIterationBehavior.Forever;
            opAnim.InsertKeyFrame(0f, 0.25f, smooth);
            opAnim.InsertKeyFrame(0.5f, 0.7f, smooth);
            opAnim.InsertKeyFrame(1f, 0.25f, smooth);
            rays[i].StartAnimation("Opacity", opAnim);
        }
    }

    private static void StopRays(SpriteVisual[]? rays)
    {
        if (rays == null)
        {
            return;
        }

        foreach (var r in rays)
        {
            r.StopAnimation("RotationAngleInDegrees");
            r.StopAnimation("Scale.XY");
            r.StopAnimation("Opacity");
        }
    }

    private static void ResizeRays(SpriteVisual[]? rays, float cx, float cy, float length)
    {
        if (rays == null)
        {
            return;
        }

        foreach (var r in rays)
        {
            r.Size = new Vector2(r.Size.X, length);
            r.Offset = new Vector3(cx, cy, 0);
        }
    }

    private static void DisposeArray(SpriteVisual[]? arr)
    {
        if (arr == null)
        {
            return;
        }

        foreach (var v in arr)
        {
            v.Dispose();
        }
    }
}
