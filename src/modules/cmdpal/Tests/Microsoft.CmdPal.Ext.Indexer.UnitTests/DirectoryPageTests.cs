// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class DirectoryPageTests
{
    [TestMethod]
    public void EmptyPathKeepsFileExplorerFallbackIcon()
    {
        var page = new DirectoryPage(string.Empty);

        Assert.AreSame(Icons.FileExplorerIcon, page.Icon);
    }
}
