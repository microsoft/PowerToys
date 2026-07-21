// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class VcpFeatureProbeServiceTests
{
    private const int InvalidCommand = unchecked((int)0xC0262589);
    private const int VcpNotSupported = unchecked((int)0xC0262584);

    [TestMethod]
    public async Task ProbeAsync_FirstSuccessReturnsValuesWithoutRetry()
    {
        var reader = new QueueReader(VcpReadAttempt.Success(current: 30, maximum: 100));
        var delays = new List<TimeSpan>();
        var service = CreateService(reader, delays);

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(1, reader.CallCount);
        Assert.IsTrue(result[0x10].IsSuccess);
        Assert.AreEqual(30, result[0x10].Value.Current);
        Assert.AreEqual(100, result[0x10].Value.Maximum);
        Assert.AreEqual(1, result[0x10].Attempts);
        Assert.IsNull(result[0x10].LastError);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(100) }, delays);
    }

    [TestMethod]
    public async Task ProbeAsync_TransientFailureThenSuccessRetriesWithPacing()
    {
        var reader = new QueueReader(
            VcpReadAttempt.Failure(InvalidCommand),
            VcpReadAttempt.Success(current: 45, maximum: 100));
        var delays = new List<TimeSpan>();
        var service = CreateService(reader, delays);

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(2, reader.CallCount);
        Assert.IsTrue(result[0x10].IsSuccess);
        Assert.AreEqual(2, result[0x10].Attempts);
        Assert.AreEqual(InvalidCommand, result[0x10].LastError);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100) },
            delays);
    }

    [TestMethod]
    public async Task ProbeAsync_ThreeTransientFailuresReturnIndeterminate()
    {
        var reader = new QueueReader(
            VcpReadAttempt.Failure(InvalidCommand),
            VcpReadAttempt.Failure(InvalidCommand),
            VcpReadAttempt.Failure(InvalidCommand));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(3, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.AreEqual(3, result[0x10].Attempts);
        Assert.AreEqual(InvalidCommand, result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_NonTransientFailureDoesNotRetry()
    {
        var reader = new QueueReader(VcpReadAttempt.Failure(VcpNotSupported));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(1, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.AreEqual(1, result[0x10].Attempts);
        Assert.AreEqual(VcpNotSupported, result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_InvalidSuccessfulRangeRetriesAndRemainsIndeterminate()
    {
        var reader = new QueueReader(
            VcpReadAttempt.Success(current: 10, maximum: 0),
            VcpReadAttempt.Success(current: 10, maximum: 0),
            VcpReadAttempt.Success(current: 10, maximum: 0));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(3, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.AreEqual(3, result[0x10].Attempts);
        Assert.IsNull(result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_MultipleCodesRemainSequential()
    {
        var reader = new QueueReader(
            VcpReadAttempt.Success(10, 100),
            VcpReadAttempt.Success(20, 100),
            VcpReadAttempt.Success(30, 100));
        var service = CreateService(reader, new List<TimeSpan>(), new byte[] { 0x10, 0x12, 0x62 });

        await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x10, 0x12, 0x62 }, reader.Codes);
    }

    [TestMethod]
    public async Task ProbeAsync_PreCancelledTokenSkipsDelayAndNativeReads()
    {
        var reader = new QueueReader(VcpReadAttempt.Success(10, 100));
        var delays = new List<TimeSpan>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(reader, delays, new byte[] { 0x10 });

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => service.ProbeAsync(new IntPtr(1), cancellation.Token));

        Assert.AreEqual(0, delays.Count);
        Assert.AreEqual(0, reader.CallCount);
    }

    [TestMethod]
    public async Task ProbeAsync_CancellationDuringTransactionDelayStopsBeforeNativeRead()
    {
        var reader = new QueueReader(VcpReadAttempt.Success(10, 100));
        var delays = new List<TimeSpan>();
        var delayStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var service = new VcpFeatureProbeService(
            reader,
            async (delay, token) =>
            {
                delays.Add(delay);
                delayStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            new byte[] { 0x10 });

        var probeTask = service.ProbeAsync(new IntPtr(1), cancellation.Token);

        await delayStarted.Task;
        cancellation.Cancel();

        OperationCanceledException? exception = null;
        try
        {
            await probeTask;
        }
        catch (OperationCanceledException ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);

        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(100) }, delays);
        Assert.AreEqual(0, reader.CallCount);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ProbeAsync_ReaderRunsOffCallerThread()
    {
        using var releaseReader = new ManualResetEventSlim();
        var callerThreadId = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerThreadId = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocation = new TaskCompletionSource<Task<IReadOnlyDictionary<byte, VcpProbeObservation>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new CoordinatedReader(readerThreadId, releaseReader);
        var service = CreateService(reader, new List<TimeSpan>());
        var caller = new Thread(() =>
        {
            callerThreadId.TrySetResult(Environment.CurrentManagedThreadId);
            invocation.TrySetResult(service.ProbeAsync(new IntPtr(1), CancellationToken.None));
        });

        caller.Start();
        var callerId = await callerThreadId.Task;
        var readerId = await readerThreadId.Task;
        releaseReader.Set();

        Assert.IsTrue(caller.Join(TimeSpan.FromSeconds(1)));
        var result = await await invocation.Task;

        Assert.AreNotEqual(callerId, readerId);
        Assert.IsTrue(result[0x10].IsSuccess);
    }

    private static VcpFeatureProbeService CreateService(
        IVcpFeatureReader reader,
        List<TimeSpan> delays,
        IReadOnlyList<byte>? codes = null) =>
        new(
            reader,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            codes: codes ?? new byte[] { 0x10 });

    private sealed class QueueReader(params VcpReadAttempt[] attempts) : IVcpFeatureReader
    {
        private readonly Queue<VcpReadAttempt> _attempts = new(attempts);

        public int CallCount { get; private set; }

        public List<byte> Codes { get; } = new();

        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            CallCount++;
            Codes.Add(code);
            return _attempts.Dequeue();
        }
    }

    private sealed class CoordinatedReader(
        TaskCompletionSource<int> readerThreadId,
        ManualResetEventSlim releaseReader) : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            readerThreadId.TrySetResult(Environment.CurrentManagedThreadId);
            releaseReader.Wait();
            return VcpReadAttempt.Success(10, 100);
        }
    }
}
