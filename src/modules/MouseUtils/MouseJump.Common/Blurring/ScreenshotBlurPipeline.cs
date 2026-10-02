// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using MouseJump.Common.Helpers;
using MouseJump.Models.Display;

namespace MouseJump.Common.Blurring;

/// <summary>
/// Generates and retains a blurred stand-in screenshot per physical screen, for use as an
/// activation's initial placeholder while its own fresh screenshot is still loading - see
/// <see cref="BlurHelper.CreateBlurredCopy"/> for what "blurred" means here.
/// </summary>
/// <remarks>
/// Each screen has three slots: at most one blur actually running ("doing"), at most one
/// screenshot waiting for its turn ("todo" - only ever the latest; an older queued one is
/// discarded rather than piling up), and at most one completed result ("done", served by
/// <see cref="TryGet"/> until it expires). A "doing" blur is never interrupted once started -
/// only ever superseded via "todo" - so blur work always eventually finishes and "done" keeps
/// converging on something recent even under rapid repeat activation, rather than every new
/// activation cancelling the previous one before any ever completes.
/// </remarks>
public sealed class ScreenshotBlurPipeline
{
    /// <summary>
    /// How strongly a stand-in screenshot is blurred, desaturated and darkened - see
    /// <see cref="BlurHelper.CreateBlurredCopy"/> for what the values mean. Muting the stand-in
    /// on top of blurring it is what makes the real screenshot visually "pop" into place once it
    /// arrives, rather than the swap reading as a same-ish image just sharpening.
    /// </summary>
    private const decimal BlurIntensity = 0.75m;
    private const double BlurSaturation = 0.5;
    private const double BlurBrightness = 0.55;

    /// <summary>
    /// How long a completed blur remains available - see <see cref="TryGet"/>.
    /// </summary>
    private static readonly TimeSpan ClaimWindow = TimeSpan.FromMinutes(2);

    private readonly object sync = new();

    /// <summary>
    /// Keyed by <see cref="ScreenInfo.Handle"/> - the <c>HMONITOR</c>, and the only field of
    /// <see cref="ScreenInfo"/> that's actually stable across activations for the same physical
    /// screen. Deliberately *not* keyed by <see cref="ScreenInfo"/> itself: it's a record, so its
    /// equality compares every field, including <see cref="ScreenInfo.WorkingArea"/> - which
    /// comes straight from <c>GetMonitorInfo</c>'s <c>rcWork</c> and can legitimately drift by a
    /// pixel or two between calls (e.g. the taskbar auto-hiding) even when the monitor itself
    /// hasn't changed at all. Keying by the whole record made every single activation see a
    /// "new" screen and evict the previous one's cached blur before it could ever be reused -
    /// confirmed via telemetry showing 100% cache misses despite the same handles being reused
    /// throughout.
    /// </summary>
    private readonly Dictionary<nint, ScreenshotBlurState> screens = new();

    public ScreenshotBlurPipeline(TimeProvider? timeProvider = null)
    {
        this.TimeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the clock this pipeline reads "now" from - <see cref="TryGet"/> and <see cref="RunBlur"/> use
    /// this instead of calling <see cref="DateTime.UtcNow"/> directly, so a test can fast-forward
    /// past <see cref="ClaimWindow"/>'s expiry deterministically instead of waiting on it for
    /// real. Defaults to <see cref="TimeProvider.System"/> in production.
    /// </summary>
    private TimeProvider TimeProvider
    {
        get;
    }

    /// <summary>
    /// Raised each time a background <see cref="RunBlur"/> call finishes, whether or not its
    /// result actually got committed to "done" - a screen dropped via <see cref="SetActiveScreens"/>
    /// while its blur was still running discards the result but still raises this, so a test can
    /// always deterministically know the background work is over rather than risk waiting
    /// forever for a result that was never going to arrive. Internal, test-only hook so a test
    /// can await a specific screen's blur completing instead of polling <see cref="TryGet"/> in
    /// a loop. Not used by production code.
    /// </summary>
    internal event Action<ScreenInfo>? BlurCompleted;

    /// <remarks>
    /// <see cref="global::MouseJump.Common.Telemetry.Telemetry.Current"/>, renamed here because
    /// "Telemetry" (the class) collides with the sibling <c>FancyMouse.Common.Telemetry</c>
    /// namespace - a plain <c>using</c> can't fix this (enclosing-namespace lookup wins over
    /// using-aliases for a name reachable that way), so this avoids needing every call site
    /// below to fully-qualify it instead.
    /// </remarks>
    private static global::MouseJump.Common.Telemetry.TelemetryContext CurrentTelemetry
        => global::MouseJump.Common.Telemetry.Telemetry.Current;

    /// <summary>
    /// Supplies a fresh screenshot for <paramref name="screenInfo"/>. Takes ownership of
    /// <paramref name="screenshotImage"/> - see <see cref="Capture.IScreenshotCaptureSink.SetScreenshotAsync"/>
    /// for the same convention this mirrors. If <paramref name="screenInfo"/> isn't one of the
    /// currently active screens (see <see cref="SetActiveScreens"/>), disposes
    /// <paramref name="screenshotImage"/> and returns. Otherwise, either starts blurring
    /// it immediately if nothing is already in progress for this screen, or queues it as this
    /// screen's "todo" - replacing, and disposing, whatever was previously queued, since only
    /// the latest matters.
    /// </summary>
    public void SetScreenshot(ScreenInfo screenInfo, Bitmap screenshotImage)
    {
        ArgumentNullException.ThrowIfNull(screenInfo);
        ArgumentNullException.ThrowIfNull(screenshotImage);

        lock (this.sync)
        {
            if (!this.screens.TryGetValue(screenInfo.Handle, out var state))
            {
                screenshotImage.Dispose();
                return;
            }

            ScreenshotBlurPipeline.CurrentTelemetry.WriteEvent(new { handle = screenInfo.Handle }, "screenshotBlurQueued");

            if (state.BlurInProgress)
            {
                state.Todo?.Dispose();
                state.Todo = screenshotImage;
                return;
            }

            state.BlurInProgress = true;
            _ = Task.Run(() => this.RunBlur(screenInfo, screenshotImage));
        }
    }

    /// <summary>
    /// If a blurred placeholder for <paramref name="screenInfo"/> is available and still within
    /// <see cref="ClaimWindow"/> of completing, invokes <paramref name="use"/> with it and
    /// returns <see langword="true"/>, otherwise returns <see langword="false"/> without
    /// invoking <paramref name="use"/>. TryGet locks pipeline and passes the screen's bitmap
    /// directly (no cloning) to the <paramref name="use"/> Action synchronously while this
    /// pipeline's own lock is still held. The <paramref name="use"/> Action must not retain
    /// the <see cref="Bitmap"/> passed to it beyond the call, since the pipeline is free to
    /// dispose it the moment <paramref name="use"/> returns.
    /// </summary>
    /// <remarks>
    /// Emits one of three outcomes to telemtry - "found" (a fresh completed blur was passed
    /// to <paramref name="use"/>), "not complete" (this screen has a blur queued or running,
    /// just not ready yet), or "not found" (nothing tracked for this screen at all) - to
    /// make the caching behaviour directly observable in telemetry (where enabled).
    /// </remarks>
    public bool TryGet(ScreenInfo screenInfo, Action<Bitmap> use)
    {
        ArgumentNullException.ThrowIfNull(screenInfo);
        ArgumentNullException.ThrowIfNull(use);

        lock (this.sync)
        {
            if (!this.screens.TryGetValue(screenInfo.Handle, out var state))
            {
                ScreenshotBlurPipeline.CurrentTelemetry.WriteEvent(new { handle = screenInfo.Handle }, "blurRequestedNotFound");
                return false;
            }

            if (state.Done is not null)
            {
                if (this.TimeProvider.GetUtcNow() - state.DoneAt <= ScreenshotBlurPipeline.ClaimWindow)
                {
                    ScreenshotBlurPipeline.CurrentTelemetry.WriteEvent(new { handle = screenInfo.Handle }, "blurRequestedFound");
                    use(state.Done);
                    return true;
                }

                state.Done.Dispose();
                state.Done = null;
            }

            var operation = (state.BlurInProgress || state.Todo is not null)
                ? "blurRequestedNotComplete"
                : "blurRequestedNotFound";
            ScreenshotBlurPipeline.CurrentTelemetry.WriteEvent(new { handle = screenInfo.Handle }, operation);
            return false;
        }
    }

    /// <summary>
    /// Reconciles current blur tasks and state against <paramref name="currentScreens"/> and
    /// culls anything that is no longer valid (e.g. blurred images for monitors that have
    /// been disconnected). <paramref name="currentScreens"/> is the full set of currently
    /// connected physical screens, supplied once per activation - anything tracked for
    /// a screen no longer in this list (todo/done bitmaps) is disposed and dropped entirely.
    /// Any blurs still in progress for a dropped screen can't be interrupted (see the class
    /// remarks), but <see cref="RunBlur"/> checks against the current set itself before
    /// writing back, so its result is discarded harmlessly once it finishes. New screens
    /// not seen before are initialised with empty state.
    /// </summary>
    public void SetActiveScreens(IReadOnlyCollection<ScreenInfo> currentScreens)
    {
        ArgumentNullException.ThrowIfNull(currentScreens);

        lock (this.sync)
        {
            var currentHandles = currentScreens.Select(screenInfo => screenInfo.Handle).ToHashSet();

            foreach (var handle in this.screens.Keys.Except(currentHandles).ToList())
            {
                var state = this.screens[handle];
                state.Todo?.Dispose();
                state.Done?.Dispose();
                this.screens.Remove(handle);
            }

            foreach (var handle in currentHandles)
            {
                if (!this.screens.ContainsKey(handle))
                {
                    this.screens[handle] = new ScreenshotBlurState();
                }
            }
        }
    }

    /// <summary>
    /// Blurs <paramref name="image"/> on this background thread, then writes the result into
    /// this screen's "done" slot, unless <paramref name="screenInfo"/> has since been dropped
    /// via <see cref="SetActiveScreens"/>, in which case the result is discarded instead. If a
    /// newer screenshot was queued in the meantime ("todo"), starts blurring that next.
    /// </summary>
    /// <remarks>
    /// Ownership of <paramref name="image"/> is transferred to RunBlur, and the image is disposed
    /// once it has been read from.
    /// </remarks>
    private void RunBlur(ScreenInfo screenInfo, Bitmap image)
    {
        Bitmap blurredImage;
        using (ScreenshotBlurPipeline.CurrentTelemetry.BeginTimer(new { }, "CreateBlurredCopy"))
        using (image)
        {
            blurredImage = BlurHelper.CreateBlurredCopy(
                image, ScreenshotBlurPipeline.BlurIntensity, ScreenshotBlurPipeline.BlurSaturation, ScreenshotBlurPipeline.BlurBrightness);
        }

        try
        {
            lock (this.sync)
            {
                if (!this.screens.TryGetValue(screenInfo.Handle, out var state))
                {
                    blurredImage.Dispose();
                    return;
                }

                state.Done?.Dispose();
                state.Done = blurredImage;
                state.DoneAt = this.TimeProvider.GetUtcNow();
                state.BlurInProgress = false;

                if (state.Todo is not null)
                {
                    var next = state.Todo;
                    state.Todo = null;
                    state.BlurInProgress = true;
                    _ = Task.Run(() => this.RunBlur(screenInfo, next));
                }
            }
        }
        finally
        {
            // raised outside the lock, so a subscriber is free to call back into this pipeline
            // (e.g. TryGet) without risking a deadlock against the lock this method just held
            this.BlurCompleted?.Invoke(screenInfo);
        }
    }
}
