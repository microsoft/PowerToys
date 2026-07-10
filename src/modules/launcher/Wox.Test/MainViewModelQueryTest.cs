// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class MainViewModelQueryTest
    {
        [TestMethod]
        public void EquivalentReconstructedQueryMatchesCurrentQuery()
        {
            // Async plugins are allowed to reconstruct their Query, so correlation must use query content rather than object identity.
            var currentQuery = new Query("search text", ">");
            var reconstructedQuery = new Query("search text", ">");

            Assert.IsTrue(MainViewModel.AreEquivalentQueries(currentQuery, reconstructedQuery));
        }

        [TestMethod]
        public void BusyPluginDoesNotBlockAnotherPlugin()
        {
            // A non-returning plugin must consume only its own slot so unrelated plugins remain searchable.
            var gate = new PluginQueryExecutionGate();
            var firstPlugin = new PluginPair(new PluginMetadata());
            var secondPlugin = new PluginPair(new PluginMetadata());

            Assert.IsTrue(gate.TryEnter(firstPlugin, out var firstLease));
            using (firstLease)
            {
                Assert.IsFalse(gate.TryEnter(firstPlugin, out _));
                Assert.IsTrue(gate.TryEnter(secondPlugin, out var secondLease));
                secondLease.Dispose();
            }

            Assert.IsTrue(gate.TryEnter(firstPlugin, out var resumedLease));
            resumedLease.Dispose();
        }
    }
}
