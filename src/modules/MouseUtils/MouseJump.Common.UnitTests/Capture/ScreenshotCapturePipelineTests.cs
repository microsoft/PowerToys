// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Capture;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
using MouseJump.Models.Styles;

namespace MouseJump.Common.UnitTests.Capture;

[TestClass]
public sealed class ScreenshotCapturePipelineTests
{
    [TestMethod]
    public async Task SuccessfulCapturesAreAllPushedToTheSink()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
            ScreenshotCapturePipelineTests.CreateScreenLayout(2),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        using var bitmap1 = new Bitmap(1, 1);
        using var bitmap2 = new Bitmap(1, 1);
        var provider = new FakeScreenshotCaptureProvider(
            _ => Task.FromResult(bitmap1),
            _ => Task.FromResult(bitmap2));

        var sink = new FakeScreenshotCaptureSink();
        await using var pipeline = new ScreenshotCapturePipeline(sink);

        pipeline.AddCaptureTasks(deviceLayout, provider);
        await pipeline.WaitForCompletionAsync();

        Assert.AreEqual(2, sink.Received.Count);
        CollectionAssert.AreEquivalent(
            screenLayouts,
            sink.Received.Select(entry => entry.ScreenLayout).ToList());
        CollectionAssert.AreEquivalent(
            new Bitmap[] { bitmap1, bitmap2 },
            sink.Received.Select(entry => entry.Bitmap).ToList());
    }

    [TestMethod]
    public async Task AGenuineCaptureFailureIsThrownAsAnAggregateException()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
            ScreenshotCapturePipelineTests.CreateScreenLayout(2),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        using var bitmap1 = new Bitmap(1, 1);
        var provider = new FakeScreenshotCaptureProvider(
            _ => Task.FromResult(bitmap1),
            _ => Task.FromException<Bitmap>(new InvalidOperationException("capture failed")));

        var sink = new FakeScreenshotCaptureSink();
        await using var pipeline = new ScreenshotCapturePipeline(sink);

        pipeline.AddCaptureTasks(deviceLayout, provider);

        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(
            () => pipeline.WaitForCompletionAsync());
        Assert.AreEqual(1, exception.InnerExceptions.Count);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerExceptions[0].InnerException);

        // the screen that failed should never have reached the sink - only the one that succeeded
        Assert.AreEqual(1, sink.Received.Count);
        Assert.AreEqual(screenLayouts[0], sink.Received[0].ScreenLayout);
    }

    [TestMethod]
    public async Task ACancelledCaptureIsNotTreatedAsAFailure()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
            ScreenshotCapturePipelineTests.CreateScreenLayout(2),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        using var bitmap1 = new Bitmap(1, 1);
        var provider = new FakeScreenshotCaptureProvider(
            _ => Task.FromResult(bitmap1),
            _ => Task.FromCanceled<Bitmap>(new CancellationToken(canceled: true)));

        var sink = new FakeScreenshotCaptureSink();
        await using var pipeline = new ScreenshotCapturePipeline(sink);

        pipeline.AddCaptureTasks(deviceLayout, provider);

        // should complete without throwing - a cancelled capture is an expected outcome
        // (e.g. a newer activation superseded this one), not a failure
        await pipeline.WaitForCompletionAsync();

        Assert.AreEqual(1, sink.Received.Count);
        Assert.AreEqual(screenLayouts[0], sink.Received[0].ScreenLayout);
    }

    [TestMethod]
    public async Task DisposeAsyncDisposesProvidersEvenAfterAFailure()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        var provider = new FakeScreenshotCaptureProvider(
            _ => Task.FromException<Bitmap>(new InvalidOperationException("capture failed")));

        var sink = new FakeScreenshotCaptureSink();
        var pipeline = new ScreenshotCapturePipeline(sink);

        pipeline.AddCaptureTasks(deviceLayout, provider);

        // DisposeAsync's own job is just to release providers - it shouldn't throw or leak the
        // provider even though the capture behind it failed
        await pipeline.DisposeAsync();

        Assert.IsTrue(provider.Disposed);
    }

    [TestMethod]
    public async Task CapturesArePushedToTheSinkInCompletionOrderNotScreenOrder()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
            ScreenshotCapturePipelineTests.CreateScreenLayout(2),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        var capture1 = new TaskCompletionSource<Bitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture2 = new TaskCompletionSource<Bitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeScreenshotCaptureProvider(
            _ => capture1.Task,
            _ => capture2.Task);

        var sink = new FakeScreenshotCaptureSink();
        await using var pipeline = new ScreenshotCapturePipeline(sink);

        var captureTasks = pipeline.AddCaptureTasks(deviceLayout, provider);

        // screen 2 finishes first - it should reach the sink straight away, without waiting
        // for screen 1 (a slow screen mustn't hold up the others' previews)
        using var bitmap2 = new Bitmap(1, 1);
        capture2.SetResult(bitmap2);
        sink.WaitForCount(1);
        Assert.AreEqual(screenLayouts[1], sink.Received[0].ScreenLayout);
        Assert.IsFalse(captureTasks[0].CaptureTask.IsCompleted);

        using var bitmap1 = new Bitmap(1, 1);
        capture1.SetResult(bitmap1);
        await pipeline.WaitForCompletionAsync();

        CollectionAssert.AreEqual(
            new[] { screenLayouts[1], screenLayouts[0] },
            sink.Received.Select(entry => entry.ScreenLayout).ToList());
    }

    [TestMethod]
    public async Task ThePipelineNeverDisposesABitmapItHasHandedToTheSink()
    {
        var screenLayouts = new[]
        {
            ScreenshotCapturePipelineTests.CreateScreenLayout(1),
        };
        var deviceLayout = ScreenshotCapturePipelineTests.CreateDeviceLayout(screenLayouts);

        using var bitmap = new Bitmap(3, 2);
        var provider = new FakeScreenshotCaptureProvider(
            _ => Task.FromResult(bitmap));

        var sink = new FakeScreenshotCaptureSink();
        var pipeline = new ScreenshotCapturePipeline(sink);

        pipeline.AddCaptureTasks(deviceLayout, provider);
        await pipeline.DisposeAsync();

        // ownership transferred to the sink (see IScreenshotCaptureSink.SetScreenshotAsync), so
        // the bitmap must still be usable even after the pipeline itself has been disposed - a
        // disposed GDI+ bitmap throws on any property access
        Assert.AreSame(bitmap, sink.Received.Single().Bitmap);
        Assert.AreEqual(3, bitmap.Width);
    }

    private static ScreenLayout CreateScreenLayout(nint handle)
        => new(
            screenInfo: new ScreenInfo(handle, primary: false, displayArea: RectangleInfo.Empty, workingArea: null),
            screenBounds: BoxBounds.Empty,
            screenStyle: BoxStyle.Empty);

    private static DeviceLayout CreateDeviceLayout(IEnumerable<ScreenLayout> screenLayouts)
    {
        var layouts = screenLayouts.ToList();
        return new(
            deviceInfo: new DeviceInfo(hostname: "localhost", localhost: true, screens: layouts.Select(layout => layout.ScreenInfo)),
            deviceBounds: BoxBounds.Empty,
            deviceStyle: BoxStyle.Empty,
            screenLayouts: layouts);
    }

    private sealed class FakeScreenshotCaptureProvider : IScreenshotCaptureProvider, IDisposable
    {
        private readonly Queue<Func<CancellationToken, Task<Bitmap>>> results;

        public FakeScreenshotCaptureProvider(params Func<CancellationToken, Task<Bitmap>>[] results)
        {
            this.results = new(results);
        }

        public bool Disposed
        {
            get;
            private set;
        }

        public Task<Bitmap> CaptureAsync(
            RectangleInfo sourceArea, SizeInfo thumbnailSize, CancellationToken cancellationToken = default)
            => this.results.Dequeue()(cancellationToken);

        public void Dispose()
            => this.Disposed = true;
    }

    private sealed class FakeScreenshotCaptureSink : IScreenshotCaptureSink
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        private readonly List<(ScreenLayout ScreenLayout, Bitmap Bitmap)> received = new();

        /// <summary>
        /// Gets a snapshot of everything received so far, in arrival order - captures can
        /// complete (and push here) on background threads, so this copies under the lock.
        /// </summary>
        public List<(ScreenLayout ScreenLayout, Bitmap Bitmap)> Received
        {
            get
            {
                lock (this.received)
                {
                    return this.received.ToList();
                }
            }
        }

        public Task SetScreenshotAsync(ScreenLayout screenLayout, Bitmap bitmap)
        {
            lock (this.received)
            {
                this.received.Add((screenLayout, bitmap));
                Monitor.PulseAll(this.received);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Blocks until at least <paramref name="count"/> screenshots have been received in total.
        /// </summary>
        public void WaitForCount(int count)
        {
            var deadline = DateTime.UtcNow + FakeScreenshotCaptureSink.WaitTimeout;
            lock (this.received)
            {
                while (this.received.Count < count)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if ((remaining <= TimeSpan.Zero) || !Monitor.Wait(this.received, remaining))
                    {
                        Assert.Fail("timed out waiting for the sink to receive a screenshot");
                    }
                }
            }
        }
    }
}
