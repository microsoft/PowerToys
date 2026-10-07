// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCatalogVisibilityTests
{
    [TestMethod]
    public void ExclusionPatterns_CompareNormalizedSettings()
    {
        var rules = new AppCatalogVisibility(["*Updater*"], [@"C:\Tools\*"]);

        Assert.IsTrue(rules.HasSamePatterns(new AppCatalogVisibility([" *UPDATER* ", " "], [" c:/tools/* "])));
        Assert.IsFalse(rules.HasSamePatterns(new AppCatalogVisibility(["*Editor*"], [@"C:\Tools\*"])));
        Assert.IsFalse(rules.HasSamePatterns(new AppCatalogVisibility(["*Updater*"], [@"C:\Other\*"])));
    }

    [TestMethod]
    [DataRow("*updater*", "", "Contoso UPDATER", @"C:\Apps\Contoso.exe", true)]
    [DataRow("Contoso", "", "Contoso Updater", @"C:\Apps\Contoso.exe", false)]
    [DataRow(" App ? ", "", "App 1", @"C:\Apps\App.exe", true)]
    [DataRow("App ?", "", "App 10", @"C:\Apps\App.exe", false)]
    [DataRow("[App]", "", "[App]", @"C:\Apps\App.exe", true)]
    [DataRow("", @"c:\tools\*", "Editor", @"C:\Tools\Nested\Editor.exe", true)]
    [DataRow("", @"C:\Tools\*", "Editor", @"C:\Toolshed\Editor.exe", false)]
    [DataRow("", "C:/Tools/*.exe", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow("", @"\\server\share\*", "Editor", @"\\SERVER\Share\Editor.exe", true)]
    [DataRow("No match", @"C:\Tools\*", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow("*Editor*", @"C:\Other\*", "Editor", @"C:\Tools\Editor.exe", true)]
    [DataRow(" ", " ", "Editor", @"C:\Tools\Editor.exe", false)]
    public void ExclusionPatterns_MatchNamesOrPathsWithSimpleWildcards(
        string namePattern, string pathPattern, string name, string path, bool expected)
    {
        var rules = new AppCatalogVisibility([namePattern], [pathPattern]);
        var item = new AppCatalogItem(
            "win32:app",
            0,
            new AppCatalogSourceReference("test", path),
            [],
            new Win32AppPayload { Name = name, TargetPath = path });

        Assert.AreEqual(
            expected ? AppVisibility.HiddenByPattern : AppVisibility.Visible,
            rules.GetVisibility(item, FrozenSet<string>.Empty, FrozenSet<string>.Empty));
    }

    [TestMethod]
    public void PathExclusions_CoverShortcutsAliasesPackagesAndMergedRepresentations()
    {
        var rules = new AppCatalogVisibility([], [@"C:\Hidden\*"]);
        IAppCatalogPayload[] payloads =
        [
            new Win32AppPayload { LnkFilePath = @"C:\Hidden\App.lnk" },
            new Win32AppPayload { AppExecutionAliasTargetPath = @"C:\Hidden\App.exe" },
            new PackagedAppPayload { PackageLocation = @"C:\Hidden\Package" },
        ];
        foreach (var payload in payloads)
        {
            var item = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], payload);
            Assert.AreEqual(AppVisibility.HiddenByPattern, rules.GetVisibility(item, FrozenSet<string>.Empty, FrozenSet<string>.Empty));
        }

        var preferred = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], new PackagedAppPayload());
        var shortcut = new AppCatalogItem("app", 1, new AppCatalogSourceReference("shortcuts", @"C:\Hidden\App.lnk"), [], new Win32AppPayload());
        var target = new AppCatalogItem("app", 1, new AppCatalogSourceReference("other", "app"), [@"C:\Hidden\App.exe"], new Win32AppPayload());

        Assert.AreEqual(AppVisibility.Visible, rules.GetVisibility(preferred, FrozenSet<string>.Empty, FrozenSet<string>.Empty));
        Assert.AreEqual(AppVisibility.HiddenByPattern, rules.GetVisibility(preferred.MergeProvenance(shortcut), FrozenSet<string>.Empty, FrozenSet<string>.Empty));
        Assert.AreEqual(AppVisibility.HiddenByPattern, rules.GetVisibility(preferred.MergeProvenance(target), FrozenSet<string>.Empty, FrozenSet<string>.Empty));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HiddenIdentities_RetainAndClearCoalescedRepresentations(bool preferPackaged)
    {
        const string oldIdentity = @"win32:C:\Tools\app.exe|args:|cwd:C:\Tools";
        const string canonicalIdentity = @"win32:C:\Tools\app.exe|args:";
        var metadata = TestDataHelper.CreateTestWin32Metadata("App");
        var oldItem = new AppCatalogItem(oldIdentity, 0, new AppCatalogSourceReference("test", metadata.TargetPath), [], Win32AppPayload.From(metadata));
        var item = oldItem.WithIdentity(canonicalIdentity);
        if (preferPackaged)
        {
            var packaged = new AppCatalogItem(
                canonicalIdentity,
                -1,
                new AppCatalogSourceReference("packaged", "Contoso.App!app"),
                [],
                new PackagedAppPayload { Name = "App", AppUserModelId = "Contoso.App!app" });
            item = item.MergeProvenance(packaged).WithIdentity("packaged:Contoso.App!app");
        }

        var rules = new AppCatalogVisibility([], []);
        var aliases = FrozenDictionary<string, string>.Empty;
        var hidden = AppCatalogVisibility.UpdateHiddenIdentities(FrozenSet<string>.Empty, oldItem, true, aliases);
        var commandIds = AppCatalogVisibility.ResolveHiddenCommandIds(hidden, aliases);
        Assert.AreEqual(AppVisibility.Hidden, rules.GetVisibility(item, hidden, commandIds));

        hidden = AppCatalogVisibility.UpdateHiddenIdentities(hidden, item, true, aliases);
        hidden = AppCatalogVisibility.UpdateHiddenIdentities(hidden, item, false, aliases);
        Assert.AreEqual(0, hidden.Count);
        Assert.AreEqual(AppVisibility.Visible, rules.GetVisibility(item, hidden, FrozenSet<string>.Empty));
        Assert.AreEqual(AppVisibility.Visible, rules.GetVisibility(oldItem, hidden, FrozenSet<string>.Empty));
    }
}
