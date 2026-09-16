// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class TryRunContextTests
{
    [TestMethod]
    public void FileSearchAndBrowseOfferTryRunWithoutChangingPrimaryAction()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CmdPal-TryRun-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var file = Path.Combine(directory, "demo.ps1");
            File.WriteAllText(file, "Write-Output 'hello'");
            foreach (var path in new[] { file, directory })
            {
                IListItem[] results = [new IndexerListItem(new IndexerItem(path)), new ExploreListItem(new IndexerItem(path))];
                foreach (var result in results)
                {
                    Assert.IsFalse(result.Command is OpenInTryRunCommand);
                    Assert.AreEqual(1, result.MoreCommands.OfType<ICommandContextItem>().Count(item => item.Command is OpenInTryRunCommand));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
