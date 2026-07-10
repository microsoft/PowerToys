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
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void CancelSignalsCapturedTokenAfterSessionReplacement()
        {
            // Regression guard: an old query must retain its own canceled token after a newer query replaces it.
            var firstSession = QuerySession.Start(_ => Task.CompletedTask);
            var firstToken = firstSession.Token;
            var secondSession = QuerySession.Start(_ => Task.CompletedTask);

            firstSession.Cancel();

            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(secondSession.Token.IsCancellationRequested);
        }

        [TestMethod]
        public void DisposeWhenCompleteDoesNotDisposeSourceWhileQueryRuns()
        {
            // A superseded query can still be inside plugin code, so its token source must remain usable until it exits.
            using var releaseQuery = new ManualResetEventSlim();
            CancellationToken capturedToken = default;
            var session = QuerySession.Start(token =>
            {
                capturedToken = token;
                return Task.Run(() => releaseQuery.Wait(WaitTimeout));
            });

            session.Cancel();
            var disposal = session.DisposeWhenComplete();

            using (capturedToken.Register(() => { }))
            {
                Assert.IsFalse(disposal.IsCompleted);
            }

            releaseQuery.Set();
            Assert.IsTrue(disposal.Wait(WaitTimeout));
        }

        [TestMethod]
        public void CancelAndWaitDefersDisposalWhenPluginIgnoresCancellation()
        {
            // Misbehaving plugins may outlive shutdown's wait budget; they must not observe a prematurely disposed source.
            using var releaseQuery = new ManualResetEventSlim();
            CancellationToken capturedToken = default;
            var session = QuerySession.Start(token =>
            {
                capturedToken = token;
                return Task.Run(() => releaseQuery.Wait(WaitTimeout));
            });

            Assert.IsFalse(session.CancelAndWait(TimeSpan.FromMilliseconds(50)));
            using (capturedToken.Register(() => { }))
            {
            }

            releaseQuery.Set();
            Assert.IsTrue(session.Completion.Wait(WaitTimeout));
        }

        [TestMethod]
        public void DisposeIsSafeAfterPriorDisposalRequest()
        {
            // Query replacement and application shutdown can race, so repeated cleanup must remain harmless.
            var session = QuerySession.Start(_ => Task.CompletedTask);

            _ = session.DisposeWhenComplete();
            session.Dispose();
            session.Cancel();
        }
    }
}
