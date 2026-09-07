// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class VcpFeatureProbeServiceTests
{
    [TestMethod]
    public async Task ProbeAsync_FirstSuccessReturnsValuesWithoutRetry()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(current: 30, maximum: 100));
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
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand),
            VcpReadAttempt.Success(current: 45, maximum: 100));
        var delays = new List<TimeSpan>();
        var service = CreateService(reader, delays);

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(2, reader.CallCount);
        Assert.IsTrue(result[0x10].IsSuccess);
        Assert.AreEqual(2, result[0x10].Attempts);
        Assert.AreEqual(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand, result[0x10].LastError);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100) },
            delays);
    }

    [TestMethod]
    public async Task ProbeAsync_ThreeTransientFailuresReturnIndeterminate()
    {
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand),
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand),
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(3, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.AreEqual(3, result[0x10].Attempts);
        Assert.AreEqual(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand, result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_NonTransientFailureDoesNotRetry()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(1, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.IsFalse(result[0x10].Replied);
        Assert.AreEqual(1, result[0x10].Attempts);
        Assert.AreEqual(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported, result[0x10].LastError);
    }

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public async Task ProbeAsync_PhysicalMonitorUnavailableStopsRemainingFeatureProbes(int errorCode)
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(errorCode));
        var service = CreateService(reader, new List<TimeSpan>(), new byte[] { 0x10, 0x12, 0x62 });

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(1, reader.CallCount);
        CollectionAssert.AreEqual(new byte[] { 0x10 }, reader.Codes);
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0x10].IsPhysicalMonitorUnavailable);
        Assert.AreEqual(errorCode, result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_VcpNotSupportedContinuesWithRemainingFeatures()
    {
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            VcpReadAttempt.Success(current: 20, maximum: 100));
        var service = CreateService(reader, new List<TimeSpan>(), new byte[] { 0x10, 0x12 });

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(2, reader.CallCount);
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x12 }, reader.Codes);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.IsFalse(result[0x10].IsPhysicalMonitorUnavailable);
        Assert.IsTrue(result[0x12].IsSuccess);
    }

    [TestMethod]
    public async Task ProbeAsync_InvalidSuccessfulRangeStopsWithoutRetry()
    {
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Success(current: 10, maximum: 0),
            VcpReadAttempt.Success(current: 10, maximum: 0));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        // The reply already settles the only question this probe asks, so the remaining budget is
        // not spent on the I2C bus.
        Assert.AreEqual(1, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.AreEqual(1, result[0x10].Attempts);
        Assert.IsNull(result[0x10].LastError);

        // The device answered, so support is proven even though no usable range was obtained.
        Assert.IsTrue(result[0x10].Replied);
    }

    [TestMethod]
    public async Task ProbeAsync_TransientFailureThenInvalidRangeKeepsLastError()
    {
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageChecksum),
            VcpReadAttempt.Success(current: 10, maximum: 0));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(2, reader.CallCount);
        Assert.AreEqual(2, result[0x10].Attempts);
        Assert.IsTrue(result[0x10].Replied);
        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.IsFalse(result[0x10].IsPhysicalMonitorUnavailable);

        // Returning early on the reply must not discard the error the earlier attempt reported.
        Assert.AreEqual(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageChecksum, result[0x10].LastError);
    }

    [TestMethod]
    public async Task ProbeAsync_ThrowingReadIsContainedToItsOwnCode()
    {
        // A throwing native read must cost the caller this one VCP code, not every monitor sharing
        // the hMonitor through the pipeline-wide catch in DdcCiController.
        var reader = new ThrowingReader(0x10, VcpReadAttempt.Success(current: 40, maximum: 100));
        var service = CreateService(reader, new List<TimeSpan>(), new byte[] { 0x10, 0x12 });

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.IsFalse(result[0x10].IsSuccess);
        Assert.IsFalse(result[0x10].IsPhysicalMonitorUnavailable);
        Assert.IsFalse(result[0x10].Replied);
        Assert.IsTrue(result[0x12].IsSuccess);
        Assert.AreEqual(40, result[0x12].Value.Current);
    }

    [TestMethod]
    public async Task ProbeAsync_MultipleCodesRemainSequential()
    {
        var reader = new RecordingVcpReader(
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
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(10, 100));
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
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(10, 100));
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

    private sealed class ThrowingReader(byte throwingCode, VcpReadAttempt otherwise) : IVcpFeatureReader
    {
        public VcpReadAttempt Read(IntPtr handle, byte code) => code == throwingCode
            ? throw new InvalidOperationException("simulated native failure")
            : otherwise;
    }
}
