// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PowerLauncher.ViewModel
{
    /// <summary>
    /// Owns the cancellation source and completion lifetime for one query.
    /// </summary>
    internal sealed class QuerySession : IDisposable
    {
        private readonly CancellationTokenSource _cancellationSource;
        private readonly TaskCompletionSource<bool> _startSource;

        // Interlocked.Exchange requires an int and provides the memory barrier for idempotent disposal.
        private int _disposed;

        private QuerySession(CancellationTokenSource cancellationSource, CancellationToken token, Task completion, TaskCompletionSource<bool> startSource = null)
        {
            _cancellationSource = cancellationSource;
            Token = token;
            Completion = completion;
            _startSource = startSource;
        }

        public CancellationToken Token { get; }

        public Task Completion { get; }

        public static QuerySession Start(Func<CancellationToken, Task> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            var cancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;
            try
            {
                var completion = pipeline(token) ?? Task.CompletedTask;
                return new QuerySession(cancellationSource, token, completion);
            }
            catch
            {
                cancellationSource.Dispose();
                throw;
            }
        }

        public static QuerySession StartSuspended(Func<CancellationToken, Task> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            var cancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;
            var startSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = RunPipelineAsync();
            return new QuerySession(cancellationSource, token, completion, startSource);

            async Task RunPipelineAsync()
            {
                await startSource.Task.WaitAsync(token).ConfigureAwait(false);
                await (pipeline(token) ?? Task.CompletedTask).ConfigureAwait(false);
            }
        }

        public void Resume()
        {
            _startSource?.TrySetResult(true);
        }

        public void Cancel()
        {
            try
            {
                _cancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public Task DisposeWhenComplete()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return Task.CompletedTask;
            }

            return Completion.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state).Dispose(),
                _cancellationSource,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public bool CancelAndWait(TimeSpan timeout)
        {
            Cancel();

            bool completed;
            try
            {
                completed = Completion.IsCompleted || Completion.Wait(timeout);
            }
            catch (AggregateException)
            {
                completed = true;
            }

            _ = DisposeWhenComplete();
            return completed;
        }

        public void Dispose()
        {
            Cancel();
            _ = DisposeWhenComplete();
        }
    }
}
