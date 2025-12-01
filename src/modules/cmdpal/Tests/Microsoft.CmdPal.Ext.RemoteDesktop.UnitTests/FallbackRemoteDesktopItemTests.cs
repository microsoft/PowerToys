// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[TestClass]
public class FallbackRemoteDesktopItemTests
{
    private static readonly CompositeFormat OpenHostCompositeFormat = CompositeFormat.Parse(Resources.remotedesktop_open_host);

    [TestMethod]
    public void UpdateQuery_WhenMatchingConnectionExists_UsesConnectionName()
    {
        var connectionName = "my-rdp-server";

        // Arrange
        var setup = CreateFallback(connectionName);
        var fallback = setup.Fallback;

        // Act
        fallback.UpdateQuery("my-rdp-server");

        // Assert
        Assert.AreEqual(connectionName, fallback.Title);
        var expectedSubtitle = string.Format(CultureInfo.CurrentCulture, OpenHostCompositeFormat, connectionName);
        Assert.AreEqual(expectedSubtitle, fallback.Subtitle);

        var command = fallback.Command as OpenRemoteDesktopCommand;
        Assert.IsNotNull(command);
        Assert.AreEqual(Resources.remotedesktop_command_connect, command.Name);
        Assert.AreEqual(connectionName, GetCommandHost(command));
    }

    [TestMethod]
    public void UpdateQuery_WhenQueryIsValidHostWithoutExistingConnection_UsesQuery()
    {
        // Arrange
        var setup = CreateFallback();
        var fallback = setup.Fallback;
        const string hostname = "test.corp";

        // Act
        fallback.UpdateQuery(hostname);

        // Assert
        var expectedTitle = string.Format(CultureInfo.CurrentCulture, OpenHostCompositeFormat, hostname);
        Assert.AreEqual(expectedTitle, fallback.Title);
        Assert.AreEqual(Resources.remotedesktop_title, fallback.Subtitle);

        var command = fallback.Command as OpenRemoteDesktopCommand;
        Assert.IsNotNull(command);
        Assert.AreEqual(Resources.remotedesktop_command_connect, command.Name);
        Assert.AreEqual(hostname, GetCommandHost(command));
    }

    [TestMethod]
    public void UpdateQuery_WhenQueryIsWhitespace_ResetsCommand()
    {
        // Arrange
        var setup = CreateFallback("rdp-server-two");
        var fallback = setup.Fallback;

        // Act
        fallback.UpdateQuery("   ");

        // Assert
        Assert.AreEqual(Resources.remotedesktop_command_open, fallback.Title);
        Assert.AreEqual(string.Empty, fallback.Subtitle);

        var command = fallback.Command as OpenRemoteDesktopCommand;
        Assert.IsNotNull(command);
        Assert.AreEqual(Resources.remotedesktop_command_open, command.Name);
        Assert.AreEqual(string.Empty, GetCommandHost(command));
    }

    [TestMethod]
    public void UpdateQuery_WhenQueryIsInvalidHost_ClearsCommand()
    {
        // Arrange
        var setup = CreateFallback("rdp-server-three");
        var fallback = setup.Fallback;

        // Act
        fallback.UpdateQuery("not a valid host");

        // Assert
        Assert.AreEqual(Resources.remotedesktop_command_open, fallback.Title);
        Assert.AreEqual(string.Empty, fallback.Subtitle);

        var command = fallback.Command as OpenRemoteDesktopCommand;
        Assert.IsNotNull(command);
        Assert.AreEqual(Resources.remotedesktop_command_open, command.Name);
        Assert.AreEqual(string.Empty, GetCommandHost(command));
    }

    private static string GetCommandHost(OpenRemoteDesktopCommand command)
    {
        var field = typeof(OpenRemoteDesktopCommand).GetField("_rdpHost", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null)
        {
            return string.Empty;
        }

        return field.GetValue(command) as string ?? string.Empty;
    }

    private static (FallbackRemoteDesktopItem Fallback, IRdpConnectionsManager Manager) CreateFallback(params string[] connectionNames)
    {
        var settingsManager = new MockSettingsManager(connectionNames);
        var connectionsManager = new MockRdpConnectionsManager(settingsManager);

        var fallback = new FallbackRemoteDesktopItem(connectionsManager);

        return (fallback, connectionsManager);
    }
}
