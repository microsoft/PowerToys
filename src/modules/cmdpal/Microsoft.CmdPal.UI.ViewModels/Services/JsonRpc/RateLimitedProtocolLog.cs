// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

/// <summary>
/// Bounds the volume of protocol-error logging so a peer that sends a flood of malformed or
/// undecodable frames cannot produce unbounded log output. Within each fixed time window at most a
/// configured number of log entries are emitted; further entries are counted and reported as a
/// single suppressed-count summary when the next window begins. The limiter itself holds only a
/// fixed set of counters, so it never grows with the number of errors it throttles.
/// </summary>
internal sealed class RateLimitedProtocolLog
{
    private readonly int _maxPerWindow;
    private readonly long _windowMs;
    private readonly Func<long> _nowMs;
    private readonly Action<long> _onSuppressedSummary;
    private readonly object _gate = new();

    private long _windowStartMs;
    private int _emittedInWindow;
    private long _suppressedInWindow;
    private long _totalSuppressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitedProtocolLog"/> class.
    /// </summary>
    /// <param name="maxPerWindow">The maximum number of log entries emitted within a single window. Values below one are clamped to one.</param>
    /// <param name="window">The length of each rate-limit window.</param>
    /// <param name="onSuppressedSummary">Invoked once per window (outside the internal lock) with the number of entries suppressed in the window that just ended, whenever that number is greater than zero.</param>
    /// <param name="nowMs">An optional monotonic millisecond clock used to measure windows. Defaults to <see cref="Environment.TickCount64"/>. Supplied by tests for determinism.</param>
    public RateLimitedProtocolLog(int maxPerWindow, TimeSpan window, Action<long> onSuppressedSummary, Func<long>? nowMs = null)
    {
        ArgumentNullException.ThrowIfNull(onSuppressedSummary);

        _maxPerWindow = Math.Max(1, maxPerWindow);
        _windowMs = Math.Max(1, (long)window.TotalMilliseconds);
        _onSuppressedSummary = onSuppressedSummary;
        _nowMs = nowMs ?? (static () => Environment.TickCount64);
        _windowStartMs = _nowMs();
    }

    /// <summary>
    /// Gets the total number of log entries suppressed across all windows for this instance.
    /// </summary>
    public long TotalSuppressed
    {
        get
        {
            lock (_gate)
            {
                return _totalSuppressed;
            }
        }
    }

    /// <summary>
    /// Emits a protocol-error log entry through <paramref name="emit"/> when the current window still
    /// has budget; otherwise records the entry as suppressed. When a new window has begun and the
    /// previous window suppressed entries, the suppressed-count summary is reported first. Both
    /// callbacks are invoked outside the internal lock so a sink may safely take its own locks.
    /// </summary>
    /// <param name="emit">The action that performs the actual logging when budget is available.</param>
    public void Run(Action emit)
    {
        ArgumentNullException.ThrowIfNull(emit);

        bool shouldEmit;
        long suppressedToReport = 0;

        lock (_gate)
        {
            var now = _nowMs();
            if (now - _windowStartMs >= _windowMs)
            {
                suppressedToReport = _suppressedInWindow;
                _suppressedInWindow = 0;
                _emittedInWindow = 0;
                _windowStartMs = now;
            }

            if (_emittedInWindow < _maxPerWindow)
            {
                _emittedInWindow++;
                shouldEmit = true;
            }
            else
            {
                _suppressedInWindow++;
                _totalSuppressed++;
                shouldEmit = false;
            }
        }

        if (suppressedToReport > 0)
        {
            _onSuppressedSummary(suppressedToReport);
        }

        if (shouldEmit)
        {
            emit();
        }
    }
}
