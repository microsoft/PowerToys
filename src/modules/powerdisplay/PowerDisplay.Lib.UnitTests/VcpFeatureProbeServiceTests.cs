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
        Assert.AreEqual(InvalidCommand, result[0x10].ErrorCode);
    }

    [TestMethod]
    public async Task ProbeAsync_NonTransientFailureDoesNotRetry()
    {
        var reader = new QueueReader(VcpReadAttempt.Failure(VcpNotSupported));
        var service = CreateService(reader, new List<TimeSpan>());

        var result = await service.ProbeAsync(new IntPtr(1), CancellationToken.None);

        Assert.AreEqual(1, reader.CallCount);
        Assert.IsFalse(result[0x10].IsSuccess);
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
    public async Task ProbeAsync_CancellationBeforeFirstReadStopsNativeCalls()
    {
        var reader = new QueueReader(VcpReadAttempt.Success(10, 100));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new VcpFeatureProbeService(
            reader,
            (_, token) => Task.FromCanceled(token),
            new byte[] { 0x10 });

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => service.ProbeAsync(new IntPtr(1), cancellation.Token));

        Assert.AreEqual(0, reader.CallCount);
    }

    private static VcpFeatureProbeService CreateService(
        QueueReader reader,
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
}
