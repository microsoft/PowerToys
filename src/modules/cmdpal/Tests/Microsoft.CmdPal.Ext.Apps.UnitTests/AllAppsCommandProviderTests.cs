// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsCommandProviderTests : AppsTestBase
{
    [TestMethod]
    public async Task CatalogPublication_ReloadsUnresolvedCommandsAndStopsAfterDisposal()
    {
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var catalog = new MockAppCatalog();
        catalog.DeferInitialization(initialized.Task);
        using var source = new AppListItemSource(catalog, Settings);
        using var page = new AllAppsPage(source);
        using var provider = new AllAppsCommandProvider(page, source, Settings);
        var program = TestDataHelper.CreateTestWin32Program("Editor");
        var id = new AppListItem(Catalog.Win32AppPayload.From(program).ToAppItem(), false).Command.Id;
        var notifications = 0;
        provider.ItemsChanged += (_, _) => notifications++;

        Assert.IsNull(provider.GetCommandItem(id));
        catalog.AddWin32Program(program);
        Assert.AreEqual(1, notifications);
        Assert.IsNotNull(provider.GetCommandItem(id));

        initialized.SetResult();
        await WaitForPageInitializationAsync(page);
        Assert.AreEqual(1, notifications);

        provider.Dispose();
        catalog.ClearAll();
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public async Task EmptyCatalogInitialization_DoesNotReloadProviderCommands()
    {
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var catalog = new MockAppCatalog();
        catalog.DeferInitialization(initialized.Task);
        using var source = new AppListItemSource(catalog, Settings);
        using var page = new AllAppsPage(source);
        using var provider = new AllAppsCommandProvider(page, source, Settings);
        var notifications = 0;
        provider.ItemsChanged += (_, _) => notifications++;

        Assert.IsNull(provider.GetCommandItem("missing"));
        initialized.SetResult();
        await WaitForPageInitializationAsync(page);

        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var provider = new AllAppsCommandProvider(Page, AppListItemSource, Settings);

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var provider = new AllAppsCommandProvider(Page, AppListItemSource, Settings);

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var provider = new AllAppsCommandProvider(Page, AppListItemSource, Settings);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }

    [TestMethod]
    public void TopLevelCommandsIncludeRefreshCommand()
    {
        // Arrange
        var provider = new AllAppsCommandProvider(Page, AppListItemSource, Settings);

        // Act
        var refreshCommand = provider.TopLevelCommands()
            .Single()
            .MoreCommands
            .OfType<CommandContextItem>()
            .Single(item => item.Command.Name == Properties.Resources.refresh_app_list);

        // Assert
        Assert.IsNotNull(refreshCommand);
    }

    [TestMethod]
    public void ProviderWithMockData_TopLevelCommands_IncludesListItem()
    {
        // Arrange
        var provider = new AllAppsCommandProvider(Page, AppListItemSource, Settings);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length >= 1); // At least the list item should be present
    }
}
