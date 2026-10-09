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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
            => this.utcNow;

        public void Advance(TimeSpan delta)
            => this.utcNow += delta;
    }
}
