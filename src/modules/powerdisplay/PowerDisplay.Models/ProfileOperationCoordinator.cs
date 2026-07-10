// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Models
{
    public sealed class ProfileOperationCoordinator : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public bool IsRunning { get; private set; }

        public event EventHandler? IsRunningChanged;

        public async Task<T> RunAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                SetIsRunning(true);
                return await operation(cancellationToken);
            }
            finally
            {
                try
                {
                    SetIsRunning(false);
                }
                finally
                {
                    _gate.Release();
                }
            }
        }

        public async Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await RunAsync(
                async token =>
                {
                    await operation(token);
                    return true;
                },
                cancellationToken);
        }

        private void SetIsRunning(bool value)
        {
            if (IsRunning == value)
            {
                return;
            }

            IsRunning = value;
            IsRunningChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }
}
