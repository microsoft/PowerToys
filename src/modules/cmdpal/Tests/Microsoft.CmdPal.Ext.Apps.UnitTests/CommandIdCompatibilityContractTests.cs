// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class CommandIdCompatibilityContractTests
{
    // These IDs are issued by the prerequisite before catalog discovery ships.
    // Keep the literal values when changing discovery or command projection.
    [TestMethod]
    [DataRow(@"win32:C:\Apps\Editor.exe|args:", "app-v1-win32-editor.exe-95F6C3A04FDF38462DA407F8F7EAE7E0FDDD9370E37415F2DAC7189B628D6311")]
    [DataRow(@"win32:C:\Apps\Editor.exe|args:2D2D50726F66696C653D576F726B|cwd:C:\Work", "app-v1-win32-editor.exe-7BACD6B478AE6DA7B92410BC13273F605B3C11C2F3BF46CD5C39CAED8E8DF5EE")]
    [DataRow(@"win32:C:\Apps\Editor.exe|args:2D2D50726F66696C653D776F726B|cwd:C:\Work", "app-v1-win32-editor.exe-022BA19D4B7826C72558552245BBF782F1A2B6026413595761392008BEC3EDCB")]
    [DataRow(@"win32:url:68747470733A2F2F6578616D706C652E746573742F436173653F6B65793D56616C7565|args:", "app-v1-win32-8C1C75C4ED29CF54807EA69D85A353269430D634377404A078A8652892DFC352")]
    [DataRow("packaged:Contoso.App_123!Main", "app-v1-packaged-Contoso.App_123!Main")]
    public void PreCatalogCommandIdsKeepTheirBytesAndResolveSavedReferences(string identity, string issuedId)
    {
        Assert.AreEqual(issuedId, AppIdentity.ForCommand(identity));
        var app = new AppItem { CatalogId = identity, Name = "Localized name", CommandIds = ["Released name_123"] };
        var row = new AppListItem(app, useThumbnails: false);
        var snapshot = new AppListItemSnapshot([row], []);
        Assert.AreEqual(issuedId, row.Command!.Id);
        Assert.AreSame(row, snapshot.GetApp(issuedId));
        Assert.AreEqual("Released name_123", snapshot.GetCommandItem("Released name_123")?.Command?.Id);
        Assert.AreSame(row, snapshot.GetApp(issuedId.ToUpperInvariant()));
    }
}
