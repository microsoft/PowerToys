// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[TestClass]
public class RDPConnectionsManagerTests
{
    [TestMethod]
    public void Constructor_AddsOpenCommandItem()
    {
        // Act
        var manager = new RDPConnectionsManager(new MockSettingsManager(["testhome.local"]));

        // Assert
        Assert.IsTrue(manager.Connections.Any(item => string.IsNullOrEmpty(item.ConnectionName)));
    }

    [TestMethod]
    public void FindConnection_ReturnsExactMatch()
    {
        // Arrange
        var connection = new ConnectionListItem("rdp-contoso");

        // Act
        var result = RDPConnectionsManager.FindConnection("rdp-contoso", new[] { connection });

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("rdp-contoso", result.ConnectionName);
    }

    [TestMethod]
    public void FindConnection_ReturnsNullForWhitespaceQuery()
    {
        // Arrange
        var connection = new ConnectionListItem("rdp-fabrikam");

        // Act
        var result = RDPConnectionsManager.FindConnection("   ", new[] { connection });

        // Assert
        Assert.IsNull(result);
    }
}
