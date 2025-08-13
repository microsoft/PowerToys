// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsCommandProviderTests
{
    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }

    [TestMethod]
    public void LookupAppReturnsValidResult()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act - try to lookup a common app
        var result = provider.LookupApp("notepad");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void LookupAppWithEmptyNameReturnsNull()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act
        var result = provider.LookupApp(string.Empty);

        // Assert
        Assert.IsNull(result);
    }
}
