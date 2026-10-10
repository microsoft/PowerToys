// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class StartMenuAppSourceTests
{
    [TestMethod]
    public void GetPaths_ExcludesUserAndCommonStartupSubtreesOnly()
    {
        using var fixture = new StartMenuFixture();
        var normal = fixture.CreateShortcut(fixture.UserRoot, "Programs", "OpenRGB.lnk");
        var sibling = fixture.CreateShortcut(fixture.UserRoot, "Programs", "StartupBackup", "Backup.lnk");
        var nested = fixture.CreateShortcut(fixture.CommonRoot, "Programs", "Tools", "Startup", "Tool.lnk");
        fixture.CreateShortcut(fixture.UserStartup, "Automatic.lnk");
        fixture.CreateShortcut(fixture.UserStartup, "Nested", "Deep.lnk");
        fixture.CreateShortcut(fixture.CommonStartup, "System.lnk");
        var source = fixture.CreateSource();

        CollectionAssert.AreEquivalent(new[] { normal, sibling, nested }, source.GetPaths().ToArray());
    }

    [TestMethod]
    public void GetPathsForChange_ParentRescanSkipsStartupSubtrees()
    {
        using var fixture = new StartMenuFixture();
        var normal = fixture.CreateShortcut(fixture.UserRoot, "Programs", "OpenRGB.lnk");
        var common = fixture.CreateShortcut(fixture.CommonRoot, "Programs", "Shared.lnk");
        fixture.CreateShortcut(fixture.UserStartup, "Automatic.lnk");
        fixture.CreateShortcut(fixture.CommonStartup, "Nested", "Automatic.lnk");
        var source = fixture.CreateSource();

        CollectionAssert.AreEquivalent(new[] { normal, common }, source.GetPathsForChange(fixture.Root).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StartupChanges_AreIrrelevantBeforeAndAfterDeletionRegardlessOfCase(bool common)
    {
        using var fixture = new StartMenuFixture();
        var startup = common ? fixture.CommonStartup : fixture.UserStartup;
        var shortcut = fixture.CreateShortcut(startup, "Apps.v2", "Automatic.lnk");
        var directory = Path.GetDirectoryName(shortcut)!;
        var source = fixture.CreateSource(
            [fixture.UserStartup.ToUpperInvariant() + Path.DirectorySeparatorChar, fixture.CommonStartup.ToUpperInvariant()]);

        foreach (var path in new[] { startup, directory, shortcut })
        {
            Assert.IsFalse(source.IsRelevantPath(path.ToUpperInvariant()), path);
            Assert.AreEqual(0, source.GetPathsForChange(path.ToUpperInvariant()).Count(), path);
        }

        Directory.Delete(startup, recursive: true);

        foreach (var path in new[] { startup, directory, shortcut })
        {
            Assert.IsFalse(source.IsRelevantPath(path.ToUpperInvariant()), path);
            Assert.AreEqual(0, source.GetPathsForChange(path.ToUpperInvariant()).Count(), path);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_RenameIntoAndOutOfStartupRemovesAndRestoresApplication()
    {
        using var fixture = new StartMenuFixture();
        var normal = fixture.CreateShortcut(fixture.UserRoot, "Programs", "OpenRGB.lnk");
        var startup = Path.Combine(fixture.UserStartup, "OpenRGB.lnk");
        using var source = new Win32AppSource(
            fixture.CreateSource(),
            (path, _) =>
            {
                var program = TestDataHelper.CreateTestWin32Metadata("OpenRGB", Path.Combine(fixture.Root, "OpenRGB.exe"));
                program.AppType = Win32AppType.Win32Application;
                program.LnkFilePath = path;
                return program;
            },
            createWatchers: false);
        var items = await source.LoadAsync(CancellationToken.None);
        Assert.AreEqual(1, items.Count);

        File.Move(normal, startup);
        items = await source.ApplyChangesAsync(
            items,
            [new AppSourcePathChange(WatcherChangeTypes.Renamed, startup, normal)],
            CancellationToken.None);
        Assert.AreEqual(0, items.Count);

        File.Move(startup, normal);
        items = await source.ApplyChangesAsync(
            items,
            [new AppSourcePathChange(WatcherChangeTypes.Renamed, normal, startup)],
            CancellationToken.None);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(normal, items[0].ToAppItem().LaunchTarget);
    }

    [TestMethod]
    public void ExplicitCustomStartupSource_StillIndexesStartupShortcuts()
    {
        using var fixture = new StartMenuFixture();
        var direct = fixture.CreateShortcut(fixture.UserStartup, "OpenRGB.lnk");
        var nested = fixture.CreateShortcut(fixture.UserStartup, "Nested", "Another.lnk");
        var source = new CustomDirectoryAppSource(
            "custom-shortcut",
            fixture.UserStartup,
            ["lnk"],
            Win32ProgramSourceProfile.RecurseSubdirectories,
            int.MaxValue);

        CollectionAssert.AreEquivalent(new[] { direct, nested }, source.GetPaths().ToArray());
        Assert.IsTrue(source.IsRelevantPath(direct));
        CollectionAssert.AreEquivalent(new[] { direct, nested }, source.GetPathsForChange(fixture.UserStartup).ToArray());
    }

    [TestMethod]
    public void SourceKeys_ChangeWhenStartupExclusionsAreAdded()
    {
        using var fixture = new StartMenuFixture();
        var unrestricted = fixture.CreateSource([]);
        var excluded = fixture.CreateSource();

        Assert.AreNotEqual(unrestricted.CacheKey, excluded.CacheKey);
        Assert.AreNotEqual(unrestricted.ConfigurationKey, excluded.ConfigurationKey);
    }

    private sealed class StartMenuFixture : IDisposable
    {
        public string Root { get; }

        public string UserRoot { get; }

        public string CommonRoot { get; }

        public string UserStartup { get; }

        public string CommonStartup { get; }

        public AllAppsSettings Settings { get; }

        public StartMenuFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"cmdpal-start-menu-{Guid.NewGuid():N}");
            UserRoot = Path.Combine(Root, "User", "Start Menu");
            CommonRoot = Path.Combine(Root, "Common", "Start Menu");
            UserStartup = Path.Combine(UserRoot, "Programs", "Startup");
            CommonStartup = Path.Combine(CommonRoot, "Programs", "Startup");
            Directory.CreateDirectory(UserStartup);
            Directory.CreateDirectory(CommonStartup);
            Settings = new AllAppsSettings(Path.Combine(Root, "settings.json"));
        }

        public StartMenuAppSource CreateSource(IReadOnlyList<string> startupDirectories = null)
        {
            return new StartMenuAppSource(
                Settings,
                [UserRoot, CommonRoot],
                startupDirectories ?? [UserStartup, CommonStartup]);
        }

        public string CreateShortcut(string directory, params string[] relativePath)
        {
            var path = Path.Combine([directory, .. relativePath]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
