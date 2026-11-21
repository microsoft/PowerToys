// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerLauncher.Plugin;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestClass]
    public class HistoryPluginTest
    {
        [TestMethod]
        public void QueryShouldSkipHistoryItemsOfMissingPlugins()
        {
            // Arrange
            var metadata = new PluginMetadata
            {
                ID = "installedPluginId",
                Name = "installedPlugin",
                IcoPathDark = "dummyIcoPath",
                IcoPathLight = "dummyIcoPath",
                ExecuteFileName = "dummyExecuteFileName",
                PluginDirectory = "dummyPluginDirectory",
            };
            var pluginMock = new Mock<IPlugin>();
            pluginMock.Setup(r => r.Query(It.IsAny<Query>())).Returns(() => new List<Result> { new Result { Title = "installedTitle", SubTitle = "installedSubTitle" } });
            PluginManager.SetAllPlugins(new List<PluginPair>
            {
                new PluginPair(metadata)
                {
                    Plugin = pluginMock.Object,
                    IsPluginInitialized = true,
                },
            });

            var query = new Query("!!", "!!")
            {
                SelectedItems = new Dictionary<string, UserSelectedRecord.UserSelectedRecordItem>
                {
                    ["installed"] = new UserSelectedRecord.UserSelectedRecordItem
                    {
                        PluginID = "installedPluginId",
                        Title = "installedTitle",
                        SubTitle = "installedSubTitle",
                        Search = "installedSearch",
                        LastSelected = new DateTime(2024, 1, 1),
                    },
                    ["missing"] = new UserSelectedRecord.UserSelectedRecordItem
                    {
                        PluginID = "missingPluginId",
                        Title = "missingTitle",
                        SubTitle = "missingSubTitle",
                        Search = "missingSearch",
                        LastSelected = new DateTime(2024, 1, 2),
                    },
                },
            };

            var historyPlugin = new Microsoft.PowerToys.Run.Plugin.History.Main();

            // Act
            var results = historyPlugin.Query(query);

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("installedTitle", results[0].Title);
            Assert.AreEqual(2, query.SelectedItems.Count);
        }
    }
}
