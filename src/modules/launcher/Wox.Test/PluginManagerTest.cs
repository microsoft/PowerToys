// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Test
{
    [TestFixture]
    public class PluginManagerTest
    {
        [TestCase(">", "dummyQueryText", "dummyTitle", "> dummyQueryText")]
        [TestCase(">", null, "dummyTitle", "> dummyTitle")]
        [TestCase(">", "", "dummyTitle", "> dummyTitle")]
        [TestCase("", "dummyQueryText", "dummyTitle", "dummyQueryText")]
        [TestCase("", null, "dummyTitle", "dummyTitle")]
        [TestCase("", "", "dummyTitle", "dummyTitle")]
        [TestCase(null, "dummyQueryText", "dummyTitle", "dummyQueryText")]
        [TestCase(null, null, "dummyTitle", "dummyTitle")]
        [TestCase(null, "", "dummyTitle", "dummyTitle")]
        public void QueryForPluginSetsActionKeywordWhenQueryTextDisplayIsEmpty(string actionKeyword, string queryTextDisplay, string title, string expectedResult)
        {
            // Arrange
            var query = new Query
            {
                ActionKeyword = actionKeyword,
            };
            var metadata = new PluginMetadata
            {
                ID = "dummyName",
                IcoPath = "dummyIcoPath",
                ExecuteFileName = "dummyExecuteFileName",
                PluginDirectory = "dummyPluginDirectory",
            };
            var result = new Result()
            {
                QueryTextDisplay = queryTextDisplay,
                Title = title,
            };
            var results = new List<Result>() { result };
            var pluginMock = new Mock<IPlugin>();
            pluginMock.Setup(r => r.Query(query)).Returns(results);
            var pluginPair = new PluginPair
            {
                Plugin = pluginMock.Object,
                Metadata = metadata,
            };

            // Act
            var queryOutput = PluginManager.QueryForPlugin(pluginPair, query);

            // Assert
            Assert.AreEqual(expectedResult, queryOutput[0].QueryTextDisplay);
        }
    }
}
