// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class IndexerListItemTests
{
    [TestMethod]
    [DataRow(".exe", true)]
    [DataRow(".CMD", true)]
    [DataRow(".bat", true)]
    [DataRow(".msc", true)]
    [DataRow(".MSC", true)]
    [DataRow(".cpl", true)]
    [DataRow(".CPL", true)]
    [DataRow(".com", false)]
    [DataRow(".pif", false)]
    [DataRow(".lnk", false)]
    [DataRow(".txt", false)]
    [DataRow(".dll", false)]
    [DataRow(".ps1", false)]
    public void FileCommands_OfferRunAsAdministratorForSupportedFileTypes(string extension, bool expected)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, string.Empty);
        try
        {
            var adminCommands = IndexerListItem.FileCommands(path).Where(command => command.Command is RunAsAdministratorCommand).ToArray();

            Assert.HasCount(expected ? 1 : 0, adminCommands);
            if (expected)
            {
                Assert.AreEqual(WellKnownKeyChords.RunAsAdministrator, adminCommands[0].RequestedShortcut);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void DirectoryCommands_DoNotOfferRunAsAdministrator()
    {
        Assert.IsFalse(IndexerListItem.FileCommands(Environment.SystemDirectory).Any(command => command.Command is RunAsAdministratorCommand));
    }

    [TestMethod]
    public void EmptyPathDoesNotCreateShellIconRequest()
    {
        var item = new IndexerListItem(
            new IndexerItem
            {
                FileName = "Result without a launch target",
                FullPath = string.Empty,
            });

        Assert.IsNull(item.Icon);
    }
}
