// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PowerLauncher.ViewModel
{
    /// <summary>
    /// Owns the cancellation and task lifetime of a single PT Run query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construct via <see cref="Start"/>: the caller passes a factory that builds
    /// its full task pipeline (e.g. <c>Task.Run</c> plus an optional
    /// <c>ContinueWith</c>) against the supplied token, and the resulting tail
    /// task is captured as the immutable <see cref="Completion"/>. After
    /// construction the session exposes only terminal operations (<see cref="Cancel"/>,
    /// <see cref="DisposeWhenComplete"/>, <see cref="CancelAndWait"/>,
    /// <see cref="Dispose"/>) — there is no add-task / register / mutate-after-start
    /// surface, so there is no lifecycle race to synchronize against.
    /// </para>
    /// <para>
    /// The only structural guarantee that matters for the #36041 bug is that
    /// <see cref="Token"/> returns a value-type snapshot bound to <em>this</em>
    /// session's <see cref="CancellationTokenSource"/>. Any task body that
    /// captures the token as a local cannot be tricked by a later
    /// <c>_currentSession = new QuerySession()</c> reassignment into observing a
    /// fresh (non-cancelled) token.
    /// <para>
    /// Note: "structural" applies only to callers that capture <see cref="Token"/>
    /// into a local before passing it into a task body. <see cref="QuerySession"/>
    /// cannot prevent a future author from re-reading <c>_currentSession.Token</c>
    /// inside a loop / continuation, which would reintroduce the original bug
    /// shape — code review of every observer site is still required.
    /// </para>
    /// </remarks>
    internal sealed class QuerySession : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private int _disposed;

        private QuerySession()
        {
            _cts = new CancellationTokenSource();
            Token = _cts.Token;
        }

        /// <summary>
        /// Gets the snapshot of the session's cancellation token. Always capture
        /// into a local before passing into a task body; never re-read from a
        /// shared field that may have been reassigned to a newer session.
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// Gets the task whose completion gates disposal of the underlying
        /// <see cref="CancellationTokenSource"/>. This is the tail of the
        /// pipeline returned by the factory passed to <see cref="Start"/>.
        /// </summary>
        public Task Completion { get; private set; }

        /// <summary>
        /// Constructs a session and immediately invokes <paramref name="pipeline"/>
        /// with this session's token. The task returned by the factory becomes the
        /// session's <see cref="Completion"/>; it is the only task whose lifetime
        /// the session tracks for disposal purposes.
        /// </summary>
        public static QuerySession Start(Func<CancellationToken, Task> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            var session = new QuerySession();
            try
            {
                session.Completion = pipeline(session.Token) ?? Task.CompletedTask;
            }
            catch
            {
                session._cts.Dispose();
                throw;
            }

            return session;
        }

        /// <summary>Requests cancellation. Idempotent; never throws.</summary>
        public void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Lost a benign race with Dispose; cancellation is already moot.
            }
        }

        /// <summary>
        /// Fire-and-forget disposal of the underlying
        /// <see cref="CancellationTokenSource"/> once <see cref="Completion"/>
        /// finishes. Safe to call from the swap path that replaces this session
        /// with a fresh one.
        /// </summary>
        public Task DisposeWhenComplete()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return Task.CompletedTask;
            }

            if (Completion == null || Completion.IsCompleted)
            {
                _cts.Dispose();
                return Task.CompletedTask;
            }

            // DenyChildAttach so a plugin-created attached child task cannot
            // delay disposal of the CTS past its parent's completion.
            return Completion.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state).Dispose(),
                _cts,
                CancellationToken.None,
                TaskContinuationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Shutdown path: cancels, waits up to <paramref name="timeout"/> for
        /// <see cref="Completion"/>, then disposes the CTS. Returns true if the
        /// tracked task completed within the timeout (or none was registered).
        /// </summary>
        public bool CancelAndWait(TimeSpan timeout)
        {
            Cancel();

            bool completed = true;
            if (Completion != null && !Completion.IsCompleted)
            {
                try
                {
                    completed = Completion.Wait(timeout);
                }
                catch (AggregateException)
                {
                    // Cancellation / plugin faults are not our concern on shutdown.
                    completed = true;
                }
            }

            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                if (completed)
                {
                    _cts.Dispose();
                }
                else
                {
                    // The tracked task outlived our timeout; disposing _cts under
                    // it would risk ObjectDisposedException if the task later
                    // touches token WaitHandles. Defer disposal until the task
                    // actually completes. DenyChildAttach so an attached child
                    // task cannot delay disposal past the parent's completion.
                    _ = Completion.ContinueWith(
                        static (_, state) => ((CancellationTokenSource)state).Dispose(),
                        _cts,
                        CancellationToken.None,
                        TaskContinuationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                }
            }

            return completed;
        }

        public void Dispose() => CancelAndWait(TimeSpan.Zero);
    }
}
