// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void ValidatePageCreation()
    {
        // Setup
        var page = new AllAppsPage();

        // Assert
        Assert.IsNotNull(page);
        Assert.IsNotNull(page.Name);
        Assert.IsNotNull(page.Icon);
    }

    [TestMethod]
    public void ValidateGetItems()
    {
        // Setup
        var page = new AllAppsPage();

        // Act - wait a bit for async loading
        System.Threading.Thread.Sleep(2000);
        var resultList = page.GetItems();

        // Assert - Just verify the page doesn't crash and returns some structure
        Assert.IsNotNull(resultList);
    }

    [TestMethod]
    public void ValidatePageProperties()
    {
        // Setup
        var page = new AllAppsPage();

        // Assert
        Assert.IsTrue(page.ShowDetails);
        Assert.IsNotNull(page.PlaceholderText);
    }
}
