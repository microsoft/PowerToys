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
    private readonly Dictionary<object, ClientRegistration> _clients = new(ReferenceEqualityComparer.Instance);
    private readonly System.Timers.Timer _timer = new() { AutoReset = false };
    private readonly Func<DateTime> _clock;
    private readonly bool _enableTimer;
    private EventHandler[] _secondUpdateHandlers = [];
    private EventHandler[] _minuteUpdateHandlers = [];
    private DateTime _lastDispatchedMinute;
    private bool _disposed;

    internal ClockUpdateService(Func<DateTime>? clock = null, bool enableTimer = true)
    {
        _clock = clock ?? (() => DateTime.Now);
        _enableTimer = enableTimer;
        _lastDispatchedMinute = GetMinute(_clock());
        _timer.Elapsed += Timer_Elapsed;
    }

    internal void Subscribe(object client, EventHandler handler, bool requiresSecondUpdates)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_clients.Count == 0)
            {
                _lastDispatchedMinute = GetMinute(_clock());
            }

            _clients[client] = new(handler, requiresSecondUpdates);
            RebuildHandlerSnapshots();
            ScheduleNextTick();
        }
    }

    internal void SetRequiresSecondUpdates(object client, bool requiresSeconds)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_clients.TryGetValue(client, out var registration))
            {
                throw new InvalidOperationException("The clock client must be subscribed before its update cadence can change.");
            }

            if (registration.RequiresSecondUpdates == requiresSeconds)
            {
                return;
            }

            _clients[client] = registration with { RequiresSecondUpdates = requiresSeconds };
            RebuildHandlerSnapshots();
            ScheduleNextTick();
        }
    }

    internal void Unsubscribe(object client)
    {
        lock (_lock)
        {
            if (_disposed || !_clients.Remove(client))
            {
                return;
            }

            RebuildHandlerSnapshots();
            ScheduleNextTick();
        }
    }

    internal void DispatchTick(DateTime now)
    {
        EventHandler[] secondUpdateHandlers;
        EventHandler[] minuteUpdateHandlers;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            secondUpdateHandlers = _secondUpdateHandlers;
            var currentMinute = GetMinute(now);
            if (currentMinute == _lastDispatchedMinute)
            {
                minuteUpdateHandlers = [];
            }
            else
            {
                _lastDispatchedMinute = currentMinute;
                minuteUpdateHandlers = _minuteUpdateHandlers;
            }
        }

        InvokeHandlers(secondUpdateHandlers);
        InvokeHandlers(minuteUpdateHandlers);
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            DispatchTick(_clock());
        }
        finally
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    ScheduleNextTick();
                }
            }
        }
    }

    private void ScheduleNextTick()
    {
        if (!_enableTimer || _clients.Count == 0)
        {
            _timer.Stop();
            return;
        }

        var now = _clock();
        var interval = _secondUpdateHandlers.Length > 0
            ? TimeSpan.FromMilliseconds(1000 - now.Millisecond)
            : TimeSpan.FromMinutes(1) - TimeSpan.FromSeconds(now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _timer.Stop();
        _timer.Interval = Math.Max(1, interval.TotalMilliseconds);
        _timer.Start();
    }

    private void RebuildHandlerSnapshots()
    {
        var secondUpdateHandlers = new List<EventHandler>();
        var minuteUpdateHandlers = new List<EventHandler>();
        foreach (var registration in _clients.Values)
        {
            (registration.RequiresSecondUpdates ? secondUpdateHandlers : minuteUpdateHandlers).Add(registration.Handler);
        }

        _secondUpdateHandlers = [.. secondUpdateHandlers];
        _minuteUpdateHandlers = [.. minuteUpdateHandlers];
    }

    private void InvokeHandlers(EventHandler[] handlers)
    {
        foreach (var handler in handlers)
        {
            handler(this, EventArgs.Empty);
        }
    }

    private static DateTime GetMinute(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _clients.Clear();
            _secondUpdateHandlers = [];
            _minuteUpdateHandlers = [];
            _timer.Stop();
        }

        _timer.Dispose();
    }

    private sealed record ClientRegistration(EventHandler Handler, bool RequiresSecondUpdates);
}
