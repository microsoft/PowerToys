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
        [Test]
        public void QueryForPlugin_SetsActionKeyword_WhenQueryTextDisplayIsSet()
        {
            // Arrange
            var actionKeyword = ">";
            var title = "dummyTitle";
            var queryTextDisplay = "dummyQueryTextDisplay";
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
            Assert.AreEqual(string.Format("{0} {1}", ">", queryTextDisplay), queryOutput[0].QueryTextDisplay);
        }

        [TestCase("")]
        [TestCase(null)]
        public void QueryForPlugin_SetsActionKeyword_WhenQueryTextDisplayIsEmpty(string queryTextDisplay)
        {
            // Arrange
            var actionKeyword = ">";
            var title = "dummyTitle";
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
            Assert.AreEqual(string.Format("{0} {1}", ">", title), queryOutput[0].QueryTextDisplay);
        }
    }
}
