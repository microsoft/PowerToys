// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

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
            var currentQuery = new Query("search text", ">") { QueryGeneration = 42 };
            var reconstructedQuery = new Query("search text", ">") { QueryGeneration = currentQuery.QueryGeneration };

            Assert.IsTrue(MainViewModel.IsCurrentQuery(currentQuery, reconstructedQuery));
        }

        [TestMethod]
        public void RepeatedQueryTextFromOlderGenerationDoesNotMatch()
        {
            // Repeating query text must not let a late asynchronous event from the earlier query overwrite current results.
            var currentQuery = new Query("search text", ">") { QueryGeneration = 42 };
            var staleQuery = new Query("search text", ">") { QueryGeneration = 40 };

            Assert.IsFalse(MainViewModel.IsCurrentQuery(currentQuery, staleQuery));
        }

        [TestMethod]
        public void BusyPluginDoesNotBlockAnotherPlugin()
        {
            // A non-returning plugin must consume only its own slot so unrelated plugins remain searchable.
            var gate = new PluginQueryExecutionGate();
            var firstPlugin = new PluginPair(new PluginMetadata { ID = "first" });
            var secondPlugin = new PluginPair(new PluginMetadata { ID = "second" });

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

        [TestMethod]
        public async Task BusyPluginQueuesItsLatestQuery()
        {
            // A normally slow plugin must run the latest query after its previous invocation completes instead of silently dropping it.
            var gate = new PluginQueryExecutionGate();
            var plugin = new PluginPair(new PluginMetadata { ID = "plugin" });

            Assert.IsTrue(gate.TryEnter(plugin, out var firstLease));
            var pendingLease = gate.EnterAsync(plugin, CancellationToken.None);
            Assert.IsFalse(pendingLease.IsCompleted);

            firstLease.Dispose();

            using var resumedLease = await pendingLease;
        }
    }
}
