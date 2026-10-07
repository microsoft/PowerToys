// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Windowing;

using MouseJump.WinUI3.Win32Gen;

using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewWindow
{
    /// <summary>
    /// This window's own handle - captured once in <see cref="InitializeWindow"/> and reused
    /// wherever later code needs to talk to the real Win32 window (<see cref="ApplyRoundRectRegion"/>
    /// in particular), rather than re-resolving it from <see cref="WinRT.Interop.WindowNative"/>
    /// every time.
    /// </summary>
    private HWND hWnd;

    /// <summary>
    /// Initializes some settings on the application window.
    /// </summary>
    private void InitializeWindow()
    {
        this.hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        var appWindow = this.AppWindow;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            // get the current window style
            var result = User32.GetWindowLong(this.hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE)
                .ThrowIfFailed()
                .GetValue();

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var style = (WINDOW_STYLE)result;
            style &= ~WINDOW_STYLE.WS_OVERLAPPEDWINDOW;
            style |= WINDOW_STYLE.WS_POPUP;
            _ = User32.SetWindowLong(this.hWnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style)
                .ThrowIfFailed();

            // get the current extended window style
            result = User32.GetWindowLong(this.hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE)
                .ThrowIfFailed()
                .GetValue();

            // set the window to be borderless, with no title bar, and hide all of the max / min / close buttons
            var exStyle = (WINDOW_EX_STYLE)result;
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW; // hide the taskbar icon
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOPMOST;    // make topmost
            _ = User32.SetWindowLong(this.hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle)
                .ThrowIfFailed();
        }

        this.InitializeEvents();

        // this window is never actually Hide()/Show()'d again after this point - "hidden" is
        // instead an empty SetWindowRgn clip (see ApplyEmptyRectRegion/HideWindow), so DWM keeps
        // compositing it continuously in the background instead of presenting a blank first
        // frame on every reveal. Clip to nothing *before* ever showing it, so there's no
        // on-screen flash of its default (unstyled) content at startup either.
        this.ApplyEmptyRectRegion();
        this.AppWindow.Show();
    }

    /// <summary>
    /// Clips the window (not just its content) to a rounded-rectangle region
    /// sized <paramref name="width"/> x <paramref name="height"/> with corner
    /// radius <paramref name="cornerRadius"/>, matching the rendered border image's
    /// outer bezel shape.
    /// </summary>
    /// <remarks>
    /// Pixels outside the region are clipped by the operating system and are never
    /// composited, genuinely showing the real desktop through them. <c>CreateRoundRectRgn</c>'s
    /// region handle is only freed by this method on failure - <c>SetWindowRgn</c> takes
    /// ownership of it on success, and deleting it afterwards would be a use-after-free from
    /// the OS's perspective.
    /// </remarks>
    private void ApplyRoundRectRegion(int width, int height, int cornerRadius)
    {
        // CreateRoundRectRgn's bottom-right point is exclusive - the non-clipped region
        // includes columns x1 to (x2-1) and rows y1 to (y2-1), confirmed by experimentation.
        // Using x2=width and y2=height would clip away the last row/column of the rendered
        // region, so we add 1 to both to ensure the region *displays* the full width x height
        // instead of columns 0 to (width-1) and rows 0 to (height-1).
        var region = Gdi32.CreateRoundRectRgn(0, 0, width + 1, height + 1, cornerRadius * 2, cornerRadius * 2)
            .ThrowIfFailed()
            .GetValue();
        this.SetWindowRegion(region);
    }

    /// <summary>
    /// Clips this window to an empty region, making it fully invisible while it stays
    /// actively composited by DWM. This avoids the need to use AppWindow.Show (and Hide)
    /// which were found to cause an instantaneous flash of the un-composited window
    /// before setting down and re-rendering the actual content. Setting the window
    /// to an empty region has the same visual effect as hiding it while not triggering
    /// recompositing with AppWindow.Show().
    /// </summary>
    /// <remarks>
    /// Deliberately use <c>CreateRectRgn(0, 0, 0, 0)</c>, not <c>CreateRoundRectRgn</c> with
    /// all-zero arguments: only <c>CreateRectRgn</c> documents that setting both
    /// diametrically-opposite corners to (0,0) creates a genuinely empty region.
    /// <c>CreateRoundRectRgn</c> makes no such guarantee for a degenerate rectangle - it can
    /// return <see langword="null"/> (a real failure this codebase saw), rather than an empty
    /// region.
    /// </remarks>
    private void ApplyEmptyRectRegion()
    {
        var region = Gdi32.CreateRectRgn(0, 0, 0, 0)
            .ThrowIfFailed()
            .GetValue();
        this.SetWindowRegion(region);
    }

    private void SetWindowRegion(HRGN region)
    {
        try
        {
            _ = User32.SetWindowRgn(this.hWnd, region, bRedraw: true)
                .ThrowIfFailed();
        }
        catch
        {
            _ = Gdi32.DeleteObject((HGDIOBJ)region)
                .IgnoreFailure();
            throw;
        }
    }
}
