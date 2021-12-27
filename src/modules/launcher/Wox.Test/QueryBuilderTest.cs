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
            if (firstQuery is null && secondQuery is null)
            {
                return true;
            }

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

            var firstQueryText = "aSearch";
            var secondQueryText = "a Search";

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
        public void QueryBuildShouldGenerateQueriesOnlyForPluginsWhoseActionKeywordsAreLongestIfMultipleMatch()
        {
            // Arrange
            string firstSearchQuery = "MyKeyword";
            string secondSearchQuery = "My Keyword";
            var firstPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "My", ID = "plugin1" });
            var secondPlugin = new PluginPair(new PluginMetadata { ActionKeyword = "MyKey", ID = "plugin2" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                firstPlugin,
                secondPlugin,
            });

            // Act
            var firstSearchQueryPluginPair = QueryBuilder.Build(firstSearchQuery);
            var firstSearchQueryFirstPlugin = firstSearchQueryPluginPair.GetValueOrDefault(firstPlugin);
            var firstSearchQuerySecondPlugin = firstSearchQueryPluginPair.GetValueOrDefault(secondPlugin);

            var secondSearchQueryPluginPair = QueryBuilder.Build(secondSearchQuery);
            var secondSearchQueryFirstPlugin = secondSearchQueryPluginPair.GetValueOrDefault(firstPlugin);
            var secondSearchQuerySecondPlugin = secondSearchQueryPluginPair.GetValueOrDefault(secondPlugin);

            // Assert
            Assert.IsTrue(AreEqual(firstSearchQueryFirstPlugin, null));
            Assert.IsTrue(AreEqual(firstSearchQuerySecondPlugin, new Query(firstSearchQuery, secondPlugin.Metadata.ActionKeyword)));
            Assert.IsTrue(AreEqual(secondSearchQueryFirstPlugin, new Query(secondSearchQuery, firstPlugin.Metadata.ActionKeyword)));
            Assert.IsTrue(AreEqual(secondSearchQuerySecondPlugin, null));
        }

        [TestMethod]
        public void QueryBuilderShouldSetTermsCorrectlyWhenCalled()
        {
            // Arrange
            string searchQuery = "MyTest search term";
            var plugin = new PluginPair(new PluginMetadata { ActionKeyword = "My", ID = "plugin1" });
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                plugin,
            });

            // Act
            var pluginQueryPairs = QueryBuilder.Build(searchQuery);

            var builtQuery = pluginQueryPairs.GetValueOrDefault(plugin);

            // Assert
            // Using Ordinal since this is used internally
            Assert.IsTrue(builtQuery.Terms.Count == 3
                && builtQuery.Terms[0].Equals("Test", StringComparison.Ordinal)
                && builtQuery.Terms[1].Equals("search", StringComparison.Ordinal)
                && builtQuery.Terms[2].Equals("term", StringComparison.Ordinal));
        }

        [TestMethod]
        public void QueryBuilderShouldReturnAllPluginsWithTheActionWord()
        {
            // Arrange
            string searchQuery = "!Query";
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
