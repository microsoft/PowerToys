// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    [DataRow("microsoft")]
    [DataRow("windows")]
    public async Task SearchInWebSearchPage(string query)
    {
        // Setup
        var settings = new MockSettingsInterface();
        var browserInfoService = new MockBrowserInfoService();

        var page = new WebSearchListPage(settings, browserInfoService);

        // Act
        page.UpdateSearchText(string.Empty, query);
        await Task.Delay(1000, TestContext.CancellationToken);

        var listItem = page.GetItems();
        Assert.IsNotNull(listItem);
        Assert.HasCount(1, listItem);

        var expectedItem = listItem.FirstOrDefault();

        Assert.IsNotNull(expectedItem);
        Assert.Contains("Search the web in", expectedItem.Subtitle, $"Expected \"search the web in chrome/edge\" but got {expectedItem.Subtitle}");
        Assert.AreEqual(query, expectedItem.Title);
    }

    [TestMethod]
    public async Task HistoryReturnsExpectedItems()
    {
        // Setup
        var mockHistoryItems = new List<HistoryItem>
        {
            new HistoryItem("test search", DateTime.Parse("2024-01-01 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search", DateTime.Parse("2024-01-02 13:00:00", CultureInfo.CurrentCulture)),
        };

        var settings = new MockSettingsInterface(mockHistory: mockHistoryItems, historyItemCount: 5);
        var browserInfoService = new MockBrowserInfoService();

        var page = new WebSearchListPage(settings, browserInfoService);

        // Act
        page.UpdateSearchText("abcdef", string.Empty);
        await Task.Delay(1000, TestContext.CancellationToken);

        var listItem = page.GetItems();

        // Assert
        Assert.IsNotNull(listItem);
        Assert.HasCount(2, listItem);

        foreach (var item in listItem)
        {
            Assert.IsNotNull(item);
            Assert.IsNotEmpty(item.Title);
            Assert.IsNotEmpty(item.Subtitle);
        }
    }

    [TestMethod]
    public async Task HistoryExceedingLimitReturnsMaxItems()
    {
        // Setup
        var mockHistoryItems = new List<HistoryItem>
        {
            new HistoryItem("test search", DateTime.Parse("2024-01-01 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search1", DateTime.Parse("2024-01-02 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search2", DateTime.Parse("2024-01-03 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search3", DateTime.Parse("2024-01-04 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search4", DateTime.Parse("2024-01-05 13:00:00", CultureInfo.CurrentCulture)),
        };

        var settings = new MockSettingsInterface(mockHistory: mockHistoryItems, historyItemCount: 5);
        var browserInfoService = new MockBrowserInfoService();

        var page = new WebSearchListPage(settings, browserInfoService);

        mockHistoryItems.Add(new HistoryItem("another search5", DateTime.Parse("2024-01-06 13:00:00", CultureInfo.CurrentCulture)));

        // Act
        page.UpdateSearchText("abcdef", string.Empty);
        await Task.Delay(1000, TestContext.CancellationToken);

        var listItem = page.GetItems();

        // Assert
        Assert.IsNotNull(listItem);

        // Make sure only load five item.
        Assert.HasCount(5, listItem);
    }

    [TestMethod]
    public async Task HistoryWhenSetToNoneReturnEmptyList()
    {
        // Setup
        var mockHistoryItems = new List<HistoryItem>
        {
            new HistoryItem("test search", DateTime.Parse("2024-01-01 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search1", DateTime.Parse("2024-01-02 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search2", DateTime.Parse("2024-01-03 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search3", DateTime.Parse("2024-01-04 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search4", DateTime.Parse("2024-01-05 13:00:00", CultureInfo.CurrentCulture)),
            new HistoryItem("another search5", DateTime.Parse("2024-01-06 13:00:00", CultureInfo.CurrentCulture)),
        };

        var settings = new MockSettingsInterface(mockHistory: mockHistoryItems, historyItemCount: 0);
        var browserInfoService = new MockBrowserInfoService();

        var page = new WebSearchListPage(settings, browserInfoService);

        // Act
        page.UpdateSearchText("abcdef", string.Empty);
        await Task.Delay(1000, TestContext.CancellationToken);

        var listItem = page.GetItems();

        // Assert
        Assert.IsNotNull(listItem);

        // Make sure only load five item.
        Assert.IsEmpty(listItem);
    }

    public TestContext TestContext { get; set; } = null!;
}
