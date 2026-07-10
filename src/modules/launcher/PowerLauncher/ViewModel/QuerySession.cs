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

        // Accessed only through Interlocked.Exchange, which provides the required memory barrier.
        private int _disposed;

        private QuerySession(CancellationTokenSource cancellationSource, CancellationToken token, Task completion)
        {
            _cancellationSource = cancellationSource;
            Token = token;
            Completion = completion;
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
