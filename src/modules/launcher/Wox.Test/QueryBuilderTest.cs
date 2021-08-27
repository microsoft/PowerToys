// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.Plugin;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class QueryBuilderTest
    {
        private static bool AreEqual(Query firstQuery, Query secondQuery)
        {
            // Using Ordinal since this is used internally
            return firstQuery.ActionKeyword.Equals(secondQuery.ActionKeyword, StringComparison.Ordinal)
                && firstQuery.Search.Equals(secondQuery.Search, StringComparison.Ordinal)
                && firstQuery.RawQuery.Equals(secondQuery.RawQuery, StringComparison.Ordinal);
        }

        [TestMethod]
        public void QueryBuilderShouldRemoveExtraSpacesForNonGlobalPlugin()
        {
            // Arrange
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                new PluginPair(new PluginMetadata() { ActionKeyword = ">" }),
            });

            string searchQuery = ">   file.txt    file2 file3";

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);
            searchQuery = pluginQueryPairs.Values.First().RawQuery;

            // Assert
            Assert.AreEqual("> file.txt file2 file3", searchQuery);
        }

        [TestMethod]
        public void QueryBuilderShouldRemoveExtraSpacesForGlobalPlugin()
        {
            // Arrange
            string searchQuery = "file.txt  file2  file3";
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                new PluginPair(new PluginMetadata() { Disabled = false, IsGlobal = true }),
            });

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);
            searchQuery = pluginQueryPairs.Values.First().RawQuery;

            // Assert
            Assert.AreEqual("file.txt file2 file3", searchQuery);
        }

        [TestMethod]
        public void QueryBuildShouldGenerateSameSearchQueryWithOrWithoutSpaceAfterActionKeyword()
        {
            // Arrange
            var plugin = new PluginPair(new PluginMetadata() { ActionKeyword = "a" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                plugin,
            });

            var firstQueryText = "asearch";
            var secondQueryText = "a search";

            // Act
            var firstPluginQueryPair = QueryBuilder.Build(firstQueryText);
            var firstQuery = firstPluginQueryPair.GetValueOrDefault(plugin);

            var secondPluginQueryPairs = QueryBuilder.Build(secondQueryText);
            var secondQuery = secondPluginQueryPairs.GetValueOrDefault(plugin);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(firstQuery.Search.Equals(secondQuery.Search, StringComparison.Ordinal));
            Assert.IsTrue(firstQuery.ActionKeyword.Equals(secondQuery.ActionKeyword, StringComparison.Ordinal));
        }

        [TestMethod]
        public void QueryBuildShouldGenerateCorrectQueryForPluginsWhoseActionKeywordsHaveSamePrefix()
        {
            // Arrange
            string searchQuery = "abcdefgh";
            var firstPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "ab", ID = "plugin1" });
            var secondPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "abcd", ID = "plugin2" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                firstPlugin,
                secondPlugin,
            });

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);

            var firstQuery = pluginQueryPairs.GetValueOrDefault(firstPlugin);
            var secondQuery = pluginQueryPairs.GetValueOrDefault(secondPlugin);

            // Assert
            Assert.IsTrue(AreEqual(firstQuery, new Query(searchQuery, firstPlugin.Metadata.ActionKeyword)));
            Assert.IsTrue(AreEqual(secondQuery, new Query(searchQuery, secondPlugin.Metadata.ActionKeyword)));
        }

        [TestMethod]
        public void QueryBuilderShouldSetTermsCorrectlyWhenCalled()
        {
            // Arrange
            string searchQuery = "abcd efgh";
            var firstPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "ab", ID = "plugin1" });
            var secondPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "abcd", ID = "plugin2" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                firstPlugin,
                secondPlugin,
            });

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);

            var firstQuery = pluginQueryPairs.GetValueOrDefault(firstPlugin);
            var secondQuery = pluginQueryPairs.GetValueOrDefault(secondPlugin);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(firstQuery.Terms[0].Equals("cd", StringComparison.Ordinal) && firstQuery.Terms[1].Equals("efgh", StringComparison.Ordinal) && firstQuery.Terms.Count == 2);
            Assert.IsTrue(secondQuery.Terms[0].Equals("efgh", StringComparison.Ordinal) && secondQuery.Terms.Count == 1);
        }

        [TestMethod]
        public void QueryBuilderShouldReturnAllPluginsWithTheActionWord()
        {
            // Arrange
            string searchQuery = "!efgh";
            var firstPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "!", ID = "plugin1" });
            var secondPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "!", ID = "plugin2" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                firstPlugin,
                secondPlugin,
            });

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);

            // Assert
            Assert.AreEqual(2, pluginQueryPairs.Count);
        }
    }
}
