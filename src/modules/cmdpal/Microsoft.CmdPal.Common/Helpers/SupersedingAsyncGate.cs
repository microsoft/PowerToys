// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// An async gate that ensures only one operation runs at a time.
/// If ExecuteAsync is called while already executing, it cancels the current execution
/// and starts the operation again (superseding behavior).
/// </summary>
public sealed partial class SupersedingAsyncGate : IDisposable
{
    private readonly Func<CancellationToken, Task> _action;
    private readonly TaskFactory _taskFactory;
    private readonly Lock _lock = new();
    private int _callId;
    private TaskCompletionSource<bool>? _currentTcs;
    private CancellationToken _currentExternalCancellationToken;
    private CancellationTokenSource? _currentCancellationSource;
    private bool _isExecuting;
    private bool _isDisposed;

    public SupersedingAsyncGate(Func<CancellationToken, Task> action)
        : this(action, TaskScheduler.Default)
    {
    }

    public SupersedingAsyncGate(Func<CancellationToken, Task> action, TaskScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scheduler);
        _action = action;
        _taskFactory = new(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, scheduler);
    }

    /// <summary>
    /// Executes the configured action. If another execution is running, this call will
    /// cancel the current execution and restart the operation.
    /// </summary>
    /// <param name="cancellationToken">Optional external cancellation token</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<bool> tcs;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            var superseded = _currentTcs;
            tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentTcs = tcs;
            _currentExternalCancellationToken = cancellationToken;
            _callId++;

            superseded?.TrySetCanceled(CancellationToken.None);
            _currentCancellationSource?.Cancel();

            if (!_isExecuting && _currentTcs is not null)
            {
                _isExecuting = true;
                try
                {
                    _ = _taskFactory.StartNew(ExecuteLoop).Unwrap();
                }
                catch (TaskSchedulerException)
                {
                    _isExecuting = false;
                    _currentTcs = null;
                    _currentExternalCancellationToken = default;
                    throw;
                }
            }
        }

        await using var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        await tcs.Task.ConfigureAwait(false);
    }

    private async Task ExecuteLoop()
    {
        while (true)
        {
            TaskCompletionSource<bool>? currentTcs;
            CancellationTokenSource currentCts;
            int currentCallId;

            lock (_lock)
            {
                currentTcs = _currentTcs;
                currentCallId = _callId;

                if (currentTcs is null)
                {
                    // Retire under the request lock so a new call cannot be lost during shutdown.
                    _isExecuting = false;
                    _currentExternalCancellationToken = default;
                    return;
                }

                currentCts = CancellationTokenSource.CreateLinkedTokenSource(_currentExternalCancellationToken);
                _currentCancellationSource = currentCts;
            }

            try
            {
                currentCts.Token.ThrowIfCancellationRequested();
                await _action(currentCts.Token).ConfigureAwait(false);
                currentCts.Token.ThrowIfCancellationRequested();
                CompleteIfCurrent(currentTcs, currentCallId, static t => t.TrySetResult(true));
            }
            catch (OperationCanceledException)
            {
                CompleteIfCurrent(currentTcs, currentCallId, tcs => tcs.TrySetCanceled(currentCts.Token));
            }
            catch (Exception ex)
            {
                CompleteIfCurrent(currentTcs, currentCallId, tcs => tcs.TrySetException(ex));
            }
            finally
            {
                lock (_lock)
                {
                    _currentCancellationSource = null;
                }

                currentCts.Dispose();
            }
        }
    }

    private void CompleteIfCurrent(
        TaskCompletionSource<bool> candidate,
        int id,
        Action<TaskCompletionSource<bool>> complete)
    {
        lock (_lock)
        {
            if (_currentTcs == candidate && _callId == id)
            {
                complete(candidate);
                _currentTcs = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            _currentCancellationSource?.Cancel();
            _currentTcs?.TrySetException(new ObjectDisposedException(nameof(SupersedingAsyncGate)));
            _currentTcs = null;
            _currentExternalCancellationToken = default;
        }

        GC.SuppressFinalize(this);
    }
}
