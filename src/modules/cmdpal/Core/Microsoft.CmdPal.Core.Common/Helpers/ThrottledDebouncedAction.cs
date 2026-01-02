// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Helpers;

public sealed class ThrottledDebouncedAction : IDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(150);

    public event EventHandler<Exception>? UnhandledException;

    private readonly Lock _lock = new();
    private readonly Action _action;
    private readonly TimeSpan _interval;
    private readonly bool _runImmediately;

    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _isPending;

    public ThrottledDebouncedAction(Action action)
        : this(action, DefaultInterval)
    {
    }

    public ThrottledDebouncedAction(Action action, TimeSpan interval, bool runImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

        _action = action;
        _interval = interval;
        _runImmediately = runImmediately;
    }

    public void Dispose()
    {
        Cancel();
    }

    private void SafeInvoke()
    {
        try
        {
            _action();
        }
        catch (Exception ex)
        {
            UnhandledException?.Invoke(this, ex);
        }
    }

    public void Invoke()
    {
        if (_interval == TimeSpan.Zero)
        {
            _action();
            return;
        }

        if (!_runImmediately)
        {
            // Trailing-edge debounce
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
                        await Task.Delay(_interval, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        SafeInvoke();
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
                    return;
                }

                _isRunning = true;
            }

            SafeInvoke();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(_interval).ConfigureAwait(false);

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
                        SafeInvoke();
                    }
                }
            });
        }
    }

    public void InvokeImmediately()
    {
        Cancel();
        SafeInvoke();
    }

    public void Cancel()
    {
        CancellationTokenSource? toCancel;
        lock (_lock)
        {
            toCancel = _cts;
            _cts = null;
            _isPending = false;
        }

        toCancel?.Cancel();
        toCancel?.Dispose();
    }
}
