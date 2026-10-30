// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using MouseJump.Common.Helpers;
using MouseJump.Models.Display;

namespace MouseJump.Common.Blurring;

/// <summary>
/// Generates and caches the blurred stand-in screenshots that are shown
/// as placeholders when a new screenshot takes more than the grace period
/// to capture. The blurred image is generated from the *previous*
/// activation's actual screen capture on the assumption screens don't
/// change that much between activations.
/// </summary>
/// <remarks>
/// Each screen has three slots:
///
/// * One that may contain a screenshot waiting its turn to be blurred ("todo"). This is
///   only ever the latest submitted image - submitting a new screenshot to be blurred
///   for a screen replaces anything currently waiting in this slot
///
/// * One that may contain a screenshot which is currently being blurred ("doing")
///
/// * One that may contain a completed result ("done"). This is lent out by <see cref="TryBorrow"/>
///   until it expires
///
/// This approach allows the "doing" screenshot to complete and move to "done" before
/// starting the next "todo" screenshot. Under very high frequency activation all that
/// happens is the "todo" image is repeatedly replaced while the "doing" screenshot is
/// being processed, ensuring a regular supply of updated blurred "done" images, rather
/// than constantly cancelling in-flight "doing" screenshots to start a more recent "todo"
/// but never actually finishing any.
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
    /// How long a completed blur remains available - see <see cref="TryBorrow"/>.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenshotBlurPipeline"/> class
    /// that uses the system clock to determine when a blurred image expires.
    /// </summary>
    public ScreenshotBlurPipeline()
        : this(TimeProvider.System, ScreenshotBlurPipeline.CreateBlurredCopy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenshotBlurPipeline"/> class
    /// that uses the specified TimeProvider to determine when a blurred image expires.
    /// Can be used with a fake or mock TimerProvider in unit tests to test image
    /// expiry behaviour without waiting for real time to pass.
    /// </summary>
    public ScreenshotBlurPipeline(TimeProvider timeProvider)
        : this(timeProvider, ScreenshotBlurPipeline.CreateBlurredCopy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreenshotBlurPipeline"/> class
    /// that uses the specified TimeProvider to determine when a blurred image expires
    /// and uses the specified "blur" function to blur images. The custom "blur"
    /// function can be used to deterministically hold a blur "in progress" while
    /// a unit test submits more screenshots or changes the active screens to test
    /// difficult-to-orchestrate race conditions and timing edge cases. It's
    /// "internal" and not used in production code.
    /// </summary>
    internal ScreenshotBlurPipeline(TimeProvider? timeProvider, Func<Bitmap, Bitmap> blur)
    {
        this.TimeProvider = timeProvider ?? TimeProvider.System;
        this.Blur = blur ?? throw new ArgumentNullException(nameof(blur));
    }

    /// <summary>
    /// Gets the clock this pipeline reads "now" from - <see cref="TryBorrow"/> and
    /// <see cref="DoBlur"/> use this instead of calling <see cref="DateTime.UtcNow"/>
    /// directly, so a test can fast-forward a mock TimeProvider past <see cref="ClaimWindow"/>'s
    /// expiry deterministically instead of waiting on it for real. Defaults to
    /// <see cref="TimeProvider.System"/> in production.
    /// </summary>
    private TimeProvider TimeProvider
    {
        get;
    }

    /// <summary>
    /// Gets the function that produces a blurred copy of a screenshot - always
    /// <see cref="CreateBlurredCopy"/> in production.
    /// </summary>
    private Func<Bitmap, Bitmap> Blur
    {
        get;
    }

    /// <summary>
    /// Raised every time a background <see cref="DoBlur"/> call finishes, even if
    /// its result didn't actually get committed to the "done" slot. Not used by
    /// production code.
    /// </summary>
    /// <remarks>
    /// If a blur task is running for a screen and a call to
    /// <see cref="SetActiveScreens"/> *removes* that screen while the task is
    /// still running, the task will continue to run but will discard its result
    /// when completed and will still raise this event. This allows a unit test to
    /// deterministically wait for background work to complete even if the
    /// resulting blurred image doesn't appear in the "done" slot. Not used by
    /// production code.
    /// </remarks>
    internal event Action<ScreenInfo>? BlurCompleted;

    /// <remarks>
    /// <see cref="global::MouseJump.Common.Telemetry.Telemetry.Current"/>, renamed here because
    /// "Telemetry" (the class) collides with the sibling <c>MouseJump.Common.Telemetry</c>
    /// namespace - a plain <c>using</c> can't fix this (enclosing-namespace lookup wins over
    /// using-aliases for a name reachable that way), so this avoids needing every call site
    /// below to fully-qualify it instead.
    /// </remarks>
    private static global::MouseJump.Common.Telemetry.TelemetryContext CurrentTelemetry
        => global::MouseJump.Common.Telemetry.Telemetry.Current;

    /// <summary>
    /// Updates the collection of screens we're tracking screenshots for. Any
    /// currently-tracked screens not in the new list are removed and their
    /// resources disposed. Any new screens in the list are added to the
    /// tracked screen collection.
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
    /// Supplies a fresh screenshot for <paramref name="screenInfo"/> and takes
    /// ownership of <paramref name="screenshotImage"/> - see
    /// <see cref="Capture.IScreenshotCaptureSink.SetScreenshotAsync"/> for the
    /// same convention this mirrors.
    /// </summary>
    /// <remarks>
    /// If <paramref name="screenInfo"/> isn't one of the currently active
    /// screens (see <see cref="SetActiveScreens"/>), it disposes
    /// <paramref name="screenshotImage"/> and returns. Otherwise, either
    /// starts blurring it immediately if nothing is already in progress for
    /// this screen, or queues it as this screen's "todo" - replacing, and
    /// disposing, whatever was previously queued, since only the latest
    /// matters.
    /// </remarks>
    public void SetScreenshot(ScreenInfo screenInfo, Bitmap screenshotImage)
    {
        ArgumentNullException.ThrowIfNull(screenInfo);
        ArgumentNullException.ThrowIfNull(screenshotImage);

        lock (this.sync)
        {
            // make sure we recognize this screen in our dictionary
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
            _ = Task.Run(() => this.DoBlur(screenInfo, state, screenshotImage));
        }
    }

    /// <summary>
    /// Invokes 'action' on the latest blurred image for screenInfo, if there is one.
    /// 'action' borrows the image - it does not take ownership and must not dispose it.
    /// </summary>
    /// <returns>
    /// Returns <see langword="true"/> if <paramref name="action"/> was invoked,
    /// otherwise returns <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The bitmap is only loaned to 'action', not handed over - for performance reasons
    /// it's the pipeline's own copy so that clone doesn't have to be created. It's
    /// passed to <paramref name="action"/> synchronously while the pipeline's lock is held.
    /// <paramref name="action"/> must not keep the <see cref="Bitmap"/> beyond the call (copy
    /// it if it's needed for longer), since the pipeline is free to dispose it the moment
    /// <paramref name="action"/> returns - and should be quick, since other background blurs
    /// can't complete while it runs.
    /// </para>
    /// <para>
    /// Emits one of three outcomes to telemetry - "found" (a fresh completed blur was lent
    /// to <paramref name="action"/>), "not complete" (this screen has a blur queued or running,
    /// just not ready yet), or "not found" (nothing tracked for this screen at all) - to
    /// make the caching behaviour directly observable in telemetry (where enabled).
    /// </para>
    /// </remarks>
    public bool TryBorrow(ScreenInfo screenInfo, Action<Bitmap> action)
    {
        ArgumentNullException.ThrowIfNull(screenInfo);
        ArgumentNullException.ThrowIfNull(action);

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
                    action(state.Done);
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
    /// Blurs <paramref name="image"/> on this background thread, then writes the result into
    /// <paramref name="owner"/>'s "done" slot, unless <paramref name="owner"/> has since been
    /// removed from the currently-tracked screens by a call to <see cref="SetActiveScreens"/>,
    /// in which case the result is discarded instead. If a newer screenshot was queued in the
    /// meantime ("todo"), starts blurring that next.
    /// </summary>
    /// <remarks>
    /// Ownership of <paramref name="image"/> is transferred to DoBlur, and the image is disposed
    /// once it has been read from - including if blurring it fails, in which case "done" is left
    /// untouched but the screen is still released for its next "todo" as normal.
    /// </remarks>
    private void DoBlur(ScreenInfo screenInfo, ScreenshotBlurState owner, Bitmap image)
    {
        Bitmap? blurredImage = null;
        try
        {
            using (ScreenshotBlurPipeline.CurrentTelemetry.BeginTimer(new { }, "CreateBlurredCopy"))
            using (image)
            {
                blurredImage = this.Blur(image);
            }
        }
        catch (Exception ex)
        {
            // nothing awaits this background task, so an exception escaping here would be
            // silently swallowed *and* leave BlurInProgress stuck on - every later screenshot
            // for this screen would then sit in "todo" forever. instead, treat a failed blur as
            // "no result": keep whatever "done" already holds, and carry on to the next "todo"
            ScreenshotBlurPipeline.CurrentTelemetry.WriteEvent(new { handle = screenInfo.Handle, error = ex.Message }, "screenshotBlurFailed");
        }

        try
        {
            lock (this.sync)
            {
                // compare the state instance, not just the handle - if the screen was removed and
                // re-added while this blur ran, the handle now maps to a *new* state with its own
                // blur in progress, and this result mustn't overwrite or advance that
                if (!this.screens.TryGetValue(screenInfo.Handle, out var state) || !object.ReferenceEquals(state, owner))
                {
                    blurredImage?.Dispose();
                    return;
                }

                if (blurredImage is not null)
                {
                    state.Done?.Dispose();
                    state.Done = blurredImage;
                    state.DoneAt = this.TimeProvider.GetUtcNow();
                }

                state.BlurInProgress = false;

                if (state.Todo is not null)
                {
                    var next = state.Todo;
                    state.Todo = null;
                    state.BlurInProgress = true;
                    _ = Task.Run(() => this.DoBlur(screenInfo, state, next));
                }
            }
        }
        finally
        {
            // raised outside the lock, so a subscriber is free to call back into this pipeline
            // (e.g. TryBorrow) without risking a deadlock against the lock this method just held
            this.BlurCompleted?.Invoke(screenInfo);
        }
    }

    private static Bitmap CreateBlurredCopy(Bitmap image)
        => BlurHelper.CreateBlurredCopy(
            image, ScreenshotBlurPipeline.BlurIntensity, ScreenshotBlurPipeline.BlurSaturation, ScreenshotBlurPipeline.BlurBrightness);
}
