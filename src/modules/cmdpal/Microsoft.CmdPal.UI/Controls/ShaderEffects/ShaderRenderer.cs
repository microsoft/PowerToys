// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Diagnostics;
using ComputeSharp;
using ComputeSharp.D2D1.WinUI;
using Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;
using Microsoft.CmdPal.UI.Controls.ShaderEffects.Shaders;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls.ShaderEffects;

/// <summary>
/// Active effect type.
/// </summary>
public enum EffectType
{
    AudioGlow,
    Alchemy,
    WinampScope,
    WaveformTunnel,
    RadialSpectrum,
    Kaleidoscope,
    KaleidoTunnel,
    WaveformSpace,
}

/// <summary>
/// Manages the rendering loop, audio capture, and shader execution.
/// Bridges the CanvasAnimatedControl (Win2D) with ComputeSharp pixel shaders.
/// </summary>
public sealed class ShaderRenderer : IDisposable
{
    private const int BandCount = 12;
    private const float HueRotationSpeed = 0.0033f;

    private AudioLoopbackService? _audioService;
    private readonly float[] _levels = new float[BandCount];
    private readonly Stopwatch _clock = new();
    private bool _audioAvailable;
    private float _hueOffset;
    private Rect _cropRect;

    private PixelShaderEffect<AudioGlowShader>? _audioGlowEffect;
    private PixelShaderEffect<AlchemyShader>? _alchemyEffect;
    private PixelShaderEffect<WinampScopeShader>? _winampScopeEffect;
    private PixelShaderEffect<WaveformTunnelShader>? _waveformTunnelEffect;
    private PixelShaderEffect<RadialSpectrumShader>? _radialSpectrumEffect;
    private PixelShaderEffect<KaleidoscopeShader>? _kaleidoscopeEffect;
    private PixelShaderEffect<KaleidoTunnelShader>? _kaleidoTunnelEffect;
    private PixelShaderEffect<WaveformSpaceShader>? _waveformSpaceEffect;

    public EffectType CurrentEffect { get; set; } = EffectType.AudioGlow;

    public void OnCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
    {
        _audioGlowEffect = new PixelShaderEffect<AudioGlowShader>();
        _alchemyEffect = new PixelShaderEffect<AlchemyShader>();
        _winampScopeEffect = new PixelShaderEffect<WinampScopeShader>();
        _waveformTunnelEffect = new PixelShaderEffect<WaveformTunnelShader>();
        _radialSpectrumEffect = new PixelShaderEffect<RadialSpectrumShader>();
        _kaleidoscopeEffect = new PixelShaderEffect<KaleidoscopeShader>();
        _kaleidoTunnelEffect = new PixelShaderEffect<KaleidoTunnelShader>();
        _waveformSpaceEffect = new PixelShaderEffect<WaveformSpaceShader>();

        _audioService = new AudioLoopbackService(BandCount);
        _audioAvailable = _audioService.Start();

        if (!_audioAvailable)
        {
            _audioService.Dispose();
            _audioService = null;
        }

        _clock.Start();
    }

    public void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        var size = sender.Size;

        // D2D.GetScenePosition() returns pixel coordinates, so scale by DPI
        float dpiScale = sender.Dpi / 96f;
        int w = (int)Math.Round(size.Width * dpiScale);
        int h = (int)Math.Round(size.Height * dpiScale);

        if (w <= 0 || h <= 0)
        {
            return;
        }

        UpdateAudio();

        float elapsed = (float)_clock.Elapsed.TotalSeconds;

        // CropEffect SourceRectangle must match shader pixel dimensions
        _cropRect = new Rect(0, 0, w, h);

        switch (CurrentEffect)
        {
            case EffectType.AudioGlow:
                DrawAudioGlow(args, w, h, elapsed);
                break;
            case EffectType.Alchemy:
                DrawAlchemy(args, w, h, elapsed);
                break;
            case EffectType.WinampScope:
                DrawWinampScope(args, w, h, elapsed);
                break;
            case EffectType.WaveformTunnel:
                DrawWaveformTunnel(args, w, h, elapsed);
                break;
            case EffectType.RadialSpectrum:
                DrawRadialSpectrum(args, w, h, elapsed);
                break;
            case EffectType.Kaleidoscope:
                DrawKaleidoscope(args, w, h, elapsed);
                break;
            case EffectType.KaleidoTunnel:
                DrawKaleidoTunnel(args, w, h, elapsed);
                break;
            case EffectType.WaveformSpace:
                DrawWaveformSpace(args, w, h, elapsed);
                break;
        }
    }

    private void DrawAudioGlow(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_audioGlowEffect == null)
        {
            return;
        }

        // Advance hue for continuous color morphing
        _hueOffset += HueRotationSpeed;
        if (_hueOffset >= 1f)
        {
            _hueOffset -= 1f;
        }

        // Compute overall energy
        float overall = 0f;
        for (int i = 0; i < _levels.Length; i++)
        {
            overall += _levels[i];
        }

        overall /= _levels.Length;

        _audioGlowEffect.ConstantBuffer = new AudioGlowShader(
            time: elapsed,
            hueOffset: _hueOffset,
            overallEnergy: overall,
            band0: _levels[0],
            band1: _levels[1],
            band2: _levels[2],
            band3: _levels[3],
            band4: _levels[4],
            width: w,
            height: h);

        using var crop = new CropEffect
        {
            Source = _audioGlowEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawAlchemy(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_alchemyEffect == null)
        {
            return;
        }

        // Bass = average of lowest 2 bands
        float bass = (_levels[0] + _levels[1]) * 0.5f;

        // Overall energy
        float overall = 0f;
        for (int i = 0; i < _levels.Length; i++)
        {
            overall += _levels[i];
        }

        overall /= _levels.Length;

        _alchemyEffect.ConstantBuffer = new AlchemyShader(
            time: elapsed,
            bassLevel: bass,
            overallEnergy: overall,
            width: w,
            height: h,
            outerBands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            outerBands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            innerBands: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _alchemyEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawWinampScope(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_winampScopeEffect == null)
        {
            return;
        }

        _winampScopeEffect.ConstantBuffer = new WinampScopeShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _winampScopeEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawWaveformTunnel(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_waveformTunnelEffect == null)
        {
            return;
        }

        _waveformTunnelEffect.ConstantBuffer = new WaveformTunnelShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _waveformTunnelEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawRadialSpectrum(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_radialSpectrumEffect == null)
        {
            return;
        }

        _radialSpectrumEffect.ConstantBuffer = new RadialSpectrumShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _radialSpectrumEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawKaleidoscope(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_kaleidoscopeEffect == null)
        {
            return;
        }

        _kaleidoscopeEffect.ConstantBuffer = new KaleidoscopeShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _kaleidoscopeEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawKaleidoTunnel(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_kaleidoTunnelEffect == null)
        {
            return;
        }

        _kaleidoTunnelEffect.ConstantBuffer = new KaleidoTunnelShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _kaleidoTunnelEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void DrawWaveformSpace(CanvasAnimatedDrawEventArgs args, int w, int h, float elapsed)
    {
        if (_waveformSpaceEffect == null)
        {
            return;
        }

        _waveformSpaceEffect.ConstantBuffer = new WaveformSpaceShader(
            time: elapsed,
            width: w,
            height: h,
            bands0: new float4(_levels[0], _levels[1], _levels[2], _levels[3]),
            bands1: new float4(_levels[4], _levels[5], _levels[6], _levels[7]),
            bands2: new float4(_levels[8], _levels[9], _levels[10], _levels[11]));

        using var crop = new CropEffect
        {
            Source = _waveformSpaceEffect,
            SourceRectangle = _cropRect,
        };

        args.DrawingSession.DrawImage(crop);
    }

    private void UpdateAudio()
    {
        if (_audioAvailable && _audioService != null)
        {
            _audioService.GetBandLevels(_levels);
        }
        else
        {
            // Fallback: moderate idle animation
            float time = (float)(Environment.TickCount64 / 1000.0);
            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = 0.1f + 0.08f * MathF.Sin((time * 1.5f) + (i * 0.5f));
            }
        }
    }

    public void Dispose()
    {
        _clock.Stop();
        _audioService?.Dispose();
        _audioService = null;
        _audioGlowEffect?.Dispose();
        _alchemyEffect?.Dispose();
        _winampScopeEffect?.Dispose();
        _waveformTunnelEffect?.Dispose();
        _radialSpectrumEffect?.Dispose();
        _kaleidoscopeEffect?.Dispose();
        _kaleidoTunnelEffect?.Dispose();
        _waveformSpaceEffect?.Dispose();
        _audioGlowEffect = null;
        _alchemyEffect = null;
        _winampScopeEffect = null;
        _waveformTunnelEffect = null;
        _radialSpectrumEffect = null;
        _kaleidoscopeEffect = null;
        _kaleidoTunnelEffect = null;
        _waveformSpaceEffect = null;
    }
}
