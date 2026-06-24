// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[TestClass]
public class SettingsManagerTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public async Task HistoryChangedEventIsRaisedWhenItemIsAdded()
    {
        // Setup
        var settings = new MockSettingsInterface(historyItemCount: 5);
        var browserInfoService = new MockBrowserInfoService();

        var page = new WebSearchListPage(settings, browserInfoService);

        var eventRaised = false;

        try
        {
            settings.HistoryChanged += Handler;

            // Act
            settings.AddHistoryItem(new HistoryItem("test event", DateTime.UtcNow));
            await Task.Delay(50);

            // Assert
            Assert.IsTrue(eventRaised, "Expected HistoryChanged to be raised when saving history.");
        }
        finally
        {
            settings.HistoryChanged -= Handler;
            page.Dispose();
        }

        return;

        void Handler(object s, EventArgs e) => eventRaised = true;
    }
}
