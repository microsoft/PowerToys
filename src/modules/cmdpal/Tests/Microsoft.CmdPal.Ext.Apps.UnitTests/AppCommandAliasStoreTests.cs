// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Persistence;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCommandAliasStoreTests
{
    [TestMethod]
    public async Task CommandAliases_StoresSuppliedDataWithoutApplyingCatalogRules()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OldName"] = "current",
                ["oldname"] = "other",
                ["ambiguous"] = string.Empty,
            }.ToFrozenDictionary(StringComparer.Ordinal);
            using var store = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath));

            store.SetSnapshot(snapshot);
            await store.WaitForSavesAsync();

            Assert.AreSame(snapshot, store.GetSnapshot());
            using var reloaded = new AppCommandAliasStore(store.FilePath);
            Assert.AreEqual(snapshot.Count, reloaded.GetSnapshot().Count);
            foreach (var alias in snapshot)
            {
                Assert.AreEqual(alias.Value, reloaded.GetSnapshot()[alias.Key]);
            }
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void CommandAliases_LoadDropsOnlyExactSelfMappingsWithoutRewritingFile()
    {
        var settingsPath = TemporarySettingsPath();
        var aliasPath = TestDataHelper.GetAliasesPath(settingsPath);
        const string content = """
            {"AppCommandAliases":{"self":"self","OldName":"oldname","legacy":"current","ambiguous":""}}
            """;
        File.WriteAllText(aliasPath, content);
        try
        {
            using var store = new AppCommandAliasStore(aliasPath);
            var aliases = store.GetSnapshot();

            Assert.AreEqual(3, aliases.Count);
            Assert.IsFalse(aliases.ContainsKey("self"));
            Assert.AreEqual("oldname", aliases["OldName"]);
            Assert.IsFalse(aliases.ContainsKey("oldname"));
            Assert.AreEqual("current", aliases["legacy"]);
            Assert.AreEqual(string.Empty, aliases["ambiguous"]);
            Assert.AreEqual(content, File.ReadAllText(aliasPath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void CommandAliases_UseAnExplicitPathWithoutPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cmdpal-alias-path-{Guid.NewGuid():N}");
        var aliasPath = Path.Combine(directory, "custom.aliases.json");
        Directory.CreateDirectory(directory);
        try
        {
            using var aliases = new AppCommandAliasStore(aliasPath);

            Assert.AreEqual(aliasPath, aliases.FilePath);
            Assert.AreEqual(0, aliases.GetSnapshot().Count);
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandAliases_ExistingFileIsAuthoritativeAndUnchangedByReads(bool emptySidecar)
    {
        var settingsPath = TemporarySettingsPath();
        var aliasPath = TestDataHelper.GetAliasesPath(settingsPath);
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            var content = emptySidecar ? "{\"AppCommandAliases\":{}}" : "{\"AppCommandAliases\":{\"Current_42\":\"app-v1-packaged-Current!App\",\"ambiguous_42\":\"\"}}";
            File.WriteAllText(aliasPath, content);
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = settingsAliases.GetSnapshot();
            await settingsAliases.WaitForSavesAsync();

            Assert.AreEqual(emptySidecar ? 0 : 2, aliases.Count);
            if (!emptySidecar)
            {
                Assert.AreEqual("app-v1-packaged-Current!App", aliases["Current_42"]);
                Assert.AreEqual(string.Empty, aliases["ambiguous_42"]);
            }

            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.IsFalse(savedSettings.ContainsKey("AppCommandAliases"));
            Assert.AreEqual("preserved", savedSettings["futureSetting"]!.GetValue<string>());
            Assert.AreEqual(content, File.ReadAllText(aliasPath), "Reading aliases must not rewrite their file.");

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedAliasesStore = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(reloaded.FilePath));
            Assert.AreEqual(aliases.Count, reloadedAliasesStore.GetSnapshot().Count);
            await reloadedAliasesStore.WaitForSavesAsync();
            Assert.AreEqual(content, File.ReadAllText(aliasPath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandAliases_LoadAndSaveWithoutValidPreferences(bool invalidPreferences)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            if (invalidPreferences)
            {
                File.WriteAllText(settingsPath, "[]");
            }

            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            var aliasPath = TestDataHelper.GetAliasesPath(settingsPath);
            File.WriteAllText(aliasPath, new JsonObject
            {
                ["AppCommandAliases"] = new JsonObject { ["Earlier Editor_42"] = AppIdentity.ForCommand(app.CatalogId) },
            }.ToJsonString());
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();
            var row = new AppListItem(app);
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);

            Assert.AreSame(row, snapshot.GetVisibleApp("Earlier Editor_42"));
            Assert.AreEqual("Earlier Editor_42", snapshot.GetCommandItem("Earlier Editor_42")?.Command?.Id);
            Assert.IsNull(snapshot.GetVisibleApp("earlier editor_42"));
            var savedAliases = JsonNode.Parse(File.ReadAllText(aliasPath))!["AppCommandAliases"]!.AsObject();
            Assert.AreEqual(AppIdentity.ForCommand(app.CatalogId), savedAliases[AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget)]!.GetValue<string>());
            if (invalidPreferences)
            {
                Assert.AreEqual("[]", File.ReadAllText(settingsPath));
            }
            else
            {
                Assert.IsFalse(File.Exists(settingsPath), "Alias publication must not create a preference file.");
            }
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_AliasOnlySavePreservesManualPreferenceEdits()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{\"apps.SearchResultLimit\":\"5\",\"futureSetting\":\"original\"}");
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            const string editedPreferences = "{\"apps.SearchResultLimit\":\"1\",\"futureSetting\":\"edited\"}";
            File.WriteAllText(settingsPath, editedPreferences);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };

            TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();

            Assert.AreEqual(5, settings.EffectiveSearchResultLimit);
            Assert.AreEqual(editedPreferences, File.ReadAllText(settingsPath));
            Assert.IsTrue(File.Exists(settingsAliases.FilePath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"AppCommandAliases\":{\"old_42\":null}}")]
    [DataRow("{\"AppCommandAliases\":{\"old_42\":42}}")]
    [DataRow("{\"AppCommandAliases\":{\" \":\"target\"}}")]
    [DataRow("invalid json")]
    [DataRow("directory")]
    public async Task CommandAliases_UnreadableSidecarPreservesHistoryAndAllowsPreferenceSaves(string content)
    {
        var settingsPath = TemporarySettingsPath();
        var aliasPath = TestDataHelper.GetAliasesPath(settingsPath);
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            if (content == "directory")
            {
                Directory.CreateDirectory(aliasPath);
            }
            else
            {
                File.WriteAllText(aliasPath, content);
            }

            var logger = new RecordingLogger<AppCommandAliasStore>();
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath), logger);
            Assert.AreEqual(6, logger.LastEventId?.Id);
            Assert.AreEqual("Failed to load Apps command aliases.", logger.LastMessage);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            Assert.IsFalse(TestDataHelper.RetainCommandAliases(settingsAliases, [app]).ContainsKey("stale_42"));
            await settingsAliases.WaitForSavesAsync();

            var form = (SettingsForm)settings.Settings.ToContent().Single();
            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.AreEqual("true", savedSettings["apps.HideAppDescriptions"]!.GetValue<string>());
            Assert.AreEqual("preserved", savedSettings["futureSetting"]!.GetValue<string>());
            if (content == "directory")
            {
                Assert.IsTrue(Directory.Exists(aliasPath));
                Directory.Delete(aliasPath);
            }
            else
            {
                Assert.AreEqual(content, File.ReadAllText(aliasPath));
            }

            File.WriteAllText(aliasPath, "{\"AppCommandAliases\":{\"Recovered_42\":\"app-v1-packaged-Recovered!App\"}}");
            using var recoveredStore = new AppCommandAliasStore(aliasPath);
            var recovered = TestDataHelper.RetainCommandAliases(recoveredStore, [app]);
            await recoveredStore.WaitForSavesAsync();
            Assert.IsTrue(recovered.ContainsKey("Recovered_42"));
            Assert.IsFalse(recovered.ContainsKey("stale_42"));
            Assert.IsFalse(JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject().ContainsKey("AppCommandAliases"));
            Assert.IsTrue(JsonNode.Parse(File.ReadAllText(aliasPath))!["AppCommandAliases"]!.AsObject().ContainsKey(AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget)));
        }
        finally
        {
            if (Directory.Exists(aliasPath))
            {
                Directory.Delete(aliasPath);
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_TransientSharingViolationPreservesAliasesAndAllowsSaves()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            const string ambiguityMarker = "Ambiguous Editor_42";
            var app = new AppItem
            {
                CatalogId = @"win32:D:\New\Editor.exe|args:",
                Name = "Editor",
                LaunchTarget = @"C:\Links\Editor.lnk",
                CommandIds = [ambiguityMarker],
            };
            var previousId = AppIdentity.ForCommand(@"win32:C:\Old\Editor.exe|args:");
            var currentId = AppIdentity.ForCommand(app.CatalogId);
            var aliasPath = TestDataHelper.GetAliasesPath(settingsPath);
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            File.WriteAllText(aliasPath, new JsonObject
            {
                ["AppCommandAliases"] = new JsonObject
                {
                    [previousId] = currentId,
                    [ambiguityMarker] = string.Empty,
                },
            }.ToJsonString());
            using var lockedFile = new FileStream(aliasPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var logger = new RecordingLogger<AppCommandAliasStore>
            {
                OnLog = eventId =>
                {
                    if (eventId.Id == 8)
                    {
                        lockedFile.Dispose();
                    }
                },
            };
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath), logger);
            Assert.AreEqual(8, logger.LastEventId?.Id);
            Assert.AreEqual("Retrying Apps data file read after an I/O failure.", logger.LastMessage);
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();
            var row = new AppListItem(app);
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: aliases);

            Assert.AreEqual(currentId, aliases[previousId]);
            Assert.AreEqual(string.Empty, aliases[ambiguityMarker]);
            Assert.IsFalse(aliases.ContainsKey("stale_42"));
            Assert.AreSame(row, snapshot.GetVisibleApp(previousId));
            Assert.AreEqual(previousId, snapshot.GetCommandItem(previousId)?.Command?.Id);
            Assert.IsNull(snapshot.GetCommandItem(ambiguityMarker));
            var savedAliases = JsonNode.Parse(File.ReadAllText(aliasPath))!["AppCommandAliases"]!.AsObject();
            Assert.AreEqual(currentId, savedAliases[previousId]!.GetValue<string>());
            Assert.AreEqual(string.Empty, savedAliases[ambiguityMarker]!.GetValue<string>());
            Assert.AreEqual(currentId, savedAliases[AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget)]!.GetValue<string>(), "A recovered read must allow subsequent alias publication to be saved.");
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_WriteFailureAllowsPreferenceSavesAndLaterUpdates()
    {
        var settingsPath = TemporarySettingsPath();
        var logger = new RecordingLogger<AppCommandAliasStore>();
        var settings = new AllAppsSettings(settingsPath);
        using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath), logger);
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            Directory.CreateDirectory(settingsAliases.FilePath);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();

            Assert.AreEqual(7, logger.LastEventId?.Id);
            Assert.AreEqual("Failed to save Apps command aliases.", logger.LastMessage);
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.AreEqual("true", savedSettings["apps.HideAppDescriptions"]!.GetValue<string>());
            Assert.AreEqual("preserved", savedSettings["futureSetting"]!.GetValue<string>());
            Assert.IsTrue(Directory.Exists(settingsAliases.FilePath));

            Directory.Delete(settingsAliases.FilePath);
            settings.SaveSettings();
            Assert.IsFalse(File.Exists(settingsAliases.FilePath), "Preference saves must not save aliases.");
            app.Name = "Renamed Editor";
            TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();
            Assert.IsTrue(File.Exists(settingsAliases.FilePath));
            Assert.IsFalse(JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject().ContainsKey("AppCommandAliases"));
        }
        finally
        {
            await settingsAliases.WaitForSavesAsync();
            if (Directory.Exists(settingsAliases.FilePath))
            {
                Directory.Delete(settingsAliases.FilePath);
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_HiddenAppsFollowMovesWithoutRewritingPreferences()
    {
        var settingsPath = TemporarySettingsPath();
        File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
        var settings = new AllAppsSettings(settingsPath);
        using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
        var visibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settings.FilePath));
        try
        {
            var metadata = TestDataHelper.CreateTestWin32Metadata("Editor", @"C:\Old\Editor.exe");
            metadata.LnkFilePath = @"C:\Links\Editor.lnk";
            var original = new AppCatalogItem(
                @"win32:C:\Old\Editor.exe|args:",
                0,
                new AppCatalogSourceReference("test", metadata.LnkFilePath),
                [],
                Win32AppPayload.From(metadata));
            Assert.IsTrue(TestDataHelper.SetHidden(visibility, original, hidden: true, aliases: settingsAliases));
            visibility.Persist();
            TestDataHelper.RetainCommandAliases(settingsAliases, [original.ToAppItem()]);
            await settingsAliases.WaitForSavesAsync();
            var preferences = File.ReadAllText(settingsPath);
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(visibility, original, settings: settings, aliases: settingsAliases));

            metadata.TargetPath = @"D:\New\Editor.exe";
            var moved = new AppCatalogItem(
                @"win32:D:\New\Editor.exe|args:",
                0,
                new AppCatalogSourceReference("test", metadata.LnkFilePath),
                [],
                Win32AppPayload.From(metadata));
            TestDataHelper.RetainCommandAliases(settingsAliases, [moved.ToAppItem()]);
            await settingsAliases.WaitForSavesAsync();
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(visibility, moved, settings: settings, aliases: settingsAliases));
            Assert.AreEqual(preferences, File.ReadAllText(settingsPath), "Following a redirect must not rewrite preferences.");

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedAliases = new AppCommandAliasStore(settingsAliases.FilePath);
            var reloadedVisibility = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(reloaded.FilePath));
            Assert.AreEqual(AppVisibility.Hidden, TestDataHelper.GetVisibility(reloadedVisibility, moved, settings: reloaded, aliases: reloadedAliases));
            Assert.IsTrue(TestDataHelper.SetHidden(reloadedVisibility, moved, hidden: false, aliases: reloadedAliases));
            reloadedVisibility.Persist();
            Assert.AreEqual(AppVisibility.Visible, TestDataHelper.GetVisibility(reloadedVisibility, original, settings: reloaded, aliases: reloadedAliases));
            Assert.AreEqual(AppVisibility.Visible, TestDataHelper.GetVisibility(reloadedVisibility, moved, settings: reloaded, aliases: reloadedAliases));
            Assert.AreEqual(0, JsonNode.Parse(File.ReadAllText(reloadedVisibility.FilePath))!["HiddenAppIdentities"]!.AsArray().Count);
        }
        finally
        {
            await settingsAliases.WaitForSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_PersistLatestPublicationAndStickyAmbiguityMarkers()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var apps = Enumerable.Range(0, 500).Select(index => new AppItem
            {
                CatalogId = $@"win32:C:\Apps\App{index}.exe|args:",
                Name = $"App {index}",
                LaunchTarget = $@"C:\Links\App{index}.lnk",
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
                foreach (var alias in app.CommandIds.Append(AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget)).Append(commandId))
                {
                    if (alias != commandId)
                    {
                        flatAliases[alias] = commandId;
                    }
                }
            }

            flatAliases["CaseSensitiveAlias_42"] = AppIdentity.ForCommand(apps[0].CatalogId);
            flatAliases["casesensitivealias_42"] = AppIdentity.ForCommand(apps[1].CatalogId);
            var stickyLegacyId = AppCommand.GenerateId(apps[2].Name, apps[2].Subtitle, apps[2].LaunchTarget);
            flatAliases[stickyLegacyId] = string.Empty;
            var aliasesJson = new JsonObject
            {
                ["AppCommandAliases"] = flatAliases,
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TestDataHelper.GetAliasesPath(settingsPath), aliasesJson);
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            var settings = new AllAppsSettings(settingsPath);
            using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
            var aliases = TestDataHelper.RetainCommandAliases(settingsAliases, apps);
            await settingsAliases.WaitForSavesAsync();
            var saved = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var savedAliases = JsonNode.Parse(File.ReadAllText(settingsAliases.FilePath))!["AppCommandAliases"]!.AsObject();
            Assert.IsFalse(saved.ContainsKey("AppCommandAliases"), "Alias updates must not write aliases into preferences.");
            Assert.AreEqual(aliases.Count, savedAliases.Count);
            Assert.IsTrue(apps.All(app => !savedAliases.ContainsKey(AppIdentity.ForCommand(app.CatalogId))), "Redundant self-aliases must not be persisted.");
            Assert.AreEqual("preserved", saved["futureSetting"]!.GetValue<string>());

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedAliasesStore = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(reloaded.FilePath));
            var reloadedAliases = TestDataHelper.RetainCommandAliases(reloadedAliasesStore, apps);
            await reloadedAliasesStore.WaitForSavesAsync();
            foreach (var alias in aliases)
            {
                Assert.AreEqual(alias.Value, reloadedAliases[alias.Key]);
            }

            Assert.AreEqual(string.Empty, reloadedAliases["ambiguous_old-id"]);
            Assert.AreEqual(string.Empty, reloadedAliases[stickyLegacyId], "A sticky ambiguity marker must survive even when only one current app claims it.");
            Assert.AreEqual(AppIdentity.ForCommand(apps[0].CatalogId), reloadedAliases["CaseSensitiveAlias_42"]);
            Assert.AreEqual(AppIdentity.ForCommand(apps[1].CatalogId), reloadedAliases["casesensitivealias_42"]);
            var originalId = AppIdentity.ForCommand(apps[0].CatalogId);
            var legacyId = AppCommand.GenerateId(apps[0].Name, apps[0].Subtitle, apps[0].LaunchTarget);
            apps[0].Name = "Renamed App";
            TestDataHelper.RetainCommandAliases(settingsAliases, apps);
            apps[0].CatalogId = @"win32:D:\Moved\App0.exe|args:";
            TestDataHelper.RetainCommandAliases(settingsAliases, apps);
            await settingsAliases.WaitForSavesAsync();
            reloaded = new AllAppsSettings(settingsPath);
            using var movedAliasesStore = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath));
            reloadedAliases = TestDataHelper.RetainCommandAliases(movedAliasesStore, apps);
            await movedAliasesStore.WaitForSavesAsync();
            var currentId = AppIdentity.ForCommand(apps[0].CatalogId);
            Assert.AreEqual(currentId, reloadedAliases[originalId]);
            Assert.AreEqual(currentId, reloadedAliases[legacyId]);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_CasingOnlyPackagedUpdatePreservesLegacyAliasesAfterReload()
    {
        var settingsPath = TemporarySettingsPath();
        var settings = new AllAppsSettings(settingsPath);
        using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath));
        try
        {
            const string earlierAumid = "contoso.app_123!main";
            const string currentAumid = "Contoso.App_123!Main";
            const string historicalLegacyId = "Earlier display name_42";
            var app = new AppItem
            {
                CatalogId = AppIdentity.ForPackaged(earlierAumid),
                Name = "App display name",
                AppUserModelId = earlierAumid,
                IsPackaged = true,
                CommandIds = [historicalLegacyId],
            };
            var legacyId = AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget);
            TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            await settingsAliases.WaitForSavesAsync();

            app.CatalogId = AppIdentity.ForPackaged(currentAumid);
            app.AppUserModelId = currentAumid;
            var updatedAliases = TestDataHelper.RetainCommandAliases(settingsAliases, [app]);
            Assert.AreNotEqual(string.Empty, updatedAliases[legacyId]);
            Assert.AreNotEqual(string.Empty, updatedAliases[historicalLegacyId]);
            await settingsAliases.WaitForSavesAsync();

            var reloaded = new AllAppsSettings(settingsPath);
            using var reloadedAliasesStore = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(reloaded.FilePath));
            var reloadedAliases = TestDataHelper.RetainCommandAliases(reloadedAliasesStore, [app]);
            await reloadedAliasesStore.WaitForSavesAsync();
            var row = new AppListItem(app);
            var snapshot = new AppListItemSnapshot([row], [], commandAliases: reloadedAliases);
            foreach (var requestedId in new[] { legacyId, historicalLegacyId })
            {
                Assert.AreNotEqual(string.Empty, reloadedAliases[requestedId]);
                Assert.AreSame(row, snapshot.GetVisibleApp(requestedId));
                Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
                Assert.IsNull(snapshot.GetVisibleApp(requestedId.ToUpperInvariant()));
            }

            Assert.AreEqual(AppIdentity.ForCommand(app.CatalogId), row.Command!.Id);
            Assert.AreEqual(currentAumid, row.App.AppUserModelId);
        }
        finally
        {
            await settingsAliases.WaitForSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_FlushDrainsChangesQueuedDuringAnActiveSave()
    {
        var settingsPath = TemporarySettingsPath();
        var writerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new RecordingLogger<AppCommandAliasStore>
        {
            OnLog = eventId =>
            {
                if (eventId.Id == 7)
                {
                    writerStarted.TrySetResult();
                    releaseWriter.Task.GetAwaiter().GetResult();
                }
            },
        };
        var settings = new AllAppsSettings(settingsPath);
        using var settingsAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settings.FilePath), logger);
        try
        {
            var original = new AppItem { CatalogId = @"win32:C:\Old\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            Directory.CreateDirectory(settingsAliases.FilePath);
            TestDataHelper.RetainCommandAliases(settingsAliases, [original]);
            var flush = settingsAliases.WaitForSavesAsync();
            await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // A blocked alias writer must not hold preference loading or saving hostage.
            await Task.Run(() =>
            {
                settings.LoadSettings();
                settings.SaveSettings();
            }).WaitAsync(TimeSpan.FromSeconds(5));

            Directory.Delete(settingsAliases.FilePath);
            var moved = new AppItem { CatalogId = @"win32:D:\New\Editor.exe|args:", Name = original.Name, LaunchTarget = original.LaunchTarget };
            TestDataHelper.RetainCommandAliases(settingsAliases, [moved]);
            Assert.AreSame(flush, settingsAliases.WaitForSavesAsync(), "A flush must wait for the same writer to drain changes queued while saving.");
            releaseWriter.SetResult();
            await flush.WaitAsync(TimeSpan.FromSeconds(5));

            var aliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath)).GetSnapshot();
            Assert.AreEqual(AppIdentity.ForCommand(moved.CatalogId), aliases[AppIdentity.ForCommand(original.CatalogId)]);

            moved.Name = "Renamed Editor";
            TestDataHelper.RetainCommandAliases(settingsAliases, [moved]);
            await settingsAliases.WaitForSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var renamedAliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath)).GetSnapshot();
            Assert.IsTrue(renamedAliases.ContainsKey(AppCommand.GenerateId(moved.Name, moved.Subtitle, moved.LaunchTarget)));
        }
        finally
        {
            releaseWriter.TrySetResult();
            await settingsAliases.WaitForSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (Directory.Exists(settingsAliases.FilePath))
            {
                Directory.Delete(settingsAliases.FilePath);
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void CommandAliases_DisposeDrainsPendingChanges()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            using (var aliases = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath)))
            {
                TestDataHelper.RetainCommandAliases(aliases, [app]);
            }

            using var reloaded = new AppCommandAliasStore(TestDataHelper.GetAliasesPath(settingsPath));
            Assert.AreEqual(AppIdentity.ForCommand(app.CatalogId), reloaded.GetSnapshot()[AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget)]);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
    }

    private sealed class RecordingLogger<T> : MEL.ILogger<T>
    {
        public Action<EventId> OnLog { get; init; }

        public LogLevel? LastLevel { get; private set; }

        public EventId? LastEventId { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

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
