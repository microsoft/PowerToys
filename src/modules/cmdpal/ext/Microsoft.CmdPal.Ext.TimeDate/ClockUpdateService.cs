// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CmdPal.Ext.TimeDate;

/// <summary>Provides a single, extension-wide cadence for live dock clocks.</summary>
internal sealed partial class ClockUpdateService : IDisposable
{
    private readonly Lock _lock = new();
    private readonly HashSet<object> _secondPrecisionClients = [];
    private readonly System.Timers.Timer _timer = new() { AutoReset = false };

    internal event EventHandler? Tick;

    internal ClockUpdateService()
    {
        _timer.Elapsed += Timer_Elapsed;
    }

    internal void SetRequiresSecondUpdates(object client, bool requiresSeconds)
    {
        lock (_lock)
        {
            if (requiresSeconds)
            {
                _secondPrecisionClients.Add(client);
            }
            else
            {
                _secondPrecisionClients.Remove(client);
            }

            ScheduleNextTick();
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Tick?.Invoke(this, EventArgs.Empty);
        lock (_lock)
        {
            ScheduleNextTick();
        }
    }

    private void ScheduleNextTick()
    {
        if (Tick is null)
        {
            _timer.Stop();
            return;
        }

        var now = DateTime.Now;
        var interval = _secondPrecisionClients.Count > 0
            ? TimeSpan.FromMilliseconds(1000 - now.Millisecond)
            : TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _timer.Stop();
        _timer.Interval = Math.Max(1, interval.TotalMilliseconds);
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
