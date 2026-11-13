// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.UI.Events;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class HeartbeatTelemetry : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly Func<int> _getLoadedExtensionCount;
    private readonly Action<CmdPalHeartbeat> _logHeartbeat;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _initialUptime;
    private readonly long _startTimestamp;
    private readonly ITimer _timer;
    private readonly Lock _lock = new();
    private bool _disposed;
    private int _builtInsLoaded;
    private int _externalExtensionsLoaded;

    public HeartbeatTelemetry(Func<int> getLoadedExtensionCount, Action<CmdPalHeartbeat> logHeartbeat, TimeProvider? timeProvider = null)
    {
        _getLoadedExtensionCount = getLoadedExtensionCount;
        _logHeartbeat = logHeartbeat;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startTimestamp = _timeProvider.GetTimestamp();
        using var process = Process.GetCurrentProcess();
        var processUptime = _timeProvider.GetUtcNow() - process.StartTime.ToUniversalTime();
        _initialUptime = processUptime > TimeSpan.Zero ? processUptime : TimeSpan.Zero;

        // Align periodic heartbeats to process startup, not extension loading or service creation.
        var dueTime = Interval - TimeSpan.FromTicks(_initialUptime.Ticks % Interval.Ticks);
        _timer = _timeProvider.CreateTimer(_ => Emit("Periodic"), null, dueTime, Interval);
    }

    public void BuiltInExtensionsLoaded()
    {
        if (Interlocked.Exchange(ref _builtInsLoaded, 1) == 0)
        {
            Emit("BuiltInExtensionsLoaded");
        }
    }

    public void ExternalExtensionsLoaded()
    {
        if (Interlocked.Exchange(ref _externalExtensionsLoaded, 1) == 0)
        {
            Emit("ExternalExtensionsLoaded");
        }
    }

    private void Emit(string trigger)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                var uptime = _initialUptime + _timeProvider.GetElapsedTime(_startTimestamp);
                _logHeartbeat(new CmdPalHeartbeat(
                    trigger,
                    (ulong)uptime.TotalMilliseconds,
                    _getLoadedExtensionCount(),
                    process.WorkingSet64,
                    process.PrivateMemorySize64,
                    GC.GetTotalMemory(forceFullCollection: false)));
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to emit Command Palette heartbeat telemetry", ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }
}
