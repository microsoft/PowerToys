// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.UnitTests;

[TestClass]
public class RemoteDesktopListPageTests
{
    // Helper: create a RemoteDesktopListPage backed by mock connections.
    private static RemoteDesktopListPage CreatePage(params string[] connectionNames)
    {
        var settingsManager = new MockSettingsManager(connectionNames);
        var connectionsManager = new MockRdpConnectionsManager(settingsManager);
        return new RemoteDesktopListPage(connectionsManager);
    }

    [TestMethod]
    public void EmptyQuery_ReturnsOnlyExistingConnections()
    {
        // Arrange
        var page = CreatePage("server1", "server2");

        // Act
        page.UpdateSearchText(string.Empty, string.Empty);
        var items = page.GetItems();

        // Assert — only the 2 preexisting connections, no arbitrary host item
        Assert.AreEqual(2, items.Length);
    }

    [TestMethod]
    public void WhitespaceQuery_ReturnsOnlyExistingConnections()
    {
        // Arrange
        var page = CreatePage("server1");

        // Act
        page.UpdateSearchText(string.Empty, "   ");
        var items = page.GetItems();

        // Assert
        Assert.AreEqual(1, items.Length);
    }

    [TestMethod]
    public void ValidHostname_NotMatchingConnection_PrependsArbitraryItem()
    {
        // Arrange
        var page = CreatePage("server1");

        // Act
        page.UpdateSearchText(string.Empty, "test.corp");
        var items = page.GetItems();

        // Assert — arbitrary item prepended, then the existing connection
        Assert.AreEqual(2, items.Length);
        var firstItem = items[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem, "First item should be a ConnectionListItem for the arbitrary host");
        Assert.AreEqual("test.corp", firstItem.ConnectionName);
    }

    [TestMethod]
    public void QueryExactlyMatchingExistingConnection_NoArbitraryItem()
    {
        // Arrange
        var page = CreatePage("rdp-server");

        // Act
        page.UpdateSearchText(string.Empty, "rdp-server");
        var items = page.GetItems();

        // Assert — no extra item; count stays at 1
        Assert.AreEqual(1, items.Length);
        var item = items[0] as ConnectionListItem;
        Assert.IsNotNull(item);
        Assert.AreEqual("rdp-server", item.ConnectionName);
    }

    [TestMethod]
    public void InvalidHostname_ReturnsOnlyExistingConnections()
    {
        // Arrange
        var page = CreatePage("server1");

        // Act
        page.UpdateSearchText(string.Empty, "!!!invalid");
        var items = page.GetItems();

        // Assert — no arbitrary item for an invalid hostname
        Assert.AreEqual(1, items.Length);
    }

    [TestMethod]
    public void ValidHostnameWithPort_ArbitraryItemIncludesPort()
    {
        // Arrange
        var page = CreatePage("server1");

        // Act
        page.UpdateSearchText(string.Empty, "localhost:3389");
        var items = page.GetItems();

        // Assert — full host:port string preserved as the ConnectionName
        Assert.AreEqual(2, items.Length);
        var firstItem = items[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem);
        Assert.AreEqual("localhost:3389", firstItem.ConnectionName);
    }

    [TestMethod]
    public void SequentialCalls_ValidThenEmpty_ClearsArbitraryItem()
    {
        // Arrange
        var page = CreatePage("server1");

        // Act — first call adds arbitrary item
        page.UpdateSearchText(string.Empty, "test.corp");
        var itemsAfterValid = page.GetItems();

        // Assert — arbitrary item present
        Assert.AreEqual(2, itemsAfterValid.Length);
        var firstItem = itemsAfterValid[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem);
        Assert.AreEqual("test.corp", firstItem.ConnectionName);

        // Act — second call clears it
        page.UpdateSearchText("test.corp", string.Empty);
        var itemsAfterEmpty = page.GetItems();

        // Assert — back to only existing connections
        Assert.AreEqual(1, itemsAfterEmpty.Length);
    }

    [TestMethod]
    public void ValidHostname_NoExistingConnections_ReturnsSingleArbitraryItem()
    {
        // Arrange — no preexisting connections
        var page = CreatePage();

        // Act
        page.UpdateSearchText(string.Empty, "alpha.corp");
        var items = page.GetItems();

        // Assert
        Assert.AreEqual(1, items.Length);
        var firstItem = items[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem);
        Assert.AreEqual("alpha.corp", firstItem.ConnectionName);
    }

    [TestMethod]
    public void IPv4Address_ReturnsArbitraryItem()
    {
        // Arrange
        var page = CreatePage();

        // Act
        page.UpdateSearchText(string.Empty, "192.168.1.100");
        var items = page.GetItems();

        // Assert
        Assert.AreEqual(1, items.Length);
        var firstItem = items[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem);
        Assert.AreEqual("192.168.1.100", firstItem.ConnectionName);
    }

    [TestMethod]
    public void IPv4AddressWithPort_ReturnsArbitraryItemWithPort()
    {
        // Arrange
        var page = CreatePage();

        // Act
        page.UpdateSearchText(string.Empty, "192.168.1.100:3390");
        var items = page.GetItems();

        // Assert — full IP:port string preserved
        Assert.AreEqual(1, items.Length);
        var firstItem = items[0] as ConnectionListItem;
        Assert.IsNotNull(firstItem);
        Assert.AreEqual("192.168.1.100:3390", firstItem.ConnectionName);
    }
}
