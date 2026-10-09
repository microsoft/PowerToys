// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    [TestMethod]
    public async Task OpenAllAppsPage_PublishesNewShortcutNamesWithoutNavigation()
    {
        var snapshot = new AppListItemSnapshot([], []);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        using var page = new AllAppsPage(source.Object, new FuzzyMatcherProvider(new()));
        var viewModel = CreateViewModel(page);
        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            await ObserveNextItemsUpdateAsync(viewModel, () => viewModel.SearchTextBox = "Deeps");

            foreach (var name in new[] { "Deeps", "On Level 3" })
            {
                var row = new AppListItem(new AppItem
                {
                    Name = name,
                    CatalogId = @"win32:C:\Commands\_PrintMe.exe|args:",
                    LaunchTarget = $@"C:\Desktop\{name}.lnk",
                    ResolvedTarget = @"C:\Commands\_PrintMe.exe",
                    MatchTerms = ["Deeps", "On Level 3"],
                    Shortcuts = [new("Deeps", @"C:\Desktop\Deeps.lnk"), new("On Level 3", @"C:\Desktop\On Level 3.lnk")],
                });
                snapshot = new AppListItemSnapshot([row], []);
                await ObserveNextItemsUpdateAsync(viewModel, () => source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty));

                Assert.AreEqual(1, viewModel.FilteredItems.Count);
                var result = viewModel.FilteredItems[0].Model.Unsafe as AppListItem;
                Assert.IsNotNull(result);
                Assert.AreEqual("Deeps", result.Title);
                Assert.AreEqual(row.Command!.Id, result.Command!.Id);
                Assert.AreSame(row.App, result.App, "An open page must adopt the new catalog row while retaining the matched shortcut name.");
            }

            await ObserveNextItemsUpdateAsync(viewModel, () => viewModel.SearchTextBox = "On Level 3");
            Assert.AreEqual(1, viewModel.FilteredItems.Count);
            Assert.AreSame(snapshot.VisibleItems[0], viewModel.FilteredItems[0].Model.Unsafe);
        }
        finally
        {
            viewModel.Dispose();
            viewModel.SafeCleanup();
        }
    }
}
