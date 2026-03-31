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
/// Audio-reactive full-width glow along the top edge. A single wide glow
/// band spans the entire width and pulses downward with overall audio energy.
/// The color continuously morphs through the spectrum over time.
/// Multiple overlapping layers create a rich, blended look.
/// </summary>
internal sealed class AudioGlowEffect : IBackgroundEffect
{
    private const int LayerCount = 5;
    private const float GlowHeight = 500f;
    private const float HueRotationSpeed = 0.0033f;

    private Compositor? _compositor;
    private SpriteVisual[]? _layers;
    private CompositionRadialGradientBrush[]? _layerBrushes;
    private SpriteVisual? _fullWidthPulse;
    private CompositionLinearGradientBrush? _pulseBrush;
    private Vector2 _size;

    private AudioLoopbackService? _audioService;
    private DispatcherQueueTimer? _updateTimer;
    private float[]? _levelBuffer;
    private bool _audioAvailable;
    private float _hueOffset;

    public void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size)
    {
        _compositor = compositor;
        _size = size;

        var glowH = Math.Min(GlowHeight, size.Y);

        // Full-width pulse layer — reacts to overall energy, spans entire dock
        _fullWidthPulse = compositor.CreateSpriteVisual();
        _fullWidthPulse.Size = new Vector2(size.X, glowH);
        _fullWidthPulse.Offset = new Vector3(0, 0, 0);
        _fullWidthPulse.Opacity = 0f;

        _pulseBrush = compositor.CreateLinearGradientBrush();
        _pulseBrush.StartPoint = new Vector2(0.5f, 0f);
        _pulseBrush.EndPoint = new Vector2(0.5f, 1f);
        UpdatePulseBrushColors(compositor, _pulseBrush, 0f);
        _fullWidthPulse.Brush = _pulseBrush;
        rootVisual.Children.InsertAtTop(_fullWidthPulse);

        // Overlapping glow layers — each responds to a frequency range
        // They span the full width but are offset horizontally so the
        // color bleeds across the entire band
        _layers = new SpriteVisual[LayerCount];
        _layerBrushes = new CompositionRadialGradientBrush[LayerCount];

        for (var i = 0; i < LayerCount; i++)
        {
            var layer = compositor.CreateSpriteVisual();
            layer.Size = new Vector2(size.X, glowH);

            // Spread layers across the width with heavy overlap
            var xCenter = size.X * ((i + 0.5f) / LayerCount);
            layer.Offset = new Vector3(0, 0, 0);
            layer.Opacity = 0f;

            var brush = compositor.CreateRadialGradientBrush();
            brush.EllipseCenter = new Vector2(0.5f, 0f);
            brush.EllipseRadius = new Vector2(0.5f, 0.9f);

            var color = HueToColor((float)i / LayerCount);
            SetGlowBrushColor(compositor, brush, color);

            layer.Brush = brush;
            _layers[i] = layer;
            _layerBrushes[i] = brush;
            rootVisual.Children.InsertAtTop(layer);
        }

        _levelBuffer = new float[LayerCount + 1];
    }

    public void Start()
    {
        _audioService = new AudioLoopbackService(_levelBuffer?.Length ?? LayerCount + 1);
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
    }

    public void Resize(Vector2 newSize)
    {
        _size = newSize;
        var glowH = Math.Min(GlowHeight, newSize.Y);

        if (_fullWidthPulse != null)
        {
            _fullWidthPulse.Size = new Vector2(newSize.X, glowH);
        }

        if (_layers != null)
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                _layers[i].Size = new Vector2(newSize.X, glowH);
                _layers[i].Offset = new Vector3(0, 0, 0);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _fullWidthPulse?.Dispose();
        if (_layers != null)
        {
            foreach (var l in _layers)
            {
                l.Dispose();
            }
        }

        _fullWidthPulse = null;
        _layers = null;
        _layerBrushes = null;
    }

    private void OnAudioTick(DispatcherQueueTimer sender, object args)
    {
        if (_levelBuffer == null || _layers == null || _layerBrushes == null || _compositor == null)
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
                _levelBuffer[i] = 0.15f + (0.1f * MathF.Sin((time * 2f) + (i * 0.5f)));
            }
        }

        // Advance hue — continuous color morphing
        _hueOffset += HueRotationSpeed;
        if (_hueOffset >= 1f)
        {
            _hueOffset -= 1f;
        }

        // Compute overall energy for the full-width pulse
        var overall = 0f;
        for (var i = 0; i < _levelBuffer.Length; i++)
        {
            overall += _levelBuffer[i];
        }

        overall /= _levelBuffer.Length;

        // Full-width pulse reacts to overall energy — the whole dock glows
        if (_fullWidthPulse != null && _pulseBrush != null)
        {
            _fullWidthPulse.Opacity = 0.1f + (0.8f * overall);
            var scaleY = 0.3f + (2.0f * overall);
            _fullWidthPulse.Scale = new Vector3(1f, scaleY, 1f);
            UpdatePulseBrushColors(_compositor, _pulseBrush, _hueOffset);
        }

        // Individual layers add frequency-specific color highlights on top
        for (var i = 0; i < LayerCount && i < _layers.Length; i++)
        {
            var level = (i < _levelBuffer.Length) ? _levelBuffer[i] : overall;

            _layers[i].Opacity = 0.05f + (0.9f * level);
            var scaleY = 0.2f + (2.5f * level);
            _layers[i].Scale = new Vector3(1f, scaleY, 1f);

            // Update color with hue rotation
            var hue = (((float)i / LayerCount) + _hueOffset) % 1f;
            var color = HueToColor(hue);
            SetGlowBrushColor(_compositor, _layerBrushes[i], color);
        }
    }

    private static void UpdatePulseBrushColors(Compositor compositor, CompositionLinearGradientBrush brush, float hueOffset)
    {
        var c = HueToColor(hueOffset);
        brush.ColorStops.Clear();
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(200, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.2f, Color.FromArgb(120, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(40, c.R, c.G, c.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, c.R, c.G, c.B)));
    }

    private static void SetGlowBrushColor(Compositor compositor, CompositionRadialGradientBrush brush, Color color)
    {
        brush.ColorStops.Clear();
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0f, Color.FromArgb(255, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.15f, Color.FromArgb(200, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(0.4f, Color.FromArgb(80, color.R, color.G, color.B)));
        brush.ColorStops.Add(compositor.CreateColorGradientStop(1f, Color.FromArgb(0, color.R, color.G, color.B)));
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
