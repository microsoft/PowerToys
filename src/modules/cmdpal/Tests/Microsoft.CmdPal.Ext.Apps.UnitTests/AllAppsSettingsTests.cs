// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsSettingsTests
{
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
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create(namePattern)),
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(pathPattern)),
            }.ToJsonString());
            using var visibility = new SettingsAppVisibilityStore(settings);
            var item = new AppCatalogItem(
                "win32:app",
                0,
                new AppCatalogSourceReference("test", path),
                [],
                new Win32AppPayload { Name = name, FullPath = path });

            Assert.AreEqual(expected ? AppVisibility.HiddenByPattern : AppVisibility.Visible, visibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void ExclusionPatterns_FormSubmissionPersistsAndIgnoresUnrelatedSettingsChanges()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);
            var changedCount = 0;
            visibility.Changed += (_, _) => changedCount++;
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            var values = new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create("*Updater*")).ToJsonString(),
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Tools\*")).ToJsonString(),
            };
            form.SubmitForm(values.ToJsonString(), string.Empty);

            Assert.AreEqual(1, changedCount);
            var reloaded = new AllAppsSettings(settingsPath);
            Assert.AreEqual("*Updater*", reloaded.ExcludedAppNames.Single());
            Assert.AreEqual(@"C:\Tools\*", reloaded.ExcludedAppPaths.Single());

            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            Assert.AreEqual(1, changedCount);
            visibility.Dispose();
            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\"}", string.Empty);
            Assert.AreEqual(1, changedCount);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void PathExclusions_CoverShortcutsAliasesPackagesAndMergedRepresentations()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Hidden\*")),
            }.ToJsonString());
            using var visibility = new SettingsAppVisibilityStore(settings);
            IAppCatalogPayload[] payloads =
            [
                new Win32AppPayload { LnkFilePath = @"C:\Hidden\App.lnk" },
                new Win32AppPayload { AppExecutionAliasTargetPath = @"C:\Hidden\App.exe" },
                new PackagedAppSnapshot { PackageLocation = @"C:\Hidden\Package" },
            ];
            foreach (var payload in payloads)
            {
                var item = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], payload);
                Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(item));
            }

            var preferred = new AppCatalogItem("app", 0, new AppCatalogSourceReference("test", "app"), [], new PackagedAppSnapshot());
            var shortcut = new AppCatalogItem("app", 1, new AppCatalogSourceReference("shortcuts", @"C:\Hidden\App.lnk"), [], new Win32AppPayload());
            var target = new AppCatalogItem("app", 1, new AppCatalogSourceReference("other", "app"), [@"C:\Hidden\App.exe"], new Win32AppPayload());

            Assert.AreEqual(AppVisibility.Visible, visibility.GetVisibility(preferred));
            Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(preferred.MergeProvenance(shortcut)));
            Assert.AreEqual(AppVisibility.HiddenByPattern, visibility.GetVisibility(preferred.MergeProvenance(target)));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void SettingsVisibilityStore_PersistsHiddenIdentityAcrossSettingsReload()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            var item = CreateCatalogItem("win32:stable-identity");
            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);

            Assert.IsTrue(visibility.SetHidden(item, hidden: true));
            visibility.Persist();
            StringAssert.Contains(File.ReadAllText(settingsPath), "\"futureSetting\": \"preserved\"");
            var temporaryFiles = Directory.GetFiles(
                Path.GetDirectoryName(settingsPath)!,
                $"{Path.GetFileName(settingsPath)}.*.tmp");
            Assert.AreEqual(0, temporaryFiles.Length);

            var reloadedSettings = new AllAppsSettings(settingsPath);
            using var reloadedVisibility = new SettingsAppVisibilityStore(reloadedSettings);
            Assert.AreEqual(AppVisibility.Hidden, reloadedVisibility.GetVisibility(item));

            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            reloadedVisibility.Persist();

            var unhiddenSettings = new AllAppsSettings(settingsPath);
            using var unhiddenVisibility = new SettingsAppVisibilityStore(unhiddenSettings);
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SettingsVisibilityStore_RetainsAndClearsHiddenIdentitiesAfterCoalescing(bool preferPackaged)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            const string oldIdentity = @"win32:C:\Tools\app.exe|args:|cwd:C:\Tools";
            const string canonicalIdentity = @"win32:C:\Tools\app.exe|args:";
            var oldItem = CreateCatalogItem(oldIdentity);
            var item = oldItem.WithIdentity(canonicalIdentity);
            if (preferPackaged)
            {
                var packaged = new AppCatalogItem(
                    canonicalIdentity,
                    -1,
                    new AppCatalogSourceReference("packaged", "Contoso.App!app"),
                    [],
                    new PackagedAppSnapshot { Name = "App", UserModelId = "Contoso.App!app" });
                item = item.MergeProvenance(packaged).WithIdentity("packaged:Contoso.App!app");
            }

            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);
            Assert.IsTrue(visibility.SetHidden(oldItem, hidden: true));
            visibility.Persist();

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedVisibility = new SettingsAppVisibilityStore(reloaded);
            Assert.AreEqual(AppVisibility.Hidden, reloadedVisibility.GetVisibility(item));
            Assert.IsTrue(reloaded.SetAppHidden(item.Identity, hidden: true));
            Assert.IsTrue(reloadedVisibility.SetHidden(item, hidden: false));
            Assert.AreEqual(AppVisibility.Visible, reloadedVisibility.GetVisibility(item));
            Assert.IsFalse(reloaded.IsAppHidden(oldIdentity));
            reloadedVisibility.Persist();

            using var unhiddenVisibility = new SettingsAppVisibilityStore(new AllAppsSettings(settingsPath));
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(oldItem));
            Assert.AreEqual(AppVisibility.Visible, unhiddenVisibility.GetVisibility(item));
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_PreserveFlatStorageAndPersistLatestPublication()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var apps = Enumerable.Range(0, 500).Select(index => new AppItem
            {
                CatalogId = $@"win32:C:\Apps\App{index}.exe|args:",
                Name = $"App {index}",
                ExePath = $@"C:\Links\App{index}.lnk",
                CommandIds =
                [
                    AppIdentity.ForCommand($@"win32:C:\Desktop\App{index}.lnk|args:"),
                    AppIdentity.ForCommand($@"win32:C:\Start Menu\App{index}.lnk|args:"),
                    AppCommand.GenerateId($"Registry App {index}", string.Empty, $@"C:\Apps\App{index}.exe"),
                ],
            }).ToArray();
            var flatAliases = new JsonObject { ["ambiguous_old-id"] = string.Empty };
            foreach (var app in apps)
            {
                var commandId = AppIdentity.ForCommand(app.CatalogId);
                foreach (var alias in app.CommandIds.Append(AppCommand.GenerateId(app.Name, app.Subtitle, app.ExePath)).Append(commandId))
                {
                    flatAliases[alias] = commandId;
                }
            }

            var legacyJson = new JsonObject
            {
                ["futureSetting"] = "preserved",
                ["AppCommandAliases"] = flatAliases,
                ["AppCommandAliasGroups"] = new JsonObject
                {
                    ["unused_target"] = new JsonArray(JsonValue.Create("unused_alias")),
                },
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, legacyJson);
            var settings = new AllAppsSettings(settingsPath);
            var aliases = settings.RetainAppCommandAliases(apps);
            settings.SaveSettings();
            await settings.WaitForAliasSavesAsync();
            var saved = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var savedAliases = saved["AppCommandAliases"]!.AsObject();
            Assert.IsFalse(saved.ContainsKey("AppCommandAliasGroups"), "The grouped alias format from earlier pre-release builds must be cleared from the settings file.");
            Assert.AreEqual(aliases.Count, savedAliases.Count);
            Assert.IsTrue(apps.All(app => !savedAliases.ContainsKey(AppIdentity.ForCommand(app.CatalogId))), "Redundant self-aliases must not be persisted.");
            Assert.AreEqual("preserved", saved["futureSetting"]!.GetValue<string>());

            var reloadedAliases = new AllAppsSettings(settingsPath).RetainAppCommandAliases(apps);
            foreach (var alias in aliases)
            {
                Assert.AreEqual(alias.Value, reloadedAliases[alias.Key]);
            }

            Assert.AreEqual(string.Empty, reloadedAliases["ambiguous_old-id"]);
            var originalId = AppIdentity.ForCommand(apps[0].CatalogId);
            var legacyId = AppCommand.GenerateId(apps[0].Name, apps[0].Subtitle, apps[0].ExePath);
            apps[0].Name = "Renamed App";
            settings.RetainAppCommandAliases(apps);
            apps[0].CatalogId = @"win32:D:\Moved\App0.exe|args:";
            settings.RetainAppCommandAliases(apps);
            await settings.WaitForAliasSavesAsync();
            reloadedAliases = new AllAppsSettings(settingsPath).RetainAppCommandAliases(apps);
            var currentId = AppIdentity.ForCommand(apps[0].CatalogId);
            Assert.AreEqual(currentId, reloadedAliases[originalId]);
            Assert.AreEqual(currentId, reloadedAliases[legacyId]);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_CasingOnlyPackagedUpdatePreservesLegacyAliasesAfterReload()
    {
        var settingsPath = TemporarySettingsPath();
        var settings = new AllAppsSettings(settingsPath);
        try
        {
            const string earlierAumid = "contoso.app_123!main";
            const string currentAumid = "Contoso.App_123!Main";
            const string historicalLegacyId = "Earlier display name_42";
            var app = new AppItem
            {
                CatalogId = AppIdentity.ForPackaged(earlierAumid),
                Name = "App display name",
                UserModelId = earlierAumid,
                IsPackaged = true,
                CommandIds = [historicalLegacyId],
            };
            var legacyId = AppCommand.GenerateId(app.Name, app.Subtitle, app.ExePath);
            settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();

            app.CatalogId = AppIdentity.ForPackaged(currentAumid);
            app.UserModelId = currentAumid;
            var updatedAliases = settings.RetainAppCommandAliases([app]);
            Assert.AreNotEqual(string.Empty, updatedAliases[legacyId]);
            Assert.AreNotEqual(string.Empty, updatedAliases[historicalLegacyId]);
            await settings.WaitForAliasSavesAsync();

            var reloaded = new AllAppsSettings(settingsPath);
            var reloadedAliases = reloaded.RetainAppCommandAliases([app]);
            await reloaded.WaitForAliasSavesAsync();
            var row = new AppListItem(app, useThumbnails: false);
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: reloadedAliases);
            foreach (var requestedId in new[] { legacyId, historicalLegacyId })
            {
                Assert.AreNotEqual(string.Empty, reloadedAliases[requestedId]);
                Assert.AreSame(row, snapshot.GetVisibleApp(requestedId));
                Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
                Assert.IsNull(snapshot.GetVisibleApp(requestedId.ToUpperInvariant()));
            }

            Assert.AreEqual(AppIdentity.ForCommand(app.CatalogId), row.Command!.Id);
            Assert.AreEqual(currentAumid, row.App.UserModelId);
        }
        finally
        {
            await settings.WaitForAliasSavesAsync();
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_FlushDrainsChangesQueuedDuringAnActiveSave()
    {
        var settingsPath = TemporarySettingsPath();
        var writerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new RecordingLogger<AllAppsSettings>
        {
            OnLog = eventId =>
            {
                if (eventId.Id == 1)
                {
                    writerStarted.TrySetResult();
                    releaseWriter.Task.GetAwaiter().GetResult();
                }
            },
        };
        var settings = new AllAppsSettings(settingsPath, logger);
        try
        {
            var original = new AppItem { CatalogId = @"win32:C:\Old\Editor.exe|args:", Name = "Editor", ExePath = @"C:\Links\Editor.lnk" };
            File.WriteAllText(settingsPath, "[]");
            settings.RetainAppCommandAliases([original]);
            var flush = settings.WaitForAliasSavesAsync();
            await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            File.WriteAllText(settingsPath, "{}");
            var moved = new AppItem { CatalogId = @"win32:D:\New\Editor.exe|args:", Name = original.Name, ExePath = original.ExePath };
            settings.RetainAppCommandAliases([moved]);
            Assert.AreSame(flush, settings.WaitForAliasSavesAsync(), "A flush must wait for the same writer to drain changes queued while saving.");
            releaseWriter.SetResult();
            await flush.WaitAsync(TimeSpan.FromSeconds(5));

            var aliases = new AllAppsSettings(settingsPath).RetainAppCommandAliases([]);
            Assert.AreEqual(AppIdentity.ForCommand(moved.CatalogId), aliases[AppIdentity.ForCommand(original.CatalogId)]);

            moved.Name = "Renamed Editor";
            settings.RetainAppCommandAliases([moved]);
            await settings.WaitForAliasSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var renamedAliases = new AllAppsSettings(settingsPath).RetainAppCommandAliases([]);
            Assert.IsTrue(renamedAliases.ContainsKey(AppCommand.GenerateId(moved.Name, moved.Subtitle, moved.ExePath)));
        }
        finally
        {
            releaseWriter.TrySetResult();
            await settings.WaitForAliasSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_AcceptsLegacyBareHiddenIdentity()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:legacy\"],\"apps.HideAppDescriptions\":\"true\"}");

            var settings = new AllAppsSettings(settingsPath);
            var item = CreateCatalogItem("win32:legacy");

            using var visibility = new SettingsAppVisibilityStore(settings);
            Assert.AreEqual(AppVisibility.Hidden, visibility.GetVisibility(item));
            Assert.IsTrue(settings.HideAppDescriptions);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_ReplacesThePublishedHiddenIdentitySet()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:first\"],\"apps.HideAppDescriptions\":\"true\"}");
            var settings = new AllAppsSettings(settingsPath);

            Assert.IsTrue(settings.IsAppHidden("win32:first"));

            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[\"win32:second\"],\"apps.HideAppDescriptions\":\"false\"}");
            settings.LoadSettings();

            Assert.IsFalse(settings.IsAppHidden("win32:first"));
            Assert.IsTrue(settings.IsAppHidden("win32:second"));
            StringAssert.Contains(File.ReadAllText(settingsPath), "win32:second");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void LoadSettings_ReportsDiagnosticsThroughInjectedLogger()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "[]");
            var logger = new RecordingLogger<AllAppsSettings>();

            _ = new AllAppsSettings(settingsPath, logger);

            Assert.AreEqual(LogLevel.Warning, logger.LastLevel);
            Assert.AreEqual(1, logger.LastEventId?.Id);
            StringAssert.Contains(logger.LastMessage, "Failed to parse the Apps settings file");
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public void AppListItemSource_ExposesConfiguredTopLevelResultLimit()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update("{\"apps.SearchResultLimit\":\"5\"}");
            using var catalog = new MockAppCatalog();
            using var source = new AppListItemSource(catalog, settings);

            Assert.AreEqual(5, source.TopLevelResultLimit);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
        => Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");

    private static AppCatalogItem CreateCatalogItem(string identity)
    {
        var program = TestDataHelper.CreateTestWin32Program("Hidden app");
        return new AppCatalogItem(
            identity,
            priority: 0,
            new AppCatalogSourceReference("test", program.FullPath),
            [],
            Win32AppPayload.From(program));
    }

    private sealed class RecordingLogger<T> : MEL.ILogger<T>
    {
        public Action<EventId> OnLog { get; init; }

        public LogLevel? LastLevel { get; private set; }

        public EventId? LastEventId { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            LastLevel = logLevel;
            LastEventId = eventId;
            LastMessage = formatter(state, exception);
            OnLog?.Invoke(eventId);
        }
    }
}
