// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
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
/// </remarks>
public partial class TransparentWindow : WinUIEx.WindowEx
{
    private const uint DwmwaColorNone = 0xFFFFFFFE;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpDoNotRound = 1;

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private const int SwShowNa = 8;

    private readonly nint _hwnd;

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

        SystemBackdrop = new TransparentTintBackdrop();
    }

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
