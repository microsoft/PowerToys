// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class ClipboardHistoryReaderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompletedCopiesAreReleasedWithoutStartingAnotherRead(bool timesOut)
    {
        WeakReference history = ReadAndReleaseCopies(timesOut);
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    return !history.IsAlive;
                },
                TimeSpan.FromSeconds(5)),
            "A completed read must not keep the copies alive, including one that finishes after its caller timed out.");

        // Only after collection: starting this sooner could hide retention by replacing the previous task.
        var fresh = new ClipboardHistory(ClipboardHistoryState.Read, ["new copy"]);
        Assert.AreSame(fresh, ClipboardHistoryReader.Read(static () => true, () => Task.FromResult(fresh), TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void AFailedReadDoesNotBlockTheNextRead()
    {
        Assert.AreSame(
            ClipboardHistory.NotRead,
            ClipboardHistoryReader.Read(static () => true, static () => Task.FromException<ClipboardHistory>(new InvalidOperationException()), TimeSpan.FromSeconds(5)));

        var fresh = new ClipboardHistory(ClipboardHistoryState.Read, ["new copy"]);
        Assert.AreSame(fresh, ClipboardHistoryReader.Read(static () => true, () => Task.FromResult(fresh), TimeSpan.FromSeconds(5)));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ReadAndReleaseCopies(bool timesOut)
    {
        var history = new ClipboardHistory(ClipboardHistoryState.Read, [new string('x', 1024)]);
        var weak = new WeakReference(history);
        if (!timesOut)
        {
            Assert.AreSame(history, ClipboardHistoryReader.Read(static () => true, () => Task.FromResult(history), TimeSpan.FromSeconds(5)));
            return weak;
        }

        using var started = new ManualResetEventSlim();
        var completion = new TaskCompletionSource<ClipboardHistory>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Assert.AreSame(
                ClipboardHistory.NotRead,
                ClipboardHistoryReader.Read(
                    static () => true,
                    () =>
                    {
                        started.Set();
                        return completion.Task;
                    },
                    TimeSpan.Zero));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)), "The read must start before its late completion is tested.");
        }
        finally
        {
            completion.TrySetResult(history);
        }

        return weak;
    }
}
