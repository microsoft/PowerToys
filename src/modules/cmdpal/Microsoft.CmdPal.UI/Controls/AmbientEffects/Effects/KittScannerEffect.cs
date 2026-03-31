// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Knight Rider / KITT scanner effect.
/// A persistent ambient red glow spans the entire bottom edge. A brighter
/// "hot spot" sweeps left↔right rapidly, intensifying the existing glow
/// rather than appearing as a separate dot.
/// </summary>
internal sealed class KittScannerEffect : IBackgroundEffect
{
    private const float HotSpotWidthRatio = 0.25f;

    private readonly DockSide? _dockSide;
    private Compositor? _compositor;
    private SpriteVisual? _ambientGlow;
    private SpriteVisual? _hotSpot;
    private SpriteVisual? _hotSpotTrail;
    private Vector2 _size;

    private CompositionScopedBatch? _batch;
    private bool _isRunning;
    private bool _movingForward = true;

    public KittScannerEffect(DockSide? dockSide = null)
    {
        _dockSide = dockSide;
    }

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        // Layer 1: persistent ambient red glow across the top edge
        _ambientGlow = compositor.CreateSpriteVisual();
        _ambientGlow.Size = new Vector2(size.X, Math.Min(120f, size.Y));
        _ambientGlow.Offset = new Vector3(0, 0, 0);
        _ambientGlow.Opacity = 1f;

        var ambientBrush = compositor.CreateLinearGradientBrush();
        ambientBrush.StartPoint = new Vector2(0.5f, 0f);
        ambientBrush.EndPoint = new Vector2(0.5f, 1f);
        ambientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(90, 220, 20, 0)));
        ambientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.2f, Color.FromArgb(60, 200, 10, 0)));
        ambientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(30, 180, 0, 0)));
        ambientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 160, 0, 0)));
        _ambientGlow.Brush = ambientBrush;

        rootVisual.Children.InsertAtTop(_ambientGlow);

        // Layer 2: wider trailing intensification
        var hotW = size.X * HotSpotWidthRatio;
        var hotH = Math.Min(140f, size.Y);

        _hotSpotTrail = compositor.CreateSpriteVisual();
        _hotSpotTrail.Size = new Vector2(hotW * 1.8f, hotH);
        _hotSpotTrail.Offset = new Vector3(0, 0, 0);
        _hotSpotTrail.Opacity = 0.5f;

        var trailBrush = compositor.CreateRadialGradientBrush();
        trailBrush.EllipseCenter = new Vector2(0.5f, 0f);
        trailBrush.EllipseRadius = new Vector2(0.5f, 0.85f);
        trailBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(140, 255, 20, 0)));
        trailBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.4f, Color.FromArgb(60, 200, 0, 0)));
        trailBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 120, 0, 0)));
        _hotSpotTrail.Brush = trailBrush;

        rootVisual.Children.InsertAtTop(_hotSpotTrail);

        // Layer 3: bright hot spot — the "scanner beam" that sweeps
        _hotSpot = compositor.CreateSpriteVisual();
        _hotSpot.Size = new Vector2(hotW, hotH);
        _hotSpot.Offset = new Vector3(0, 0, 0);
        _hotSpot.Opacity = 0.9f;

        var hotBrush = compositor.CreateRadialGradientBrush();
        hotBrush.EllipseCenter = new Vector2(0.5f, 0f);
        hotBrush.EllipseRadius = new Vector2(0.45f, 0.8f);
        hotBrush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(255, 255, 220, 200)));
        hotBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.1f, Color.FromArgb(255, 255, 60, 30)));
        hotBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.35f, Color.FromArgb(200, 240, 10, 0)));
        hotBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.6f, Color.FromArgb(80, 180, 0, 0)));
        hotBrush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, 100, 0, 0)));
        _hotSpot.Brush = hotBrush;

        rootVisual.Children.InsertAtTop(_hotSpot);
    }

    public void Start()
    {
        _isRunning = true;
        _movingForward = true;

        // Pulse the ambient glow
        if (_compositor != null && _ambientGlow != null)
        {
            var easing = _compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.35f, 0f), new Vector2(0.65f, 1f));
            var pulse = _compositor.CreateScalarKeyFrameAnimation();
            pulse.Duration = TimeSpan.FromSeconds(1.8);
            pulse.IterationBehavior = AnimationIterationBehavior.Forever;
            pulse.InsertKeyFrame(0f, 0.7f, easing);
            pulse.InsertKeyFrame(0.5f, 1f, easing);
            pulse.InsertKeyFrame(1f, 0.7f, easing);
            _ambientGlow.StartAnimation("Opacity", pulse);
        }

        AnimateSweep();
    }

    public void Stop()
    {
        _isRunning = false;
        _ambientGlow?.StopAnimation("Opacity");
        if (_batch != null)
        {
            _batch.Completed -= OnBatchCompleted;
            _batch = null;
        }
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;

        if (_ambientGlow != null)
        {
            _ambientGlow.Size = new Vector2(newSize.X, Math.Min(120f, newSize.Y));
            _ambientGlow.Offset = new Vector3(0, 0, 0);
        }

        var hotW = newSize.X * HotSpotWidthRatio;
        var hotH = Math.Min(140f, newSize.Y);

        if (_hotSpot != null)
        {
            _hotSpot.Size = new Vector2(hotW, hotH);
            _hotSpot.Offset = new Vector3(_hotSpot.Offset.X, 0, 0);
        }

        if (_hotSpotTrail != null)
        {
            _hotSpotTrail.Size = new Vector2(hotW * 1.8f, hotH);
            _hotSpotTrail.Offset = new Vector3(_hotSpotTrail.Offset.X, 0, 0);
        }
    }

    public void Dispose()
    {
        Stop();
        _ambientGlow?.Dispose();
        _hotSpot?.Dispose();
        _hotSpotTrail?.Dispose();
        _ambientGlow = null;
        _hotSpot = null;
        _hotSpotTrail = null;
    }

    private void AnimateSweep()
    {
        if (!_isRunning || _compositor == null || _hotSpot == null || _hotSpotTrail == null)
        {
            return;
        }

        var hotW = _hotSpot.Size.X;
        var startPos = -hotW * 0.2f;
        var endPos = _size.X - (hotW * 0.8f);
        var from = _movingForward ? startPos : endPos;
        var to = _movingForward ? endPos : startPos;

        // Fast sweep — 1 second per pass
        var duration = TimeSpan.FromSeconds(1.0);
        var easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.4f, 0f), new Vector2(0.6f, 1f));

        _batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        _batch.Completed += OnBatchCompleted;

        // Hot spot sweep
        var hotAnim = _compositor.CreateScalarKeyFrameAnimation();
        hotAnim.Duration = duration;
        hotAnim.InsertKeyFrame(0f, from, easing);
        hotAnim.InsertKeyFrame(1f, to, easing);
        _hotSpot.StartAnimation("Offset.X", hotAnim);

        // Trail follows slightly behind
        var trailAnim = _compositor.CreateScalarKeyFrameAnimation();
        trailAnim.Duration = duration;
        trailAnim.DelayTime = TimeSpan.FromMilliseconds(60);
        trailAnim.InsertKeyFrame(0f, from, easing);
        trailAnim.InsertKeyFrame(1f, to, easing);
        _hotSpotTrail.StartAnimation("Offset.X", trailAnim);

        _batch.End();
    }

    private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
    {
        if (_batch != null)
        {
            _batch.Completed -= OnBatchCompleted;
            _batch = null;
        }

        _movingForward = !_movingForward;
        AnimateSweep();
    }
}
