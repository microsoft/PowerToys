// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Serializes computation and application. A new request cancels older requests and their work.
/// Only the latest result is applied; an application already in progress is allowed to finish.
/// </summary>
/// <typeparam name="T">The type of the computed value.</typeparam>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed partial class SupersedingAsyncValueGate<T> : IDisposable, IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<T>> _valueFactory;
    private readonly Action<T> _apply;
    private readonly Lock _lock = new();
    private readonly Lock _applyLock = new();
    private Request? _current;
    private Request? _pending;
    private Request? _running;
    private TaskCompletionSource? _idle;
    private bool _workerActive;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupersedingAsyncValueGate{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">Computes a value using a token canceled on supersession or disposal.</param>
    /// <param name="apply">Applies a committed value synchronously on the worker, outside the scheduling lock.</param>
    /// <exception cref="ArgumentNullException">Either callback is <see langword="null"/>.</exception>
    public SupersedingAsyncValueGate(Func<CancellationToken, Task<T>> valueFactory, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentNullException.ThrowIfNull(apply);
        _valueFactory = valueFactory;
        _apply = apply;
    }

    /// <summary>
    /// Requests a new computation.
    /// </summary>
    /// <param name="cancellationToken">Cancels only the caller's wait, not computation or application.</param>
    /// <returns>
    /// A task that returns the computed value after application completes. It is canceled when the caller's wait or
    /// factory is canceled, or the request is superseded or the gate is disposed before application commits.
    /// Requests after disposal also return a canceled task. Otherwise, factory or application exceptions fault the task,
    /// including <see cref="OperationCanceledException"/> thrown by application.
    /// </returns>
    /// <remarks>
    /// To share a computation, call without a token and use Task.WaitAsync for each caller.
    /// </remarks>
    public Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        Task<T> task;
        lock (_lock)
        {
            if (_disposed)
            {
                return Task.FromCanceled<T>(new CancellationToken(true));
            }

            _current?.Completion.TrySetCanceled(CancellationToken.None);
            _current = _pending = new Request();
            task = _current.Completion.Task;
            CancelRunning();

            if (!_workerActive)
            {
                _workerActive = true;
                _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = Task.Run(RunAsync, CancellationToken.None);
            }
        }

        return task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Waits for computation, application and cancellation callbacks to finish.
    /// </summary>
    /// <returns>A task that completes when the current worker and cancellation callbacks have finished.</returns>
    /// <remarks>
    /// Do not wait for idleness from the factory, application or cancellation callbacks; the gate waits for them to finish.
    /// </remarks>
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
            Request request;
            lock (_lock)
            {
                if (_pending is null)
                {
                    _workerActive = false;
                    _idle!.TrySetResult();
                    return;
                }

                request = _running = _pending;
                _pending = null;
                request.Cancellation = new();
            }

            try
            {
                var value = await _valueFactory(request.Cancellation.Token).ConfigureAwait(false);
                lock (_applyLock)
                {
                    lock (_lock)
                    {
                        if (_current != request)
                        {
                            continue;
                        }

                        // Commit application before releasing the scheduling lock.
                        _current = null;
                    }

                    try
                    {
                        _apply(value);
                        request.Completion.TrySetResult(value);
                    }
                    catch (Exception ex)
                    {
                        request.Completion.TrySetException(ex);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                request.Completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                request.Completion.TrySetException(ex);
            }
            finally
            {
                Task cancellation;
                lock (_lock)
                {
                    if (_current == request)
                    {
                        _current = null;
                    }

                    _running = null;
                    cancellation = request.CancellationTask ?? Task.CompletedTask;
                }

                try
                {
                    await cancellation.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A cancellation callback must not strand the replacement request.
                }

                request.Cancellation.Dispose();
            }
        }
    }

    private void CancelRunning()
    {
        if (_running is { } running)
        {
            // CancelAsync marks the token now but invokes callbacks asynchronously.
            running.CancellationTask ??= running.Cancellation!.CancelAsync();
        }
    }

    /// <summary>
    /// Cancels pending work and waits for an active application to finish. No later result is applied.
    /// </summary>
    /// <remarks>
    /// Do not call while holding a lock or running on a thread needed by application.
    /// Await <see cref="DisposeAsync"/> instead after releasing such locks.
    /// </remarks>
    public void Dispose()
    {
        StopAcceptingRequests();

        lock (_applyLock)
        {
            // Wait for a committed application to finish.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cancels pending work and asynchronously waits for computation, application and callbacks.
    /// </summary>
    /// <returns>A task that completes when the gate is idle after disposal.</returns>
    /// <remarks>
    /// Await after releasing locks needed by the work or cancellation callbacks; do not block on the result.
    /// Do not wait for disposal from the factory, application or cancellation callbacks; disposal waits for them to finish.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        StopAcceptingRequests();
        GC.SuppressFinalize(this);
        return new ValueTask(WhenIdleAsync());
    }

    private void StopAcceptingRequests()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _current?.Completion.TrySetCanceled();
            _current = _pending = null;
            CancelRunning();
        }
    }

    private string GetDebuggerDisplay()
    {
        // Do not take locks here; the debugger may have suspended their owner.
        return $"Disposed = {_disposed}, WorkerActive = {_workerActive}, Current = {_current is not null}, Pending = {_pending is not null}, Running = {_running is not null}";
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    private sealed class Request
    {
        public TaskCompletionSource<T> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource? Cancellation { get; set; }

        public Task? CancellationTask { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"Completion = {Completion.Task.Status}, CancellationRequested = {Cancellation?.IsCancellationRequested ?? false}, Callbacks = {CancellationTask?.Status.ToString() ?? "None"}";
        }
    }
}
