// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Test
{
    public class QueryBuilderTest
    {
        private bool AreEqual(Query firstQuery, Query secondQuery)
        {
            return firstQuery.ActionKeyword.Equals(secondQuery.ActionKeyword)
                && firstQuery.Search.Equals(secondQuery.Search)
                && firstQuery.RawQuery.Equals(secondQuery.RawQuery);
        }

        [Test]
        public void QueryBuilder_ShouldRemoveExtraSpaces_ForNonGlobalPlugin()
        {
            // Arrange
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { ">", new PluginPair { Metadata = new PluginMetadata { ActionKeywords = new List<string> { ">" } } } },
            };
            string searchQuery = ">   file.txt    file2 file3";

            // Act
            var pluginQueryPairs = QueryBuilder.Build(ref searchQuery, nonGlobalPlugins);

            // Assert
            Assert.AreEqual("> file.txt file2 file3", searchQuery);
        }

        [Test]
        public void QueryBuilder_ShouldRemoveExtraSpaces_ForDisabledNonGlobalPlugin()
        {
            // Arrange
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { ">", new PluginPair { Metadata = new PluginMetadata { ActionKeywords = new List<string> { ">" }, Disabled = true } } },
            };
            string searchQuery = ">   file.txt    file2 file3";

            // Act
            var pluginQueryPairs = QueryBuilder.Build(ref searchQuery, nonGlobalPlugins);

            // Assert
            Assert.AreEqual("> file.txt file2 file3", searchQuery);
        }

        [Test]
        public void QueryBuilder_ShouldRemoveExtraSpaces_ForGlobalPlugin()
        {
            // Arrange
            string searchQuery = "file.txt  file2  file3";

            // Act
            var pluginQueryPairs = QueryBuilder.Build(ref searchQuery, new Dictionary<string, PluginPair>());

            // Assert
            Assert.AreEqual("file.txt file2 file3", searchQuery);
        }

        [Test]
        public void QueryBuilder_ShouldGenerateSameQuery_IfEitherActionKeywordOrActionKeywordsListIsSet()
        {
            // Arrange
            string searchQuery = "> query";
            var firstPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeywords = new List<string> { ">" } } };
            var secondPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeyword = ">" } };

            var nonGlobalPluginWithActionKeywords = new Dictionary<string, PluginPair>
            {
                { ">", firstPlugin },
            };

            var nonGlobalPluginWithActionKeyword = new Dictionary<string, PluginPair>
            {
                { ">", secondPlugin },
            };
            string[] terms = { ">", "query" };
            Query expectedQuery = new Query("> query", "query", terms, ">");

            // Act
            var queriesForPluginsWithActionKeywords = QueryBuilder.Build(ref searchQuery, nonGlobalPluginWithActionKeywords);
            var queriesForPluginsWithActionKeyword = QueryBuilder.Build(ref searchQuery, nonGlobalPluginWithActionKeyword);

            var firstQuery = queriesForPluginsWithActionKeyword.GetValueOrDefault(firstPlugin);
            var secondQuery = queriesForPluginsWithActionKeywords.GetValueOrDefault(secondPlugin);

            // Assert
            Assert.IsTrue(AreEqual(firstQuery, expectedQuery));
            Assert.IsTrue(AreEqual(firstQuery, secondQuery));
        }

        [Test]
        public void QueryBuilder_ShouldGenerateCorrectQueries_ForPluginsWithMultipleActionKeywords()
        {
            // Arrange
            var plugin = new PluginPair { Metadata = new PluginMetadata { ActionKeywords = new List<string> { "a", "b" } } };
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { "a", plugin },
                { "b", plugin },
            };

            var firstQueryText = "asearch";
            var secondQueryText = "bsearch";

            // Act
            var firstPluginQueryPair = QueryBuilder.Build(ref firstQueryText, nonGlobalPlugins);
            var firstQuery = firstPluginQueryPair.GetValueOrDefault(plugin);

            var secondPluginQueryPairs = QueryBuilder.Build(ref secondQueryText, nonGlobalPlugins);
            var secondQuery = secondPluginQueryPairs.GetValueOrDefault(plugin);

            // Assert
            Assert.IsTrue(AreEqual(firstQuery, new Query { ActionKeyword = "a", RawQuery = "asearch", Search = "search" }));
            Assert.IsTrue(AreEqual(secondQuery, new Query { ActionKeyword = "b", RawQuery = "bsearch", Search = "search" }));
        }

        [Test]
        public void QueryBuild_ShouldGenerateSameSearchQuery_WithOrWithoutSpaceAfterActionKeyword()
        {
            // Arrange
            var plugin = new PluginPair { Metadata = new PluginMetadata { ActionKeywords = new List<string> { "a" } } };
            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { "a", plugin },
            };

            var firstQueryText = "asearch";
            var secondQueryText = "a search";

            // Act
            var firstPluginQueryPair = QueryBuilder.Build(ref firstQueryText, nonGlobalPlugins);
            var firstQuery = firstPluginQueryPair.GetValueOrDefault(plugin);

            var secondPluginQueryPairs = QueryBuilder.Build(ref secondQueryText, nonGlobalPlugins);
            var secondQuery = secondPluginQueryPairs.GetValueOrDefault(plugin);

            // Assert
            Assert.IsTrue(firstQuery.Search.Equals(secondQuery.Search));
            Assert.IsTrue(firstQuery.ActionKeyword.Equals(secondQuery.ActionKeyword));
        }

        [Test]
        public void QueryBuild_ShouldGenerateCorrectQuery_ForPluginsWhoseActionKeywordsHaveSamePrefix()
        {
            // Arrange
            string searchQuery = "abcdefgh";
            var firstPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeyword = "ab", ID = "plugin1" } };
            var secondPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeyword = "abcd", ID = "plugin2" } };

            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { "ab", firstPlugin },
                { "abcd", secondPlugin },
            };

            // Act
            var pluginQueryPairs = QueryBuilder.Build(ref searchQuery, nonGlobalPlugins);

            var firstQuery = pluginQueryPairs.GetValueOrDefault(firstPlugin);
            var secondQuery = pluginQueryPairs.GetValueOrDefault(secondPlugin);

            // Assert
            Assert.IsTrue(AreEqual(firstQuery, new Query { RawQuery = searchQuery, Search = searchQuery.Substring(firstPlugin.Metadata.ActionKeyword.Length), ActionKeyword = firstPlugin.Metadata.ActionKeyword } ));
            Assert.IsTrue(AreEqual(secondQuery, new Query { RawQuery = searchQuery, Search = searchQuery.Substring(secondPlugin.Metadata.ActionKeyword.Length), ActionKeyword = secondPlugin.Metadata.ActionKeyword }));
        }

        [Test]
        public void QueryBuilder_ShouldSetTermsCorrently_WhenCalled()
        {
            // Arrange
            string searchQuery = "abcd efgh";
            var firstPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeyword = "ab", ID = "plugin1" } };
            var secondPlugin = new PluginPair { Metadata = new PluginMetadata { ActionKeyword = "abcd", ID = "plugin2" } };

            var nonGlobalPlugins = new Dictionary<string, PluginPair>
            {
                { "ab", firstPlugin },
                { "abcd", secondPlugin },
            };

            // Act
            var pluginQueryPairs = QueryBuilder.Build(ref searchQuery, nonGlobalPlugins);

            var firstQuery = pluginQueryPairs.GetValueOrDefault(firstPlugin);
            var secondQuery = pluginQueryPairs.GetValueOrDefault(secondPlugin);

            // Assert
            Assert.IsTrue(firstQuery.Terms[0].Equals("cd") && firstQuery.Terms[1].Equals("efgh") && firstQuery.Terms.Length == 2);
            Assert.IsTrue(secondQuery.Terms[0].Equals("efgh") && secondQuery.Terms.Length == 1);
        }
    }
}
