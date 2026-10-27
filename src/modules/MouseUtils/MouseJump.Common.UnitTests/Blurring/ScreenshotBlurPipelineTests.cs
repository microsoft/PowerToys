// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Blurring;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;

namespace MouseJump.Common.UnitTests.Blurring;

[TestClass]
public sealed class ScreenshotBlurPipelineTests
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ACompletedBlurIsLentByTryBorrow()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var pipeline = new ScreenshotBlurPipeline();
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        var screenshot = new Bitmap(8, 6);
        pipeline.SetScreenshot(screen, screenshot);
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        Size? blurredSize = null;
        Assert.IsTrue(pipeline.TryBorrow(screen, blurred =>
        {
            Assert.AreNotSame(screenshot, blurred);
            blurredSize = blurred.Size;
        }));
        Assert.AreEqual(new Size(8, 6), blurredSize);
    }

    [TestMethod]
    public async Task AFailedBlurDoesNotBlockLaterScreenshotsForTheSameScreen()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var pipeline = new ScreenshotBlurPipeline();
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        // an already-disposed bitmap makes BlurHelper.CreateBlurredCopy throw on the background
        // thread - the failure still has to be reported as "completed", and must release the
        // screen for its next screenshot rather than leaving it stuck as "in progress" forever
        pipeline.SetScreenshot(screen, ScreenshotBlurPipelineTests.CreateDisposedBitmap());
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        Assert.IsFalse(pipeline.TryBorrow(screen, _ => Assert.Fail("a failed blur should not produce a result")));

        pipeline.SetScreenshot(screen, new Bitmap(4, 4));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        Assert.IsTrue(pipeline.TryBorrow(screen, _ => { }));
    }

    [TestMethod]
    public async Task AFailedBlurKeepsThePreviouslyCompletedResult()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var pipeline = new ScreenshotBlurPipeline();
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        pipeline.SetScreenshot(screen, new Bitmap(4, 4));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        pipeline.SetScreenshot(screen, ScreenshotBlurPipelineTests.CreateDisposedBitmap());
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        // the earlier blur is still a perfectly good stand-in - a later failure shouldn't throw it away
        Size? blurredSize = null;
        Assert.IsTrue(pipeline.TryBorrow(screen, blurred => blurredSize = blurred.Size));
        Assert.AreEqual(new Size(4, 4), blurredSize);
    }

    [TestMethod]
    public async Task ACompletedBlurExpiresAfterTheClaimWindow()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var timeProvider = new ManualTimeProvider();
        var pipeline = new ScreenshotBlurPipeline(timeProvider);
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        pipeline.SetScreenshot(screen, new Bitmap(4, 4));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        Assert.IsTrue(pipeline.TryBorrow(screen, _ => { }));

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.IsFalse(pipeline.TryBorrow(screen, _ => Assert.Fail("an expired blur should not be served")));
    }

    [TestMethod]
    public void AScreenshotForAnInactiveScreenIsDisposedAndIgnored()
    {
        var activeScreen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var inactiveScreen = ScreenshotBlurPipelineTests.CreateScreenInfo(2);
        var pipeline = new ScreenshotBlurPipeline();
        pipeline.SetActiveScreens([activeScreen]);

        var screenshot = new Bitmap(4, 4);
        pipeline.SetScreenshot(inactiveScreen, screenshot);

        // the pipeline took ownership, so it's responsible for disposing what it won't use -
        // a disposed GDI+ bitmap throws on any property access
        Assert.ThrowsExactly<ArgumentException>(() => _ = screenshot.Width);
        Assert.IsFalse(pipeline.TryBorrow(inactiveScreen, _ => Assert.Fail("an inactive screen should have nothing to serve")));
    }

    [TestMethod]
    public async Task RapidSubmissionsDuringABlurOnlyKeepTheLatestAndDisposeTheRest()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var blur = new GatedBlur();
        var pipeline = new ScreenshotBlurPipeline(null, blur.Blur);
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        // the first screenshot starts blurring (and is held there by the gate) - every one
        // submitted after it queues as "todo", each replacing (and disposing) the one before
        pipeline.SetScreenshot(screen, new Bitmap(1, 1));
        var queued = Enumerable.Range(2, 9).Select(size => new Bitmap(size, size)).ToList();
        foreach (var screenshot in queued)
        {
            pipeline.SetScreenshot(screen, screenshot);
        }

        CollectionAssert.AreEqual(
            queued.Select(ScreenshotBlurPipelineTests.IsDisposed).ToList(),
            queued.Select(screenshot => screenshot != queued[^1]).ToList(),
            "every superseded \"todo\" should be disposed, and only the latest kept");

        blur.ReleaseAll();
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        // only two blurs ever ran - the one already in progress, then the latest "todo"
        CollectionAssert.AreEqual(new[] { new Size(1, 1), new Size(10, 10) }, blur.Inputs.ToList());

        Size? blurredSize = null;
        Assert.IsTrue(pipeline.TryBorrow(screen, blurred => blurredSize = blurred.Size));
        Assert.AreEqual(new Size(10, 10), blurredSize);
    }

    [TestMethod]
    public async Task ANewerResultReplacesAndDisposesTheOlderOne()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var blur = new GatedBlur();
        blur.ReleaseAll();
        var pipeline = new ScreenshotBlurPipeline(null, blur.Blur);
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        pipeline.SetScreenshot(screen, new Bitmap(1, 1));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        pipeline.SetScreenshot(screen, new Bitmap(2, 2));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        var outputs = blur.Outputs;
        Assert.IsTrue(ScreenshotBlurPipelineTests.IsDisposed(outputs[0]));
        Assert.IsTrue(pipeline.TryBorrow(screen, blurred => Assert.AreSame(outputs[1], blurred)));
    }

    [TestMethod]
    public async Task RemovingAScreenDuringABlurDiscardsItsResultAndQueuedScreenshot()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var blur = new GatedBlur();
        var pipeline = new ScreenshotBlurPipeline(null, blur.Blur);
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        pipeline.SetScreenshot(screen, new Bitmap(1, 1));
        var queued = new Bitmap(2, 2);
        pipeline.SetScreenshot(screen, queued);

        // the monitor goes away while its blur is still running - the queued screenshot is
        // disposed straight away, but the running blur can't be interrupted
        pipeline.SetActiveScreens([]);
        Assert.IsTrue(ScreenshotBlurPipelineTests.IsDisposed(queued));

        // when the running blur does finish, it still reports completion, but its result is
        // disposed rather than stored, and the queued screenshot never gets blurred
        blur.ReleaseAll();
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        Assert.IsTrue(ScreenshotBlurPipelineTests.IsDisposed(blur.Outputs.Single()));
        CollectionAssert.AreEqual(new[] { new Size(1, 1) }, blur.Inputs.ToList());

        pipeline.SetActiveScreens([screen]);
        Assert.IsFalse(pipeline.TryBorrow(screen, _ => Assert.Fail("a removed screen's result should have been discarded")));
    }

    [TestMethod]
    public async Task AnOldBlurCannotAffectTheStateOfTheSameScreenAfterItIsRemovedAndReAdded()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var blur = new GatedBlur();
        var pipeline = new ScreenshotBlurPipeline(null, blur.Blur);
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        // an old blur is running when the monitor is removed and re-added (same handle, fresh state)
        pipeline.SetScreenshot(screen, new Bitmap(1, 1));
        blur.WaitForInputs(1);
        pipeline.SetActiveScreens([]);
        pipeline.SetActiveScreens([screen]);

        // the replacement state starts its own blur, and queues another behind it
        pipeline.SetScreenshot(screen, new Bitmap(2, 2));
        blur.WaitForInputs(2);
        var queued = new Bitmap(3, 3);
        pipeline.SetScreenshot(screen, queued);

        // the old blur finishing must not touch the replacement state at all - no result stored,
        // and no "in progress" flag cleared or queued work started while its own blur still runs
        blur.Release(new Size(1, 1));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        Assert.IsTrue(ScreenshotBlurPipelineTests.IsDisposed(blur.Outputs.Single()));
        Assert.IsFalse(pipeline.TryBorrow(screen, _ => Assert.Fail("an old blur's result should not reach the replacement state")));
        Assert.IsFalse(ScreenshotBlurPipelineTests.IsDisposed(queued));
        Assert.AreEqual(2, blur.Inputs.Count, "the queued screenshot should still be waiting for the replacement state's own blur");

        // the replacement state then carries on exactly as normal
        blur.ReleaseAll();
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        CollectionAssert.AreEqual(new[] { new Size(1, 1), new Size(2, 2), new Size(3, 3) }, blur.Inputs.ToList());

        Size? blurredSize = null;
        Assert.IsTrue(pipeline.TryBorrow(screen, blurred => blurredSize = blurred.Size));
        Assert.AreEqual(new Size(3, 3), blurredSize);
    }

    private static ScreenInfo CreateScreenInfo(nint handle)
        => new(handle, primary: false, displayArea: RectangleInfo.Empty, workingArea: null);

    private static Bitmap CreateDisposedBitmap()
    {
        var bitmap = new Bitmap(4, 4);
        bitmap.Dispose();
        return bitmap;
    }

    /// <summary>
    /// Counts <see cref="ScreenshotBlurPipeline.BlurCompleted"/> events, so a test can await each
    /// background blur finishing in turn - subscribe *before* calling SetScreenshot so the event
    /// can't fire before anything is listening.
    /// </summary>
    private static SemaphoreSlim TrackCompletions(ScreenshotBlurPipeline pipeline)
    {
        var completions = new SemaphoreSlim(0);
        pipeline.BlurCompleted += _ => completions.Release();
        return completions;
    }

    private static async Task WaitForCompletionAsync(SemaphoreSlim completions)
    {
        Assert.IsTrue(
            await completions.WaitAsync(ScreenshotBlurPipelineTests.CompletionTimeout),
            "timed out waiting for a background blur to complete");
    }

    /// <summary>
    /// A disposed GDI+ bitmap throws on any property access.
    /// </summary>
    private static bool IsDisposed(Bitmap bitmap)
    {
        try
        {
            _ = bitmap.Width;
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    /// <summary>
    /// A stand-in for the real blur that blocks every call until it's released - either all at
    /// once (<see cref="ReleaseAll"/>) or one input at a time by its size (<see cref="Release"/>) -
    /// so a test can hold a blur "in progress" for as long as it needs. Records each input's size
    /// (the input is disposed as soon as the blur returns) and each output bitmap it returns.
    /// </summary>
    private sealed class GatedBlur
    {
        private readonly object sync = new();
        private readonly List<Size> inputs = [];
        private readonly List<Bitmap> outputs = [];
        private readonly HashSet<Size> releasedSizes = [];
        private bool released;

        public IReadOnlyList<Size> Inputs
        {
            get
            {
                lock (this.sync)
                {
                    return this.inputs.ToList();
                }
            }
        }

        public IReadOnlyList<Bitmap> Outputs
        {
            get
            {
                lock (this.sync)
                {
                    return this.outputs.ToList();
                }
            }
        }

        public Bitmap Blur(Bitmap source)
        {
            lock (this.sync)
            {
                this.inputs.Add(source.Size);
                Monitor.PulseAll(this.sync);

                var deadline = DateTime.UtcNow + ScreenshotBlurPipelineTests.CompletionTimeout;
                while (!this.released && !this.releasedSizes.Contains(source.Size))
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if ((remaining <= TimeSpan.Zero) || !Monitor.Wait(this.sync, remaining))
                    {
                        throw new TimeoutException("blur was never released");
                    }
                }

                var output = new Bitmap(source.Width, source.Height);
                this.outputs.Add(output);
                return output;
            }
        }

        public void ReleaseAll()
        {
            lock (this.sync)
            {
                this.released = true;
                Monitor.PulseAll(this.sync);
            }
        }

        /// <summary>
        /// Releases only the blur (or blurs) whose input is <paramref name="size"/>.
        /// </summary>
        public void Release(Size size)
        {
            lock (this.sync)
            {
                this.releasedSizes.Add(size);
                Monitor.PulseAll(this.sync);
            }
        }

        /// <summary>
        /// Blocks until at least <paramref name="count"/> blurs have started - blurs run on the
        /// thread pool, so this is the only way to know which one has actually begun.
        /// </summary>
        public void WaitForInputs(int count)
        {
            lock (this.sync)
            {
                var deadline = DateTime.UtcNow + ScreenshotBlurPipelineTests.CompletionTimeout;
                while (this.inputs.Count < count)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if ((remaining <= TimeSpan.Zero) || !Monitor.Wait(this.sync, remaining))
                    {
                        Assert.Fail("timed out waiting for a blur to start");
                    }
                }
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
            => this.utcNow;

        public void Advance(TimeSpan delta)
            => this.utcNow += delta;
    }
}
