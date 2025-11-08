// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Pages;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CmdPal.Ext.RemoteDesktop.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[TestClass]
public class RemoteDesktopListPageTests
{
    [TestMethod]
    public void ToCommandItem_PopulatesExpectedMetadata()
    {
        // Arrange
        var setup = CreatePage();
        var page = setup.Page;

        // Act
        var commandItem = page.ToCommandItem();

        // Assert
        Assert.AreEqual(Resources.remotedesktop_title, commandItem.Title);
        Assert.AreEqual(Resources.remotedesktop_subtitle, commandItem.Subtitle);
        Assert.IsNotNull(commandItem.Icon);
        Assert.IsNotNull(commandItem.MoreCommands);
        Assert.AreEqual(1, commandItem.MoreCommands.Length);
        Assert.IsInstanceOfType(commandItem.MoreCommands[0], typeof(CommandContextItem));

        var contextItem = (CommandContextItem)commandItem.MoreCommands[0];
        Assert.IsInstanceOfType(contextItem.Command, typeof(IContentPage));
    }

    private static (RemoteDesktopListPage Page, ServiceProvider Provider, IRDPConnectionManager Manager) CreatePage(params string[] connectionNames)
    {
        var settingsManager = new MockSettingsManager(connectionNames);
        var connectionsManager = new MockRDPConnectionsManager(settingsManager);

        var services = new ServiceCollection();
        services.AddSingleton<ISettingsInterface>(settingsManager);
        services.AddSingleton<IRDPConnectionManager>(connectionsManager);

        var provider = services.BuildServiceProvider();
        var page = new RemoteDesktopListPage(provider);

        return (page, provider, connectionsManager);
    }
}
