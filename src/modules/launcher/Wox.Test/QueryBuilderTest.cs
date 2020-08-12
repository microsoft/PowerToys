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
    }
}
