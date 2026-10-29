// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class EarlyCommandIdMigrationTests : AppsTestBase
{
    [TestMethod]
    public async Task ReleasedIdsResolveAfterRenameAndAliasFileReload()
    {
        var program = TestDataHelper.CreateTestWin32Program("Old editor", @"C:\Apps\Editor.exe");
        MockCache.AddWin32Program(program);
        var page = new AllAppsPage(MockCache, Settings);
        var row = page.GetItems().OfType<AppListItem>().Single();
        var oldId = AppCommand.GenerateId(program.Name, program.Description, program.FullPath);
        var canonicalId = row.Command.Id;
        StringAssert.StartsWith(canonicalId, "app-v1-win32-editor.exe-");
        Assert.AreEqual(oldId, new AllAppsCommandProvider(page).GetCommandItem(oldId)?.Command?.Id);
        await Settings.WaitForAliasSavesAsync();

        program.Name = "New editor";
        program.Description = "New localized description";
        var settings = new AllAppsSettings(Settings.FilePath);
        var reopened = new AllAppsPage(MockCache, settings);
        Assert.AreEqual(canonicalId, reopened.GetItems().Single().Command.Id);
        Assert.AreEqual(oldId, new AllAppsCommandProvider(reopened).GetCommandItem(oldId)?.Command?.Id);
        await settings.WaitForAliasSavesAsync();
    }

    [TestMethod]
    public void ShortcutProfilesRetainArgumentsAndWorkingDirectory()
    {
        var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
        program.LnkFilePath = @"C:\Links\Editor.lnk";
        program.Arguments = "--Profile=Work";
        program.WorkingDirectory = @"C:\Work";
        var app = program.ToAppItem();
        Assert.AreEqual(@"win32:C:\Apps\Editor.exe|args:2D2D50726F66696C653D576F726B|cwd:C:\Work", app.CatalogId);
        CollectionAssert.Contains(app.CommandIds.ToArray(), AppCommand.GenerateId(app.Name, app.Subtitle, program.LnkFilePath));
        program.Arguments = "--Profile=work";
        Assert.AreNotEqual(app.CatalogId, program.ToAppItem().CatalogId);
        program.Arguments = "--Profile=Work";
        program.WorkingDirectory = @"C:\Other";
        Assert.AreNotEqual(app.CatalogId, program.ToAppItem().CatalogId);
    }

    [TestMethod]
    public void EquivalentDiscoveriesKeepEveryReleasedId()
    {
        var first = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
        var second = TestDataHelper.CreateTestWin32Program("Other editor label", @"C:\Apps\Editor.exe");
        second.LnkFilePath = @"C:\Links\Editor.lnk";
        MockCache.AddWin32Program(first);
        MockCache.AddWin32Program(second);
        var page = new AllAppsPage(MockCache, Settings);
        Assert.AreEqual(1, page.GetItems().Length);
        var provider = new AllAppsCommandProvider(page);
        foreach (var program in new[] { first, second })
        {
            var app = program.ToAppItem();
            var id = AppCommand.GenerateId(app.Name, app.Subtitle, app.ExePath);
            Assert.AreEqual(id, provider.GetCommandItem(id)?.Command?.Id);
        }
    }

    [TestMethod]
    public async Task UnreadableAliasFileIsPreserved()
    {
        var path = AllAppsSettings.AppCommandAliasesPath(Settings.FilePath);
        await File.WriteAllTextAsync(path, "broken aliases");
        using var store = new AppCommandAliasStore(path);
        store.SetSnapshot(new System.Collections.Generic.Dictionary<string, string> { ["legacy"] = "current" });
        await store.WaitForSavesAsync();
        Assert.AreEqual("broken aliases", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public void LegacyDiscoveryKeepsDistinctWorkingDirectoryProfiles()
    {
        var first = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
        var second = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
        first.WorkingDirectory = @"C:\Work";
        second.WorkingDirectory = @"C:\Personal";
        var discoveries = new System.Collections.Generic.HashSet<Win32Program> { first, second };
        Assert.AreEqual(2, discoveries.Count);
        Assert.AreNotEqual(first.ToAppItem().CatalogId, second.ToAppItem().CatalogId);
    }

    [TestMethod]
    public async Task InstallMoveRetainsReferencesFromANonPreferredShortcut()
    {
        var direct = TestDataHelper.CreateTestWin32Program("A Editor", @"C:\Apps\Old\Editor.exe");
        var shortcut = TestDataHelper.CreateTestWin32Program("Z Editor", direct.FullPath);
        shortcut.LnkFilePath = @"C:\Links\Editor.lnk";
        MockCache.AddWin32Program(direct);
        MockCache.AddWin32Program(shortcut);
        var page = new AllAppsPage(MockCache, Settings);
        var original = page.GetItems().Single();
        Assert.AreEqual(direct.Name, original.Title);
        var originalId = original.Command!.Id;
        var releasedShortcutId = AppCommand.GenerateId(shortcut.Name, shortcut.Description, shortcut.LnkFilePath);
        Assert.AreEqual(releasedShortcutId, new AllAppsCommandProvider(page).GetCommandItem(releasedShortcutId)?.Command?.Id);
        await Settings.WaitForAliasSavesAsync();

        direct.FullPath = shortcut.FullPath = @"C:\Apps\New\Editor.exe";
        var settings = new AllAppsSettings(Settings.FilePath);
        var reopened = new AllAppsPage(MockCache, settings);
        Assert.AreNotEqual(originalId, reopened.GetItems().Single().Command!.Id);
        var provider = new AllAppsCommandProvider(reopened);
        Assert.AreEqual(releasedShortcutId, provider.GetCommandItem(releasedShortcutId)?.Command?.Id);
        Assert.AreEqual(originalId, provider.GetCommandItem(originalId)?.Command?.Id);
        await settings.WaitForAliasSavesAsync();
    }
}
