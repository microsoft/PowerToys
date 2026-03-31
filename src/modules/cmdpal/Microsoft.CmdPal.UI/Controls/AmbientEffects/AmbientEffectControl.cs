// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects;

/// <summary>
/// A single XAML control that renders GPU-accelerated ambient background effects
/// via the Composition API. Drop it into any layout as a non-interactive overlay.
/// </summary>
internal sealed partial class AmbientEffectControl : Control
{
    public static readonly DependencyProperty EffectTypeProperty =
        DependencyProperty.Register(
            nameof(EffectType),
            typeof(AmbientEffectType),
            typeof(AmbientEffectControl),
            new PropertyMetadata(AmbientEffectType.Off, OnEffectTypeChanged));

    public static readonly DependencyProperty DockSideProperty =
        DependencyProperty.Register(
            nameof(DockSide),
            typeof(DockSide),
            typeof(AmbientEffectControl),
            new PropertyMetadata(DockSide.Top, OnEffectTypeChanged));

    public static readonly DependencyProperty IsDockModeProperty =
        DependencyProperty.Register(
            nameof(IsDockMode),
            typeof(bool),
            typeof(AmbientEffectControl),
            new PropertyMetadata(false, OnEffectTypeChanged));

    private Compositor? _compositor;
    private ContainerVisual? _rootVisual;
    private IBackgroundEffect? _currentEffect;
    private bool _isVisible;

    public AmbientEffectType EffectType
    {
        get => (AmbientEffectType)GetValue(EffectTypeProperty);
        set => SetValue(EffectTypeProperty, value);
    }

    public DockSide DockSide
    {
        get => (DockSide)GetValue(DockSideProperty);
        set => SetValue(DockSideProperty, value);
    }

    public bool IsDockMode
    {
        get => (bool)GetValue(IsDockModeProperty);
        set => SetValue(IsDockModeProperty, value);
    }

    public AmbientEffectControl()
    {
        DefaultStyleKey = typeof(AmbientEffectControl);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isVisible = true;
        EnsureCompositor();
        ApplyEffect();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isVisible = false;
        CleanupEffect();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var newSize = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        if (_rootVisual != null)
        {
            _rootVisual.Size = newSize;
        }

        _currentEffect?.Resize(newSize);
    }

    private static void OnEffectTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AmbientEffectControl control && control._compositor != null)
        {
            control.ApplyEffect();
        }
    }

    private void EnsureCompositor()
    {
        if (_compositor != null)
        {
            return;
        }

        var elementVisual = ElementCompositionPreview.GetElementVisual(this);
        _compositor = elementVisual.Compositor;
        _rootVisual = _compositor.CreateContainerVisual();
        _rootVisual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        _rootVisual.Clip = _compositor.CreateInsetClip();
        ElementCompositionPreview.SetElementChildVisual(this, _rootVisual);
    }

    private void ApplyEffect()
    {
        CleanupEffect();

        if (_compositor == null || _rootVisual == null || EffectType == AmbientEffectType.Off)
        {
            return;
        }

        var size = new Vector2((float)ActualWidth, (float)ActualHeight);
        _currentEffect = BackgroundEffectFactory.Create(EffectType, IsDockMode ? DockSide : null);

        if (_currentEffect != null)
        {
            _currentEffect.Initialize(_compositor, _rootVisual, size);
            if (_isVisible)
            {
                _currentEffect.Start();
            }
        }
    }

    private void CleanupEffect()
    {
        _currentEffect?.Stop();
        _currentEffect?.Dispose();
        _currentEffect = null;

        // Clear the container visual's children
        if (_rootVisual != null)
        {
            _rootVisual.Children.RemoveAll();
        }
    }
}
