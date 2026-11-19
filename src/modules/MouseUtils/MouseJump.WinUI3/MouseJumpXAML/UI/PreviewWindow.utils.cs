// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Xaml;

using MouseJump.Common.Capture;
using MouseJump.Common.Helpers;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
using MouseJump.Models.Styles;
using MouseJump.WinUI3.Internal.Helpers;
using MouseJump.WinUI3.Win32Gen;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewWindow
{
    /// <summary>
    /// Waits for the screenshot capture pipeline to complete all of the requested
    /// screenshot captures, and logs if any of them fail.
    /// </summary>
    private async Task ObserveAndDisposeAsync(ScreenshotCapturePipeline pipeline)
    {
        try
        {
            await pipeline.WaitForCompletionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("one or more screenshot captures failed\r\n" + ex.ToString());
        }
        finally
        {
            await pipeline.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Calculates the high-dpi scaling ratio based on the current monitor's display settings.
    /// </summary>
    private double GetHighDpiScalingRatio()
    {
        var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowDpi = User32.GetDpiForWindow(hWnd)
            .ThrowIfFailed()
            .GetValue();
        var scalingRatio = (double)PInvoke.USER_DEFAULT_SCREEN_DPI / windowDpi;
        return scalingRatio;
    }

    internal async Task InvokeOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // already on the ui thread - just run it
        if (this.DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        // the action might be called from a task that we're awaiting
        // so we need to make sure we use the UI thread
        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!this.DispatcherQueue.TryEnqueue(
            () =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(
                new InvalidOperationException("DispatcherQueue is not accepting work items."));
        }

        // wait for the task to complete
        await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Sets this window to be the operating system's foreground window.
    /// </summary>
    /// <remarks>
    /// This only works if the current process meets specific conditions (documented at
    /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow#remarks).
    /// Note that it generally *will* meet those conditions if called from the HotKeyManager's
    /// event handler because that satisfies the "calling process received the last input event."
    /// requirement so the process can set the foreground window even if it isn't currently
    /// the foreground process.
    /// </remarks>
    private void SetAsForegroundWindow()
    {
        var hWnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = User32.SetForegroundWindow(hWnd)
            .IgnoreFailure();
    }

    /// <summary>
    /// Sets keyboard focus on <see cref="PreviewPane"/>, which owns all keyboard navigation
    /// (see <see cref="PreviewPane.NavigateTo"/>) via its own <c>PreviewKeyDown</c>.
    /// </summary>
    private void FocusPreviewPane()
    {
        if (this.PreviewPane.Focus(FocusState.Programmatic))
        {
            // success
            return;
        }

        // <control>.Focus can fail silently if the pane hasn't finished layout
        // yet - most likely the very first time the window is ever shown after
        // the app starts - so retry (once) on the next UI thread tick
        this.DispatcherQueue.TryEnqueue(() => this.PreviewPane.Focus(FocusState.Programmatic));
    }

    /// <summary>
    /// Renders this window's own border image and assigns it to <see cref="BorderImage"/>.
    /// This is rendered directly by the host rather than the pane, since the border is
    /// deliberately not one of the PreviewPane's concerns.
    /// </summary>
    /// <returns>
    /// The window-region dimensions (see <see cref="ApplyRoundRectRegion"/>) that match the
    /// rendered border - deliberately *not* applied here, so the window's clip region only ever
    /// changes once <see cref="ShowWindowAsync"/> reveals fully-built content, not partway
    /// through rendering it. The caller passes these straight back into
    /// <see cref="ShowWindowAsync"/>.
    /// </returns>
    private async Task<(int Width, int Height, int CornerRadius)> RenderBorderAsync(PreviewLayout previewLayout, BoxStyle previewWindowStyle)
    {
        // render against a zero-based host box - a border image is its own bitmap, so its
        // pixel coordinates need to start at (0,0) regardless of where the (possibly
        // negative, once enlarged outward from a zero-based content box) host bounds would
        // otherwise place it.
        var localHostBounds = LayoutHelper.GetPreviewWindowBounds(previewLayout.CanvasLayout.CanvasBounds.OuterBounds, previewWindowStyle)
            .MoveTo(new PointInfo(0, 0));

        using var borderBitmap = DrawingHelper.RenderBorder(localHostBounds.BorderBounds.Size, previewWindowStyle.BorderStyle);

        await this.InvokeOnUiThreadAsync(
            () =>
            {
                var highDpiScalingRatio = this.GetHighDpiScalingRatio();
                this.BorderImage.Width = borderBitmap.Width * highDpiScalingRatio;
                this.BorderImage.Height = borderBitmap.Height * highDpiScalingRatio;
                this.BorderImage.Source = MediaHelper.ToBitmapImage(borderBitmap);

                // position PreviewPane so it lines up exactly with the transparent hole in
                // the middle of the border image - the offset is always the host box's own
                // border+padding thickness (margin is excluded - RenderBorder's bitmap starts
                // at the border's own outer edge, not the margin's), regardless of where
                // localHostBounds itself sits.
                var offsetX = (localHostBounds.ContentBounds.X - localHostBounds.BorderBounds.X) * (decimal)highDpiScalingRatio;
                var offsetY = (localHostBounds.ContentBounds.Y - localHostBounds.BorderBounds.Y) * (decimal)highDpiScalingRatio;
                this.PreviewPane.Margin = new Thickness((double)offsetX, (double)offsetY, 0, 0);
            }).ConfigureAwait(false);

        return (borderBitmap.Width, borderBitmap.Height, (int)previewWindowStyle.BorderStyle.Left);
    }

    private async Task SetPreviewPaneLayoutAsync(PreviewLayout previewLayout, ScreenInfo activatedScreen)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                this.PreviewPane.Layout = previewLayout;
                this.PreviewPane.ActiveScreen = activatedScreen;
            }).ConfigureAwait(false);
    }
}
