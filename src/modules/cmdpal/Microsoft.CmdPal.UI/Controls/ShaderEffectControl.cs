// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.Controls.ShaderEffects;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Hosts a CanvasAnimatedControl that renders GPU pixel shader effects
/// driven by system audio. Drop into any layout as a non-interactive overlay.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed in Unloaded handler")]
internal sealed partial class ShaderEffectControl : UserControl
{
    public static readonly DependencyProperty EffectTypeProperty =
        DependencyProperty.Register(
            nameof(EffectType),
            typeof(ShaderEffectType),
            typeof(ShaderEffectControl),
            new PropertyMetadata(ShaderEffectType.Off, OnEffectTypeChanged));

    private ShaderRenderer? _renderer;
    private CanvasAnimatedControl? _canvas;
    private bool _isLoaded;

    public ShaderEffectType EffectType
    {
        get => (ShaderEffectType)GetValue(EffectTypeProperty);
        set => SetValue(EffectTypeProperty, value);
    }

    public ShaderEffectControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        EnsureCanvas();
        ApplyEffect();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        CleanupCanvas();
    }

    private static void OnEffectTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShaderEffectControl control && control._isLoaded)
        {
            control.ApplyEffect();
        }
    }

    private void ApplyEffect()
    {
        if (EffectType == ShaderEffectType.Off)
        {
            CleanupCanvas();
            Visibility = Visibility.Collapsed;
            return;
        }

        EnsureCanvas();
        Visibility = Visibility.Visible;

        if (_renderer != null)
        {
            _renderer.CurrentEffect = EffectType switch
            {
                ShaderEffectType.AudioGlow => ShaderEffects.EffectType.AudioGlow,
                ShaderEffectType.Alchemy => ShaderEffects.EffectType.Alchemy,
                ShaderEffectType.MilkDrop => ShaderEffects.EffectType.WinampScope,
                ShaderEffectType.WaveformTunnel => ShaderEffects.EffectType.WaveformTunnel,
                ShaderEffectType.RadialSpectrum => ShaderEffects.EffectType.RadialSpectrum,
                ShaderEffectType.Kaleidoscope => ShaderEffects.EffectType.Kaleidoscope,
                ShaderEffectType.KaleidoTunnel => ShaderEffects.EffectType.KaleidoTunnel,
                ShaderEffectType.WaveformSpace => ShaderEffects.EffectType.WaveformSpace,
                _ => ShaderEffects.EffectType.AudioGlow,
            };
        }
    }

    private void EnsureCanvas()
    {
        if (_canvas != null)
        {
            return;
        }

        _canvas = new CanvasAnimatedControl
        {
            ClearColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0),
            IsFixedTimeStep = false,
        };
        _canvas.CreateResources += OnCreateResources;
        _canvas.Draw += OnDraw;
        Content = _canvas;
    }

    private void CleanupCanvas()
    {
        if (_canvas != null)
        {
            _canvas.CreateResources -= OnCreateResources;
            _canvas.Draw -= OnDraw;
            _canvas.RemoveFromVisualTree();
            _canvas = null;
        }

        _renderer?.Dispose();
        _renderer = null;
        Content = null;
    }

    private void OnCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
    {
        _renderer = new ShaderRenderer();
        _renderer.OnCreateResources(sender, args);
        ApplyEffect();
    }

    private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        _renderer?.OnDraw(sender, args);
    }
}
