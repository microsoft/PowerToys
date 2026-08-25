// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
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
///   <item>Apply a <see cref="TransparentTintBackdrop"/> so the HWND is fully
///   see-through and the visible chrome can be drawn by the content.</item>
/// </list>
/// <para>This window is intentionally animation-agnostic: it does not own any
/// chrome or motion. Consumers supply their own content (typically a
/// <see cref="TransientSurface"/>) which draws the acrylic, border, corners and
/// shadow, and animates itself. <see cref="Show()"/> and <see cref="Hide"/>
/// coordinate <c>SW_SHOWNA</c> (no-activate) with the
/// <see cref="Showing"/> / <see cref="Hiding"/> events: a content surface
/// subscribes to those (e.g. via <see cref="TransientSurface.SubscribeTo"/>)
/// and plays its in/out animation. The <see cref="Hiding"/> event supports
/// deferrals, so the underlying
/// <see cref="Microsoft.UI.Windowing.AppWindow.Hide"/> is delayed until the
/// content has finished animating out. With no listener the window simply shows
/// or hides immediately.</para>
/// <para><b>Multiple surfaces.</b> More than one <see cref="TransientSurface"/>
/// may host on the same window by each calling
/// <see cref="TransientSurface.SubscribeTo"/>. The <see cref="Showing"/> and
/// <see cref="Hiding"/> events are simply raised for every subscriber, and
/// because <see cref="HidingEventArgs"/> aggregates deferrals the underlying
/// window is hidden only after <em>all</em> surfaces have finished animating
/// out. To let each surface play its own distinct transition, call the
/// parameterless <see cref="Show()"/> (so every surface uses its configured
/// <c>ShowTransition</c>/<c>HideTransition</c>); the <see cref="Show(Transition)"/>
/// overload instead broadcasts a single transition to all surfaces. Sizing the
/// window and positioning each surface within it remain the consumer's
/// responsibility (this window owns no layout).</para>
/// </remarks>
public partial class TransparentWindow : WinUIEx.WindowEx
{
    private const uint DwmwaColorNone = 0xFFFFFFFE;
    private const int DwmwaCloak = 13;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;

    private const int GwlpHwndParent = -8;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;

    private const int SwHide = 0;
    private const int SwShowNa = 8;

    private readonly nint _hwnd;

    private Microsoft.UI.Xaml.Window? _hiddenOwnerWindow;
    private bool _inputHooked;
    private bool _seenActivated;
    private bool _cloakWhenHidden;
    private bool _cloaked;

    public TransparentWindow()
    {
        AppWindow.Hide();
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        ApplyTransparentChrome();

        SystemBackdrop = new TransparentTintBackdrop();

        Activated += OnActivatedForDismiss;
    }

    /// <summary>
    /// Applies (or re-applies) the baseline transparent chrome: strips the
    /// native frame, disables the Win11 DWM border color and corner rounding,
    /// and marks the window as a tool window. Idempotent and safe to call again
    /// after a cross-monitor move — a DPI change can reset some of these
    /// attributes, so consumers that reposition across monitors may re-invoke it.
    /// </summary>
    protected void ApplyTransparentChrome()
    {
        if (_hwnd == 0)
        {
            return;
        }

        HwndExtensions.ToggleWindowStyle(_hwnd, false, WindowStyle.TiledWindow);

        unsafe
        {
            uint borderColor = DwmwaColorNone;
            _ = DwmSetWindowAttribute(_hwnd, DwmwaBorderColor, &borderColor, sizeof(uint));

            int cornerPref = DwmwcpDoNotRound;
            _ = DwmSetWindowAttribute(_hwnd, DwmwaWindowCornerPreference, &cornerPref, sizeof(int));
        }

        ApplyExStyleBit(WsExToolWindow, true);
    }

    /// <summary>
    /// Gets or sets a value indicating whether pressing <c>Esc</c> while the
    /// window content has keyboard focus dismisses the window (<see cref="Hide"/>).
    /// Defaults to <see langword="false"/>. The window is shown without
    /// activation, so the consumer must activate it for its content to receive
    /// keyboard input.
    /// </summary>
    public bool DismissOnEscape { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window dismisses itself
    /// (<see cref="Hide"/>) when it loses focus (is deactivated), i.e. light
    /// dismiss. Defaults to <see langword="false"/>. Only takes effect after the
    /// window has been activated at least once since the last <see cref="Show()"/>,
    /// so the transient deactivation that can occur during the show sequence does
    /// not dismiss it prematurely. The window is shown without activation, so the
    /// consumer must activate it for this to apply.
    /// </summary>
    public bool DismissOnFocusLost { get; set; }

    /// <summary>
    /// Raised (without activation) when <see cref="Show()"/> makes the window
    /// visible. A content surface subscribes to this to play its in-animation,
    /// using <see cref="ShowingEventArgs.Transition"/>.
    /// </summary>
    public event TypedEventHandler<TransparentWindow, ShowingEventArgs>? Showing;

    /// <summary>
    /// Raised when <see cref="Hide"/> begins dismissing the window. A content
    /// surface subscribes to this to play its out-animation, taking a deferral
    /// (<see cref="HidingEventArgs.GetDeferral"/>) so the underlying window stays
    /// visible until the animation completes.
    /// </summary>
    public event TypedEventHandler<TransparentWindow, HidingEventArgs>? Hiding;

    /// <summary>
    /// Shows the window without activation (<c>SW_SHOWNA</c>) and raises
    /// <see cref="Showing"/> without a transition, so subscribed content animates
    /// in using its own configured show transition. After
    /// <see cref="EnableCloakedHide"/> the window stays cloaked here and only
    /// becomes visible on <see cref="Reveal"/>.
    /// </summary>
    public void Show() => RaiseShow(null);

    /// <summary>
    /// Shows the window without activation (<c>SW_SHOWNA</c>) and raises
    /// <see cref="Showing"/> so subscribed content animates in using
    /// <paramref name="transition"/>, overriding its configured show transition.
    /// After <see cref="EnableCloakedHide"/> the window stays cloaked here and only
    /// becomes visible on <see cref="Reveal"/>.
    /// </summary>
    /// <param name="transition">The transition the content should play.</param>
    public void Show(Transition transition) => RaiseShow(transition);

    private void RaiseShow(Transition? transition)
    {
        // A new show can interrupt a deferred hide. In that case HideCore never runs, so the
        // previous Reveal left the HWND uncloaked. Cloak synchronously before the caller returns to
        // the dispatcher; otherwise content rebuilt for this summon can render at the old bounds.
        if (DispatcherQueue.HasThreadAccess)
        {
            // CloakAndKeepShown uses SW_HIDE, which can raise Deactivated. Reset this first so an
            // internal show transition is not mistaken for a user-initiated focus loss.
            _seenActivated = false;
            EnsureCloakedBeforeShow();
        }

        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                _seenActivated = false;

                // Also cover callers that entered Show from another thread.
                EnsureCloakedBeforeShow();
                EnsureInputHooks();

                // Cloaked mode is made SW_SHOWNA-visible only after cloaking succeeds.
                if (!_cloakWhenHidden)
                {
                    _ = ShowWindow(_hwnd, SwShowNa);
                }

                Showing?.Invoke(this, new ShowingEventArgs(transition));
            });
    }

    /// <summary>
    /// Raises <see cref="Hiding"/> so subscribed content animates out, then hides
    /// the underlying <see cref="Microsoft.UI.Windowing.AppWindow"/> - or cloaks the
    /// window when <see cref="EnableCloakedHide"/> was called - once every deferral
    /// taken by a handler has completed (immediately if none were taken).
    /// </summary>
    public void Hide()
    {
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                var args = new HidingEventArgs();
                Hiding?.Invoke(this, args);
                args.RunWhenComplete(HideCore);
            });
    }

    /// <summary>
    /// Switches this window from hiding to <b>cloaking</b>, and immediately puts it
    /// into that state.
    /// </summary>
    /// <remarks>
    /// <para>A hidden WinUI 3 window renders nothing, so its composition surface keeps
    /// whatever frame it was showing when it was hidden, and the next <see cref="Show()"/>
    /// puts that stale frame back on screen before the new content has been laid out.
    /// A cloaked window is equally invisible but stays <c>SW_SHOWNA</c>-shown, so XAML
    /// keeps laying it out and painting it and there is no stale frame to put back.</para>
    /// <para>This changes what the show sequence means: <see cref="Show()"/> still raises
    /// <see cref="Showing"/> so the content lays out and animates in, but the window stays
    /// cloaked - <see cref="Reveal"/> is what puts it on screen. A consumer that rebuilds
    /// its content on every summon can therefore lay that content out while still invisible
    /// and reveal a window that is correct in its first visible frame.</para>
    /// <para>Enabling it also warms the window up: the XAML tree is built, templated and
    /// painted right away rather than on the first summon.</para>
    /// <para>Call this once, from the consumer's constructor after its content has been
    /// set. Cloaking is a DWM feature; if DWM refuses, the window remains hidden and the
    /// next <see cref="Show()"/> retries.</para>
    /// </remarks>
    protected void EnableCloakedHide()
    {
        // Unlike a hidden HWND, a cloaked HWND remains WS_VISIBLE. Give it a hidden owner before
        // the first SW_SHOWNA so Explorer reliably keeps it out of the taskbar on every virtual-
        // desktop taskbar configuration; WS_EX_TOOLWINDOW alone is not sufficient there.
        EnsureHiddenOwner();
        _cloakWhenHidden = true;
        HideCore();
    }

    /// <summary>
    /// Puts a cloaked window on screen. Consumers call this once the content that
    /// <see cref="Show()"/> laid out is ready to be seen. Does nothing unless
    /// <see cref="EnableCloakedHide"/> was called and the window is currently cloaked.
    /// </summary>
    public void Reveal()
    {
        if (!_cloaked)
        {
            return;
        }

        // Keep the state and click-through style when DWM refuses to uncloak. A later Reveal can
        // then retry instead of returning early while the HWND is still invisible.
        if (!SetCloak(false))
        {
            return;
        }

        _cloaked = false;

        // Restore hit-testing: the window is on screen again, so it must behave like any
        // other window (see CloakAndKeepShown for why it is click-through while cloaked).
        ApplyExStyleBit(WsExTransparent, false);
    }

    private void HideCore()
    {
        if (_cloakWhenHidden)
        {
            CloakAndKeepShown();
            return;
        }

        AppWindow.Hide();
    }

    private void EnsureCloakedBeforeShow()
    {
        if (_cloakWhenHidden && !_cloaked)
        {
            CloakAndKeepShown();
        }
    }

    private void EnsureHiddenOwner()
    {
        if (_hiddenOwnerWindow is not null || _hwnd == 0)
        {
            return;
        }

        _hiddenOwnerWindow = new Microsoft.UI.Xaml.Window();
        nint hiddenOwnerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_hiddenOwnerWindow);
        _ = SetWindowLongPtr(_hwnd, GwlpHwndParent, hiddenOwnerHwnd);

        // WS_EX_APPWINDOW overrides normal owner-based taskbar suppression.
        ApplyExStyleBit(WsExAppWindow, false);
    }

    private void CloakAndKeepShown()
    {
        if (_cloaked)
        {
            return;
        }

        // Hide first so a DWM failure cannot leave an uncloaked overlay on screen. The next Show
        // retries cloaking; only a successfully cloaked HWND is made SW_SHOWNA-visible again.
        _ = ShowWindow(_hwnd, SwHide);
        if (!SetCloak(true))
        {
            return;
        }

        _cloaked = true;

        // Cloaking only takes the window out of composition, not out of hit-testing, and
        // this HWND sits exactly where the user is working. Make it click-through so the
        // invisible window cannot swallow input meant for the app underneath it.
        ApplyExStyleBit(WsExTransparent, true);

        // SW_HIDE above hands the foreground back to whatever window should own it (only the OS
        // can pick the right one). Now that cloaking succeeded, SW_SHOWNA leaves this window
        // "shown", which keeps XAML painting it, while the cloak keeps it off screen.
        _ = ShowWindow(_hwnd, SwShowNa);
    }

    private bool SetCloak(bool cloak)
    {
        if (_hwnd == 0)
        {
            return false;
        }

        unsafe
        {
            int value = cloak ? 1 : 0;
            return DwmSetWindowAttribute(_hwnd, DwmwaCloak, &value, sizeof(int)) == 0;
        }
    }

    private void OnActivatedForDismiss(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (DismissOnFocusLost && _seenActivated)
            {
                Hide();
            }

            return;
        }

        _seenActivated = true;
    }

    private void EnsureInputHooks()
    {
        if (_inputHooked || Content is not UIElement element)
        {
            return;
        }

        element.KeyDown += OnContentKeyDown;
        _inputHooked = true;
    }

    private void OnContentKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (DismissOnEscape && e.Key == global::Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            Hide();
        }
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
