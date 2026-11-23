// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Helpers;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class ClipboardWriteOperationTests
{
    private const int CannotOpenClipboard = unchecked((int)0x800401D0);

    [TestMethod]
    public async Task ExecuteAsync_FirstFlushSucceeds_WritesOnceWithoutDelay()
    {
        var calls = new CallRecorder();
        calls.OnFlush = () => Assert.AreEqual(1, calls.SetCalls);

        int attempts = await calls.ExecuteAsync();

        Assert.AreEqual(1, attempts);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(1, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoBusyFlushesThenSuccess_RetriesOnlyFlush()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new CallRecorder();
        calls.OnFlush = () =>
        {
            Assert.AreEqual(1, calls.SetCalls);
            if (calls.FlushCalls <= 2)
            {
                throw CreateComException("Clipboard is busy.", CannotOpenClipboard);
            }
        };

        int attempts = await calls.ExecuteAsync(cancellation.Token);

        Assert.AreEqual(3, attempts);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(3, calls.FlushCalls);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50) },
            calls.DelayIntervals);
        CollectionAssert.AreEqual(new[] { cancellation.Token, cancellation.Token }, calls.DelayTokens);
    }

    [TestMethod]
    public async Task ExecuteAsync_FiveBusyFlushes_RethrowsLastExceptionWithoutAnotherDelay()
    {
        var failures = Enumerable.Range(1, 5)
            .Select(attempt => CreateComException($"Busy attempt {attempt}.", CannotOpenClipboard))
            .ToArray();
        var calls = new CallRecorder();
        calls.OnFlush = () => throw failures[calls.FlushCalls - 1];

        COMException actual = await Assert.ThrowsExactlyAsync<COMException>(() => calls.ExecuteAsync());

        Assert.AreSame(failures[^1], actual);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(5, calls.FlushCalls);
        Assert.AreEqual(4, calls.DelayIntervals.Count);
        Assert.IsTrue(calls.DelayIntervals.All(interval => interval == TimeSpan.FromMilliseconds(50)));
    }

    [TestMethod]
    public async Task ExecuteAsync_OtherComFailure_DoesNotRetry()
    {
        var failure = CreateComException("Different COM failure.", unchecked((int)0x80004005));
        var calls = new CallRecorder { OnFlush = () => throw failure };

        COMException actual = await Assert.ThrowsExactlyAsync<COMException>(() => calls.ExecuteAsync());

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(1, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ManagedFlushFailure_DoesNotRetry()
    {
        var failure = new InvalidOperationException("Flush failed.");
        var calls = new CallRecorder { OnFlush = () => throw failure };

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => calls.ExecuteAsync());

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(1, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_SetContentBusyFailure_DoesNotFlushOrRetry()
    {
        var failure = CreateComException("SetContent is busy.", CannotOpenClipboard);
        var calls = new CallRecorder { OnSet = () => throw failure };

        COMException actual = await Assert.ThrowsExactlyAsync<COMException>(() => calls.ExecuteAsync());

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(0, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledBeforeStart_DoesNotWriteOrFlush()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = new CallRecorder();

        OperationCanceledException actual = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => calls.ExecuteAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        Assert.AreEqual(0, calls.SetCalls);
        Assert.AreEqual(0, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileWaiting_DoesNotAttemptAnotherFlush()
    {
        using var cancellation = new CancellationTokenSource();
        var pendingDelay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() => pendingDelay.TrySetCanceled(cancellation.Token));
        var calls = new CallRecorder
        {
            OnFlush = () => throw CreateComException("Clipboard is busy.", CannotOpenClipboard),
            OnDelay = (_, _) => pendingDelay.Task,
        };

        Task<int> operation = calls.ExecuteAsync(cancellation.Token);
        Assert.IsFalse(operation.IsCompleted);
        Assert.AreEqual(1, calls.DelayIntervals.Count);
        Assert.AreEqual(cancellation.Token, calls.DelayTokens[0]);
        cancellation.Cancel();

        // Awaiting the cancelled fake delay can propagate TaskCanceledException, a subtype.
        OperationCanceledException actual = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);

        Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(1, calls.FlushCalls);
        Assert.AreEqual(1, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_SetContentCancelsToken_DoesNotFlush()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new CallRecorder { OnSet = cancellation.Cancel };

        OperationCanceledException actual = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => calls.ExecuteAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(0, calls.FlushCalls);
        Assert.AreEqual(0, calls.DelayIntervals.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_DelayCompletesAfterCancellation_ChecksTokenBeforeRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = new CallRecorder
        {
            OnFlush = () => throw CreateComException("Clipboard is busy.", CannotOpenClipboard),
            OnDelay = (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            },
        };

        OperationCanceledException actual = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => calls.ExecuteAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        Assert.AreEqual(1, calls.SetCalls);
        Assert.AreEqual(1, calls.FlushCalls);
        Assert.AreEqual(1, calls.DelayIntervals.Count);
    }

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "Tests simulate COM exceptions and HRESULTs returned by the Windows clipboard API.")]
    private static COMException CreateComException(string message, int hresult)
        => new COMException(message, hresult);

    private sealed class CallRecorder
    {
        internal int SetCalls { get; private set; }

        internal int FlushCalls { get; private set; }

        internal List<TimeSpan> DelayIntervals { get; } = new();

        internal List<CancellationToken> DelayTokens { get; } = new();

        internal Action? OnSet { get; set; }

        internal Action? OnFlush { get; set; }

        internal Func<TimeSpan, CancellationToken, Task>? OnDelay { get; set; }

        internal Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
            => ClipboardWriteOperation.ExecuteAsync(SetContent, Flush, cancellationToken, Delay);

        private void SetContent()
        {
            SetCalls++;
            OnSet?.Invoke();
        }

        private void Flush()
        {
            FlushCalls++;
            OnFlush?.Invoke();
        }

        private Task Delay(TimeSpan interval, CancellationToken cancellationToken)
        {
            DelayIntervals.Add(interval);
            DelayTokens.Add(cancellationToken);
            return OnDelay?.Invoke(interval, cancellationToken) ?? Task.CompletedTask;
        }
    }
}
