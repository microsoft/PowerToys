// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Persistence;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public partial class Win32AppSourceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_RetriesShortcutWhoseTargetIsCreatedLater(bool background)
    {
        var root = CreateTemporaryDirectory("cmdpal-late-target");
        var path = Path.Combine(root, "Portable.lnk");
        var target = Path.Combine(root, "Portable.exe");
        try
        {
            CreateShortcut(path, target);
            using var source = new Win32AppSource(CreateShortcutDirectorySource(root), Win32AppReader.LoadFromPath, createWatchers: false);
            var initial = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background);
            Assert.AreEqual(0, initial.Count);
            CollectionAssert.AreEqual(new[] { path }, initial.RetryPaths.ToArray());

            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), target);
            var recovered = (AppSourceScanResult)await source.ApplyChangesAsync(
                initial,
                [new AppSourcePathChange(WatcherChangeTypes.Changed, path)],
                CancellationToken.None,
                background);
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual(0, recovered.RetryPaths.Count);
            Assert.IsTrue(recovered.IsComplete);

            // Discovery must release both the shortcut and the executable before returning.
            using var shortcutWriter = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var executableWriter = File.Open(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_SharingViolationIsIncompleteAndRecoveryReleasesTheFile(bool background)
    {
        var root = CreateTemporaryDirectory("cmdpal-locked-shortcut");
        var path = Path.Combine(root, "Console.lnk");
        try
        {
            CreateShortcut(path, Path.Combine(Environment.SystemDirectory, "cmd.exe"));
            using var source = new Win32AppSource(CreateShortcutDirectorySource(root), Win32AppReader.LoadFromPath, createWatchers: false);
            using (var writer = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var failed = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background);
                Assert.IsFalse(failed.IsComplete);
                Assert.AreEqual(0, failed.Count);
                CollectionAssert.AreEqual(new[] { path }, failed.RetryPaths.ToArray());
            }

            var recovered = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None, background);
            Assert.IsTrue(recovered.IsComplete);
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual(0, recovered.RetryPaths.Count);
            var renamed = Path.ChangeExtension(path, ".renamed");
            File.Move(path, renamed);
            using var exclusiveWriter = File.Open(renamed, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
                return TestDataHelper.CreateTestWin32Metadata("App", @"C:\Apps\app.exe");
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, loadCount);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(10, items[0].Provenance.Priority);
        Assert.IsTrue(ContainsSourceReference(items[0], "start-menu", @"C:\Apps\app.lnk"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_ShellShortcutTitlePreservesLaunchAndCommandIds(bool namespaceShortcut)
    {
        const string shortcutPath = @"C:\Links\dfrgui.LNK";
        var program = TestDataHelper.CreateTestWin32Metadata(
            "dfrgui",
            namespaceShortcut ? shortcutPath : @"C:\Tools\dfrgui.exe");
        program.LnkFilePath = namespaceShortcut ? string.Empty : shortcutPath;
        program.AppType = namespaceShortcut
            ? Win32AppType.ShortcutApplication
            : Win32AppType.Win32Application;
        using var source = new Win32AppSource(
            new TestProgramSource("start-menu", 10, includeNonApps: false, shortcutPath),
            (_, _) => program,
            createWatchers: false);
        var before = (await source.LoadAsync(CancellationToken.None)).Single();
        var previousCommandId = new AppCommand(before.ToAppItem()).Id;
        var releasedId = before.Payload.GetCommandId();
        program.DisplayName = "Defragment and Optimize Drives";

        var after = (await source.LoadAsync(CancellationToken.None)).Single();
        var payload = (Win32AppPayload)after.Payload;
        var app = after.ToAppItem();
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);

        Assert.AreEqual(program.DisplayName, row.Title);
        Assert.AreEqual(program.Name, payload.Name);
        Assert.AreEqual(program.DisplayName, payload.DisplayName);
        Assert.AreEqual(shortcutPath, app.LaunchTarget);
        Assert.AreEqual(before.Identity, after.Identity);
        Assert.AreEqual(previousCommandId, new AppCommand(app).Id);
        Assert.AreEqual(releasedId, payload.GetCommandId());
        CollectionAssert.AreEqual(before.CommandIds.ToArray(), after.CommandIds.ToArray());
        CollectionAssert.Contains(after.MatchTerms.ToArray(), program.Name);
        CollectionAssert.Contains(after.MatchTerms.ToArray(), program.DisplayName);
        Assert.AreSame(row, snapshot.GetVisibleApp(previousCommandId));
        Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
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
    public async Task LoadAsync_AppExecutionAlias_DoesNotReceiveRawExecutablePenalty()
    {
        const string aliasPath = @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\app.exe";
        var pathSource = new TestProgramSource(
            "path",
            40,
            Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.LoadAsRunCommand,
            aliasPath);
        using var source = new Win32AppSource(
            pathSource,
            (path, asRunCommand) =>
            {
                var program = CreateExecutable("app", path);
                program.AppType = Win32AppType.RunCommand;
                program.AppExecutionAlias = new ReparsePoint.AppExecutionAliasInfo
                {
                    Aumid = "Contoso.App_123!app",
                    TargetPath = @"C:\Program Files\WindowsApps\Contoso.App_1.0.0.0_x64__123\app.exe",
                };
                return program;
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(40, items[0].Provenance.Priority);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_SameTargetWithDifferentNames_CreatesSharedIdentityAndPrefersShortcut(bool defaultWorkingDirectory)
    {
        const string targetPath = @"C:\Program Files\Microsoft Office\WINWORD.EXE";
        const string shortcutPath = @"C:\Start Menu\Word.lnk";
        using var shortcutSource = new Win32AppSource(
            new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, shortcutPath),
            (path, asRunCommand) =>
            {
                var program = CreateExecutable("Word", targetPath, shortcutPath);
                program.WorkingDirectory = defaultWorkingDirectory ? Path.GetDirectoryName(targetPath)! : string.Empty;
                return program;
            },
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
        var app = merged.ToAppItem();
        Assert.AreEqual(shortcutPath, app.LaunchTarget);
        Assert.AreEqual(targetPath, app.ResolvedTarget);
        Assert.AreEqual(defaultWorkingDirectory ? Path.GetDirectoryName(targetPath) : string.Empty, ((Win32AppPayload)merged.Payload).WorkingDirectory);
        CollectionAssert.Contains(merged.CommandIds.ToArray(), shortcutItems[0].Payload.GetCommandId());
        CollectionAssert.Contains(merged.CommandIds.ToArray(), registryItems[0].Payload.GetCommandId());
        Assert.AreEqual(new AppCommand(shortcutItems[0].ToAppItem()).Id, new AppCommand(registryItems[0].ToAppItem()).Id);
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);
        var shortcutIdentity = $"win32:{shortcutPath}|args:";
        CollectionAssert.Contains(merged.IdentityAliases.ToArray(), shortcutIdentity);
        foreach (var commandId in merged.CommandIds.Append(AppIdentity.ForCommand(shortcutIdentity)))
        {
            Assert.AreSame(row, snapshot.GetVisibleApp(commandId));
            Assert.AreEqual(commandId, snapshot.GetCommandItem(commandId)?.Command?.Id);
        }

        if (defaultWorkingDirectory)
        {
            var syntheticIdentity = $"win32:{targetPath}|args:|cwd:{Path.GetDirectoryName(targetPath)}";
            CollectionAssert.DoesNotContain(merged.IdentityAliases.ToArray(), syntheticIdentity);
            Assert.IsNull(snapshot.GetVisibleApp(AppIdentity.ForCommand(syntheticIdentity)));
        }
    }

    [TestMethod]
    public async Task LoadAsync_DefaultWorkingDirectoryDoesNotPersistSyntheticCommandAlias()
    {
        var root = CreateTemporaryDirectory("cmdpal-3dsmax-aliases");
        try
        {
            const string targetPath = @"C:\Program Files\Autodesk\3ds Max 2026\3dsmax.exe";
            const string shortcutPath = @"C:\Start Menu\3ds Max 2026.lnk";
            var program = CreateExecutable("3ds Max 2026", targetPath, shortcutPath);
            program.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
            using var source = new Win32AppSource(
                new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, shortcutPath),
                (_, _) => program,
                createWatchers: false);
            var item = (await source.LoadAsync(CancellationToken.None)).Single();
            var syntheticId = AppIdentity.ForCommand($"win32:{targetPath}|args:|cwd:{program.WorkingDirectory}");
            var shortcutId = AppIdentity.ForCommand($"win32:{shortcutPath}|args:");
            var releasedId = item.Payload.GetCommandId();
            var settings = new AllAppsSettings(Path.Combine(root, "settings.json"));
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [item.ToAppItem()]);
            await settingsAliases.WaitForSavesAsync();

            Assert.AreEqual($"win32:{targetPath}|args:", item.Identity);
            Assert.IsFalse(aliases.ContainsKey(syntheticId));
            var savedAliases = JsonNode.Parse(File.ReadAllText(settingsAliases.FilePath))!["AppCommandAliases"]!.AsObject();
            Assert.IsFalse(savedAliases.ContainsKey(syntheticId));
            Assert.AreEqual(AppIdentity.ForCommand(item.Identity), savedAliases[shortcutId]!.GetValue<string>());
            Assert.AreEqual(AppIdentity.ForCommand(item.Identity), savedAliases[releasedId]!.GetValue<string>());
            var row = new AppListItem(item.ToAppItem());
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);
            Assert.IsNull(snapshot.GetCommandItem(syntheticId));
            Assert.AreSame(row, snapshot.GetVisibleApp(shortcutId));
            Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\Google\Chrome\Application\chrome_proxy.exe", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\chrome_proxy.exe")]
    [DataRow(@"C:\Program Files\BraveSoftware\Brave-Browser\Application\chrome_proxy.exe", @"C:\Program Files\Google\Chrome\Application\chrome_proxy.exe")]
    public async Task LoadAsync_ChromiumPwasMergeDuplicateShortcutsAndKeepLaunchProfilesSeparate(string browser, string otherBrowser)
    {
        const string arguments = "--profile-directory=Default --app-id=agimnkijcaahngcdmfeangaknmldooml";
        var programs = new Dictionary<string, Win32AppMetadata>(StringComparer.OrdinalIgnoreCase);
        var entries = new[]
        {
            (Path: @"C:\Desktop\YouTube.lnk", Target: browser, Arguments: arguments),
            (Path: @"C:\Start Menu\YouTube renamed.lnk", Target: browser, Arguments: arguments),
            (Path: @"C:\Start Menu\YouTube work.lnk", Target: browser, Arguments: arguments.Replace("=Default", "=Work", StringComparison.Ordinal)),
            (Path: @"C:\Start Menu\YouTube other data.lnk", Target: browser, Arguments: arguments + " --user-data-dir=\"C:\\Profiles\\Other\""),
            (Path: @"C:\Start Menu\YouTube other browser.lnk", Target: otherBrowser, Arguments: arguments),
            (Path: @"C:\Start Menu\Another app.lnk", Target: browser, Arguments: arguments.Replace("agimnkijcaahngcdmfeangaknmldooml", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", StringComparison.Ordinal)),
        };
        foreach (var entry in entries)
        {
            var program = CreateExecutable(Path.GetFileNameWithoutExtension(entry.Path), entry.Target, entry.Path);
            program.AppType = Win32AppType.WebApplication;
            program.Arguments = entry.Arguments;
            program.WorkingDirectory = Path.GetDirectoryName(entry.Target)!;
            programs.Add(entry.Path, program);
        }

        using var source = new Win32AppSource(
            new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, programs.Keys.ToArray()),
            (path, _) => programs[path],
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(5, items.Count);
        var duplicate = items.Single(item => item.Provenance.References.Length == 2);
        CollectionAssert.AreEquivalent(entries.Take(2).Select(entry => entry.Path).ToArray(), duplicate.Provenance.References.Select(reference => reference.ItemId).ToArray());
        var app = duplicate.ToAppItem();
        Assert.AreEqual(arguments, app.LaunchArguments);
        Assert.AreEqual(browser, app.ResolvedTarget);
        Assert.IsTrue(app.IsWebApp);
        Assert.IsFalse(app.IsPackaged);
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);
        foreach (var entry in entries.Take(2))
        {
            Assert.AreSame(row, snapshot.GetApp(Win32AppPayload.From(programs[entry.Path]).GetCommandId()));
        }
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
        var first = items.Single(item => item.ToAppItem().LaunchArguments == "/Profile A");
        var second = items.Single(item => item.ToAppItem().LaunchArguments == "/profile a");
        Assert.AreNotEqual(first.Identity, second.Identity);
        var firstApp = first.ToAppItem();
        var secondApp = second.ToAppItem();
        Assert.AreEqual(@"C:\Links\First.lnk", firstApp.LaunchTarget);
        Assert.AreEqual(@"C:\Links\Second.lnk", secondApp.LaunchTarget);
        Assert.AreEqual(targetPath, firstApp.ResolvedTarget);
        Assert.AreEqual(targetPath, secondApp.ResolvedTarget);
        Assert.AreNotEqual(new AppCommand(firstApp).Id, new AppCommand(secondApp).Id);
    }

    [TestMethod]
    [DataRow(@"C:\Projects\First", @"C:\Projects\Second", 2)]
    [DataRow("", @"C:\Projects\First", 2)]
    [DataRow(@"C:\Projects\First", "c:/projects/first/", 1)]
    [DataRow("", @"C:\Tools", 1)]
    [DataRow("c:/TOOLS/", "", 1)]
    public async Task LoadAsync_WorkingDirectoryParticipatesInShortcutEquivalence(string firstDirectory, string secondDirectory, int expectedCount)
    {
        var definition = new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, @"C:\Links\First.lnk", @"C:\Links\Second.lnk");
        using var source = new Win32AppSource(
            definition,
            (path, _) =>
            {
                var program = CreateExecutable(Path.GetFileNameWithoutExtension(path), @"C:\Tools\console.exe", path);
                program.Arguments = "/k";
                program.WorkingDirectory = path.Contains("First", StringComparison.Ordinal) ? firstDirectory : secondDirectory;
                return program;
            },
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(expectedCount, items.Count);
        Assert.AreEqual(2, items.SelectMany(item => item.CommandIds).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(expectedCount, items.Select(item => new AppCommand(item.ToAppItem()).Id).Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    [DataRow("GitHubDesktop", "GitHubDesktop.exe", "2.7.2", "3.4.9", "3.6.3")]
    [DataRow("AnthropicClaude", "claude.exe", "1.1.7464", null, "2.19675.0")]
    [DataRow("SourceTree", "SourceTree.exe", "3.4.3", null, "3.4.32")]
    public async Task LoadAsync_SquirrelVersionShortcutsMergeWithoutLosingLaunchDataOrSavedState(
        string packageName,
        string executableName,
        string firstOldVersion,
        string secondOldVersion,
        string currentVersion)
    {
        var root = CreateTemporaryDirectory("cmdpal-squirrel-profiles");
        try
        {
            var installDirectory = Path.Combine(@"C:\Users\Test\AppData\Local", packageName);
            var targetPath = Path.Combine(installDirectory, executableName);
            var startMenuPath = Path.Combine(@"C:\Start Menu", $"{packageName}.lnk");
            var versions = secondOldVersion is null ? new[] { firstOldVersion } : new[] { firstOldVersion, secondOldVersion };
            var desktopPaths = versions.Select((version, index) => Path.Combine(@"C:\Desktop", $"{packageName}-{index}.lnk")).ToArray();
            var programs = new Dictionary<string, Win32AppMetadata>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < versions.Length; index++)
            {
                var program = CreateExecutable(packageName, targetPath, desktopPaths[index]);
                program.WorkingDirectory = Path.Combine(installDirectory, $"app-{versions[index]}");
                program.ExplicitAppUserModelId = $"com.squirrel.{packageName}.{Path.GetFileNameWithoutExtension(executableName)}";
                programs.Add(desktopPaths[index], program);
            }

            var preferredProgram = CreateExecutable(packageName, targetPath, startMenuPath);
            preferredProgram.WorkingDirectory = Path.Combine(installDirectory, $"app-{currentVersion}");
            preferredProgram.ExplicitAppUserModelId = $"com.squirrel.{packageName}.{Path.GetFileNameWithoutExtension(executableName)}";
            programs.Add(startMenuPath, preferredProgram);
            using var desktopSource = new Win32AppSource(
                new TestProgramSource("desktop", 20, Win32ProgramSourceProfile.None, desktopPaths),
                (path, _) => programs[path],
                createWatchers: false);
            using var startMenuSource = new Win32AppSource(
                new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, startMenuPath),
                (path, _) => programs[path],
                createWatchers: false);
            var desktopItems = await desktopSource.LoadAsync(CancellationToken.None);
            var startMenuItems = await startMenuSource.LoadAsync(CancellationToken.None);
            var merged = desktopItems.Single().MergeProvenance(startMenuItems.Single());

            Assert.AreEqual($"win32:{targetPath}|args:", merged.Identity);
            Assert.AreEqual(10, merged.Provenance.Priority);
            Assert.AreEqual(programs.Count, merged.Provenance.References.Length);
            foreach (var path in desktopPaths)
            {
                Assert.IsTrue(ContainsSourceReference(merged, "desktop", path));
            }

            Assert.IsTrue(ContainsSourceReference(merged, "start-menu", startMenuPath));
            var payload = (Win32AppPayload)merged.Payload;
            Assert.AreEqual(preferredProgram.WorkingDirectory, payload.WorkingDirectory);
            Assert.AreEqual(targetPath, merged.Payload.GetCanonicalTargetPath());
            Assert.IsNull(merged.Payload.GetCanonicalIdentityHint());
            Assert.AreEqual(startMenuPath, merged.ToAppItem().LaunchTarget);
            var row = new AppListItem(merged.ToAppItem());
            var snapshot = new AppListItemSnapshot([row], []);
            foreach (var program in programs.Values)
            {
                var shortcutIdentity = $"win32:{program.LnkFilePath}|args:";
                CollectionAssert.Contains(merged.IdentityAliases.ToArray(), shortcutIdentity);
                var shortcutId = AppIdentity.ForCommand(shortcutIdentity);
                Assert.AreSame(row, snapshot.GetVisibleApp(shortcutId));
                Assert.AreEqual(shortcutId, snapshot.GetCommandItem(shortcutId)?.Command?.Id);
                Assert.AreSame(row, snapshot.GetVisibleApp(Win32AppPayload.From(program).GetCommandId()));
                var syntheticIdentity = $"win32:{targetPath}|args:|cwd:{program.WorkingDirectory}";
                CollectionAssert.DoesNotContain(merged.IdentityAliases.ToArray(), syntheticIdentity);
                Assert.IsNull(snapshot.GetCommandItem(AppIdentity.ForCommand(syntheticIdentity)));
            }

            var desktopPayload = (Win32AppPayload)desktopItems.Single().Payload;
            Assert.AreEqual(programs[desktopPayload.LnkFilePath].WorkingDirectory, desktopPayload.WorkingDirectory);
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            TestDataHelper.WriteHiddenIdentities(settingsPath, $"win32:{desktopPaths[0]}|args:");
            var visibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settings.FilePath));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(visibility, merged, settings: settings, aliases: settingsAliases));
            visibility.Persist();
            var reloadedVisibilitySettings = new AllAppsSettings(settingsPath);
            using var reloadedVisibilityAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(reloadedVisibilitySettings.FilePath));
            var reloadedVisibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(reloadedVisibilitySettings.FilePath));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(reloadedVisibility, merged, settings: reloadedVisibilitySettings, aliases: reloadedVisibilityAliases));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("--profile work", "app-2.7.2")]
    [DataRow("", @"C:\Projects\Work")]
    [DataRow("", "app-custom")]
    public async Task LoadAsync_SquirrelIdPreservesDistinctArgumentsAndCustomWorkingDirectories(string arguments, string secondDirectory)
    {
        const string installDirectory = @"C:\Users\Test\AppData\Local\GitHubDesktop";
        var targetPath = Path.Combine(installDirectory, "GitHubDesktop.exe");
        var first = CreateExecutable("GitHub Desktop", targetPath, @"C:\Links\First.lnk");
        first.WorkingDirectory = Path.Combine(installDirectory, "app-3.6.3");
        first.ExplicitAppUserModelId = "com.squirrel.GitHubDesktop.GitHubDesktop";
        var second = CreateExecutable("GitHub Desktop", targetPath, @"C:\Links\Second.lnk");
        second.Arguments = arguments;
        second.WorkingDirectory = Path.IsPathFullyQualified(secondDirectory) ? secondDirectory : Path.Combine(installDirectory, secondDirectory);
        second.ExplicitAppUserModelId = first.ExplicitAppUserModelId;
        using var source = new Win32AppSource(
            new TestProgramSource("start-menu", 10, Win32ProgramSourceProfile.None, first.LnkFilePath, second.LnkFilePath),
            (path, _) => path == first.LnkFilePath ? first : second,
            createWatchers: false);

        var items = await source.LoadAsync(CancellationToken.None);

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(2, items.Select(item => new AppCommand(item.ToAppItem()).Id).Distinct(StringComparer.Ordinal).Count());
        var secondPayload = (Win32AppPayload)FindItemByReference(items, second.LnkFilePath).Payload;
        Assert.AreEqual(arguments, secondPayload.Arguments);
        Assert.AreEqual(second.WorkingDirectory, secondPayload.WorkingDirectory);
    }

    [TestMethod]
    [DataRow((int)Win32AppType.ShortcutApplication)]
    [DataRow((int)Win32AppType.GenericFile)]
    public async Task LoadAsync_FallbackIdentityIgnoresDisplayNameAndRetainsReleasedCommandIds(int appTypeValue)
    {
        var appType = (Win32AppType)appTypeValue;
        var root = CreateTemporaryDirectory("cmdpal-fallback-aliases");
        try
        {
            const string path = @"C:\Links\App.lnk";
            var name = "Original";
            using var source = new Win32AppSource(
                new TestProgramSource("test", 0, Win32ProgramSourceProfile.IncludeNonApplications, path),
                (_, _) =>
                {
                    var program = CreateExecutable(name, path, path);
                    program.AppType = appType;
                    return program;
                },
                createWatchers: false);
            var original = (await source.LoadAsync(CancellationToken.None)).Single();
            var settings = new AllAppsSettings(Path.Combine(root, "settings.json"));
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            TestDataHelper.RetainCommandAliases(settingsAliases, [original.ToAppItem()]);
            name = "Localized";
            var renamed = (await source.LoadAsync(CancellationToken.None)).Single();
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [renamed.ToAppItem()]);
            await settingsAliases.WaitForSavesAsync();

            Assert.AreEqual(original.Identity, renamed.Identity);
            Assert.AreEqual(new AppCommand(original.ToAppItem()).Id, new AppCommand(renamed.ToAppItem()).Id);
            var row = new AppListItem(renamed.ToAppItem());
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);
            foreach (var item in new[] { original, renamed })
            {
                var syntheticIdentity = $"win32:{((Win32AppPayload)item.Payload).Name}|{path}|args:";
                CollectionAssert.DoesNotContain(item.IdentityAliases.ToArray(), syntheticIdentity);
                Assert.IsNull(snapshot.GetCommandItem(AppIdentity.ForCommand(syntheticIdentity)));
                var releasedId = item.Payload.GetCommandId();
                Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
                Assert.AreEqual(releasedId, snapshot.GetCommandItem(releasedId)?.Command?.Id);
            }

            Assert.AreSame(row, snapshot.GetVisibleApp(AppIdentity.ForCommand($"win32:{path}|args:")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_CommandAliasesFollowShortcutWhenTargetAndDisplayNameChangeTogether()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-move-{Guid.NewGuid():N}.json");
        try
        {
            const string shortcut = @"C:\Links\Editor.lnk";
            var program = CreateExecutable("Editor", @"C:\Old\Editor.exe", shortcut);
            program.Arguments = "--profile Work";
            using var source = new Win32AppSource(
                new TestProgramSource("test", 0, includeNonApps: false, shortcut),
                (_, _) => program,
                createWatchers: false);
            var original = (await source.LoadAsync(CancellationToken.None)).Single();
            var primaryId = new AppCommand(original.ToAppItem()).Id;
            var legacyId = original.Payload.GetCommandId();
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            TestDataHelper.RetainCommandAliases(settingsAliases, [original.ToAppItem()]);

            program.Name = "Éditeur";
            program.Description = "Éditeur de texte";
            program.TargetPath = @"D:\New\Editor.exe";
            var current = (await source.LoadAsync(CancellationToken.None)).Single();
            Assert.AreNotEqual(original.Identity, current.Identity);
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [current.ToAppItem()]);
            var row = new AppListItem(current.ToAppItem());
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);
            Assert.AreSame(row, snapshot.GetVisibleApp(primaryId));
            Assert.AreSame(row, snapshot.GetVisibleApp(legacyId));
            await settingsAliases.WaitForSavesAsync();
            using var reloadedAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath));
            aliases = TestDataHelper.RetainCommandAliases(reloadedAliases, [current.ToAppItem()]);
            snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);
            Assert.AreEqual("Éditeur", snapshot.GetCommandItem(primaryId)?.Title);
            Assert.AreEqual(program.TargetPath, snapshot.GetVisibleApp(legacyId)?.App.ResolvedTarget);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task LoadAsync_RelativeWorkingDirectoryIdentityIgnoresCurrentDirectory()
    {
        var originalDirectory = Environment.CurrentDirectory;
        var root = CreateTemporaryDirectory("cmdpal-relative-start-in");
        try
        {
            const string shortcut = @"C:\Links\Console.lnk";
            var program = CreateExecutable("Console", @"C:\Tools\Console.exe", shortcut);
            program.WorkingDirectory = ".";
            using var source = new Win32AppSource(
                new TestProgramSource("test", 0, includeNonApps: false, shortcut),
                (_, _) => program,
                createWatchers: false);
            Environment.CurrentDirectory = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            var original = (await source.LoadAsync(CancellationToken.None)).Single();
            Environment.CurrentDirectory = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
            var current = (await source.LoadAsync(CancellationToken.None)).Single();

            Assert.AreEqual(@"win32:C:\Tools\Console.exe|args:|cwd:.", current.Identity);
            Assert.AreEqual(original.Identity, current.Identity);
            CollectionAssert.AreEqual(original.IdentityAliases.ToArray(), current.IdentityAliases.ToArray());
            CollectionAssert.AreEqual(original.ToAppItem().CommandIds.ToArray(), current.ToAppItem().CommandIds.ToArray());
            Assert.AreEqual(".", ((Win32AppPayload)current.Payload).WorkingDirectory);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    [DataRow("steam://rungameid/123")]
    [DataRow("com.epicgames.launcher://apps/Example?action=launch&silent=true")]
    public async Task LoadAsync_GameUriIdentityAndSavedStateIgnoreCurrentDirectory(string uri)
    {
        var originalDirectory = Environment.CurrentDirectory;
        var root = CreateTemporaryDirectory("cmdpal-game-uri");
        try
        {
            var shortcut = Path.Combine(root, "Game.url");
            File.WriteAllText(shortcut, $"[InternetShortcut]\r\nURL={uri}\r\nIconFile=C:\\Icons\\Game.ico\r\n");
            using var source = new Win32AppSource(
                new TestProgramSource("test", 0, includeNonApps: false, shortcut),
                Win32AppReader.LoadFromPath,
                createWatchers: false);
            Environment.CurrentDirectory = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            var original = (await source.LoadAsync(CancellationToken.None)).Single();
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [original.ToAppItem()]);
            var originalVisibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));
            TestDataHelper.SetHidden(originalVisibility, original, hidden: true, aliases: settingsAliases);
            originalVisibility.Persist();
            await settingsAliases.WaitForSavesAsync();

            Environment.CurrentDirectory = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
            var current = (await source.LoadAsync(CancellationToken.None)).Single();
            Assert.AreEqual($"win32:{LaunchTarget.Url(uri).IdentityToken}|args:", current.Identity);
            Assert.AreEqual(original.Identity, current.Identity);
            Assert.AreEqual(uri, current.ToAppItem().LaunchTarget);
            var snapshot = new AppListItemSnapshot([new AppListItem(current.ToAppItem())], [], commandAliases: aliases);
            Assert.IsNotNull(snapshot.GetCommandItem(new AppCommand(original.ToAppItem()).Id));
            Assert.IsNotNull(snapshot.GetCommandItem(original.Payload.GetCommandId()));
            var visibilitySettings = new AllAppsSettings(settingsPath);
            using var visibilityAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(visibilitySettings.FilePath));
            var visibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(visibilitySettings.FilePath));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(visibility, current, settings: visibilitySettings, aliases: visibilityAliases));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_GameUrlsDifferingByCase_KeepSeparateCommandsAndHides()
    {
        var root = CreateTemporaryDirectory("cmdpal-game-url-case");
        try
        {
            var firstPath = Path.Combine(root, "First.url");
            var secondPath = Path.Combine(root, "Second.url");
            const string firstUrl = "com.epicgames.launcher://apps/Example?action=launch";
            const string secondUrl = "com.epicgames.launcher://apps/example?action=launch";
            File.WriteAllText(firstPath, $"[InternetShortcut]\r\nURL={firstUrl}\r\n");
            File.WriteAllText(secondPath, $"[InternetShortcut]\r\nURL={secondUrl}\r\n");
            using var source = new Win32AppSource(
                new TestProgramSource("test", 0, includeNonApps: false, firstPath, secondPath),
                Win32AppReader.LoadFromPath,
                createWatchers: false);
            var items = await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(2, items.Select(item => item.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            var first = items.Single(item => ((Win32AppPayload)item.Payload).TargetPath == firstUrl);
            var second = items.Single(item => ((Win32AppPayload)item.Payload).TargetPath == secondUrl);
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, items.Select(item => item.ToAppItem()));
            var snapshot = new AppListItemSnapshot(items.Select(item => new AppListItem(item.ToAppItem())).ToArray(), [], commandAliases: aliases);
            Assert.AreEqual(firstUrl, snapshot.GetVisibleApp(first.Payload.GetCommandId())?.App.LaunchTarget);
            Assert.AreEqual(secondUrl, snapshot.GetVisibleApp(second.Payload.GetCommandId())?.App.LaunchTarget);

            var visibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settings.FilePath));
            Assert.IsTrue(TestDataHelper.SetHidden(visibility, first, hidden: true, aliases: settingsAliases));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(visibility, first, settings: settings, aliases: settingsAliases));
            Assert.AreEqual(AppVisibility.Visible, TestDataHelper.GetVisibility(visibility, second, settings: settings, aliases: settingsAliases));
            settings.SaveSettings();
            Assert.IsFalse(File.Exists(visibility.FilePath), "Saving preferences must not persist manual hides.");
            visibility.Persist();
            await settingsAliases.WaitForSavesAsync();
            var reloadedVisibilitySettings = new AllAppsSettings(settingsPath);
            using var reloadedVisibilityAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(reloadedVisibilitySettings.FilePath));
            var reloadedVisibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(reloadedVisibilitySettings.FilePath));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(reloadedVisibility, first, settings: reloadedVisibilitySettings, aliases: reloadedVisibilityAliases));
            Assert.AreEqual(AppVisibility.Visible, TestDataHelper.GetVisibility(reloadedVisibility, second, settings: reloadedVisibilitySettings, aliases: reloadedVisibilityAliases));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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

            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            Assert.IsFalse(((AppSourceScanResult)updatedItems).IsFullScan);
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

    [TestMethod]
    public async Task LoadAsync_DifferentArguments_KeepsBothPrograms()
    {
        var first = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        first.Arguments = "--profile first";

        var second = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        second.Arguments = "--profile second";

        var result = await LoadCatalogItemsAsync(first, second);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_SquirrelVersionDirectoriesPreserveArguments(bool differentArguments)
    {
        const string target = @"C:\Apps\GitHubDesktop\GitHubDesktop.exe";
        const string explicitId = "com.squirrel.GitHubDesktop.GitHubDesktop";
        var first = TestDataHelper.CreateTestWin32Metadata("GitHub Desktop", target);
        first.ExplicitAppUserModelId = explicitId;
        first.WorkingDirectory = @"C:\Apps\GitHubDesktop\app-2.7.2";
        var second = TestDataHelper.CreateTestWin32Metadata("GitHub Desktop", target);
        second.ExplicitAppUserModelId = explicitId;
        second.WorkingDirectory = @"C:\Apps\GitHubDesktop\app-3.6.3";
        second.Arguments = differentArguments ? "--profile Work" : string.Empty;

        Assert.AreEqual(differentArguments ? 2 : 1, (await LoadCatalogItemsAsync(first, second)).Count);
        Assert.AreEqual(@"C:\Apps\GitHubDesktop\app-2.7.2", first.WorkingDirectory);
        Assert.AreEqual(@"C:\Apps\GitHubDesktop\app-3.6.3", second.WorkingDirectory);
    }

    [TestMethod]
    public async Task LoadAsync_DefaultWorkingDirectory_RemovesDuplicate()
    {
        var first = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        var second = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        second.WorkingDirectory = "c:/TOOLS/";

        Assert.AreEqual(1, (await LoadCatalogItemsAsync(first, second)).Count);
        Assert.AreEqual("c:/TOOLS/", second.WorkingDirectory, "Identity normalization must retain launch data.");
    }

    [TestMethod]
    public async Task LoadAsync_DifferentWorkingDirectories_KeepsBothPrograms()
    {
        var first = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        first.WorkingDirectory = @"C:\Projects\First";
        var second = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        second.WorkingDirectory = @"C:\Projects\Second";

        Assert.AreEqual(2, (await LoadCatalogItemsAsync(first, second)).Count);
    }

    [TestMethod]
    public async Task LoadAsync_CaseOnlyDifference_RemovesDuplicate()
    {
        var first = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        first.Arguments = "--profile default";
        first.WorkingDirectory = @"C:\Projects\Default";

        var second = TestDataHelper.CreateTestWin32Metadata("CONSOLE", @"c:\tools\CONSOLE.exe");
        second.Arguments = "--profile default";
        second.WorkingDirectory = @"c:\projects\DEFAULT";

        var result = await LoadCatalogItemsAsync(first, second);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task LoadAsync_UrlPayloadDiffersByCase_KeepsBothPrograms()
    {
        var first = TestDataHelper.CreateTestWin32Metadata("Game", "com.epicgames.launcher://apps/Example?action=launch");
        first.AppType = Win32AppType.InternetShortcutApplication;
        var second = TestDataHelper.CreateTestWin32Metadata("Game", "com.epicgames.launcher://apps/example?action=launch");
        second.AppType = Win32AppType.InternetShortcutApplication;
        Assert.AreEqual(2, (await LoadCatalogItemsAsync(first, second)).Count);
    }

    private static async Task<IReadOnlyList<AppCatalogItem>> LoadCatalogItemsAsync(params Win32AppMetadata[] metadata)
    {
        var paths = metadata.Select((_, index) => $@"C:\Links\CatalogIdentity-{index}.lnk").ToArray();
        using var source = new Win32AppSource(
            new TestProgramSource("test", 0, Win32ProgramSourceProfile.IncludeRawExecutables, paths),
            (path, _) => metadata[Array.IndexOf(paths, path)],
            createWatchers: false);
        return await source.LoadAsync(CancellationToken.None);
    }

    private static Win32AppMetadata CreateNonApp()
    {
        var program = TestDataHelper.CreateTestWin32Metadata("Document", @"C:\Documents\document.txt");
        program.AppType = Win32AppType.GenericFile;
        program.LnkFilePath = @"C:\Links\document.lnk";
        return program;
    }

    private static Win32AppMetadata CreateExecutable(string name, string targetPath, string shortcutPath = "")
    {
        var program = TestDataHelper.CreateTestWin32Metadata(name, targetPath);
        program.AppType = Win32AppType.Win32Application;
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

    private static void CreateShortcut(string path, string target)
    {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(path);
        try
        {
            shortcut.TargetPath = target;
            shortcut.Description = "Recovery fixture";
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static CustomDirectoryAppSource CreateShortcutDirectorySource(string root)
    {
        return new(
            "custom-shortcut",
            root,
            ["lnk"],
            Win32ProgramSourceProfile.IncludeNonApplications | Win32ProgramSourceProfile.RecurseSubdirectories,
            int.MaxValue);
    }

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
    {
        return (profile & option) != 0;
    }

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

        public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false, IReadOnlyList<AppSourcePathChange> dirtyPaths = null)
        {
            return Task.FromResult<IReadOnlyList<AppCatalogItem>>([]);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestProgramSource : IWin32ProgramSource
    {
        private readonly IReadOnlyList<string> _paths;

        public string Id { get; }

        public int Priority { get; }

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile { get; }

        public string CacheKey => $"{Id}|{Profile}";

        public string ConfigurationKey => CacheKey;

        public IReadOnlyList<string> WatchPaths => [];

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

        public IEnumerable<string> GetPaths()
        {
            return _paths;
        }

        public bool IsRelevantPath(string path)
        {
            return true;
        }
    }
}
