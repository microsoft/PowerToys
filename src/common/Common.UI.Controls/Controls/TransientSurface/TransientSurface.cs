// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Common.UI.Controls;

/// <summary>
/// A floating, self-animating "pseudo window" surface for transient PowerToys
/// overlays (toasts, banners, indicators). It looks like a control but behaves
/// like a lightweight window: it provides the PowerToys-standard chrome — 1 px
/// border in <c>SurfaceStrokeColorDefaultBrush</c>, 8 px corner radius, a
/// <c>ThemeShadow</c>, and an always-active desktop acrylic backdrop — and owns
/// its own show/hide animations.
/// </summary>
/// <remarks>
/// <para>Designed to be declared as the root content of a
/// <see cref="TransparentWindow"/>, which stays animation-agnostic. Call
/// <see cref="SubscribeTo"/> once (e.g. from the hosting window's constructor)
/// to wire this surface to the window's <see cref="TransparentWindow.Showing"/> /
/// <see cref="TransparentWindow.Hiding"/> events. From then on the surface
/// animates itself in/out whenever the window is shown or hidden, and uses the
/// <see cref="TransparentWindow.Hiding"/> deferral to keep the window visible
/// until its out-animation finishes.</para>
/// <para>The slide direction comes from the window's
/// <see cref="TransparentWindow.Show(SlideDirection)"/> call (or from
/// <see cref="SlideFrom"/> when shown without one). Animations target the
/// surface itself, so the entire surface (border, acrylic, shadow, inner
/// content) animates as one. Apps that want a different look supply their own
/// <c>Style TargetType="TransientSurface"</c> in resources — the standard WinUI
/// restyle path.</para>
/// </remarks>
public sealed partial class TransientSurface : ContentControl
{
    private const float ShadowDepth = 32f;
    private const double SlideInOffset = 24;
    private const double SlideOutOffset = 12;

    public static readonly DependencyProperty SlideFromProperty = DependencyProperty.Register(
        nameof(SlideFrom),
        typeof(SlideDirection),
        typeof(TransientSurface),
        new PropertyMetadata(SlideDirection.None, OnSlideFromChanged));

    public static readonly DependencyProperty AcrylicKindProperty = DependencyProperty.Register(
        nameof(AcrylicKind),
        typeof(DesktopAcrylicKind),
        typeof(TransientSurface),
        new PropertyMetadata(DesktopAcrylicKind.Thin));

    private readonly DispatcherQueueTimer _hideCompletedTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private ImplicitAnimationSet _showAnimations = new();
    private ImplicitAnimationSet _hideAnimations = new();
    private bool _hasCustomShowAnimations;
    private bool _hasCustomHideAnimations;

    public TransientSurface()
    {
        DefaultStyleKey = typeof(TransientSurface);

        RebuildDefaultAnimations();

        // Start hidden so the first Show() animates in from the configured pose.
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Raised after <see cref="Hide"/> once the longest animation in
    /// <see cref="HideAnimations"/> (delay + duration) has completed.
    /// </summary>
    public event EventHandler? HideCompleted;

    /// <summary>
    /// Gets or sets the edge the surface slides in from (and out toward) when it
    /// is shown without an explicit direction. Defaults to
    /// <see cref="SlideDirection.None"/>, which plays no animation at all (the
    /// surface appears and disappears instantly); any other value adds a fade
    /// plus a slide from that edge. Changing this regenerates the default
    /// <see cref="ShowAnimations"/> / <see cref="HideAnimations"/> unless they
    /// have been set explicitly.
    /// </summary>
    public SlideDirection SlideFrom
    {
        get => (SlideDirection)GetValue(SlideFromProperty);
        set => SetValue(SlideFromProperty, value);
    }

    /// <summary>
    /// Gets or sets the desktop acrylic material variant painted behind the
    /// surface. Defaults to <see cref="DesktopAcrylicKind.Thin"/> (a lighter,
    /// more translucent material); set <see cref="DesktopAcrylicKind.Default"/>
    /// for the standard, more opaque acrylic or <see cref="DesktopAcrylicKind.Base"/>
    /// for the base material. Has no effect when a custom template without the
    /// default acrylic backdrop is applied.
    /// </summary>
    public DesktopAcrylicKind AcrylicKind
    {
        get => (DesktopAcrylicKind)GetValue(AcrylicKindProperty);
        set => SetValue(AcrylicKindProperty, value);
    }

    /// <summary>
    /// Gets or sets the animations played when <see cref="Show()"/> flips the
    /// surface to <see cref="Visibility.Visible"/>. Defaults to a fade-in plus a
    /// slide derived from <see cref="SlideFrom"/>. Assigning a value marks the
    /// set as custom so <see cref="SlideFrom"/> no longer overwrites it.
    /// </summary>
    public ImplicitAnimationSet ShowAnimations
    {
        get => _showAnimations;
        set
        {
            _showAnimations = value ?? new ImplicitAnimationSet();
            _hasCustomShowAnimations = true;
        }
    }

    /// <summary>
    /// Gets or sets the animations played when <see cref="Hide"/> flips the
    /// surface to <see cref="Visibility.Collapsed"/>. Defaults to a fade-out plus
    /// a slide derived from <see cref="SlideFrom"/>. Assigning a value marks the
    /// set as custom so <see cref="SlideFrom"/> no longer overwrites it.
    /// </summary>
    public ImplicitAnimationSet HideAnimations
    {
        get => _hideAnimations;
        set
        {
            _hideAnimations = value ?? new ImplicitAnimationSet();
            _hasCustomHideAnimations = true;
        }
    }

    /// <summary>
    /// Wires this surface to a hosting <see cref="TransparentWindow"/> so it
    /// animates itself in and out in response to the window's
    /// <see cref="TransparentWindow.Showing"/> / <see cref="TransparentWindow.Hiding"/>
    /// events. Call this once after the surface has been set as (or placed within)
    /// the window's content.
    /// </summary>
    /// <param name="host">The window whose show/hide transitions drive this surface.</param>
    public void SubscribeTo(TransparentWindow host)
    {
        ArgumentNullException.ThrowIfNull(host);

        host.Showing += OnHostShowing;
        host.Hiding += OnHostHiding;
    }

    /// <summary>
    /// Resets the surface to its hidden pose and flips it to
    /// <see cref="Visibility.Visible"/> so <see cref="ShowAnimations"/> plays,
    /// sliding in from <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">The edge to slide in from.</param>
    public void Show(SlideDirection direction)
    {
        SlideFrom = direction;
        Show();
    }

    /// <summary>
    /// Resets the surface to its hidden pose and flips it to
    /// <see cref="Visibility.Visible"/> so <see cref="ShowAnimations"/> plays.
    /// Repeated calls re-trigger the show animation cleanly and cancel any
    /// pending <see cref="HideCompleted"/> notification.
    /// </summary>
    public void Show()
    {
        _hideCompletedTimer.Stop();

        // Re-apply each call so swapping animation collections at runtime takes
        // effect on the next show/hide cycle.
        Implicit.SetShowAnimations(this, _showAnimations);
        Implicit.SetHideAnimations(this, _hideAnimations);

        // Reset to the hidden pose so the show animation always animates from the
        // configured starting frame.
        Visibility = Visibility.Collapsed;
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Flips the surface to <see cref="Visibility.Collapsed"/> so
    /// <see cref="HideAnimations"/> plays, then raises <see cref="HideCompleted"/>
    /// once the longest animation in <see cref="HideAnimations"/> (delay +
    /// duration) has completed.
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;

        _hideCompletedTimer.Debounce(
            () => HideCompleted?.Invoke(this, EventArgs.Empty),
            interval: GetAnimationSetTotalDuration(_hideAnimations),
            immediate: false);
    }

    private static void OnSlideFromChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TransientSurface)d).RebuildDefaultAnimations();
    }

    private static TimeSpan GetAnimationSetTotalDuration(ImplicitAnimationSet set)
    {
        TimeSpan longest = TimeSpan.Zero;
        foreach (var animation in set)
        {
            if (animation is Animation anim)
            {
                var total = (anim.Delay ?? TimeSpan.Zero) + (anim.Duration ?? TimeSpan.Zero);
                if (total > longest)
                {
                    longest = total;
                }
            }
        }

        return longest;
    }

    private static (string? ShowFrom, string? HideTo) GetSlideOffsets(SlideDirection direction) => direction switch
    {
        SlideDirection.Bottom => ($"0,{SlideInOffset},{ShadowDepth}", $"0,{SlideOutOffset},{ShadowDepth}"),
        SlideDirection.Top => ($"0,{-SlideInOffset},{ShadowDepth}", $"0,{-SlideOutOffset},{ShadowDepth}"),
        SlideDirection.Left => ($"{-SlideInOffset},0,{ShadowDepth}", $"{-SlideOutOffset},0,{ShadowDepth}"),
        SlideDirection.Right => ($"{SlideInOffset},0,{ShadowDepth}", $"{SlideOutOffset},0,{ShadowDepth}"),
        _ => (null, null),
    };

    private void OnHostShowing(TransparentWindow sender, ShowingEventArgs e)
    {
        if (e.Direction is SlideDirection direction)
        {
            Show(direction);
        }
        else
        {
            Show();
        }
    }

    private void OnHostHiding(TransparentWindow sender, HidingEventArgs e)
    {
        // Take a deferral so the host keeps its window visible until our
        // out-animation has finished, then complete it from HideCompleted.
        var deferral = e.GetDeferral();

        void OnHideCompleted(object? s, EventArgs args)
        {
            HideCompleted -= OnHideCompleted;
            deferral.Complete();
        }

        HideCompleted += OnHideCompleted;
        Hide();
    }

    private void RebuildDefaultAnimations()
    {
        var (showFrom, hideTo) = GetSlideOffsets(SlideFrom);

        if (!_hasCustomShowAnimations)
        {
            _showAnimations = BuildShowAnimations(showFrom);
        }

        if (!_hasCustomHideAnimations)
        {
            _hideAnimations = BuildHideAnimations(hideTo);
        }
    }

    private static ImplicitAnimationSet BuildShowAnimations(string? slideFrom)
    {
        var animations = new ImplicitAnimationSet();

        if (slideFrom is null)
        {
            // SlideDirection.None: no animation at all.
            return animations;
        }

        animations.Add(new OpacityAnimation
        {
            From = 0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
            EasingType = EasingType.Cubic,
        });

        animations.Add(new TranslationAnimation
        {
            From = slideFrom,
            To = $"0,0,{ShadowDepth}",
            Duration = TimeSpan.FromMilliseconds(250),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
            EasingType = EasingType.Cubic,
        });

        return animations;
    }

    private static ImplicitAnimationSet BuildHideAnimations(string? slideTo)
    {
        var animations = new ImplicitAnimationSet();

        if (slideTo is null)
        {
            // SlideDirection.None: no animation at all.
            return animations;
        }

        animations.Add(new OpacityAnimation
        {
            From = 1.0,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
            EasingType = EasingType.Cubic,
        });

        animations.Add(new TranslationAnimation
        {
            From = $"0,0,{ShadowDepth}",
            To = slideTo,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
            EasingType = EasingType.Cubic,
        });

        return animations;
    }
}
