// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[TestClass]
public class RdpConnectionsManagerTests
{
    [TestMethod]
    public void Constructor_AddsOpenCommandItem()
    {
        // Act
        var manager = new RdpConnectionsManager(new MockSettingsManager(["test.local"]));

        // Assert
        Assert.IsTrue(manager.Connections.Any(item => string.IsNullOrEmpty(item.ConnectionName)));
    }

    [TestMethod]
    public void FindConnection_ReturnsExactMatch()
    {
        // Arrange
        var connectionName = "rdp-test";
        var connection = new ConnectionListItem(connectionName);

        // Act
        var result = ConnectionHelpers.FindConnection(connectionName, new[] { connection });

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(connectionName, result.ConnectionName);
    }

    [TestMethod]
    public void FindConnection_ReturnsNullForWhitespaceQuery()
    {
        // Arrange
        var connection = new ConnectionListItem("rdp-test");

        // Act
        var result = ConnectionHelpers.FindConnection("   ", new[] { connection });

        // Assert
        Assert.IsNull(result);
    }
}
