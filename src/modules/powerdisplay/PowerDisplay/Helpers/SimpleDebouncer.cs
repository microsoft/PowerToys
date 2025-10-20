// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Simple debouncer that delays execution of an action until a quiet period.
    /// Replaces the complex PropertyUpdateQueue with a much simpler approach (KISS principle).
    /// </summary>
    public partial class SimpleDebouncer : IDisposable
    {
        private readonly int _delayMs;
        private readonly object _lock = new object();
        private CancellationTokenSource? _cts;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleDebouncer"/> class.
        /// Create a debouncer with specified delay
        /// </summary>
        /// <param name="delayMs">Delay in milliseconds before executing action</param>
        public SimpleDebouncer(int delayMs = 300)
        {
            _delayMs = delayMs;
        }

        /// <summary>
        /// Debounce an async action. Cancels previous invocation if still pending.
        /// </summary>
        /// <param name="action">Async action to execute after delay</param>
        public async void Debounce(Func<Task> action)
        {
            if (_disposed)
            {
                return;
            }

            CancellationTokenSource cts;

            lock (_lock)
            {
                // Cancel previous invocation
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                cts = _cts;
            }

            try
            {
                // Wait for quiet period
                await Task.Delay(_delayMs, cts.Token);

                // Execute action if not cancelled
                if (!cts.Token.IsCancellationRequested)
                {
                    await action();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when debouncing - a newer call cancelled this one
                Logger.LogTrace("Debounced action cancelled (replaced by newer call)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Debounced action failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Debounce a synchronous action
        /// </summary>
        public void Debounce(Action action)
        {
            Debounce(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                _disposed = true;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
