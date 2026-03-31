// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Effects;

/// <summary>
/// Audio-reactive equalizer bars that visualize system audio in real-time.
/// Bars glow using the system accent color with a soft radial gradient.
/// Uses WASAPI loopback → FFT → CompositionPropertySet → ExpressionAnimation.
/// </summary>
internal sealed class LiveBarsEffect : IBackgroundEffect
{
    private const int BarCount = 48;
    private const float BarGap = 2f;
    private const float MaxBarHeightRatio = 0.8f;
    private const float BarOpacity = 0.7f;

    private Compositor? _compositor;
    private SpriteVisual[]? _bars;
    private SpriteVisual[]? _glows;
    private SpriteVisual[]? _peaks;
    private CompositionPropertySet? _propertySet;
    private Vector2 _size;

    private AudioLoopbackService? _audioService;
    private DispatcherQueueTimer? _updateTimer;
    private float[]? _levelBuffer;
    private float[]? _peakLevels;
    private bool _audioAvailable;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        var accentColor = GetAccentColor();

        _propertySet = compositor.CreatePropertySet();
        for (var i = 0; i < BarCount; i++)
        {
            _propertySet.InsertScalar($"B{i}", 0f);
        }

        _bars = new SpriteVisual[BarCount];
        _glows = new SpriteVisual[BarCount];
        _peaks = new SpriteVisual[BarCount];
        _levelBuffer = new float[BarCount];
        _peakLevels = new float[BarCount];

        var barWidth = (size.X - ((BarCount - 1) * BarGap)) / BarCount;
        barWidth = Math.Max(barWidth, 2f);
        var maxH = size.Y * MaxBarHeightRatio;

        for (var i = 0; i < BarCount; i++)
        {
            var xPos = i * (barWidth + BarGap);

            // Glow behind each bar — wider, softer radial gradient
            var glow = compositor.CreateSpriteVisual();
            glow.Size = new Vector2(barWidth * 3f, 0);
            glow.Offset = new Vector3(xPos - barWidth, size.Y, 0);
            glow.Opacity = 0.35f;
            glow.Brush = CreateGlowBrush(compositor, accentColor);
            _glows[i] = glow;
            rootVisual.Children.InsertAtTop(glow);

            // Main bar
            var bar = compositor.CreateSpriteVisual();
            bar.Size = new Vector2(barWidth, 0);
            bar.Offset = new Vector3(xPos, size.Y, 0);
            bar.Opacity = BarOpacity;
            bar.Brush = CreateBarBrush(compositor, accentColor);
            _bars[i] = bar;
            rootVisual.Children.InsertAtTop(bar);

            // Expression animations driven by property set
            BindBarExpressions(compositor, bar, glow, i, maxH, size.Y);

            // Peak indicator — accent colored
            var peak = compositor.CreateSpriteVisual();
            peak.Size = new Vector2(barWidth + 2f, 2f);
            peak.Offset = new Vector3(xPos - 1f, size.Y, 0);
            peak.Opacity = 0.9f;
            peak.Brush = compositor.CreateColorBrush(accentColor);
            _peaks[i] = peak;
            rootVisual.Children.InsertAtTop(peak);
        }
    }

    public void Start()
    {
        _audioService = new AudioLoopbackService(BarCount);
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
            _updateTimer.Tick += OnUpdateTick;
            _updateTimer.Start();
        }
    }

    public void Stop()
    {
        _updateTimer?.Stop();
        if (_updateTimer != null)
        {
            _updateTimer.Tick -= OnUpdateTick;
        }

        _updateTimer = null;
        _audioService?.Dispose();
        _audioService = null;
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        if (_bars == null || _glows == null || _compositor == null || _propertySet == null)
        {
            return;
        }

        var barWidth = (newSize.X - ((BarCount - 1) * BarGap)) / BarCount;
        barWidth = Math.Max(barWidth, 2f);
        var maxH = newSize.Y * MaxBarHeightRatio;

        for (var i = 0; i < BarCount; i++)
        {
            var xPos = i * (barWidth + BarGap);
            _bars[i].Size = new Vector2(barWidth, _bars[i].Size.Y);
            _glows[i].Size = new Vector2(barWidth * 3f, _glows[i].Size.Y);
            BindBarExpressions(_compositor, _bars[i], _glows[i], i, maxH, newSize.Y);

            if (_peaks != null)
            {
                _peaks[i].Size = new Vector2(barWidth + 2f, 2f);
                _peaks[i].Offset = new Vector3(xPos - 1f, _peaks[i].Offset.Y, 0);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        DisposeArray(_bars);
        DisposeArray(_glows);
        DisposeArray(_peaks);
        _propertySet?.Dispose();
        _bars = null;
        _glows = null;
        _peaks = null;
        _propertySet = null;
    }

    private void BindBarExpressions(Compositor compositor, SpriteVisual bar, SpriteVisual glow, int index, float maxH, float totalH)
    {
        if (_propertySet == null)
        {
            return;
        }

        var heightExpr = compositor.CreateExpressionAnimation($"props.B{index} * maxH");
        heightExpr.SetReferenceParameter("props", _propertySet);
        heightExpr.SetScalarParameter("maxH", maxH);
        bar.StartAnimation("Size.Y", heightExpr);

        var offsetExpr = compositor.CreateExpressionAnimation($"totalH - (props.B{index} * maxH)");
        offsetExpr.SetReferenceParameter("props", _propertySet);
        offsetExpr.SetScalarParameter("totalH", totalH);
        offsetExpr.SetScalarParameter("maxH", maxH);
        bar.StartAnimation("Offset.Y", offsetExpr);

        // Glow follows the bar
        var glowHExpr = compositor.CreateExpressionAnimation($"props.B{index} * maxH * 1.3");
        glowHExpr.SetReferenceParameter("props", _propertySet);
        glowHExpr.SetScalarParameter("maxH", maxH);
        glow.StartAnimation("Size.Y", glowHExpr);

        var glowOffExpr = compositor.CreateExpressionAnimation($"totalH - (props.B{index} * maxH * 1.3)");
        glowOffExpr.SetReferenceParameter("props", _propertySet);
        glowOffExpr.SetScalarParameter("totalH", totalH);
        glowOffExpr.SetScalarParameter("maxH", maxH);
        glow.StartAnimation("Offset.Y", glowOffExpr);
    }

    private void OnUpdateTick(DispatcherQueueTimer sender, object args)
    {
        if (_propertySet == null || _levelBuffer == null || _peakLevels == null)
        {
            return;
        }

        if (_audioAvailable && _audioService != null)
        {
            _audioService.GetBandLevels(_levelBuffer);
        }
        else
        {
            GenerateFallbackLevels(_levelBuffer);
        }

        for (var i = 0; i < BarCount; i++)
        {
            _propertySet.InsertScalar($"B{i}", _levelBuffer[i]);

            if (_levelBuffer[i] > _peakLevels[i])
            {
                _peakLevels[i] = _levelBuffer[i];
            }
            else
            {
                _peakLevels[i] = Math.Max(0f, _peakLevels[i] - 0.008f);
            }

            if (_peaks != null)
            {
                var maxH = _size.Y * MaxBarHeightRatio;
                _peaks[i].Offset = new Vector3(
                    _peaks[i].Offset.X,
                    _size.Y - (_peakLevels[i] * maxH) - 3f,
                    0);
            }
        }
    }

    private static void GenerateFallbackLevels(float[] levels)
    {
        var time = (float)(Environment.TickCount64 / 1000.0);
        for (var i = 0; i < levels.Length; i++)
        {
            var phase = i * 0.15f;
            var val = 0.05f + (0.03f * MathF.Sin((time * 1.5f) + phase));
            val += 0.02f * MathF.Sin((time * 2.7f) + (phase * 1.3f));
            levels[i] = Math.Clamp(val, 0f, 1f);
        }
    }

    private static Color GetAccentColor()
    {
        try
        {
            var uiSettings = new UISettings();
            return uiSettings.GetColorValue(UIColorType.Accent);
        }
        catch
        {
            return Color.FromArgb(255, 0, 120, 215);
        }
    }

    private static CompositionLinearGradientBrush CreateBarBrush(Compositor compositor, Color accent)
    {
        var brush = compositor.CreateLinearGradientBrush();
        brush.StartPoint = new Vector2(0, 1);
        brush.EndPoint = new Vector2(0, 0);

        // Accent color bar: solid at base, brighter/whiter at tip
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, accent));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.7f, accent));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.9f, Color.FromArgb(
            accent.A,
            (byte)Math.Min(255, accent.R + 80),
            (byte)Math.Min(255, accent.G + 80),
            (byte)Math.Min(255, accent.B + 80))));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(240, 255, 255, 255)));

        return brush;
    }

    private static CompositionRadialGradientBrush CreateGlowBrush(Compositor compositor, Color accent)
    {
        var brush = compositor.CreateRadialGradientBrush();
        brush.EllipseCenter = new Vector2(0.5f, 1f);
        brush.EllipseRadius = new Vector2(0.5f, 0.8f);

        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(120, accent.R, accent.G, accent.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(40, accent.R, accent.G, accent.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, accent.R, accent.G, accent.B)));

        return brush;
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
