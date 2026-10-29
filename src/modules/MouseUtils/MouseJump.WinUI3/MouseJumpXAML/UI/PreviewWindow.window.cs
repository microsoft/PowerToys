// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;

using MouseJump.Common.Capture;
using MouseJump.Common.Helpers;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
using MouseJump.WinUI3.Helpers;

using Windows.Graphics;

using AppTelemetry = MouseJump.Common.Telemetry.Telemetry;

namespace MouseJump.WinUI3.UI;

internal sealed partial class PreviewWindow
{
    public async Task ShowPreviewAsync(CancellationToken cancellationToken)
    {
        Logger.LogInfo(string.Join(
            '\n',
            "-----------",
            nameof(PreviewWindow.ShowPreviewAsync),
            "-----------"));

        // claim the foreground window as early as possible - see SetAsForegroundWindow
        // remarks for why this needs to happen now, before any of the capture/layout work
        // below, rather than later when we actually show the window
        this.SetAsForegroundWindow();

        using var activationTimer = AppTelemetry.Current.BeginTimer(new { }, nameof(PreviewWindow.ShowPreviewAsync));

        // hide the form while we redraw it... (also cancels whatever the *previous* activation's
        // own activationCancellation was, via ClearPreview - harmless if the caller already
        // cancelled and awaited it before calling this method, since cancelling an
        // already-cancelled source is a no-op)
        using (AppTelemetry.Current.BeginTimer(new { }, "HideWindowAsync"))
        {
            await this.HideWindowAsync()
                .ConfigureAwait(false);
        }

        // capture this first so we get an accurate current mouse location
        // (in case the user moves it a few pixels while the form is rendered)
        var activatedLocation = MouseHelper.GetCursorPosition();

        var appSettings = this.SettingsHelper.CurrentSettings ?? throw new InvalidOperationException();

        DisplayInfo displayInfo;
        using (AppTelemetry.Current.BeginTimer(new { }, "GetDisplayInfo"))
        {
            displayInfo = DeviceHelper.GetDisplayInfo();
        }

        var activatedScreen = DeviceHelper.GetActivatedScreen(displayInfo.Devices[0], activatedLocation);

        var previewStyle = SettingsHelper.GetActivePreviewStyle(appSettings);
        PreviewLayout previewLayout;
        using (AppTelemetry.Current.BeginTimer(new { }, "GetPreviewLayout"))
        {
            previewLayout = LayoutHelper.GetPreviewLayout(
                previewStyle,
                displayInfo,
                maximumSize: activatedScreen.DisplayArea.Size);
        }

        // the outer border is this window's own responsibility, not the preview pane's -
        // see LayoutHelper.GetPreviewWindowStyle. PreviewLayout itself has no desktop position
        // (only a size - see PreviewLayout), so positioning the window on the desktop -
        // centered on the activated location, clamped to the activated screen - is entirely
        // this window's own job too.
        var previewWindowStyle = LayoutHelper.GetPreviewWindowStyle(previewStyle.CanvasStyle);
        var hostBounds = LayoutHelper.GetPreviewWindowBounds(new RectangleInfo(previewLayout.PreviewSize), previewWindowStyle);
        var positionedHostOuterBounds = LayoutHelper.PositionOnScreen(hostBounds.OuterBounds, activatedScreen, activatedLocation);

        // a newer activation superseding this one is the common, expected case under rapid
        // repeat activation - check here, before paying for the border render below, rather
        // than only ever noticing via a later awaited call
        cancellationToken.ThrowIfCancellationRequested();

        using (AppTelemetry.Current.BeginTimer(new { }, "PositionWindowAsync"))
        {
            await this.PositionWindowAsync(positionedHostOuterBounds)
                .ConfigureAwait(false);
        }

        (int Width, int Height, int CornerRadius) windowRegion;
        using (AppTelemetry.Current.BeginTimer(new { }, "RenderBorderAsync"))
        {
            windowRegion = await this.RenderBorderAsync(previewLayout, previewWindowStyle)
                .ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // builds every screen's bezel + placeholder fill immediately, so there's something to
        // show as soon as the window becomes visible - screenshots backfill afterwards
        using (AppTelemetry.Current.BeginTimer(new { }, "SetPreviewPaneLayoutAsync"))
        {
            await this.SetPreviewPaneLayoutAsync(previewLayout, activatedScreen)
                .ConfigureAwait(false);
        }

        // start a fresh cancellation scope right before it's first needed, rather than earlier
        // in the method - the gap between creating this and using it is exactly the window in
        // which a spurious PreviewWindow_Activated Deactivated event (e.g. from moving/hiding
        // the window above) could call ClearPreview and dispose it out from under us, so keep
        // that gap as small as possible. Linked to the caller's token, so either the caller
        // superseding this activation *or* the preview being cleared for any other reason (see
        // ClearPreview) stops the capture pipeline below.
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.activationCancellation = cancellation;

        var pipeline = new ScreenshotCapturePipeline(new PreviewPaneScreenshotSink(this), cancellation.Token);

        // create one capture provider per device - see IScreenshotCaptureProvider/
        // DesktopScreenshotCaptureProvider remarks for why a single instance is safe (and
        // necessary) to share across all of that device's screens. Ownership of each provider
        // transfers to the pipeline - see ScreenshotCapturePipeline.DisposeAsync.
        var captureTasks = new List<(ScreenLayout ScreenLayout, Task<Bitmap> CaptureTask)>();
        using (AppTelemetry.Current.BeginTimer(new { }, "kickOffCaptureRequests"))
        {
            foreach (var deviceLayout in previewLayout.CanvasLayout.DeviceLayouts)
            {
                captureTasks.AddRange(pipeline.AddCaptureTasks(
                    deviceLayout, new DesktopScreenshotCaptureProvider()));
            }
        }

        // the activated screen's capture must complete before the window is shown,
        // otherwise a *later* capture of that screen would risk capturing the preview window
        // itself, since it's positioned on top of the activated screen. We only need the
        // capture itself to be done here, not the full initialisation of that screen's controls.
        var activatedCaptureTask = captureTasks
            .Single(entry => object.ReferenceEquals(entry.ScreenLayout.ScreenInfo, activatedScreen))
            .CaptureTask;

        try
        {
            // give every screen up to ScreenshotGracePeriod to finish capturing before
            // showing the window - a typical (fast, local) activation finishes well within
            // that and shows fully populated, and a screen whose capture is still executing
            // will have either a blank background image or a blurred version of the previous
            // screenshot shown instead until the capture is ready, at which point the screen's
            // image is automatically updated.
            var allCaptureTasks = captureTasks.Select(entry => entry.CaptureTask).ToArray();
            using (AppTelemetry.Current.BeginTimer(new { }, "gracePeriodRace"))
            {
                await Task.WhenAny(Task.WhenAll(allCaptureTasks), Task.Delay(PreviewWindow.ScreenshotGracePeriod, cancellation.Token))
                    .ConfigureAwait(false);
            }

            using (AppTelemetry.Current.BeginTimer(new { }, "activatedScreenCapture"))
            {
                await activatedCaptureTask
                    .ConfigureAwait(false);
            }

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.MouseJumpShowEvent());

            using (AppTelemetry.Current.BeginTimer(new { }, "ShowWindowAsync"))
            {
                await this.ShowWindowAsync(windowRegion.Width, windowRegion.Height, windowRegion.CornerRadius, cancellation.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // a newer activation superseded this one while we were still waiting on captures
            // - let it own showing the window instead of us doing so with what's now stale
            // layout/position state
        }
        catch (Exception ex)
        {
            // a genuine (non-cancellation) failure capturing the activated screen - unlike a
            // backfill screen's failure (see ObserveAndDisposeAsync), this one has to stop us
            // from showing the window at all, since we can't guarantee it's safe to reveal
            // without knowing that screen was actually captured
            Logger.LogError($"failed to capture the activated screen ({activatedScreen}); not showing the preview window\r\n" + ex.ToString());
        }
        finally
        {
            // don't hold ShowPreviewAsync open waiting for every screen to finish
            // backfilling - ObserveAndDisposeAsync logs any backfill capture failure and
            // disposes the pipeline's providers once everything's settled, whether or not we
            // ended up showing the window above
            _ = this.ObserveAndDisposeAsync(pipeline);
        }
    }

    private void ClearPreview()
    {
        this.activationCancellation?.Cancel();
        this.activationCancellation?.Dispose();
        this.activationCancellation = null;

        if ((this.BorderImage.Source is null) && (this.PreviewPane.Layout is null))
        {
            return;
        }

        this.BorderImage.Source = null;
        this.PreviewPane.Layout = null;
        this.PreviewPane.ActiveScreen = null;
    }

    private void HideWindow()
    {
        // clip to an empty region rather than AppWindow.Hide() - see ApplyEmptyRectRegion's remarks
        this.ApplyEmptyRectRegion();
        this.ClearPreview();
    }

    private async Task HideWindowAsync()
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                this.HideWindow();
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the window by swapping its clip region from empty (see <see cref="HideWindow"/>) to
    /// <paramref name="width"/> x <paramref name="height"/> with a curved corner mask.
    /// </summary>
    private async Task ShowWindowAsync(int width, int height, int cornerRadius, CancellationToken cancellationToken)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                // the activation can be cancelled (e.g. a right-click) in the gap between
                // this callback being queued via DispatcherQueue.TryEnqueue and it actually
                // running - that queuing is asynchronous even when called from the UI thread,
                // while HideWindow/ClearPreview run synchronously the moment a right-click is
                // handled, so without this check a queued reveal could still run *after* the
                // content it's about to reveal has already been wiped, showing an empty,
                // unstyled window instead of leaving it hidden
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // we seem to need to turn off topmost and then re-enable it again
                // when we show the form - otherwise it doesn't always get shown topmost...
                var presenter = this.AppWindow.Presenter as OverlappedPresenter
                    ?? throw new InvalidOperationException();
                presenter.IsAlwaysOnTop = false;
                presenter.IsAlwaysOnTop = true;

                this.ApplyRoundRectRegion(width, height, cornerRadius);

                // we have to activate the window to make sure the deactivate event fires
                this.Activate();
                this.FocusPreviewPane();
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Resize and position the form.
    /// </summary>
    private async Task PositionWindowAsync(RectangleInfo bounds)
    {
        await this.InvokeOnUiThreadAsync(
            () =>
            {
                // note - do this with two calls to MoveAndResize rather than one as there appears to
                // be an issue with dpi scaling even when using PerMonitorV2, where if the window is
                // resized *and* moved in one call the resize uses the scaling of the *current*
                // monitor before it's moved.
                //
                // If the move then happens to be to a different monitor, *and* the monitor has a
                // different dpi scaling configured, the window size is then wrong for the dpi scaling
                // of the new monitor.
                //
                // the workaround seems to be to call MoveAndResize twice - the first call might
                // resize it incorrectly, but it moves the window to the correct monitor, and the
                // second call then resizes it correctly.
                //
                // see https://github.com/mikeclayton/FancyMouse/issues/2 for more details
                var windowBounds = new RectInt32((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
                this.AppWindow.MoveAndResize(windowBounds);
                this.AppWindow.MoveAndResize(windowBounds);
            }).ConfigureAwait(false);
    }
}
