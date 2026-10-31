// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Runs work in the background, with at most one executing and one pending batch.
/// </summary>
public sealed partial class CoalescingAsyncRunner : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Reports ordered transitions on the worker. Handlers must not wait for this runner to drain.
    /// </summary>
    public event Action<bool>? BusyChanged;

    private readonly Func<Task> _action;
    private readonly Lock _lock = new();

    private TaskCompletionSource? _pending;
    private TaskCompletionSource? _idle;
    private TaskCompletionSource? _stopped;
    private bool _workerActive;
    private bool _isBusy;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoalescingAsyncRunner"/> class.
    /// </summary>
    /// <param name="action">The asynchronous work to run for each batch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public CoalescingAsyncRunner(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
    }

    /// <summary>
    /// Gets a value indicating whether work is running or pending.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            lock (_lock)
            {
                return _isBusy;
            }
        }
    }

    /// <summary>
    /// Joins the next run that has not started. Cancel an individual wait with Task.WaitAsync.
    /// </summary>
    /// <returns>A task with the assigned run's outcome, or a canceled task if this runner is disposed.</returns>
    public Task RequestAsync()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return Task.FromCanceled(new CancellationToken(true));
            }

            _pending ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_isBusy)
            {
                _isBusy = true;
                _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (!_workerActive)
            {
                _workerActive = true;
                _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = Task.Run(RunAsync);
            }

            return _pending.Task;
        }
    }

    /// <summary>
    /// Waits for the current work to drain, regardless of its outcome.
    /// </summary>
    /// <returns>A task that completes when no work is running or pending.</returns>
    public Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return _idle?.Task ?? Task.CompletedTask;
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            NotifyBusyChanged(true);

            while (true)
            {
                TaskCompletionSource batch;
                lock (_lock)
                {
                    batch = _pending!;
                    _pending = null;
                }

                try
                {
                    await _action().ConfigureAwait(false);
                    batch.TrySetResult();
                }
                catch (OperationCanceledException ex)
                {
                    batch.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    batch.TrySetException(ex);
                }

                lock (_lock)
                {
                    if (_pending is not null)
                    {
                        continue;
                    }

                    _isBusy = false;
                    _idle!.TrySetResult();
                    break;
                }
            }

            NotifyBusyChanged(false);

            lock (_lock)
            {
                // Requests can arrive while the idle notification is being delivered
                if (_pending is not null)
                {
                    continue;
                }

                _workerActive = false;
                _stopped!.TrySetResult();
                return;
            }
        }
    }

    private void NotifyBusyChanged(bool isBusy)
    {
        if (BusyChanged is not { } handlers)
        {
            return;
        }

        foreach (Action<bool> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(isBusy);
            }
            catch (Exception ex)
            {
                try
                {
                    CoreLogger.LogError("A coalescing runner busy observer failed", ex);
                }
                catch (Exception)
                {
                    // Logging must not strand accepted work or prevent other observers from running
                }
            }
        }
    }

    /// <summary>
    /// Stops accepting requests and lets accepted work drain in the background.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    /// <remarks>Stops accepting requests and waits for accepted work and its notifications to finish.</remarks>
    public ValueTask DisposeAsync()
    {
        Dispose();
        lock (_lock)
        {
            return new ValueTask(_stopped?.Task ?? Task.CompletedTask);
        }
    }
}
