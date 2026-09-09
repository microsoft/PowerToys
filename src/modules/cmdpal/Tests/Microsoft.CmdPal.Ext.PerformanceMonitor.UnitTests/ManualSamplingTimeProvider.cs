// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

internal sealed class ManualSamplingTimeProvider : TimeProvider
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

    public sealed partial class ManualTimer(ManualSamplingTimeProvider clock, TimerCallback callback, object? state) : ITimer
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
