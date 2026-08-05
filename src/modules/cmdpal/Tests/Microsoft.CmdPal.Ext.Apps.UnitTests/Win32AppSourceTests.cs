// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32AppSourceTests
{
    [TestMethod]
    public async Task LoadAsync_IndexesOneOriginWithSourceScopedProvenance()
    {
        var startMenu = new TestProgramSource("start-menu", 10, includeNonApps: true, @"C:\Apps\app.lnk");
        var loadCount = 0;
        using var source = new Win32AppSource(
            startMenu,
            (path, asRunCommand) =>
            {
                Interlocked.Increment(ref loadCount);
                return TestDataHelper.CreateTestWin32Program("App", @"C:\Apps\app.exe");
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, loadCount);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(10, items[0].Provenance.Priority);
        Assert.IsTrue(ContainsSourceReference(items[0], "start-menu", @"C:\Apps\app.lnk"));
    }

    [TestMethod]
    public async Task LoadAsync_NonAppIncludedBySource_RemainsVisible()
    {
        var custom = new TestProgramSource("custom:portable", 0, includeNonApps: true, @"C:\Links\document.lnk");
        using var source = new Win32AppSource(
            custom,
            (path, asRunCommand) => CreateNonApp(),
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(ContainsSourceReference(items[0], "custom:portable", @"C:\Links\document.lnk"));
    }

    [TestMethod]
    public async Task LoadAsync_NonAppExcludedByOnlySource_IsNotPublished()
    {
        var desktop = new TestProgramSource("desktop", 20, includeNonApps: false, @"C:\Links\document.lnk");
        using var source = new Win32AppSource(
            desktop,
            (path, asRunCommand) => CreateNonApp(),
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task LoadAsync_SourceWithoutRawExecutableProfile_DoesNotLoadProgram()
    {
        var loadCount = 0;
        var startMenu = new TestProgramSource(
            "start-menu",
            10,
            Win32ProgramSourceProfile.None,
            @"C:\Apps\app.exe");
        using var source = new Win32AppSource(
            startMenu,
            (path, asRunCommand) =>
            {
                Interlocked.Increment(ref loadCount);
                return CreateExecutable("App", path);
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(0, loadCount);
        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task LoadAsync_RawRunCommandProfile_LoadsExecutableAsRunCommand()
    {
        bool? loadedAsRunCommand = null;
        var pathSource = new TestProgramSource(
            "path",
            40,
            Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.LoadAsRunCommand,
            @"C:\Tools\tool.exe");
        using var source = new Win32AppSource(
            pathSource,
            (path, asRunCommand) =>
            {
                loadedAsRunCommand = asRunCommand;
                return CreateExecutable("tool", path);
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, items.Count);
        Assert.IsTrue(loadedAsRunCommand == true);
    }

    [TestMethod]
    public async Task LoadAsync_SameTargetWithDifferentNames_CreatesSharedIdentityAndPrefersShortcut()
    {
        const string targetPath = @"C:\Program Files\Microsoft Office\WINWORD.EXE";
        const string shortcutPath = @"C:\Start Menu\Word.lnk";
        using var shortcutSource = new Win32AppSource(
            new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, shortcutPath),
            (path, asRunCommand) => CreateExecutable("Word", targetPath, shortcutPath),
            createWatchers: false);
        using var registrySource = new Win32AppSource(
            new TestProgramSource("registry", 30, Win32ProgramSourceProfile.IncludeRawExecutables, targetPath),
            (path, asRunCommand) => CreateExecutable("WINWORD", targetPath),
            createWatchers: false);

        var shortcutItems = await shortcutSource.LoadAsync(CancellationToken.None);
        var registryItems = await registrySource.LoadAsync(CancellationToken.None);
        var merged = shortcutItems[0].MergeProvenance(registryItems[0]);

        Assert.AreEqual(shortcutItems[0].Identity, registryItems[0].Identity);
        Assert.AreEqual("Word", (merged.Payload as Win32AppPayload)?.Name);
        Assert.AreEqual(10, merged.Provenance.Priority);
        Assert.IsTrue(ContainsSourceReference(merged, "start-menu", shortcutPath));
        Assert.IsTrue(ContainsSourceReference(merged, "registry", targetPath));
    }

    [TestMethod]
    public async Task LoadAsync_SameTargetWithArgumentsDifferingByCase_KeepsDistinctIdentities()
    {
        const string targetPath = @"C:\Apps\app.exe";
        var sourceDefinition = new TestProgramSource(
            "start-menu",
            10,
            Win32ProgramSourceProfile.None,
            @"C:\Links\First.lnk",
            @"C:\Links\Second.lnk");
        using var source = new Win32AppSource(
            sourceDefinition,
            (path, asRunCommand) =>
            {
                var program = CreateExecutable("App", targetPath, path);
                program.Arguments = path.Contains("First", StringComparison.Ordinal) ? "/Profile A" : "/profile a";
                return program;
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(2, items.Count);
    }

    [TestMethod]
    public void CreateProgramSources_UsesSeparateShortcutAndPortableProfiles()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-settings-{Guid.NewGuid():N}.json");
        try
        {
            var settingsJson = """
                {
                  "apps.CustomShortcutFolders": [{ "value": "C:\\Shortcuts" }],
                  "apps.PortableAppFolders": [{ "value": "D:\\PortableApps" }]
                }
                """;
            File.WriteAllText(settingsPath, settingsJson);
            var settings = new AllAppsSettings(settingsPath);

            IWin32ProgramSource shortcutSource = null;
            IWin32ProgramSource portableSource = null;
            foreach (var source in Win32AppSource.CreateProgramSources(settings))
            {
                if (source.Id.StartsWith("custom-shortcut:", StringComparison.Ordinal))
                {
                    shortcutSource = source;
                }
                else if (source.Id.StartsWith("portable:", StringComparison.Ordinal))
                {
                    portableSource = source;
                }
            }

            Assert.IsNotNull(shortcutSource);
            Assert.IsTrue(HasProfileOption(shortcutSource.Profile, Win32ProgramSourceProfile.IncludeNonApplications));
            Assert.IsFalse(HasProfileOption(shortcutSource.Profile, Win32ProgramSourceProfile.IncludeRawExecutables));
            Assert.AreEqual(int.MaxValue, shortcutSource.MaximumDepth);

            Assert.IsNotNull(portableSource);
            Assert.IsTrue(HasProfileOption(portableSource.Profile, Win32ProgramSourceProfile.IncludeRawExecutables));
            Assert.IsFalse(HasProfileOption(portableSource.Profile, Win32ProgramSourceProfile.IncludeNonApplications));
            Assert.IsFalse(HasProfileOption(portableSource.Profile, Win32ProgramSourceProfile.LoadAsRunCommand));
            Assert.AreEqual(1, portableSource.MaximumDepth);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void SettingsAppSourceProvider_SettingsChangeReplacesOnlyChangedCustomSources()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-settings-{Guid.NewGuid():N}.json");
        var allSources = new HashSet<IAppSource>();
        try
        {
            const string initialSettingsJson = """
                {
                  "apps.CustomShortcutFolders": [{ "value": "C:\\Shortcuts" }],
                  "apps.PortableAppFolders": []
                }
                """;
            File.WriteAllText(settingsPath, initialSettingsJson);
            var settings = new AllAppsSettings(settingsPath);
            var packagedSource = new EmptyAppSource();
            using var provider = new SettingsAppSourceProvider(settings, packagedSource);
            var providerChangeCount = 0;
            provider.Changed += (_, _) => providerChangeCount++;
            var initialSources = provider.GetSources();
            AddSources(allSources, initialSources);
            var initialStartMenu = FindSource(initialSources, "win32:start-menu");
            var initialCustom = FindSourceByPrefix(initialSources, "win32:custom-shortcut:");
            Assert.IsNotNull(initialStartMenu);
            Assert.IsNotNull(initialCustom);

            var settingsForm = (SettingsForm)settings.Settings.ToContent()[0];
            settingsForm.SubmitForm("{\"apps.EnableCatalogDiagnostics\":\"true\"}", string.Empty);

            var diagnosticsSources = provider.GetSources();
            AddSources(allSources, diagnosticsSources);
            Assert.IsTrue(settings.EnableCatalogDiagnostics);
            Assert.AreEqual(0, providerChangeCount);
            Assert.AreSame(initialStartMenu, FindSource(diagnosticsSources, "win32:start-menu"));
            Assert.AreSame(initialCustom, FindSourceByPrefix(diagnosticsSources, "win32:custom-shortcut:"));

            const string updatedFormJson = """
                {
                  "apps.CustomShortcutFolders": "[]",
                  "apps.PortableAppFolders": "[{\"value\":\"D:/PortableApps\"}]"
                }
                """;
            settingsForm.SubmitForm(updatedFormJson, string.Empty);

            var updatedSources = provider.GetSources();
            AddSources(allSources, updatedSources);
            Assert.AreSame(initialStartMenu, FindSource(updatedSources, "win32:start-menu"));
            Assert.IsNull(FindSourceByPrefix(updatedSources, "win32:custom-shortcut:"));
            Assert.IsNotNull(FindSourceByPrefix(updatedSources, "win32:portable:"));
            Assert.AreEqual(1, providerChangeCount);
        }
        finally
        {
            foreach (var source in allSources)
            {
                source.Dispose();
            }

            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void GetPaths_MaximumDepthStopsAfterImmediateSubfolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-portable-apps-{Guid.NewGuid():N}");
        var child = Path.Combine(root, "App");
        var grandchild = Path.Combine(child, "Bin");
        Directory.CreateDirectory(grandchild);
        var rootApp = Path.Combine(root, "Root.exe");
        var rootShortcut = Path.Combine(root, "Shortcut.lnk");
        var childApp = Path.Combine(child, "Child.exe");
        var grandchildApp = Path.Combine(grandchild, "Grandchild.exe");
        File.WriteAllText(rootApp, string.Empty);
        File.WriteAllText(rootShortcut, string.Empty);
        File.WriteAllText(childApp, string.Empty);
        File.WriteAllText(grandchildApp, string.Empty);

        try
        {
            var source = new CustomDirectoryAppSource(
                "portable",
                root,
                ["exe"],
                Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.RecurseSubdirectories,
                maximumDepth: 1);
            var paths = new HashSet<string>(source.GetPaths(), StringComparer.OrdinalIgnoreCase);

            Assert.AreEqual(2, paths.Count);
            Assert.IsTrue(paths.Contains(rootApp));
            Assert.IsFalse(paths.Contains(rootShortcut));
            Assert.IsTrue(paths.Contains(childApp));
            Assert.IsFalse(paths.Contains(grandchildApp));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IsRelevantPath_RejectsUnsupportedFilesAndKeepsDottedDirectoryChanges()
    {
        var root = CreateTemporaryDirectory("cmdpal-relevant-paths");
        try
        {
            var source = CreateShortcutDirectorySource(root);
            foreach (var name in new[] { "report.docx", "~$report.docx", "save.tmp", "LICENSE" })
            {
                var path = Path.Combine(root, name);
                File.WriteAllText(path, string.Empty);
                Assert.IsFalse(source.IsRelevantPath(path), name);
            }

            var shortcut = Path.Combine(root, "App.lnk");
            File.WriteAllText(shortcut, string.Empty);
            Assert.IsTrue(source.IsRelevantPath(shortcut));
            File.Delete(shortcut);
            Assert.IsTrue(source.IsRelevantPath(shortcut));

            var directory = Path.Combine(root, "Apps.v2");
            Directory.CreateDirectory(directory);
            Assert.IsTrue(source.IsRelevantPath(directory));
            Directory.Delete(directory);
            Assert.IsTrue(source.IsRelevantPath(directory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_ChangedPathOnlyReindexesAffectedApplication()
    {
        var root = CreateTemporaryDirectory("cmdpal-incremental-apps");
        var firstPath = Path.Combine(root, "First.lnk");
        var secondPath = Path.Combine(root, "Second.lnk");
        File.WriteAllText(firstPath, string.Empty);
        File.WriteAllText(secondPath, string.Empty);

        try
        {
            var loadCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var useUpdatedMetadata = false;
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, asRunCommand) =>
                {
                    loadCounts.AddOrUpdate(path, 1, static (_, count) => count + 1);
                    var name = useUpdatedMetadata && string.Equals(path, firstPath, StringComparison.OrdinalIgnoreCase)
                        ? "First updated"
                        : Path.GetFileNameWithoutExtension(path);
                    return CreateExecutable(name, path, path);
                },
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);
            var unchangedItem = FindItemByReference(initialItems, secondPath);
            loadCounts.Clear();
            useUpdatedMetadata = true;

            var updatedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, firstPath)],
                CancellationToken.None);

            Assert.AreEqual(1, loadCounts[firstPath]);
            Assert.IsFalse(loadCounts.ContainsKey(secondPath));
            Assert.AreEqual("First updated", (FindItemByReference(updatedItems, firstPath).Payload as Win32AppPayload)?.Name);
            Assert.AreSame(unchangedItem, FindItemByReference(updatedItems, secondPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_UsesFinalStateInsteadOfObservedEventKind()
    {
        var root = CreateTemporaryDirectory("cmdpal-final-state-apps");
        var appPath = Path.Combine(root, "App.lnk");
        File.WriteAllText(appPath, string.Empty);
        var currentName = "Original";

        try
        {
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, asRunCommand) => CreateExecutable(currentName, path, path),
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);

            File.Delete(appPath);
            var removedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Created, appPath)],
                CancellationToken.None);

            Assert.AreEqual(0, removedItems.Count);

            File.WriteAllText(appPath, string.Empty);
            currentName = "Restored";
            var restoredItems = await source.ApplyChangesAsync(
                removedItems,
                [new AppSourcePathChange(WatcherChangeTypes.Deleted, appPath)],
                CancellationToken.None);
            var repeatedItems = await source.ApplyChangesAsync(
                restoredItems,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, appPath)],
                CancellationToken.None);

            Assert.AreEqual(1, restoredItems.Count);
            Assert.AreEqual("Restored", (restoredItems[0].Payload as Win32AppPayload)?.Name);
            Assert.AreEqual(1, repeatedItems.Count);
            Assert.IsTrue(restoredItems[0].HasSamePersistedContent(repeatedItems[0]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_RenameReconcilesOldAndNewPathsTogether()
    {
        var root = CreateTemporaryDirectory("cmdpal-renamed-apps");
        var oldPath = Path.Combine(root, "Old.lnk");
        var newPath = Path.Combine(root, "New.lnk");
        File.WriteAllText(oldPath, string.Empty);

        try
        {
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, asRunCommand) => CreateExecutable(Path.GetFileNameWithoutExtension(path), path, path),
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);
            File.Move(oldPath, newPath);

            var updatedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Renamed, newPath, oldPath)],
                CancellationToken.None);

            Assert.AreEqual(1, updatedItems.Count);
            Assert.AreEqual("New", (updatedItems[0].Payload as Win32AppPayload)?.Name);
            Assert.IsTrue(ContainsSourceReference(updatedItems[0], $"custom-shortcut:{root}", newPath));
            Assert.IsFalse(ContainsSourceReference(updatedItems[0], $"custom-shortcut:{root}", oldPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_DeletedDirectoryRemovesItsDescendants()
    {
        var root = CreateTemporaryDirectory("cmdpal-removed-directory-apps");
        var child = Path.Combine(root, "Child");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(child, "App.lnk"), string.Empty);

        try
        {
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, asRunCommand) => CreateExecutable(Path.GetFileNameWithoutExtension(path), path, path),
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);
            Directory.Delete(child, recursive: true);

            var updatedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Created, child)],
                CancellationToken.None);

            Assert.AreEqual(0, updatedItems.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_RemovedRepresentationKeepsOverlappingApplication()
    {
        var root = CreateTemporaryDirectory("cmdpal-overlapping-incremental-apps");
        var firstDirectory = Path.Combine(root, "First");
        var secondDirectory = Path.Combine(root, "Second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        var firstShortcut = Path.Combine(firstDirectory, "Word.lnk");
        var secondShortcut = Path.Combine(secondDirectory, "Word.lnk");
        var targetPath = Path.Combine(root, "WINWORD.exe");
        File.WriteAllText(firstShortcut, string.Empty);
        File.WriteAllText(secondShortcut, string.Empty);

        try
        {
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(root),
                (path, asRunCommand) => CreateExecutable("Word", targetPath, path),
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);
            File.Delete(firstShortcut);

            var updatedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Deleted, firstShortcut)],
                CancellationToken.None);

            Assert.AreEqual(1, updatedItems.Count);
            Assert.IsFalse(ContainsSourceReference(updatedItems[0], $"custom-shortcut:{root}", firstShortcut));
            Assert.IsTrue(ContainsSourceReference(updatedItems[0], $"custom-shortcut:{root}", secondShortcut));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyChangesAsync_PortableSourceIgnoresChangesBelowItsMaximumDepth()
    {
        var root = CreateTemporaryDirectory("cmdpal-bounded-incremental-apps");
        var child = Path.Combine(root, "App");
        var grandchild = Path.Combine(child, "Bin");
        Directory.CreateDirectory(grandchild);
        var childApp = Path.Combine(child, "App.exe");
        var deepApp = Path.Combine(grandchild, "Helper.exe");
        File.WriteAllText(childApp, string.Empty);

        try
        {
            var portableSource = new CustomDirectoryAppSource(
                "portable",
                root,
                ["exe"],
                Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.RecurseSubdirectories,
                maximumDepth: 1);
            using var source = new Win32AppSource(
                portableSource,
                (path, asRunCommand) => CreateExecutable(Path.GetFileNameWithoutExtension(path), path),
                createWatchers: false);
            var initialItems = await source.LoadAsync(CancellationToken.None);
            File.WriteAllText(deepApp, string.Empty);

            var updatedItems = await source.ApplyChangesAsync(
                initialItems,
                [new AppSourcePathChange(WatcherChangeTypes.Created, deepApp)],
                CancellationToken.None);

            Assert.AreEqual(1, updatedItems.Count);
            Assert.AreSame(initialItems[0], updatedItems[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void WatchPaths_RetainsUnavailableCustomLocationForRecovery()
    {
        var unavailablePath = Path.Combine(Path.GetTempPath(), $"cmdpal-unavailable-apps-{Guid.NewGuid():N}");
        var source = CreateShortcutDirectorySource(unavailablePath);

        Assert.IsTrue(ContainsPath(source.WatchPaths, unavailablePath));
    }

    [TestMethod]
    public void GetWatcherRecoveryDelay_UsesCappedExponentialBackoff()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(30), Win32AppSource.GetWatcherRecoveryDelay(1));
        Assert.AreEqual(TimeSpan.FromMinutes(1), Win32AppSource.GetWatcherRecoveryDelay(2));
        Assert.AreEqual(TimeSpan.FromMinutes(5), Win32AppSource.GetWatcherRecoveryDelay(20));
    }

    [TestMethod]
    public async Task InitializeAsync_UnavailableRootRearmsWatcherAndReconcilesBlindInterval()
    {
        var unavailablePath = Path.Combine(Path.GetTempPath(), $"cmdpal-recovering-apps-{Guid.NewGuid():N}");
        var recoveryGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var source = new Win32AppSource(
                CreateShortcutDirectorySource(unavailablePath),
                (path, asRunCommand) => CreateExecutable(Path.GetFileNameWithoutExtension(path), path, path),
                createWatchers: true,
                recoveryDelayAsync: (delay, token) => recoveryGate.Task.WaitAsync(token));
            var fullRefreshCount = 0;
            source.Invalidated += (_, args) =>
            {
                if (args.RequiresFullRefresh)
                {
                    Interlocked.Increment(ref fullRefreshCount);
                }
            };

            await source.InitializeAsync(CancellationToken.None);
            Assert.IsFalse(ContainsPath(source.WatchedPaths, unavailablePath));

            Directory.CreateDirectory(unavailablePath);
            recoveryGate.SetResult(true);
            await WaitForConditionAsync(
                () => ContainsPath(source.WatchedPaths, unavailablePath) && fullRefreshCount == 1);

            Assert.AreEqual(1, fullRefreshCount);
        }
        finally
        {
            if (Directory.Exists(unavailablePath))
            {
                Directory.Delete(unavailablePath, recursive: true);
            }
        }
    }

    private static Win32Program CreateNonApp()
    {
        var program = TestDataHelper.CreateTestWin32Program("Document", @"C:\Documents\document.txt");
        program.AppType = Win32Program.ApplicationType.GenericFile;
        program.LnkFilePath = @"C:\Links\document.lnk";
        return program;
    }

    private static Win32Program CreateExecutable(string name, string targetPath, string shortcutPath = "")
    {
        var program = TestDataHelper.CreateTestWin32Program(name, targetPath);
        program.AppType = Win32Program.ApplicationType.Win32Application;
        program.LnkFilePath = shortcutPath;
        return program;
    }

    private static bool ContainsSourceReference(AppCatalogItem item, string sourceId, string itemId)
    {
        foreach (var reference in item.Provenance.References)
        {
            if (string.Equals(reference.SourceId, sourceId, StringComparison.Ordinal)
                && string.Equals(reference.ItemId, itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static AppCatalogItem FindItemByReference(
        IReadOnlyList<AppCatalogItem> items,
        string itemId)
    {
        foreach (var item in items)
        {
            foreach (var reference in item.Provenance.References)
            {
                if (string.Equals(reference.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static CustomDirectoryAppSource CreateShortcutDirectorySource(string root)
        => new(
            "custom-shortcut",
            root,
            ["lnk"],
            Win32ProgramSourceProfile.IncludeNonApplications | Win32ProgramSourceProfile.RecurseSubdirectories,
            int.MaxValue);

    private static string CreateTemporaryDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool ContainsPath(IReadOnlyList<string> paths, string candidate)
    {
        var normalizedCandidate = Path.GetFullPath(candidate);
        foreach (var path in paths)
        {
            if (string.Equals(Path.GetFullPath(path), normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }

    private static bool HasProfileOption(
        Win32ProgramSourceProfile profile,
        Win32ProgramSourceProfile option)
        => (profile & option) != 0;

    private static void AddSources(HashSet<IAppSource> destination, IReadOnlyList<IAppSource> sources)
    {
        foreach (var source in sources)
        {
            destination.Add(source);
        }
    }

    private static IAppSource FindSource(IReadOnlyList<IAppSource> sources, string id)
    {
        foreach (var source in sources)
        {
            if (string.Equals(source.Id, id, StringComparison.Ordinal))
            {
                return source;
            }
        }

        return null;
    }

    private static IAppSource FindSourceByPrefix(IReadOnlyList<IAppSource> sources, string prefix)
    {
        foreach (var source in sources)
        {
            if (source.Id.StartsWith(prefix, StringComparison.Ordinal))
            {
                return source;
            }
        }

        return null;
    }

    private sealed class EmptyAppSource : IAppSource
    {
        public event EventHandler<AppSourceInvalidatedEventArgs> Invalidated
        {
            add { }
            remove { }
        }

        public string Id => "packaged";

        public string CacheKey => Id;

        public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AppCatalogItem>>([]);

        public void Dispose()
        {
        }
    }

    private sealed class TestProgramSource : IWin32ProgramSource
    {
        private readonly IReadOnlyList<string> _paths;

        public TestProgramSource(string id, int priority, bool includeNonApps, params string[] paths)
            : this(
                id,
                priority,
                includeNonApps ? Win32ProgramSourceProfile.IncludeNonApplications : Win32ProgramSourceProfile.None,
                paths)
        {
        }

        public TestProgramSource(
            string id,
            int priority,
            Win32ProgramSourceProfile profile,
            params string[] paths)
        {
            Id = id;
            Priority = priority;
            Profile = profile;
            _paths = paths;
        }

        public string Id { get; }

        public int Priority { get; }

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile { get; }

        public string CacheKey => $"{Id}|{Profile}";

        public string ConfigurationKey => CacheKey;

        public IReadOnlyList<string> WatchPaths => [];

        public IEnumerable<string> GetPaths() => _paths;

        public bool IsRelevantPath(string path) => true;
    }
}
