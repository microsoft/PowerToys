// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Processes one update at a time. While an update is active, one pending slot retains only the
/// newest request. The active request is never cancelled by a newer request.
/// </summary>
public sealed class LatestWinsUpdateQueue<T>
{
    private readonly object _sync = new();
    private readonly Func<T, Task> _processUpdateAsync;
    private readonly Action<Exception>? _errorHandler;
    private TaskCompletionSource<bool>? _idleCompletionSource;
    private T _pendingUpdate = default!;
    private bool _hasPendingUpdate;
    private bool _isProcessing;

    public LatestWinsUpdateQueue(
        Func<T, Task> processUpdateAsync,
        Action<Exception>? errorHandler = null)
    {
        ArgumentNullException.ThrowIfNull(processUpdateAsync);
        _processUpdateAsync = processUpdateAsync;
        _errorHandler = errorHandler;
    }

    /// <summary>
    /// Starts <paramref name="update"/> immediately when idle, or replaces the pending update when
    /// another update is already being processed.
    /// </summary>
    public void Enqueue(T update)
    {
        lock (_sync)
        {
            if (_isProcessing)
            {
                _pendingUpdate = update;
                _hasPendingUpdate = true;
                return;
            }

            _isProcessing = true;
            _idleCompletionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = ProcessUpdatesAsync(update);
    }

    /// <summary>Removes the pending update without interrupting the active update.</summary>
    public void ClearPending()
    {
        lock (_sync)
        {
            _pendingUpdate = default!;
            _hasPendingUpdate = false;
        }
    }

    /// <summary>Returns a task that completes when the active and pending updates have drained.</summary>
    public Task WhenIdleAsync()
    {
        lock (_sync)
        {
            return _isProcessing
                ? _idleCompletionSource!.Task
                : Task.CompletedTask;
        }
    }

    private async Task ProcessUpdatesAsync(T firstUpdate)
    {
        var currentUpdate = firstUpdate;
        while (true)
        {
            try
            {
                await _processUpdateAsync(currentUpdate);
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(ex);
            }

            TaskCompletionSource<bool>? completedIdleSource;
            lock (_sync)
            {
                if (_hasPendingUpdate)
                {
                    currentUpdate = _pendingUpdate;
                    _pendingUpdate = default!;
                    _hasPendingUpdate = false;
                    continue;
                }

                _isProcessing = false;
                completedIdleSource = _idleCompletionSource;
                _idleCompletionSource = null;
            }

            completedIdleSource?.TrySetResult(true);
            return;
        }
    }
}
