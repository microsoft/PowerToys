// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// Coalesces repeated requests to run an action.
/// </summary>
/// <remarks>
/// <para>
/// In the default debounce mode, the action runs on the thread pool when the delay expires.
/// Ordinary calls restart the delay unless a priority run is pending.
/// </para>
/// <para>
/// With <c>runImmediately</c> enabled, the first call runs the action on the calling thread.
/// Calls during the cooldown trigger one more run when it ends.
/// </para>
/// <para>Calls can come from any thread. A zero interval runs the action immediately on the calling thread.</para>
/// </remarks>
public sealed partial class ThrottledDebouncedAction : IDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(150);

    private readonly Lock _lock = new();
    private readonly Action _action;
    private readonly TimeSpan _defaultInterval;
    private readonly bool _runImmediately;

    private CancellationTokenSource? _cts;
    private bool _pendingIsPriority;
    private bool _isRunning;
    private bool _isPending;
    private TimeSpan _pendingInterval;

    /// <summary>
    /// Creates a trailing-edge debounce with a 150 ms interval.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public ThrottledDebouncedAction(Action action)
        : this(action, DefaultInterval)
    {
    }

    /// <summary>
    /// Creates a trailing-edge debounce, or a throttle when <paramref name="runImmediately"/> is set.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="interval">The delay for calls that don't pass their own. Zero runs the action on every call.</param>
    /// <param name="runImmediately">true to run the first call at once and throttle the rest; false to debounce on the trailing edge.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is negative.</exception>
    public ThrottledDebouncedAction(Action action, TimeSpan interval, bool runImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

        _action = action;
        _defaultInterval = interval;
        _runImmediately = runImmediately;
    }

    /// <summary>
    /// Cancels any pending run, like <see cref="Cancel"/>.
    /// </summary>
    public void Dispose()
    {
        Cancel();
    }

    /// <summary>
    /// Requests a run with the default interval, like <see cref="Invoke(TimeSpan?)"/> with null.
    /// </summary>
    public void Invoke()
    {
        Invoke(null);
    }

    /// <summary>
    /// Requests a run. When debouncing, it replaces any pending run with one due after the interval,
    /// unless a run requested by <see cref="InvokePriority"/> is pending; then it joins that run.
    /// When throttling, it runs the action now if no cooldown is active, and otherwise asks for one
    /// more run after the cooldown.
    /// </summary>
    /// <param name="interval">The delay for this call, or null for the default. Zero cancels any pending run and runs the action now on the calling thread.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is negative.</exception>
    public void Invoke(TimeSpan? interval)
    {
        Invoke(interval, priority: false);
    }

    /// <summary>
    /// Schedules the action with priority. In debounce mode, this replaces any pending run.
    /// Only another priority call can reschedule it. Priority has no effect in throttle mode.
    /// </summary>
    /// <param name="interval">
    /// The delay, or <see langword="null"/> to use the configured interval.
    /// Zero cancels any pending run and executes the action immediately on the calling thread.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is negative.</exception>
    public void InvokePriority(TimeSpan? interval = null)
    {
        Invoke(interval, priority: true);
    }

    private void Invoke(TimeSpan? interval, bool priority)
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
            // Trailing-edge debounce: each call resets the delay with the new interval, except that
            // a plain call joins a pending priority run instead of postponing it.
            CancellationTokenSource? oldCts;
            CancellationTokenSource cts;
            CancellationToken token;

            lock (_lock)
            {
                if (!priority && _pendingIsPriority)
                {
                    return;
                }

                oldCts = _cts;
                cts = _cts = new CancellationTokenSource();
                token = cts.Token;
                _pendingIsPriority = priority;
            }

            oldCts?.Cancel();
            oldCts?.Dispose();

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(effectiveInterval, token).ConfigureAwait(false);

                        // Claim the run before acting, so a call that arrives while the action
                        // runs schedules a new run instead of joining this one.
                        lock (_lock)
                        {
                            if (!ReferenceEquals(_cts, cts))
                            {
                                return;
                            }

                            _cts = null;
                            _pendingIsPriority = false;
                        }

                        cts.Dispose();
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

    /// <summary>
    /// Cancels any pending run and runs the action now on the calling thread.
    /// </summary>
    public void InvokeImmediately() => Invoke(TimeSpan.Zero);

    /// <summary>
    /// Cancels any pending run, including a priority one. An action that's already running isn't interrupted.
    /// </summary>
    public void Cancel()
    {
        CancellationTokenSource? toCancel;
        lock (_lock)
        {
            toCancel = _cts;
            _cts = null;
            _pendingIsPriority = false;
            _isPending = false;
            _isRunning = false;
        }

        toCancel?.Cancel();
        toCancel?.Dispose();
    }
}
