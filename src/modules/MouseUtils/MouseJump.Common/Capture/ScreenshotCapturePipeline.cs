// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using MouseJump.Models.Layout;

namespace MouseJump.Common.Capture;

/// <summary>
/// Captures screenshot images for screens for a single preview activation.
/// <see cref="AddCaptureTasks"/> starts one capture per screen on a device
/// and returns each screen's own capture task, so a caller can await whichever
/// ones it specifically needs completed (typically just the activated screen,
/// to know it's safe to reveal the preview window without a later capture of
/// that same screen picking the window up). Every capture is also independently
/// pushed into <see cref="IScreenshotCaptureSink"/> as soon as it completes, in
/// whatever order that actually happens - the caller doesn't need to await
/// that part at all.
/// </summary>
/// <remarks>
/// <see cref="AddCaptureTasks"/> isn't safe to call concurrently with itself or with
/// <see cref="DisposeAsync"/> - it's expected to be called once per device, from one thread,
/// before disposal. Waiting for completion and disposal are deliberately separate concerns:
/// <see cref="WaitForCompletionAsync"/> is the synchronization point (and where failures
/// surface), while <see cref="DisposeAsync"/> is just resource cleanup - it waits internally so
/// it doesn't dispose a provider still in use, but never throws and doesn't imply cancellation.
/// Ownership of each <see cref="IScreenshotCaptureProvider"/> passed to
/// <see cref="AddCaptureTasks"/> transfers to this pipeline: <see cref="DisposeAsync"/> disposes
/// every one of them (if disposable).
/// </remarks>
public sealed class ScreenshotCapturePipeline : IAsyncDisposable
{
    private readonly IScreenshotCaptureSink sink;
    private readonly CancellationToken cancellationToken;
    private readonly List<IScreenshotCaptureProvider> providers = new();
    private readonly List<Task> pendingApplyTasks = new();

    public ScreenshotCapturePipeline(IScreenshotCaptureSink sink, CancellationToken cancellationToken = default)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Starts one capture per screen on <paramref name="deviceLayout"/> using
    /// <paramref name="provider"/>, and returns each screen paired with its own capture task.
    /// </summary>
    public IReadOnlyList<(ScreenLayout ScreenLayout, Task<Bitmap> CaptureTask)> AddCaptureTasks(
        DeviceLayout deviceLayout, IScreenshotCaptureProvider provider)
    {
        ArgumentNullException.ThrowIfNull(deviceLayout);
        ArgumentNullException.ThrowIfNull(provider);

        this.providers.Add(provider);

        var results = new List<(ScreenLayout, Task<Bitmap>)>();
        foreach (var screenLayout in deviceLayout.ScreenLayouts)
        {
            var captureTask = provider.CaptureAsync(
                screenLayout.ScreenInfo.DisplayArea,
                screenLayout.ScreenBounds.ContentBounds.Size,
                this.cancellationToken);

            this.pendingApplyTasks.Add(this.ApplyWhenReadyAsync(screenLayout, captureTask));

            results.Add((screenLayout, captureTask));
        }

        return results;
    }

    /// <summary>
    /// Waits for every capture (and its resulting sink push) started via
    /// <see cref="AddCaptureTasks"/> to finish. A capture that was cancelled - e.g. because a
    /// newer activation superseded this pipeline - is treated as an expected outcome, not a
    /// failure. Any other failure is collected rather than stopping this from waiting for the
    /// rest (one screen failing shouldn't hide what happened to the others), and thrown from
    /// here as an <see cref="AggregateException"/> once every capture has settled - so a
    /// systemic failure (e.g. every screen on one device failing the same way) is visibly
    /// distinguishable from a single one-off failure, instead of only ever surfacing whichever
    /// exception happened to be first.
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        var exceptions = new List<Exception>();

        foreach (var applyTask in this.pendingApplyTasks)
        {
            try
            {
                await applyTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // an expected outcome for a capture superseded by cancellation - not a failure
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                "One or more screenshot captures failed.", exceptions);
        }
    }

    /// <summary>
    /// Waits for every capture to finish (see <see cref="WaitForCompletionAsync"/>), then
    /// disposes every provider that was passed to <see cref="AddCaptureTasks"/> (if disposable).
    /// Providers are disposed even if that wait surfaces a failure, so one bad screen doesn't
    /// leak the others' resources - disposal's own job is just to release them, not to report
    /// failures a second time (call <see cref="WaitForCompletionAsync"/> directly for that).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await this.WaitForCompletionAsync().ConfigureAwait(false);
        }
        catch
        {
            // already the caller's to observe via WaitForCompletionAsync if they want it -
            // disposal itself shouldn't throw
        }

        foreach (var provider in this.providers.OfType<IDisposable>())
        {
            provider.Dispose();
        }
    }

    private async Task ApplyWhenReadyAsync(ScreenLayout screenLayout, Task<Bitmap> captureTask)
    {
        Bitmap bitmap;
        try
        {
            bitmap = await captureTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // cancelled before it ever produced a result - nothing to push to the sink
            return;
        }
        catch (Exception ex)
        {
            // identify which screen failed - without this, a genuine capture failure is
            // indistinguishable from any other screen's in whatever ends up observing
            // WaitForCompletionAsync's AggregateException
            throw new InvalidOperationException(
                $"Screenshot capture failed for {screenLayout.ScreenInfo}.", ex);
        }

        // ownership of the bitmap transfers to the sink from here - see IScreenshotCaptureSink's
        // remarks - so no "using" or "Dispose" here even though this method "created" it via the
        // capture task above
        await this.sink.SetScreenshotAsync(screenLayout, bitmap).ConfigureAwait(false);
    }
}
