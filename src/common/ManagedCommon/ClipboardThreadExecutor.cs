// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedCommon
{
    internal sealed partial class ClipboardThreadExecutor : IDisposable
    {
        private readonly BlockingCollection<Action> _workItems = new();
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

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _workItems.Add(() =>
                {
                    try
                    {
                        completion.SetResult(action());
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                });
            }

            return completion.Task;
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
                _workItems.CompleteAdding();
            }

            if (Thread.CurrentThread != _thread)
            {
                _thread.Join();
            }

            _workItems.Dispose();
        }

        private void ProcessWorkItems()
        {
            foreach (Action workItem in _workItems.GetConsumingEnumerable())
            {
                workItem();
            }
        }
    }
}
