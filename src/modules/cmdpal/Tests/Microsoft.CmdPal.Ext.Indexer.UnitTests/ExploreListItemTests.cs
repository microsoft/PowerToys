// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class ExploreListItemTests
{
    [TestMethod]
    public void EmptyPathDoesNotCreateShellIconRequest()
    {
        var item = new ExploreListItem(
            new IndexerItem
            {
                FileName = "Result without a launch target",
                FullPath = string.Empty,
            });

        Assert.IsNull(item.Icon);
    }
}
