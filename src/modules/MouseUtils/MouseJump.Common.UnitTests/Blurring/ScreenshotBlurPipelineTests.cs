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
    public async Task ACompletedBlurIsServedByTryGet()
    {
        var screen = ScreenshotBlurPipelineTests.CreateScreenInfo(1);
        var pipeline = new ScreenshotBlurPipeline();
        pipeline.SetActiveScreens([screen]);
        using var completions = ScreenshotBlurPipelineTests.TrackCompletions(pipeline);

        var screenshot = new Bitmap(8, 6);
        pipeline.SetScreenshot(screen, screenshot);
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);

        Size? blurredSize = null;
        Assert.IsTrue(pipeline.TryGet(screen, blurred =>
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
        Assert.IsFalse(pipeline.TryGet(screen, _ => Assert.Fail("a failed blur should not produce a result")));

        pipeline.SetScreenshot(screen, new Bitmap(4, 4));
        await ScreenshotBlurPipelineTests.WaitForCompletionAsync(completions);
        Assert.IsTrue(pipeline.TryGet(screen, _ => { }));
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
        Assert.IsTrue(pipeline.TryGet(screen, blurred => blurredSize = blurred.Size));
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
        Assert.IsTrue(pipeline.TryGet(screen, _ => { }));

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.IsFalse(pipeline.TryGet(screen, _ => Assert.Fail("an expired blur should not be served")));
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
        Assert.IsFalse(pipeline.TryGet(inactiveScreen, _ => Assert.Fail("an inactive screen should have nothing to serve")));
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
        Assert.IsTrue(pipeline.TryGet(screen, blurred => blurredSize = blurred.Size));
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
        Assert.IsTrue(pipeline.TryGet(screen, blurred => Assert.AreSame(outputs[1], blurred)));
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
        Assert.IsFalse(pipeline.TryGet(screen, _ => Assert.Fail("a removed screen's result should have been discarded")));
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
    /// A stand-in for the real blur that blocks every call until <see cref="ReleaseAll"/>, so a
    /// test can hold a blur "in progress" for as long as it needs. Records each input's size
    /// (the input is disposed as soon as the blur returns) and each output bitmap it returns.
    /// </summary>
    private sealed class GatedBlur
    {
        private readonly object sync = new();
        private readonly List<Size> inputs = [];
        private readonly List<Bitmap> outputs = [];
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

                var deadline = DateTime.UtcNow + ScreenshotBlurPipelineTests.CompletionTimeout;
                while (!this.released)
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
