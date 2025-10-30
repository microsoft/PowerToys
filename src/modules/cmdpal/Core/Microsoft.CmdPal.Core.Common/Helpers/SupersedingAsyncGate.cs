// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// An async gate that ensures only one operation runs at a time.
/// If ExecuteAsync is called while already executing, it cancels the current execution
/// and starts the operation again (superseding behavior).
/// </summary>
public sealed partial class SupersedingAsyncGate : IDisposable
{
    private readonly Func<CancellationToken, Task> _action;
    private readonly Lock _lock = new();
    private int _callId;
    private TaskCompletionSource<bool>? _currentTcs;
    private CancellationTokenSource? _currentCancellationSource;
    private Task? _executingTask;

    public SupersedingAsyncGate(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
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
            _currentCancellationSource?.Cancel();
            _currentTcs?.TrySetException(new OperationCanceledException("Superseded by newer call"));

            tcs = new();
            _currentTcs = tcs;
            _callId++;

            var shouldStartExecution = _executingTask is null;
            if (shouldStartExecution)
            {
                _executingTask = Task.Run(ExecuteLoop, CancellationToken.None);
            }
        }

        await using var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        await tcs.Task;
    }

    private async Task ExecuteLoop()
    {
        try
        {
            while (true)
            {
                TaskCompletionSource<bool>? currentTcs;
                CancellationTokenSource? currentCts;
                int currentCallId;

                lock (_lock)
                {
                    currentTcs = _currentTcs;
                    currentCallId = _callId;

                    if (currentTcs is null)
                    {
                        break;
                    }

                    _currentCancellationSource?.Dispose();
                    _currentCancellationSource = new();
                    currentCts = _currentCancellationSource;
                }

                try
                {
                    await _action(currentCts.Token);
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
            }
        }
        finally
        {
            lock (_lock)
            {
                _currentTcs = null;
                _currentCancellationSource?.Dispose();
                _currentCancellationSource = null;
                _executingTask = null;
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
            _currentCancellationSource?.Cancel();
            _currentCancellationSource?.Dispose();
            _currentTcs?.TrySetException(new ObjectDisposedException(nameof(SupersedingAsyncGate)));
            _currentTcs = null;
        }

        GC.SuppressFinalize(this);
    }
}
