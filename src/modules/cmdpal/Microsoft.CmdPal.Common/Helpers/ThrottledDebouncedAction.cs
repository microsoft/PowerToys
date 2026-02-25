// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Helpers;

public sealed class ThrottledDebouncedAction : IDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(150);

    private readonly Lock _lock = new();
    private readonly Action _action;
    private readonly TimeSpan _defaultInterval;
    private readonly bool _runImmediately;

    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _isPending;
    private TimeSpan _pendingInterval;

    public ThrottledDebouncedAction(Action action)
        : this(action, DefaultInterval)
    {
    }

    public ThrottledDebouncedAction(Action action, TimeSpan interval, bool runImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

        _action = action;
        _defaultInterval = interval;
        _runImmediately = runImmediately;
    }

    public void Dispose()
    {
        Cancel();
    }

    public void Invoke() => Invoke(null);

    public void Invoke(TimeSpan? interval)
    {
        var effectiveInterval = interval ?? _defaultInterval;
        ArgumentOutOfRangeException.ThrowIfLessThan(effectiveInterval, TimeSpan.Zero);

        if (effectiveInterval == TimeSpan.Zero)
        {
            Cancel();
            _action();
            return;
        }

        if (!_runImmediately)
        {
            // Trailing-edge debounce: each call resets the delay with the new interval.
            CancellationTokenSource? oldCts;
            CancellationToken token;

            lock (_lock)
            {
                oldCts = _cts;
                _cts = new CancellationTokenSource();
                token = _cts.Token;
            }

            oldCts?.Cancel();
            oldCts?.Dispose();

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(effectiveInterval, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        _action();
                    }
                    catch (OperationCanceledException)
                    {
                        // expected during reschedules/dispose
                    }
                },
                CancellationToken.None);
        }
        else
        {
            // Leading + Trailing throttle/debounce
            lock (_lock)
            {
                if (_isRunning)
                {
                    _isPending = true;
                    _pendingInterval = effectiveInterval;
                    return;
                }

                _isRunning = true;
            }

            _action();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    TimeSpan delayInterval;
                    lock (_lock)
                    {
                        // Snapshot the interval to use for this cooldown.
                        // If no pending call yet, use the interval from the
                        // leading invocation; otherwise use the most recent
                        // pending interval (which may be updated by new calls
                        // arriving during the delay).
                        delayInterval = _isPending ? _pendingInterval : effectiveInterval;
                    }

                    await Task.Delay(delayInterval).ConfigureAwait(false);

                    bool shouldRun;
                    lock (_lock)
                    {
                        if (!_isPending)
                        {
                            _isRunning = false;
                            return;
                        }

                        _isPending = false;
                        shouldRun = true;
                    }

                    if (shouldRun)
                    {
                        _action();
                    }
                }
            });
        }
    }

    public void InvokeImmediately() => Invoke(TimeSpan.Zero);

    public void Cancel()
    {
        CancellationTokenSource? toCancel;
        lock (_lock)
        {
            toCancel = _cts;
            _cts = null;
            _isPending = false;
            _isRunning = false;
        }

        toCancel?.Cancel();
        toCancel?.Dispose();
    }
}
