// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class CatalogCommandIdUpgradeTests
{
    [TestMethod]
    public void CatalogCoalescingKeepsTheIdIssuedBeforeCatalogAdoption()
    {
        const string originalIdentity = @"win32:C:\Apps\Editor.exe|args:";
        var issuedId = AppIdentity.ForCommand(originalIdentity);
        var sourceItem = new AppCatalogItem(
            originalIdentity,
            20,
            new AppCatalogSourceReference("start-menu", @"C:\Links\Editor.lnk"),
            [],
            new Win32AppPayload { Name = "Editor" });
        var canonical = sourceItem.WithIdentity(AppIdentity.ForPackaged("Contoso.Editor_123!Main"));
        var app = canonical.ToAppItem();
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);

        Assert.AreNotEqual(issuedId, row.Command!.Id);
        Assert.AreSame(row, snapshot.GetApp(issuedId));
        Assert.AreEqual(issuedId, snapshot.GetCommandItem(issuedId)?.Command?.Id);
        Assert.AreEqual(AppIdentity.ForCommand(canonical.Identity), row.Command.Id);
    }
}
