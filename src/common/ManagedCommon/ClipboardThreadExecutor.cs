// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ManagedCommon
{
    internal sealed partial class ClipboardThreadExecutor : IDisposable
    {
        private readonly Channel<IClipboardWorkItem> _workItems =
            Channel.CreateUnbounded<IClipboardWorkItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });

        private readonly Thread _thread;
        private readonly object _gate = new();
        private bool _disposed;

        internal ClipboardThreadExecutor()
        {
            _thread = new Thread(ProcessWorkItems)
            {
                IsBackground = true,
                Name = "PowerToys Clipboard STA",
            };

            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        internal Task<T> InvokeAsync<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            return InvokeAsync(() => Task.FromResult(action()));
        }

        internal Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var workItem = new ClipboardWorkItem<T>(action);

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                ObjectDisposedException.ThrowIf(!_workItems.Writer.TryWrite(workItem), this);
            }

            return workItem.Task;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _workItems.Writer.TryComplete();
            }

            if (Thread.CurrentThread != _thread)
            {
                _thread.Join();
            }
        }

        private void ProcessWorkItems()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            var synchronizationContext = new SingleThreadSynchronizationContext();

            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            try
            {
                while (_workItems.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                {
                    while (_workItems.Reader.TryRead(out IClipboardWorkItem? workItem))
                    {
                        workItem.Invoke(synchronizationContext);
                    }
                }
            }
            finally
            {
                synchronizationContext.Complete();
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private interface IClipboardWorkItem
        {
            void Invoke(SingleThreadSynchronizationContext synchronizationContext);
        }

        private sealed class ClipboardWorkItem<T> : IClipboardWorkItem
        {
            private readonly Func<Task<T>> _action;
            private readonly TaskCompletionSource<T> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal ClipboardWorkItem(Func<Task<T>> action)
            {
                _action = action;
            }

            internal Task<T> Task => _completion.Task;

            public void Invoke(SingleThreadSynchronizationContext synchronizationContext)
            {
                try
                {
                    T result = synchronizationContext.Run(_action);
                    _completion.SetResult(result);
                }
                catch (OperationCanceledException ex)
                {
                    _completion.SetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    _completion.SetException(ex);
                }
            }
        }

        private sealed class SingleThreadSynchronizationContext : SynchronizationContext
        {
            private readonly object _gate = new();
            private OperationPump? _currentPump;

            internal void Complete()
            {
                OperationPump? pump;
                lock (_gate)
                {
                    pump = _currentPump;
                    _currentPump = null;
                }

                pump?.Complete();
            }

            internal T Run<T>(Func<Task<T>> action)
            {
                ArgumentNullException.ThrowIfNull(action);

                var pump = new OperationPump();
                OperationPump? previousPump;
                lock (_gate)
                {
                    previousPump = _currentPump;
                    _currentPump = pump;
                }

                try
                {
                    Task<T> task = action();
                    ArgumentNullException.ThrowIfNull(task);
                    return pump.Run(task);
                }
                finally
                {
                    lock (_gate)
                    {
                        _currentPump = previousPump;
                    }
                }
            }

            public override void Post(SendOrPostCallback d, object? state)
            {
                ArgumentNullException.ThrowIfNull(d);

                OperationPump? pump;
                lock (_gate)
                {
                    pump = _currentPump;
                }

                if (pump is null)
                {
                    return;
                }

                pump.Post(d, state);
            }

            private sealed class OperationPump
            {
                private readonly Queue<PostedCallback> _callbacks = new();
                private bool _completed;

                internal void Complete()
                {
                    lock (_callbacks)
                    {
                        _completed = true;
                        _callbacks.Clear();
                        Monitor.PulseAll(_callbacks);
                    }
                }

                internal void Post(SendOrPostCallback callback, object? state)
                {
                    lock (_callbacks)
                    {
                        if (_completed)
                        {
                            return;
                        }

                        _callbacks.Enqueue(new PostedCallback(callback, state, ExecutionContext.Capture()));
                        Monitor.Pulse(_callbacks);
                    }
                }

                internal T Run<T>(Task<T> task)
                {
                    task.ContinueWith(
                        static (_, state) =>
                        {
                            Queue<PostedCallback> callbacks = (Queue<PostedCallback>)state!;
                            lock (callbacks)
                            {
                                Monitor.PulseAll(callbacks);
                            }
                        },
                        _callbacks,
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);

                    while (!task.IsCompleted)
                    {
                        PostedCallback? callback = WaitForCallback(task);
                        callback?.Invoke();
                    }

                    Complete();
                    return task.GetAwaiter().GetResult();
                }

                private PostedCallback? WaitForCallback(Task task)
                {
                    lock (_callbacks)
                    {
                        while (_callbacks.Count == 0 && !task.IsCompleted && !_completed)
                        {
                            Monitor.Wait(_callbacks);
                        }

                        return _callbacks.Count > 0 ? _callbacks.Dequeue() : null;
                    }
                }
            }

            private sealed class PostedCallback
            {
                private readonly SendOrPostCallback _callback;
                private readonly ExecutionContext? _executionContext;
                private readonly object? _state;

                internal PostedCallback(
                    SendOrPostCallback callback,
                    object? state,
                    ExecutionContext? executionContext)
                {
                    _callback = callback;
                    _state = state;
                    _executionContext = executionContext;
                }

                internal void Invoke()
                {
                    if (_executionContext is null)
                    {
                        _callback(_state);
                        return;
                    }

                    ExecutionContext.Run(
                        _executionContext,
                        static state => ((PostedCallback)state!)._callback(((PostedCallback)state!)._state),
                        this);
                }
            }
        }
    }
}
