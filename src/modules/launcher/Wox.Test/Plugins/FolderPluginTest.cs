// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Plugin.Folder;
using Moq;
using NUnit.Framework;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    internal class FolderPluginTest
    {
        [Test]
        public void ContextMenuLoader_ReturnContextMenuForFolderWithOpenInConsole_WhenLoadContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(api => api.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());
            var pluginInitContext = new PluginInitContext() { API = mock.Object };
            var contextMenuLoader = new ContextMenuLoader(pluginInitContext);
            var searchResult = new SearchResult() { Type = ResultType.Folder, FullPath = "C:/DummyFolder" };
            var result = new Result() { ContextData = searchResult };

            // Act
            List<ContextMenuResult> contextMenuResults = contextMenuLoader.LoadContextMenus(result);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 2);
            mock.Verify(x => x.GetTranslation("Microsoft_plugin_folder_copy_path"), Times.Once());
            mock.Verify(x => x.GetTranslation("Microsoft_plugin_folder_open_in_console"), Times.Once());
        }

        [Test]
        public void ContextMenuLoader_ReturnContextMenuForFileWithOpenInConsole_WhenLoadContextMenusIsCalled()
        {
            // Arrange
            var mock = new Mock<IPublicAPI>();
            mock.Setup(api => api.GetTranslation(It.IsAny<string>())).Returns(It.IsAny<string>());
            var pluginInitContext = new PluginInitContext() { API = mock.Object };
            var contextMenuLoader = new ContextMenuLoader(pluginInitContext);
            var searchResult = new SearchResult() { Type = ResultType.File, FullPath = "C:/DummyFile.cs" };
            var result = new Result() { ContextData = searchResult };

            // Act
            List<ContextMenuResult> contextMenuResults = contextMenuLoader.LoadContextMenus(result);

            // Assert
            Assert.AreEqual(contextMenuResults.Count, 3);
            mock.Verify(x => x.GetTranslation("Microsoft_plugin_folder_open_containing_folder"), Times.Once());
            mock.Verify(x => x.GetTranslation("Microsoft_plugin_folder_copy_path"), Times.Once());
            mock.Verify(x => x.GetTranslation("Microsoft_plugin_folder_open_in_console"), Times.Once());
        }
    }
}
