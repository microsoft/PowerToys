// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class HeartbeatTelemetryTests
{
    [TestMethod]
    [DataRow(0, 30)]
    [DataRow(5, 25)]
    [DataRow(30, 30)]
    [DataRow(35, 25)]
    public void PeriodicHeartbeatIsAlignedToProcessStart(int initialUptimeMinutes, int expectedDelayMinutes)
    {
        var clock = new TestTimeProvider(TimeSpan.FromMinutes(initialUptimeMinutes));
        var events = new List<CmdPalHeartbeat>();
        using var telemetry = new HeartbeatTelemetry(() => 3, events.Add, clock);

        Assert.IsEmpty(events);
        Assert.AreEqual(TimeSpan.FromMinutes(expectedDelayMinutes), clock.DueTime);
        Assert.AreEqual(TimeSpan.FromMinutes(30), clock.Period);

        clock.Advance(TimeSpan.FromMinutes(expectedDelayMinutes));
        clock.Fire();
        clock.Advance(TimeSpan.FromMinutes(30));
        clock.Fire();

        Assert.HasCount(2, events);
        Assert.AreEqual("Periodic", events[0].Trigger);
        Assert.AreEqual((ulong)TimeSpan.FromMinutes(initialUptimeMinutes + expectedDelayMinutes).TotalMilliseconds, events[0].UptimeMs);
        Assert.AreEqual((ulong)TimeSpan.FromMinutes(initialUptimeMinutes + expectedDelayMinutes + 30).TotalMilliseconds, events[1].UptimeMs);
    }

    [TestMethod]
    public void LoadingMilestonesEmitOnceWithCurrentMetricsWithoutResettingTimer()
    {
        var clock = new TestTimeProvider(TimeSpan.FromMinutes(2));
        var events = new List<CmdPalHeartbeat>();
        var extensionCount = 18;
        using var telemetry = new HeartbeatTelemetry(() => extensionCount, events.Add, clock);

        telemetry.BuiltInExtensionsLoaded();
        telemetry.BuiltInExtensionsLoaded();
        extensionCount = 21;
        clock.Advance(TimeSpan.FromMinutes(1));
        telemetry.ExternalExtensionsLoaded();
        telemetry.ExternalExtensionsLoaded();

        Assert.HasCount(2, events);
        Assert.AreEqual("BuiltInExtensionsLoaded", events[0].Trigger);
        Assert.AreEqual(18, events[0].LoadedExtensionCount);
        Assert.AreEqual(120_000UL, events[0].UptimeMs);
        Assert.AreEqual("ExternalExtensionsLoaded", events[1].Trigger);
        Assert.AreEqual(21, events[1].LoadedExtensionCount);
        Assert.AreEqual(180_000UL, events[1].UptimeMs);
        Assert.IsTrue(events.All(e => e.WorkingSetBytes > 0));
        Assert.IsTrue(events.All(e => e.PrivateBytes > 0));
        Assert.IsTrue(events.All(e => e.ManagedHeapBytes > 0));
        Assert.AreEqual(1, clock.TimerCreationCount);
        clock.Timer.Verify(t => t.Change(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()), Times.Never);

        extensionCount = 20;
        clock.Advance(TimeSpan.FromMinutes(27));
        clock.Fire();

        Assert.HasCount(3, events);
        Assert.AreEqual(20, events[2].LoadedExtensionCount);
        Assert.AreEqual(1_800_000UL, events[2].UptimeMs);
        Assert.IsTrue(events[2].PrivateBytes > 0);
        Assert.IsTrue(events[2].ManagedHeapBytes > 0);
    }

    [TestMethod]
    public void DisposeStopsTimerAndIgnoresQueuedCallbacksAndMilestones()
    {
        var clock = new TestTimeProvider(TimeSpan.Zero);
        var events = new List<CmdPalHeartbeat>();
        var telemetry = new HeartbeatTelemetry(() => 0, events.Add, clock);

        telemetry.Dispose();
        clock.Fire();
        telemetry.BuiltInExtensionsLoaded();
        telemetry.ExternalExtensionsLoaded();

        Assert.IsEmpty(events);
        clock.Timer.Verify(t => t.Dispose(), Times.Once);
    }

    private sealed class TestTimeProvider(TimeSpan initialUptime) : TimeProvider
    {
        private long _timestamp;
        private TimerCallback? _callback;
        private object? _state;

        public Mock<ITimer> Timer { get; } = new();

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public int TimerCreationCount { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow()
        {
            using var process = Process.GetCurrentProcess();
            return new DateTimeOffset(process.StartTime.ToUniversalTime()) + initialUptime;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
            Period = period;
            TimerCreationCount++;
            return Timer.Object;
        }

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;

        public void Fire() => _callback!(_state);
    }
}
