// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class TryRunContextTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void AppSearchResultIncludesTryRunAsSecondaryAction()
    {
        var cache = new MockAppCache();
        var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\editor.exe");
        program.LnkFilePath = @"C:\StartMenu\Editor.lnk";
        program.Arguments = "--new-window";
        cache.AddWin32Program(program);
        var result = new AllAppsPage(cache).GetItems().Single();
        Assert.IsFalse(result.Command is OpenInTryRunCommand);
        Assert.AreEqual(1, result.MoreCommands.OfType<ICommandContextItem>().Count(item => item.Command is OpenInTryRunCommand));
    }

    [TestMethod]
    public void ShellActivationTargetsDoNotOfferTryRun()
    {
        var program = TestDataHelper.CreateTestWin32Program("App", "shell:AppsFolder\\Package!App");
        Assert.IsFalse(program.GetCommands().OfType<ICommandContextItem>().Any(item => item.Command is OpenInTryRunCommand));
    }
}
