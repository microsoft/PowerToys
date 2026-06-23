// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using WinUIEx;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Reusable transparent host window for transient overlays
/// (toasts, banners, indicators) that should not steal foreground.
/// </summary>
/// <remarks>
/// <para>The constructor applies all of the boilerplate that PowerToys overlays
/// currently hand-roll:</para>
/// <list type="bullet">
///   <item>Strip the native frame and caption (<c>WS_THICKFRAME</c> etc.).</item>
///   <item>Disable the Win11 1-pixel DWM border and corner rounding.</item>
///   <item>Mark the window as a tool window so it stays out of the taskbar and Alt-Tab.</item>
///   <item>Extend content into the title bar and collapse the title bar.</item>
/// </list>
/// <para>The visible chrome (acrylic + border + corner radius + shadow) lives
/// in a <see cref="TransparentCard"/> that the constructor assigns to
/// <see cref="Microsoft.UI.Xaml.Window.Content"/>. Consumers supply their own
/// UI via <see cref="InnerContent"/> — which is the XAML default-content slot
/// thanks to <see cref="ContentPropertyAttribute"/> — so a derived window can
/// be written as <c>&lt;common:TransparentWindow&gt;&lt;TextBlock/&gt;&lt;/common:TransparentWindow&gt;</c>.</para>
/// <para>Transparency is achieved with a <see cref="TransparentTintBackdrop"/>
/// system backdrop so the area outside the <see cref="TransparentCard"/> is
/// fully see-through. That buffer area is NOT click-through, so consumers
/// should keep it as small as possible (just enough to give the card's
/// shadow + slide animation room to breathe — roughly 24 px on each side).</para>
/// <para><see cref="Show"/> and <see cref="Hide"/> coordinate <c>SW_SHOWNA</c>
/// (no-activate), the <see cref="Microsoft.UI.Xaml.UIElement.Visibility"/>
/// toggle on the card, and a debounced
/// <see cref="Microsoft.UI.Windowing.AppWindow.Hide"/> sized from the longest
/// animation in <see cref="HideAnimations"/>. Animations target the card so
/// the entire surface (border, acrylic, shadow, inner content) slides as one.</para>
/// </remarks>
[ContentProperty(Name = nameof(InnerContent))]
public partial class TransparentWindow : WinUIEx.WindowEx
{
    private const uint DwmwaColorNone = 0xFFFFFFFE;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private const int SwShowNa = 8;

    private readonly DispatcherQueueTimer _hideCloseTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly nint _hwnd;
    private readonly TransparentCard _card;

    private ImplicitAnimationSet _showAnimations;
    private ImplicitAnimationSet _hideAnimations;

    public TransparentWindow()
    {
        AppWindow.Hide();
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        HwndExtensions.ToggleWindowStyle(_hwnd, false, WindowStyle.TiledWindow);

        unsafe
        {
            uint borderColor = DwmwaColorNone;
            _ = DwmSetWindowAttribute(_hwnd, DwmwaBorderColor, &borderColor, sizeof(uint));

            int cornerPref = DwmwcpDoNotRound;
            _ = DwmSetWindowAttribute(_hwnd, DwmwaWindowCornerPreference, &cornerPref, sizeof(int));
        }

        ApplyExStyleBit(WsExToolWindow, true);

        _showAnimations = BuildDefaultShowAnimations();
        _hideAnimations = BuildDefaultHideAnimations();

        _card = new TransparentCard();
        Content = _card;

        SystemBackdrop = new TransparentTintBackdrop();
    }

    /// <summary>
    /// Gets the <see cref="TransparentCard"/> that provides the window's
    /// visible chrome (acrylic + border + shadow). Consumers can configure
    /// its layout (e.g. <c>HorizontalAlignment</c>, <c>VerticalAlignment</c>,
    /// <c>MaxWidth</c>, <c>Margin</c>) to position the card inside the
    /// window, or apply a custom <c>Style</c> to change its look.
    /// </summary>
    public TransparentCard Card => _card;

    /// <summary>
    /// Gets or sets the visual hosted inside the window's
    /// <see cref="TransparentCard"/>. This is the XAML default-content slot:
    /// child elements declared between the opening and closing
    /// <c>TransparentWindow</c> tags in a derived .xaml are routed here.
    /// </summary>
    public object? InnerContent
    {
        get => _card.Content;
        set => _card.Content = value;
    }

    /// <summary>
    /// Gets or sets the animations played against
    /// <see cref="Microsoft.UI.Xaml.Window.Content"/> when <see cref="Show"/>
    /// flips it to <see cref="Visibility.Visible"/>. Defaults to a 200 ms
    /// fade-in plus a 250 ms slide-up of 24 px.
    /// </summary>
    public ImplicitAnimationSet ShowAnimations
    {
        get => _showAnimations;
        set => _showAnimations = value ?? new ImplicitAnimationSet();
    }

    /// <summary>
    /// Gets or sets the animations played against
    /// <see cref="Microsoft.UI.Xaml.Window.Content"/> when <see cref="Hide"/>
    /// flips it to <see cref="Visibility.Collapsed"/>. Defaults to a 180 ms
    /// fade-out plus a 180 ms slide-down of 12 px.
    /// </summary>
    public ImplicitAnimationSet HideAnimations
    {
        get => _hideAnimations;
        set => _hideAnimations = value ?? new ImplicitAnimationSet();
    }

    /// <summary>
    /// Shows the window without activation (<c>SW_SHOWNA</c>) and flips
    /// <see cref="Microsoft.UI.Xaml.Window.Content"/> to
    /// <see cref="Visibility.Visible"/> so <see cref="ShowAnimations"/> plays.
    /// Repeated calls reset the content to its hidden pose first so the show
    /// animation re-triggers cleanly. Any pending hide is cancelled.
    /// </summary>
    public void Show()
    {
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                _hideCloseTimer.Stop();

                if (Content is UIElement content)
                {
                    // Re-apply each call so swapping animation collections at
                    // runtime takes effect on the next show/hide cycle.
                    Implicit.SetShowAnimations(content, _showAnimations);
                    Implicit.SetHideAnimations(content, _hideAnimations);

                    // Reset to the hidden pose so the show animation always
                    // animates from the configured starting frame.
                    content.Visibility = Visibility.Collapsed;
                }

                _ = ShowWindow(_hwnd, SwShowNa);

                if (Content is UIElement c2)
                {
                    c2.Visibility = Visibility.Visible;
                }
            });
    }

    /// <summary>
    /// Flips <see cref="Microsoft.UI.Xaml.Window.Content"/> to
    /// <see cref="Visibility.Collapsed"/> so <see cref="HideAnimations"/>
    /// plays, then hides the underlying
    /// <see cref="Microsoft.UI.Windowing.AppWindow"/> once the longest
    /// animation in <see cref="HideAnimations"/> (delay + duration) has
    /// completed.
    /// </summary>
    public void Hide()
    {
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                if (Content is UIElement content)
                {
                    content.Visibility = Visibility.Collapsed;
                }

                _hideCloseTimer.Debounce(
                    AppWindow.Hide,
                    interval: GetAnimationSetTotalDuration(_hideAnimations),
                    immediate: false);
            });
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

    private void ApplyExStyleBit(int bit, bool set)
    {
        if (_hwnd == 0)
        {
            return;
        }

        nint exStyle = GetWindowLongPtr(_hwnd, GwlExStyle);
        nint updated = set ? exStyle | bit : exStyle & ~(nint)bit;
        if (updated != exStyle)
        {
            _ = SetWindowLongPtr(_hwnd, GwlExStyle, updated);
        }
    }

    private static ImplicitAnimationSet BuildDefaultShowAnimations() => new()
    {
        new OpacityAnimation
        {
            From = 0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
            EasingType = EasingType.Cubic,
        },
        new TranslationAnimation
        {
            From = "0,24,32",
            To = "0,0,32",
            Duration = TimeSpan.FromMilliseconds(250),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
            EasingType = EasingType.Cubic,
        },
    };

    private static ImplicitAnimationSet BuildDefaultHideAnimations() => new()
    {
        new OpacityAnimation
        {
            From = 1.0,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
            EasingType = EasingType.Cubic,
        },
        new TranslationAnimation
        {
            From = "0,0,32",
            To = "0,12,32",
            Duration = TimeSpan.FromMilliseconds(180),
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
            EasingType = EasingType.Cubic,
        },
    };

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("dwmapi.dll")]
    private static unsafe partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, void* pvAttribute, int cbAttribute);
}
