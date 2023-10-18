// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerLauncher.Plugin;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [STATestClass]
    public class ResultViewModelTest
    {
        private ContextMenuResult contextMenuResult;
        private Mock<IMainViewModel> mainViewModelMock;
        private ResultViewModel resultViewModel;

        [TestInitialize]
        public void Setup()
        {
            var result = new Result();
            contextMenuResult = new ContextMenuResult();
            mainViewModelMock = new Mock<IMainViewModel>();
            resultViewModel = new ResultViewModel(result, mainViewModelMock.Object, null);

            var pluginMock = new Mock<IPlugin>();
            pluginMock.As<IContextMenu>().Setup(x => x.LoadContextMenus(result)).Returns(new List<ContextMenuResult> { contextMenuResult });

            var pair = new PluginPair(new PluginMetadata());
            pair.Plugin = pluginMock.Object;
            PluginManager.SetAllPlugins(new List<PluginPair>()
            {
                pair,
            });
            PluginManager.InitializePlugins(new Mock<IPublicAPI>().Object);
        }

        [TestMethod]
        public void ExecuteContextMenuResultActionThatReturnsTrueShouldHideTheMainView()
        {
            // Arrange
            contextMenuResult.Action = _ => true;

            // Act
            resultViewModel.LoadContextMenu();
            resultViewModel.ContextMenuItems.Single().Command.Execute(null);

            // Assert
            mainViewModelMock.Verify(x => x.Hide(), Times.Once());
        }

        [TestMethod]
        public void ExecuteContextMenuResultActionThatReturnsFalseShouldNotHideTheMainView()
        {
            // Arrange
            contextMenuResult.Action = _ => false;

            // Act
            resultViewModel.LoadContextMenu();
            resultViewModel.ContextMenuItems.Single().Command.Execute(null);

            // Assert
            mainViewModelMock.Verify(x => x.Hide(), Times.Never());
        }
    }
}
