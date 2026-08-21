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
using Microsoft.UI.Xaml.Hosting;

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
/// <para>The show transition comes from the window's
/// <see cref="TransparentWindow.Show(Transition)"/> call (or from
/// <see cref="ShowTransition"/> when shown without one); the hide transition
/// always comes from <see cref="HideTransition"/>. Animations target the
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

    // "Pop" transition: scale between 96% and 100% (a subtle 4% grow). Following
    // Fluent motion guidance the scale uses a decelerate (EaseOut) curve; the
    // fade is kept fast so the surface reads as an instant, light pop.
    //
    // The fade must run at least as long as the scale: if the scale outlasted the
    // fade, the surface would reach full opacity while still visibly growing,
    // which reads as a "resize" rather than a pop. Keeping the fade >= the scale
    // hides the growth under the opacity ramp, so by the time it is fully opaque
    // it is already at 100% size.
    private const float PopScaleFrom = 0.96f;
    private const double PopFadeShowMs = 180;
    private const double PopScaleShowMs = 150;
    private const double PopFadeHideMs = 120;
    private const double PopScaleHideMs = 120;

    public static readonly DependencyProperty ShowTransitionProperty = DependencyProperty.Register(
        nameof(ShowTransition),
        typeof(Transition),
        typeof(TransientSurface),
        new PropertyMetadata(Transition.None, OnTransitionChanged));

    public static readonly DependencyProperty HideTransitionProperty = DependencyProperty.Register(
        nameof(HideTransition),
        typeof(Transition),
        typeof(TransientSurface),
        new PropertyMetadata(Transition.None, OnTransitionChanged));

    public static readonly DependencyProperty AcrylicKindProperty = DependencyProperty.Register(
        nameof(AcrylicKind),
        typeof(DesktopAcrylicKind),
        typeof(TransientSurface),
        new PropertyMetadata(DesktopAcrylicKind.Thin));

    private readonly DispatcherQueueTimer _hideCompletedTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private readonly ImplicitAnimationSet _noAnimations = new();

    private ImplicitAnimationSet _showAnimations = new();
    private ImplicitAnimationSet _hideAnimations = new();
    private bool _hasCustomShowAnimations;
    private bool _hasCustomHideAnimations;
    private Action? _abandonPendingHide;

    public TransientSurface()
    {
        DefaultStyleKey = typeof(TransientSurface);

        RebuildDefaultAnimations();

        // Pin the scale center to the surface's center so the "Pop" transition
        // grows/shrinks from the middle, not the top-left corner. An expression
        // animation bound to the visual's own size keeps the center correct from
        // the very first frame (a SizeChanged handler would race the show
        // animation and let the first pop scale from 0,0).
        PinScaleCenter();

        // Start hidden so the first Show() animates in from the configured pose.
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Raised after <see cref="Hide"/> once the longest animation in
    /// <see cref="HideAnimations"/> (delay + duration) has completed.
    /// </summary>
    public event EventHandler? HideCompleted;

    /// <summary>
    /// Gets or sets the transition played when the surface is shown without an
    /// explicit one (see <see cref="Show()"/>). Defaults to
    /// <see cref="Transition.None"/>, which plays no animation at all (the
    /// surface appears instantly); a directional value adds a fade plus a slide
    /// in from that edge, and <see cref="Transition.Pop"/> a fade plus a subtle
    /// scale-up. Changing this regenerates the default <see cref="ShowAnimations"/>
    /// unless it has been set explicitly.
    /// </summary>
    public Transition ShowTransition
    {
        get => (Transition)GetValue(ShowTransitionProperty);
        set => SetValue(ShowTransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets the transition played when the surface is hidden (see
    /// <see cref="Hide"/>). Defaults to <see cref="Transition.None"/>, which
    /// plays no animation at all (the surface disappears instantly); a
    /// directional value adds a fade plus a slide out toward that edge, and
    /// <see cref="Transition.Pop"/> a fade plus a subtle scale-down. Changing
    /// this regenerates the default <see cref="HideAnimations"/> unless it has
    /// been set explicitly.
    /// </summary>
    public Transition HideTransition
    {
        get => (Transition)GetValue(HideTransitionProperty);
        set => SetValue(HideTransitionProperty, value);
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
    /// surface to <see cref="Visibility.Visible"/>. Defaults to the animation
    /// derived from <see cref="ShowTransition"/>. Assigning a value marks the set
    /// as custom so <see cref="ShowTransition"/> no longer overwrites it.
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
    /// surface to <see cref="Visibility.Collapsed"/>. Defaults to the animation
    /// derived from <see cref="HideTransition"/>. Assigning a value marks the set
    /// as custom so <see cref="HideTransition"/> no longer overwrites it.
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
    /// using <paramref name="transition"/> as the show transition.
    /// </summary>
    /// <param name="transition">The transition to play when showing.</param>
    public void Show(Transition transition)
    {
        ShowTransition = transition;
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

        // If a hide from a previous cycle is still in flight, abandon it: drop its
        // pending HideCompleted handler so the outstanding deferral is never
        // completed. We are showing again, so the host must keep the window
        // visible instead of later hiding it for this interrupted cycle.
        _abandonPendingHide?.Invoke();
        _abandonPendingHide = null;

        // Attach the show animation and detach any hide animation: when Show() is
        // called while the surface is still visible, the Collapsed -> Visible
        // restart below would otherwise play the hide animation (a fade/scale out)
        // immediately before the show, producing a visible flash. The real hide
        // animation is re-attached just-in-time in Hide().
        Implicit.SetShowAnimations(this, _showAnimations);
        Implicit.SetHideAnimations(this, _noAnimations);

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
        // Attach the hide animation just before collapsing (Show() detaches it to
        // avoid a flash when re-showing an already-visible surface).
        Implicit.SetHideAnimations(this, _hideAnimations);

        Visibility = Visibility.Collapsed;

        _hideCompletedTimer.Debounce(
            () => HideCompleted?.Invoke(this, EventArgs.Empty),
            interval: GetAnimationSetTotalDuration(_hideAnimations),
            immediate: false);
    }

    private static void OnTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    private static (string? ShowFrom, string? HideTo) GetSlideOffsets(Transition transition) => transition switch
    {
        Transition.Bottom => ($"0,{SlideInOffset},{ShadowDepth}", $"0,{SlideOutOffset},{ShadowDepth}"),
        Transition.Top => ($"0,{-SlideInOffset},{ShadowDepth}", $"0,{-SlideOutOffset},{ShadowDepth}"),
        Transition.Left => ($"{-SlideInOffset},0,{ShadowDepth}", $"{-SlideOutOffset},0,{ShadowDepth}"),
        Transition.Right => ($"{SlideInOffset},0,{ShadowDepth}", $"{SlideOutOffset},0,{ShadowDepth}"),
        _ => (null, null),
    };

    private void OnHostShowing(TransparentWindow sender, ShowingEventArgs e)
    {
        if (e.Transition is Transition transition)
        {
            Show(transition);
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
            _abandonPendingHide = null;
            deferral.Complete();
        }

        // Let a subsequent Show() cancel this hide cleanly: unsubscribe the
        // handler so the deferral is never completed (the window stays visible)
        // rather than firing AppWindow.Hide for an interrupted cycle.
        _abandonPendingHide = () => HideCompleted -= OnHideCompleted;

        HideCompleted += OnHideCompleted;
        Hide();
    }

    private void RebuildDefaultAnimations()
    {
        if (!_hasCustomShowAnimations)
        {
            _showAnimations = BuildShowAnimations(ShowTransition);
        }

        if (!_hasCustomHideAnimations)
        {
            _hideAnimations = BuildHideAnimations(HideTransition);
        }
    }

    private void PinScaleCenter()
    {
        var visual = ElementCompositionPreview.GetElementVisual(this);
        var center = visual.Compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
        visual.StartAnimation("CenterPoint", center);
    }

    private static ImplicitAnimationSet BuildShowAnimations(Transition transition)
    {
        var animations = new ImplicitAnimationSet();

        if (transition == Transition.None)
        {
            // No animation at all.
            return animations;
        }

        if (transition == Transition.Pop)
        {
            animations.Add(new OpacityAnimation
            {
                From = 0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(PopFadeShowMs),
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                EasingType = EasingType.Cubic,
            });

            animations.Add(new ScaleAnimation
            {
                From = $"{PopScaleFrom},{PopScaleFrom},1",
                To = "1,1,1",
                Duration = TimeSpan.FromMilliseconds(PopScaleShowMs),
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                EasingType = EasingType.Cubic,
            });

            return animations;
        }

        var (slideFrom, _) = GetSlideOffsets(transition);

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

    private static ImplicitAnimationSet BuildHideAnimations(Transition transition)
    {
        var animations = new ImplicitAnimationSet();

        if (transition == Transition.None)
        {
            // No animation at all.
            return animations;
        }

        if (transition == Transition.Pop)
        {
            animations.Add(new OpacityAnimation
            {
                From = 1.0,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(PopFadeHideMs),
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
                EasingType = EasingType.Cubic,
            });

            animations.Add(new ScaleAnimation
            {
                From = "1,1,1",
                To = $"{PopScaleFrom},{PopScaleFrom},1",
                Duration = TimeSpan.FromMilliseconds(PopScaleHideMs),
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
                EasingType = EasingType.Cubic,
            });

            return animations;
        }

        var (_, slideTo) = GetSlideOffsets(transition);

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
