// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;

namespace Wox.Test
{
    [TestClass]
    public class QuerySessionTest
    {
        private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void Cancel_SignalsCapturedTokenAndIsIdempotent()
        {
            var session = QuerySession.Start(_ => Task.CompletedTask);
            var captured = session.Token;

            Assert.IsFalse(captured.IsCancellationRequested);

            session.Cancel();
            session.Cancel(); // idempotent

            Assert.IsTrue(captured.IsCancellationRequested);
            Assert.ThrowsException<OperationCanceledException>(captured.ThrowIfCancellationRequested);
        }

        [TestMethod]
        public void CapturedToken_StaysBoundToOriginalSourceAcrossReplacement()
        {
            // Regression for #36041: re-reading a FIELD token after the field was
            // reassigned silently no-ops on cancellation. A LOCAL capture must stay
            // bound to its original source even after the owning reference is replaced.
            var first = QuerySession.Start(_ => Task.CompletedTask);
            var firstToken = first.Token;

            var second = QuerySession.Start(_ => Task.CompletedTask);
            first.Cancel(); // cancel the original session only

            Assert.IsTrue(firstToken.IsCancellationRequested, "Captured local token must observe cancellation of its OWN source.");
            Assert.IsFalse(second.Token.IsCancellationRequested, "New session's token must NOT be affected by cancelling the previous one.");
        }

        [TestMethod]
        public void Start_PassesSessionTokenToPipelineFactory()
        {
            CancellationToken seenInsidePipeline = default;
            var session = QuerySession.Start(t =>
            {
                seenInsidePipeline = t;
                return Task.CompletedTask;
            });

            Assert.IsTrue(seenInsidePipeline == session.Token, "Pipeline factory must receive THIS session's token.");
        }

        [TestMethod]
        public void Start_ThrowsArgumentNullException_WhenFactoryIsNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => QuerySession.Start(null));
        }

        [TestMethod]
        public void DisposeWhenComplete_WaitsForCompletionTaskBeforeDisposingCts()
        {
            var gate = new ManualResetEventSlim(initialState: false);
            bool observedCancellationInside = false;

            // Pass CancellationToken.None to Task.Run so the scheduler doesn't
            // short-circuit to Canceled before the worker starts running; we still
            // observe cancellation inside the body via the captured 'token'.
            var session = QuerySession.Start(token => Task.Run(
                () =>
                {
                    gate.Wait(WaitBudget);
                    observedCancellationInside = token.IsCancellationRequested;
                },
                CancellationToken.None));

            session.Cancel();

            var disposalCompletion = session.DisposeWhenComplete();

            Assert.IsFalse(disposalCompletion.IsCompleted, "Dispose must NOT complete while the completion task is still running.");

            gate.Set();

            Assert.IsTrue(disposalCompletion.Wait(WaitBudget), "Dispose continuation should fire once the completion task completes.");
            Assert.IsTrue(session.Completion.IsCompleted);
            Assert.IsTrue(observedCancellationInside, "Captured local token must surface cancellation to the running task body.");
        }

        [TestMethod]
        public void CancelAndWait_ReturnsTrueWhenTaskCompletesWithinTimeout()
        {
            var session = QuerySession.Start(token => Task.Run(
                () =>
                {
                    try
                    {
                        Task.Delay(Timeout.Infinite, token).Wait();
                    }
                    catch (AggregateException)
                    {
                        // expected on cancellation
                    }
                },
                CancellationToken.None));

            bool completed = session.CancelAndWait(WaitBudget);

            Assert.IsTrue(completed);
            Assert.IsTrue(session.Completion.IsCompleted);
        }

        [TestMethod]
        public void CancelAndWait_ReturnsFalseWhenTaskExceedsTimeout()
        {
            var release = new ManualResetEventSlim(initialState: false);

            // Task ignores cancellation — simulates a buggy plugin that doesn't yield.
            var session = QuerySession.Start(_ => Task.Run(() => release.Wait(WaitBudget)));

            bool completed = session.CancelAndWait(TimeSpan.FromMilliseconds(50));

            Assert.IsFalse(completed, "CancelAndWait must report timeout when the completion task doesn't yield.");

            // Cleanup so the test process doesn't leak the worker.
            release.Set();
            session.Completion.Wait(WaitBudget);
        }

        [TestMethod]
        public void CancelAndWait_DoesNotDisposeCtsWhileTaskStillRuns()
        {
            // Regression: CancelAndWait used to dispose the underlying CTS unconditionally
            // on timeout, leaving any still-running task body holding a disposed source —
            // a future code path that touches token WaitHandles (e.g. Register, WaitOne)
            // would crash with ObjectDisposedException, violating the PR's own
            // "tasks never observe a disposed CTS" invariant. The fix defers disposal
            // until the task actually completes.
            var release = new ManualResetEventSlim(initialState: false);
            CancellationToken capturedToken = default;

            var session = QuerySession.Start(token =>
            {
                capturedToken = token;
                return Task.Run(() => release.Wait(WaitBudget));
            });

            bool completed = session.CancelAndWait(TimeSpan.FromMilliseconds(50));
            Assert.IsFalse(completed, "Sanity: task must outlive the timeout.");

            // While the task is still running with the CTS undisposed,
            // CancellationToken.Register must succeed (would throw ODE if disposed).
            using (capturedToken.Register(() => { }))
            {
                // no-op; just proves Register didn't throw
            }

            // Allow the task to complete; the deferred ContinueWith should then dispose
            // the CTS, and a subsequent Register attempt is allowed to throw ODE.
            release.Set();
            session.Completion.Wait(WaitBudget);
        }

        [TestMethod]
        public void Dispose_IsIdempotent_AndSafeAfterCancel()
        {
            var session = QuerySession.Start(_ => Task.CompletedTask);

            session.Cancel();
            session.Dispose();
            session.Dispose();    // idempotent
            session.Cancel();     // safe after Dispose
        }

        [TestMethod]
        public void Token_ThrowIfCancellationRequested_NeverThrowsObjectDisposedException()
        {
            // Documents the property the original PR description got wrong: even after
            // the CTS is disposed, ThrowIfCancellationRequested only ever raises
            // OperationCanceledException — never ObjectDisposedException.
            var session = QuerySession.Start(_ => Task.CompletedTask);
            var token = session.Token;

            session.Cancel();
            session.Dispose();

            Assert.ThrowsException<OperationCanceledException>(token.ThrowIfCancellationRequested);
        }
    }
}
