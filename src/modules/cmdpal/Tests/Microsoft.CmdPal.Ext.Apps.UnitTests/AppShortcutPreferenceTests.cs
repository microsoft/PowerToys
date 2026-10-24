// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Cache;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppShortcutPreferenceTests
{
    private const string TargetPath = @"C:\Apps\App.exe";

    private static readonly string[] DesktopRoots = [@"D:\OneDrive\Desktop", @"C:\Users\Public\Desktop"];
    private static readonly string[] StartMenuRoots = [@"C:\Users\User\Start Menu", @"C:\ProgramData\Start Menu"];

    private string _settingsPath = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-shortcut-preference-{Guid.NewGuid():N}.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestDataHelper.DeleteSettingsFiles(_settingsPath);
    }

    [TestMethod]
    [DataRow("desktop", @"D:\OneDrive\Desktop\App.lnk", true, 0)]
    [DataRow("desktop", @"d:\onedrive\desktop\Archive\Nested\App.LNK", true, 2)]
    [DataRow("desktop", @"C:\Users\Public\Desktop\App.lnk", false, 0)]
    [DataRow("start-menu", @"C:\Users\User\Start Menu\Programs\App.lnk", true, 0)]
    [DataRow("start-menu", @"C:\Users\User\Start Menu\Programs\Vendor\App.lnk", true, 1)]
    [DataRow("start-menu", @"C:\ProgramData\Start Menu\Programs\Vendor\App.lnk", false, 1)]
    [DataRow("start-menu", @"C:\Users\User\Start Menu\App.lnk", true, 0)]
    public void Discovery_CapturesScopeAndRootRelativeDepth(string sourceId, string path, bool isUser, int depth)
    {
        var settings = new AllAppsSettings(_settingsPath);
        IWin32ProgramSource source = sourceId == "desktop"
            ? new DesktopAppSource(settings, DesktopRoots)
            : new StartMenuAppSource(settings, StartMenuRoots, []);

        Assert.AreEqual(new AppShortcutPreference(isUser, depth, source.Priority), source.GetShortcutPreference(path));
    }

    [TestMethod]
    public void Discovery_DoesNotClassifyOtherEntriesOrNeighboringDirectories()
    {
        var settings = new AllAppsSettings(_settingsPath);
        IWin32ProgramSource desktop = new DesktopAppSource(settings, DesktopRoots);
        IWin32ProgramSource startMenu = new StartMenuAppSource(settings, StartMenuRoots, []);
        IWin32ProgramSource custom = new CustomDirectoryAppSource("custom-shortcut", DesktopRoots[0], ["lnk"], Win32ProgramSourceProfile.None, 0);
        IWin32ProgramSource registry = new RegistryAppSource(settings);
        IWin32ProgramSource pathSource = new PathEnvironmentAppSource(settings);

        Assert.IsNull(desktop.GetShortcutPreference(@"D:\OneDrive\Desktop\App.exe"));
        Assert.IsNull(desktop.GetShortcutPreference(@"D:\OneDrive\Desktop\App.url"));
        Assert.IsNull(desktop.GetShortcutPreference(@"D:\OneDrive\Desktop Archive\App.lnk"));
        Assert.IsNull(startMenu.GetShortcutPreference(@"C:\Users\User\Start Menu Archive\App.lnk"));
        Assert.IsNull(custom.GetShortcutPreference(@"D:\OneDrive\Desktop\App.lnk"));
        Assert.IsNull(registry.GetShortcutPreference(@"C:\Apps\App.exe"));
        Assert.IsNull(pathSource.GetShortcutPreference(@"C:\Apps\App.exe"));
    }

    [TestMethod]
    [DataRow("desktop", @"D:\OneDrive\Desktop\Archive\Nested\App.lnk", "start-menu", @"C:\ProgramData\Start Menu\Programs\App.lnk")]
    [DataRow("start-menu", @"C:\Users\User\Start Menu\Programs\Vendor\App.lnk", "desktop", @"C:\Users\Public\Desktop\App.lnk")]
    [DataRow("start-menu", @"C:\Users\User\Start Menu\Programs\App.lnk", "desktop", @"D:\OneDrive\Desktop\Archive\App.lnk")]
    [DataRow("desktop", @"D:\OneDrive\Desktop\App.lnk", "start-menu", @"C:\Users\User\Start Menu\Programs\App.lnk")]
    [DataRow("desktop", @"C:\Users\Public\Desktop\App.lnk", "start-menu", @"C:\ProgramData\Start Menu\Programs\App.lnk")]
    [DataRow("desktop", @"D:\OneDrive\Desktop\Alpha.lnk", "desktop", @"D:\OneDrive\Desktop\Zulu.lnk")]
    [DataRow("desktop", @"D:\OneDrive\Desktop\Xilbaba.lnk", "desktop", @"D:\OneDrive\Desktop\Desktop Level 1\Desktop Level 2\Deeps.lnk")]
    [DataRow("desktop", @"D:\OneDrive\Desktop\Z\Alpha.lnk", "desktop", @"D:\OneDrive\Desktop\A\Zulu.lnk")]
    [DataRow("desktop", @"D:\OneDrive\Desktop\A\App.lnk", "desktop", @"D:\OneDrive\Desktop\B\App.lnk")]
    public void Merge_SelectsTheCloserShortcutAndRetainsBothIdentities(string winnerSource, string winnerPath, string otherSource, string otherPath)
    {
        var settings = new AllAppsSettings(_settingsPath);
        IWin32ProgramSource desktop = new DesktopAppSource(settings, DesktopRoots);
        IWin32ProgramSource startMenu = new StartMenuAppSource(settings, StartMenuRoots, []);
        var winner = CreateItem(
            winnerSource,
            winnerPath,
            (winnerSource == "desktop" ? desktop : startMenu).GetShortcutPreference(winnerPath));
        var other = CreateItem(
            otherSource,
            otherPath,
            (otherSource == "desktop" ? desktop : startMenu).GetShortcutPreference(otherPath));

        foreach (var merged in new[] { winner.MergeProvenance(other), other.MergeProvenance(winner) })
        {
            Assert.AreEqual(winnerPath, merged.ToAppItem().LaunchTarget);
            Assert.AreEqual(new AppCommand(other.ToAppItem()).Id, new AppCommand(merged.ToAppItem()).Id);
            CollectionAssert.AreEquivalent(winner.CommandIds.Concat(other.CommandIds).Distinct().ToArray(), merged.CommandIds.ToArray());
            Assert.AreEqual(2, merged.Provenance.References.Length);
        }
    }

    [TestMethod]
    public void Merge_UsesSourcePriorityRegardlessOfSourceId()
    {
        var desktop = CreateItem("renamed-desktop", @"C:\User\Desktop\Zulu.lnk", new AppShortcutPreference(true, 0, SourcePriority: 9));
        var startMenu = CreateItem("desktop", @"C:\User\Start Menu\Programs\Alpha.lnk", new AppShortcutPreference(true, 0, SourcePriority: 10));

        Assert.AreEqual(desktop.Payload, desktop.MergeProvenance(startMenu).Payload);
        Assert.AreEqual(desktop.Payload, startMenu.MergeProvenance(desktop).Payload);
    }

    [TestMethod]
    public void Merge_RemainsAssociativeWhenAnOlderCachedReferenceHasNoPreference()
    {
        var personal = CreateItem("start-menu", @"C:\User\App.lnk", new AppShortcutPreference(true, 0, SourcePriority: 10));
        var shared = CreateItem("desktop", @"C:\Public\App.lnk", new AppShortcutPreference(false, 0, SourcePriority: 9));
        var older = CreateItem("desktop", @"C:\Old\App.lnk", null);

        foreach (var order in new[]
        {
            new[] { personal, shared, older }, new[] { personal, older, shared },
            new[] { shared, personal, older }, new[] { shared, older, personal },
            new[] { older, personal, shared }, new[] { older, shared, personal },
        })
        {
            var left = order[0].MergeProvenance(order[1]).MergeProvenance(order[2]);
            var right = order[0].MergeProvenance(order[1].MergeProvenance(order[2]));
            Assert.AreEqual(personal.Payload, left.Payload);
            Assert.IsTrue(left.HasSamePersistedContent(right));
        }
    }

    [TestMethod]
    public void Merge_RetainsOtherRepresentationPriorities()
    {
        var standard = CreateItem("desktop", @"C:\Desktop\App.lnk", new AppShortcutPreference(true, 0, SourcePriority: 9));
        var custom = CreateItem("custom-shortcut", @"C:\Custom\App.lnk", null, priority: 0);
        var executable = CreateItem("registry", TargetPath, null, priority: 1030);

        Assert.AreEqual(custom.Payload, standard.MergeProvenance(custom).Payload);
        Assert.AreEqual(standard.Payload, standard.MergeProvenance(executable).Payload);
    }

    [TestMethod]
    public void PreferenceChange_InvalidatesCachedContentWithoutChangingReferenceIdentity()
    {
        var path = @"D:\Desktop\App.lnk";
        var older = new AppCatalogSourceReference("desktop", path, "App");
        var current = new AppCatalogSourceReference("desktop", path, "App", new AppShortcutPreference(true, 0, SourcePriority: 9));

        Assert.AreEqual(older, current);
        Assert.IsFalse(new AppCatalogProvenance(10, older).HasSamePersistedContent(new AppCatalogProvenance(10, current)));
    }

    [TestMethod]
    public void Cache_PreservesPreferenceThroughSourceGeneratedSerialization()
    {
        var personal = CreateItem("desktop", @"D:\Desktop\App.lnk", new AppShortcutPreference(true, 2, SourcePriority: 9));
        var shared = CreateItem("start-menu", @"C:\Public\App.lnk", new AppShortcutPreference(false, 0, SourcePriority: 10));
        var cache = new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [shared, personal] }],
        };
        var json = JsonSerializer.Serialize(cache, AppCatalogCacheJsonSerializationContext.Default.AppCatalogCacheFile);
        var reloaded = JsonSerializer.Deserialize(json, AppCatalogCacheJsonSerializationContext.Default.AppCatalogCacheFile)!;
        var items = reloaded.Sources.Single().Items;

        Assert.IsTrue(shared.HasSamePersistedContent(items[0]));
        Assert.IsTrue(personal.HasSamePersistedContent(items[1]));
        Assert.AreEqual(personal.Payload, items[0].MergeProvenance(items[1]).Payload);
    }

    [TestMethod]
    public async Task Indexing_AppliesThePolicyAcrossSourcesAndKeepsRawExecutablePriority()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-shortcut-index-{Guid.NewGuid():N}");
        var desktopRoot = Path.Combine(root, "Desktop");
        var startMenuRoot = Path.Combine(root, "Start Menu");
        var desktopPath = Path.Combine(desktopRoot, "App.lnk");
        var startMenuPath = Path.Combine(startMenuRoot, "Programs", "App.lnk");
        var executablePath = Path.Combine(desktopRoot, "Raw.exe");
        Directory.CreateDirectory(desktopRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(startMenuPath)!);
        File.WriteAllText(desktopPath, string.Empty);
        File.WriteAllText(startMenuPath, string.Empty);
        File.WriteAllText(executablePath, string.Empty);
        try
        {
            var settings = new AllAppsSettings(_settingsPath);
            using var desktop = new Win32AppSource(new DesktopAppSource(settings, [desktopRoot, Path.Combine(root, "Shared Desktop")]), ReadMetadata, createWatchers: false);
            using var startMenu = new Win32AppSource(new StartMenuAppSource(settings, [startMenuRoot, Path.Combine(root, "Shared Start Menu")], []), ReadMetadata, createWatchers: false);
            using var portable = new Win32AppSource(new CustomDirectoryAppSource("portable", desktopRoot, ["exe"], Win32ProgramSourceProfile.IncludeRawExecutables, 0), ReadMetadata, createWatchers: false);
            var desktopItems = await desktop.LoadAsync(CancellationToken.None);
            var startMenuItems = await startMenu.LoadAsync(CancellationToken.None);
            var executable = (await portable.LoadAsync(CancellationToken.None)).Single();
            var shortcut = desktopItems.Single(item => item.Provenance.PreferredSource.ItemId == desktopPath);

            Assert.AreEqual(1000, executable.Provenance.Priority);
            Assert.AreEqual(executablePath, executable.Provenance.PreferredSource.ItemId);
            Assert.IsNull(executable.Provenance.PreferredSource.ShortcutPreference);
            Assert.AreEqual(desktopPath, startMenuItems.Single().MergeProvenance(shortcut).ToAppItem().LaunchTarget);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AppCatalogItem CreateItem(string sourceId, string path, AppShortcutPreference? preference, int priority = 10)
    {
        var program = ReadMetadata(path, false);
        return new AppCatalogItem(
            $"win32:{LaunchTarget.FilePath(TargetPath).IdentityToken}|args:",
            priority,
            new AppCatalogSourceReference(sourceId, path, program.Name, preference),
            [program.Name],
            Win32AppPayload.From(program));
    }

    private static Win32AppMetadata ReadMetadata(string path, bool asRunCommand)
    {
        var shortcut = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        var program = TestDataHelper.CreateTestWin32Metadata(Path.GetFileNameWithoutExtension(path), shortcut ? TargetPath : path);
        program.LnkFilePath = shortcut ? path : string.Empty;
        return program;
    }
}
