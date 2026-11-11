// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class RemoteDesktopCommandProviderTests
{
    [TestMethod]
    public void ProviderHasCorrectId()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Assert
        Assert.AreEqual("com.microsoft.cmdpal.builtin.remotedesktop", provider.Id);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsGreaterThan(0, provider.DisplayName.Length);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsNotEmpty(commands);
    }

    [TestMethod]
    public void FallbackCommandsNotEmpty()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Act
        var commands = provider.FallbackCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsNotEmpty(commands);
    }

    [TestMethod]
    public void TopLevelCommandsContainListPageCommand()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.HasCount(1, commands);
        Assert.IsInstanceOfType(commands.Single().Command, typeof(RemoteDesktopListPage));
    }

    [TestMethod]
    public void FallbackCommandsContainFallbackItem()
    {
        // Setup
        var provider = new RemoteDesktopCommandProvider();

        // Act
        var commands = provider.FallbackCommands();

        // Assert
        Assert.HasCount(1, commands);
        Assert.IsInstanceOfType(commands.Single(), typeof(FallbackRemoteDesktopItem));
    }
}
