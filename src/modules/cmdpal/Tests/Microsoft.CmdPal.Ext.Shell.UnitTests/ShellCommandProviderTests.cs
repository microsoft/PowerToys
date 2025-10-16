// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class ShellCommandProviderTests
{
    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var mockHistoryService = new Mock<IRunHistoryService>();
        var provider = new ShellCommandsProvider(mockHistoryService.Object, telemetryService: null);

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var mockHistoryService = new Mock<IRunHistoryService>();
        var provider = new ShellCommandsProvider(mockHistoryService.Object, telemetryService: null);

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var mockHistoryService = new Mock<IRunHistoryService>();
        var provider = new ShellCommandsProvider(mockHistoryService.Object, telemetryService: null);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }
}
