// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// An async gate that ensures only one value computation runs at a time.
/// If ExecuteAsync is called while already executing, it cancels the current computation
/// and starts the operation again (superseding behavior).
/// Once a value is successfully computed, it is applied (via the provided <see cref="Action{T}"/>).
/// The apply step uses its own lock so that long-running apply logic does not block the
/// computation / superseding pipeline, while still remaining serialized with respect to
/// other apply calls.
/// </summary>
/// <typeparam name="T">The type of the computed value.</typeparam>
public sealed partial class SupersedingAsyncValueGate<T> : IDisposable
{
    private readonly Func<CancellationToken, Task<T>> _valueFactory;
    private readonly Action<T> _apply;
    private readonly Lock _lock = new();              // Controls scheduling / superseding
    private readonly Lock _applyLock = new();         // Serializes application of results
    private int _callId;
    private TaskCompletionSource<T>? _currentTcs;
    private CancellationTokenSource? _currentCancellationSource;
    private Task? _executingTask;

    public SupersedingAsyncValueGate(
        Func<CancellationToken, Task<T>> valueFactory,
        Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentNullException.ThrowIfNull(apply);
        _valueFactory = valueFactory;
        _apply = apply;
    }

    /// <summary>
    /// Executes the configured value computation. If another execution is running, this call will
    /// cancel the current execution and restart the computation. The returned task completes when
    /// (and only if) the computation associated with this invocation completes (or is canceled / superseded).
    /// </summary>
    /// <param name="cancellationToken">Optional external cancellation token.</param>
    /// <returns>The computed value for this invocation.</returns>
    public async Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<T> tcs;

        lock (_lock)
        {
            // Supersede any in-flight computation.
            _currentCancellationSource?.Cancel();
            _currentTcs?.TrySetException(new OperationCanceledException("Superseded by newer call"));

            tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentTcs = tcs;
            _callId++;

            if (_executingTask is null)
            {
                _executingTask = Task.Run(ExecuteLoop, CancellationToken.None);
            }
        }

        using var ctr = cancellationToken.Register(state => ((TaskCompletionSource<T>)state!).TrySetCanceled(cancellationToken), tcs);
        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task ExecuteLoop()
    {
        try
        {
            while (true)
            {
                TaskCompletionSource<T>? currentTcs;
                CancellationTokenSource? currentCts;
                int currentCallId;

                lock (_lock)
                {
                    currentTcs = _currentTcs;
                    currentCallId = _callId;

                    if (currentTcs is null)
                    {
                        break; // Nothing pending.
                    }

                    _currentCancellationSource?.Dispose();
                    _currentCancellationSource = new();
                    currentCts = _currentCancellationSource;
                }

                try
                {
                    var value = await _valueFactory(currentCts.Token).ConfigureAwait(false);
                    CompleteSuccessIfCurrent(currentTcs, currentCallId, value);
                }
                catch (OperationCanceledException)
                {
                    CompleteIfCurrent(currentTcs, currentCallId, t => t.TrySetCanceled(currentCts.Token));
                }
                catch (Exception ex)
                {
                    CompleteIfCurrent(currentTcs, currentCallId, t => t.TrySetException(ex));
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

    private void CompleteSuccessIfCurrent(TaskCompletionSource<T> candidate, int id, T value)
    {
        var shouldApply = false;
        lock (_lock)
        {
            if (_currentTcs == candidate && _callId == id)
            {
                // Mark as consumed so a new computation can start immediately.
                _currentTcs = null;
                shouldApply = true;
            }
        }

        if (!shouldApply)
        {
            return; // Superseded meanwhile.
        }

        Exception? applyException = null;
        try
        {
            lock (_applyLock)
            {
                _apply(value);
            }
        }
        catch (Exception ex)
        {
            applyException = ex;
        }

        if (applyException is null)
        {
            candidate.TrySetResult(value);
        }
        else
        {
            candidate.TrySetException(applyException);
        }
    }

    private void CompleteIfCurrent(
        TaskCompletionSource<T> candidate,
        int id,
        Action<TaskCompletionSource<T>> complete)
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
            _currentTcs?.TrySetException(new ObjectDisposedException(nameof(SupersedingAsyncValueGate<T>)));
            _currentTcs = null;
        }

        GC.SuppressFinalize(this);
    }
}
