// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class CpuSamplerTests
{
    private static readonly bool[] ExpectedProcessRequests = [false, true, false];

    [TestMethod]
    public void MultipleViews_ShareOneMeasurementPerSecondAndTheSameSnapshot()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        List<CpuSnapshot> firstSnapshots = [];
        List<CpuSnapshot> secondSnapshots = [];
        using var first = new DataManager(DataType.CPU, () => firstSnapshots.Add(sampler.Snapshot), sampler);
        using var second = new DataManager(DataType.CPU, () => secondSnapshots.Add(sampler.Snapshot), sampler);
        Assert.HasCount(0, clock.Timers);

        first.Start();
        first.Start();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        second.Start();
        second.Start();
        clock.Advance(TimeSpan.FromMilliseconds(750));

        Assert.AreEqual(1, source.Baselines);
        Assert.AreEqual(1, source.Reads);
        Assert.HasCount(1, clock.Timers);
        Assert.HasCount(1, firstSnapshots);
        Assert.HasCount(1, secondSnapshots);
        Assert.AreSame(firstSnapshots[0], secondSnapshots[0]);
        Assert.AreSame(first.GetCpuSnapshot(), second.GetCpuSnapshot());
        Assert.HasCount(2, sampler.Snapshot.History);
        Assert.AreEqual(sampler.Snapshot.History[0].GetTimestamp(), sampler.Snapshot.History[1].GetTimestamp());

        clock.Advance(TimeSpan.FromSeconds(1));
        first.Stop();
        first.Stop();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(3, source.Reads);
        Assert.HasCount(2, firstSnapshots);
        Assert.HasCount(3, secondSnapshots);
        Assert.HasCount(6, sampler.Snapshot.History);
        Assert.IsTrue(source.Intervals.All(interval => interval == TimeSpan.FromSeconds(1)));

        second.Dispose();
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(3, source.Reads);
        Assert.IsTrue(clock.Timers[0].IsDisposed);
    }

    [TestMethod]
    public void Restart_PrimesTheCounterWithoutRecordingTheInactiveInterval()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        using var view = new DataManager(DataType.CPU, () => { }, sampler);
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        var previous = sampler.Snapshot;
        var oldTimer = clock.Timers[0];

        view.Stop();
        clock.Advance(TimeSpan.FromSeconds(10));
        view.Start();
        clock.Advance(TimeSpan.Zero);
        oldTimer.Fire(); // Simulate an old callback already queued before disposal.
        Assert.AreEqual(2, source.Baselines);
        Assert.AreEqual(1, source.Reads);
        Assert.AreSame(previous, sampler.Snapshot);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, source.Reads);
        Assert.AreEqual(TimeSpan.FromSeconds(1), source.Intervals[^1]);
        Assert.HasCount(4, sampler.Snapshot.History);
        Assert.AreEqual(TimeSpan.FromSeconds(11), sampler.Snapshot.History[2].GetTimestamp() - sampler.Snapshot.History[0].GetTimestamp());
    }

    [TestMethod]
    public void TopProcesses_AreCollectedOnlyWhileARequestingViewIsActive()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        using var usage = new DataManager(DataType.CPU, () => { }, sampler);
        using var processes = new DataManager(DataType.CpuWithTopProcesses, () => { }, sampler);
        usage.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Stop();
        clock.Advance(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(ExpectedProcessRequests, source.ProcessRequests);
        Assert.AreEqual(1, source.Baselines);
    }

    [TestMethod]
    public void AStoppedObserver_IsSkippedDuringPublication()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        var secondUpdates = 0;
        using var second = new DataManager(DataType.CPU, () => secondUpdates++, sampler);
        using var first = new DataManager(DataType.CPU, second.Stop, sampler);
        first.Start();
        second.Start();
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(2, source.Reads);
        Assert.AreEqual(0, secondUpdates);
    }

    [TestMethod]
    public void AFailingObserver_DoesNotStopOtherViewsOrTheSampler()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        List<Exception> errors = [];
        using var sampler = new CpuSampler(() => source, clock, errors.Add);
        var updates = 0;
        using var failed = new DataManager(DataType.CPU, () => throw new InvalidOperationException("View failed"), sampler);
        using var healthy = new DataManager(DataType.CPU, () => updates++, sampler);
        failed.Start();
        healthy.Start();
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.AreEqual(3, source.Reads);
        Assert.AreEqual(3, updates);
        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public void AReadFailure_PrimesAgainBeforePublishingAnotherMeasurement()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock) { FailNextRead = true };
        List<Exception> errors = [];
        using var sampler = new CpuSampler(() => source, clock, errors.Add);
        var updates = 0;
        using var view = new DataManager(DataType.CPU, () => updates++, sampler);
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(2, source.Baselines);
        Assert.AreEqual(0, updates);
        Assert.IsEmpty(sampler.Snapshot.History);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, source.Reads);
        Assert.AreEqual(1, updates);
        Assert.HasCount(1, errors);
        Assert.HasCount(2, sampler.Snapshot.History);
        Assert.AreEqual(TimeSpan.FromSeconds(1), source.Intervals[^1]);
    }

    [TestMethod]
    public async Task OverlappingTicks_AreSkippedAndStoppedReadsDoNotEnterHistory()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        var updates = 0;
        using var view = new DataManager(DataType.CPU, () => updates++, sampler);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        source.OnRead = () =>
        {
            entered.Set();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
        };
        view.Start();
        clock.Advance(TimeSpan.Zero);
        var worker = Task.Run(() => clock.Advance(TimeSpan.FromSeconds(1)));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            clock.Timers[0].Fire();
            Assert.AreEqual(1, source.Reads);
            view.Stop();
        }
        finally
        {
            release.Set();
        }

        await worker;
        Assert.AreEqual(0, updates);
        Assert.IsEmpty(sampler.Snapshot.History);
        source.OnRead = null;
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Timers[0].Fire();
        Assert.AreEqual(2, source.Reads);
        Assert.AreEqual(1, updates);
        Assert.HasCount(2, sampler.Snapshot.History);
    }

    [TestMethod]
    public void DisposedSampler_StopsItsTimerAndRejectsNewSubscriptions()
    {
        var clock = new ManualTimeProvider();
        var source = new TestSource(clock);
        using var sampler = new CpuSampler(() => source, clock);
        using var view = new DataManager(DataType.CPU, () => { }, sampler);
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        sampler.Dispose();
        clock.Advance(TimeSpan.FromSeconds(5));
        clock.Timers[0].Fire();

        Assert.AreEqual(1, source.Reads);
        Assert.IsTrue(clock.Timers[0].IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = sampler.Subscribe(() => { }));
    }

    private sealed class TestSource(TimeProvider clock) : ICpuSampleSource
    {
        private long _lastRead;

        public int Baselines { get; private set; }

        public int Reads { get; private set; }

        public bool FailNextRead { get; set; }

        public Action? OnRead { get; set; }

        public List<TimeSpan> Intervals { get; } = [];

        public List<bool> ProcessRequests { get; } = [];

        public void ResetSamplingInterval()
        {
            Baselines++;
            _lastRead = clock.GetTimestamp();
        }

        public CpuSample Sample(bool includeTopProcesses)
        {
            Reads++;
            OnRead?.Invoke();
            if (FailNextRead)
            {
                FailNextRead = false;
                throw new InvalidOperationException("Counter read failed");
            }

            Intervals.Add(clock.GetElapsedTime(_lastRead));
            _lastRead = clock.GetTimestamp();
            ProcessRequests.Add(includeTopProcesses);
            return new CpuSample(Reads / 10f, Reads / 20f, 3200);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;

        public List<ManualTimer> Timers { get; } = [];

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            Timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            var end = _ticks + amount.Ticks;
            while (Timers.Where(timer => !timer.IsDisposed && timer.Due <= end).OrderBy(timer => timer.Due).FirstOrDefault() is { } next)
            {
                _ticks = next.Due;
                next.Due = next.Period == Timeout.InfiniteTimeSpan ? long.MaxValue : _ticks + next.Period.Ticks;
                next.Fire();
            }

            _ticks = end;
        }
    }

    private sealed partial class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public long Due { get; set; }

        public TimeSpan Period { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Period = period;
            Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + dueTime.Ticks;
            return !IsDisposed;
        }

        public void Fire() => callback(state);

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
