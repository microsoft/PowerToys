// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class TryRunContextTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void PackagedDesktopExecutableOffersTryRun()
    {
        var command = UWPApplication.CreateTryRunCommand(@"C:\Program Files\WindowsApps\Editor", @"Editor\Editor.exe", "Windows.FullTrustApplication");
        Assert.IsNotNull(command);
        Assert.IsInstanceOfType<OpenInTryRunCommand>(command.Command);
    }

    [TestMethod]
    [DataRow("App.exe", "Example.Uwp.App")]
    [DataRow("..\\Outside.exe", "Windows.FullTrustApplication")]
    [DataRow("C:\\Outside.exe", "Windows.FullTrustApplication")]
    [DataRow("\\\\server\\app.exe", "Windows.FullTrustApplication")]
    [DataRow("app.exe:stream", "Windows.FullTrustApplication")]
    [DataRow("", "Windows.FullTrustApplication")]
    public void PackagedActivationAndPathsOutsideThePackageAreRejected(string executable, string entryPoint)
    {
        Assert.IsNull(UWPApplication.CreateTryRunCommand(@"C:\Packages\Editor", executable, entryPoint));
    }

    [TestMethod]
    [TestCategory("LocalAppIntegration")]
    public void InstalledPackagedDesktopAppExposesTryRunFromItsManifest()
    {
        var app = UWP.All().FirstOrDefault(candidate => candidate.EntryPoint == "Windows.FullTrustApplication" && candidate.Executable.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            Assert.Inconclusive("This check requires an installed packaged desktop app, such as Windows Notepad.");
        }

        var item = app.ToAppItem();
        Assert.IsTrue(item.IsPackaged);
        Assert.AreEqual(1, item.Commands!.OfType<ICommandContextItem>().Count(command => command.Command is OpenInTryRunCommand));
    }

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
