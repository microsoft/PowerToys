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
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmncrpDisabled = 2;

    private const int GwlExStyle = -20;
    private const int WsExDlgModalFrame = 0x00000001;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExWindowEdge = 0x00000100;
    private const int WsExClientEdge = 0x00000200;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    private const int SwShowNa = 8;

    private readonly nint _hwnd;

    private bool _inputHooked;
    private bool _seenActivated;

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
    /// Opt-in, aggressive frame elimination for <b>full-monitor / edge-to-edge</b>
    /// overlays (e.g. Shortcut Guide), layered on top of
    /// <see cref="ApplyTransparentChrome"/>. On such a window the HWND edge
    /// coincides with the screen edge, so any residual 1-pixel DWM seam shows as
    /// a faint full-screen outline; this removes it by dropping the 3-D edge
    /// extended styles, disabling non-client rendering, and extending the frame
    /// across the whole client area.
    /// </summary>
    /// <remarks>
    /// This is intentionally <b>not</b> applied by default: content-sized
    /// surfaces inset their visible card behind transparent padding, so any
    /// phantom border falls in the transparent margin and is invisible — and the
    /// aggressive bits here (disabled NC rendering + sheet-of-glass frame) carry
    /// needless compositing risk for those surfaces. Like
    /// <see cref="ApplyTransparentChrome"/> it is idempotent and may be re-called
    /// after a cross-monitor move.
    /// </remarks>
    protected void ApplyFullBleedHardening()
    {
        if (_hwnd == 0)
        {
            return;
        }

        // Drop the 3-D-ish window edges Windows draws for ordinary top-level
        // windows; the remaining 1-px line around a transparent overlay comes
        // from these extended styles.
        ApplyExStyleBit(WsExWindowEdge, false);
        ApplyExStyleBit(WsExClientEdge, false);
        ApplyExStyleBit(WsExDlgModalFrame, false);

        unsafe
        {
            // Disable non-client rendering entirely so the DWM doesn't draw ANY
            // frame/border chrome (not even a 1-px line).
            int ncrpDisabled = DwmncrpDisabled;
            _ = DwmSetWindowAttribute(_hwnd, DwmwaNcRenderingPolicy, &ncrpDisabled, sizeof(int));
        }

        // Extend the frame into the entire client area. With a transparent
        // backdrop this eliminates the last possible seam between the
        // non-client and client regions that the DWM might draw.
        var margins = new Margins { CxLeftWidth = -1, CxRightWidth = -1, CyTopHeight = -1, CyBottomHeight = -1 };
        _ = DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // Force DWM to re-evaluate the frame after the style/frame changes.
        _ = SetWindowPos(_hwnd, 0, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
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
    /// in using its own configured show transition.
    /// </summary>
    public void Show() => RaiseShow(null);

    /// <summary>
    /// Shows the window without activation (<c>SW_SHOWNA</c>) and raises
    /// <see cref="Showing"/> so subscribed content animates in using
    /// <paramref name="transition"/>, overriding its configured show transition.
    /// </summary>
    /// <param name="transition">The transition the content should play.</param>
    public void Show(Transition transition) => RaiseShow(transition);

    private void RaiseShow(Transition? transition)
    {
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                _seenActivated = false;
                EnsureInputHooks();
                _ = ShowWindow(_hwnd, SwShowNa);
                Showing?.Invoke(this, new ShowingEventArgs(transition));
            });
    }

    /// <summary>
    /// Raises <see cref="Hiding"/> so subscribed content animates out, then hides
    /// the underlying <see cref="Microsoft.UI.Windowing.AppWindow"/> once every
    /// deferral taken by a handler has completed (immediately if none were taken).
    /// </summary>
    public void Hide()
    {
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                var args = new HidingEventArgs();
                Hiding?.Invoke(this, args);
                args.RunWhenComplete(AppWindow.Hide);
            });
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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins pMarInset);

    [LibraryImport("dwmapi.dll")]
    private static unsafe partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, void* pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int CxLeftWidth;
        public int CxRightWidth;
        public int CyTopHeight;
        public int CyBottomHeight;
    }
}
