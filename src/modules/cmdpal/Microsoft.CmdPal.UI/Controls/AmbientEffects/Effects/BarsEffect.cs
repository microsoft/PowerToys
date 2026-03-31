// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI.Composition;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// WMP "Bars &amp; Waves" visualization — classic VU-meter equalizer bars with
/// green→yellow→red gradient, snappy rise, gravity-like fall, and peak hold
/// indicators. Bottom-aligned with subtle transparency.
/// </summary>
internal sealed class BarsEffect : IBackgroundEffect
{
    private const int BarCount = 48;
    private const float BarGap = 1.5f;
    private const float MaxBarHeightRatio = 0.7f;
    private const float BarOpacity = 0.55f;

    private Compositor? _compositor;
    private SpriteVisual[]? _bars;
    private SpriteVisual[]? _peaks;
    private Vector2 _size;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        _bars = new SpriteVisual[BarCount];
        _peaks = new SpriteVisual[BarCount];
        var barWidth = (size.X - ((BarCount - 1) * BarGap)) / BarCount;
        barWidth = Math.Max(barWidth, 2f);

        for (var i = 0; i < BarCount; i++)
        {
            var xPos = i * (barWidth + BarGap);
            var initialH = size.Y * 0.02f;

            // Main bar — grows upward from the bottom
            var bar = compositor.CreateSpriteVisual();
            bar.Size = new Vector2(barWidth, initialH);
            bar.Offset = new Vector3(xPos, size.Y - initialH, 0);
            bar.Opacity = BarOpacity;
            bar.Brush = CreateVuMeterBrush(compositor);
            _bars[i] = bar;
            rootVisual.Children.InsertAtTop(bar);

            // Peak indicator — thin bright bar that lingers at the top
            var peak = compositor.CreateSpriteVisual();
            peak.Size = new Vector2(barWidth, 2f);
            peak.Offset = new Vector3(xPos, size.Y - initialH - 3f, 0);
            peak.Opacity = BarOpacity * 0.8f;
            peak.Brush = compositor.CreateColorBrush(Color.FromArgb(200, 255, 255, 180));
            _peaks[i] = peak;
            rootVisual.Children.InsertAtTop(peak);
        }
    }

    public void Start()
    {
        if (_compositor == null || _bars == null || _peaks == null)
        {
            return;
        }

        for (var i = 0; i < BarCount; i++)
        {
            AnimateBar(i);
        }
    }

    public void Stop()
    {
        StopArray(_bars, "Size.Y", "Offset.Y");
        StopArray(_peaks, "Offset.Y");
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
    }

    public void Dispose()
    {
        Stop();
        DisposeArray(_bars);
        DisposeArray(_peaks);
        _bars = null;
        _peaks = null;
    }

    private void AnimateBar(int index)
    {
        if (_compositor == null || _bars == null || _peaks == null)
        {
            return;
        }

        var bar = _bars[index];
        var peak = _peaks[index];
        var maxH = _size.Y * MaxBarHeightRatio;

        // Snappy rise, slow gravity fall
        var riseEase = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.1f, 0.8f), new Vector2(0.2f, 1f));
        var fallEase = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.6f, 0f), new Vector2(0.9f, 0.4f));

        // Golden-ratio-based pseudo-random seed per bar
        var golden = 1.618033988749f;
        var seed = ((index * golden) % 1f) + 0.3f;
        var period = 0.6 + (seed * 1.2);

        // 8 keyframes simulating a VU meter bouncing to a beat
        var kf = new float[8];
        for (var k = 0; k < 8; k++)
        {
            var t = (((index * 13) + (k * 37)) % 97) / 97f;
            kf[k] = maxH * (0.08f + (0.92f * t * t));
        }

        // Height animation
        var heightAnim = _compositor.CreateScalarKeyFrameAnimation();
        heightAnim.Duration = TimeSpan.FromSeconds(period * 4);
        heightAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        for (var k = 0; k < 8; k++)
        {
            var frac = k / 8f;
            var ease = (k % 2 == 0) ? riseEase : fallEase;
            heightAnim.InsertKeyFrame(frac, kf[k], ease);
        }

        heightAnim.InsertKeyFrame(1f, kf[0], fallEase);
        bar.StartAnimation("Size.Y", heightAnim);

        // Offset.Y — bars grow upward from the bottom edge
        var offsetAnim = _compositor.CreateScalarKeyFrameAnimation();
        offsetAnim.Duration = TimeSpan.FromSeconds(period * 4);
        offsetAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        for (var k = 0; k < 8; k++)
        {
            var frac = k / 8f;
            var ease = (k % 2 == 0) ? riseEase : fallEase;
            offsetAnim.InsertKeyFrame(frac, _size.Y - kf[k], ease);
        }

        offsetAnim.InsertKeyFrame(1f, _size.Y - kf[0], fallEase);
        bar.StartAnimation("Offset.Y", offsetAnim);

        // Peak tracks highest point with slower fall
        var peakAnim = _compositor.CreateScalarKeyFrameAnimation();
        peakAnim.Duration = TimeSpan.FromSeconds(period * 4);
        peakAnim.IterationBehavior = AnimationIterationBehavior.Forever;

        for (var k = 0; k < 8; k++)
        {
            var frac = k / 8f;
            peakAnim.InsertKeyFrame(frac, _size.Y - kf[k] - 3f, fallEase);
        }

        peakAnim.InsertKeyFrame(1f, _size.Y - kf[0] - 3f, fallEase);
        peak.StartAnimation("Offset.Y", peakAnim);
    }

    private static CompositionLinearGradientBrush CreateVuMeterBrush(Compositor compositor)
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 1);
        brush.EndPoint = new Vector2(0, 0);

        // Classic VU: green at base → yellow → orange → red at top
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(240, 0, 200, 40)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(240, 180, 220, 0)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.75f, Color.FromArgb(240, 255, 160, 0)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.9f, Color.FromArgb(240, 255, 40, 0)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(255, 255, 0, 0)));

        return brush;
    }

    private static void StopArray(SpriteVisual[]? arr, params string[] props)
    {
        if (arr == null)
        {
            return;
        }

        foreach (var v in arr)
        {
            foreach (var p in props)
            {
                v.StopAnimation(p);
            }
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
