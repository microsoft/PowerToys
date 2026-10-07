// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsCommandProviderTests : AppsTestBase
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void CatalogChanges_ReportProviderNotificationCounts()
    {
        using var catalog = new MockAppCatalog();
        using var source = new AppListItemSource(catalog, Settings);
        using var page = new AllAppsPage(source, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source, Settings);
        var notifications = 0;
        provider.ItemsChanged += (_, _) => notifications++;

        const int publicationCount = 30;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < publicationCount; i++)
        {
            catalog.AddWin32Program(TestDataHelper.CreateTestWin32Metadata($"App {i}", $@"C:\Apps\app{i}.exe"));
        }

        stopwatch.Stop();
        Assert.AreEqual(publicationCount, notifications);
        TestContext.WriteLine($"{publicationCount} separate catalog publications produced {notifications} provider notifications in {stopwatch.Elapsed.TotalMilliseconds:F3} ms. Host reload coalescing is outside this measurement.");

        catalog.SetRefreshing(true);
        catalog.SetRefreshing(false);
        Assert.AreEqual(publicationCount, notifications, "Refresh status alone must not request command reloads.");
    }

    [TestMethod]
    public async Task CatalogPublication_ReloadsUnresolvedCommandsAndStopsAfterDisposal()
    {
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var catalog = new MockAppCatalog();
        catalog.DeferInitialization(initialized.Task);
        using var source = new AppListItemSource(catalog, Settings);
        using var page = new AllAppsPage(source, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source, Settings);
        var program = TestDataHelper.CreateTestWin32Metadata("Editor");
        var id = new AppListItem(Catalog.Win32AppPayload.From(program).ToAppItem()).Command.Id;
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
        using var page = new AllAppsPage(source, TestDataHelper.CreateFuzzyMatcherProvider());
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
