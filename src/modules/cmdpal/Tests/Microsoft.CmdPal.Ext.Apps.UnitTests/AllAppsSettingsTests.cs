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
    [DataRow("")]
    [DataRow("unknown")]
    [DataRow("default")]
    public void ExecutableNameMatchMode_DefaultsForMissingOrUnknownSavedValues(string savedValue)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            if (!string.IsNullOrEmpty(savedValue))
            {
                File.WriteAllText(
                    settingsPath,
                    new JsonObject
                    {
                        ["apps.ExecutableNameMatchMode"] = savedValue,
                    }.ToJsonString());
            }

            var settings = new AllAppsSettings(settingsPath);

            Assert.AreEqual(ExecutableNameMatchMode.FilenameOnly, settings.ExecutableNameMatchMode);
            Assert.AreEqual("default", JsonNode.Parse(settings.Settings.ToJson())!["apps.ExecutableNameMatchMode"]!.GetValue<string>());
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("default", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("filenameAndStem", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("filenameOnly", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("disabled", ExecutableNameMatchMode.Disabled)]
    public void ExecutableNameMatchMode_FormSubmissionPersists(string value, ExecutableNameMatchMode expected)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{\"apps.ExecutableNameMatchMode\":\"filenameOnly\"}");
            var settings = new AllAppsSettings(settingsPath);
            var form = (SettingsForm)settings.Settings.ToContent().Single();

            form.SubmitForm(
                new JsonObject
                {
                    ["apps.ExecutableNameMatchMode"] = value,
                }.ToJsonString(),
                string.Empty);

            Assert.AreEqual(expected, settings.ExecutableNameMatchMode);
            Assert.AreEqual(expected, new AllAppsSettings(settingsPath).ExecutableNameMatchMode);
            Assert.AreEqual(value, JsonNode.Parse(File.ReadAllText(settingsPath))!["apps.ExecutableNameMatchMode"]!.GetValue<string>());
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
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
                new Win32AppPayload { Name = name, TargetPath = path });

            Assert.AreEqual(expected ? AppVisibility.HiddenByPattern : AppVisibility.Visible, visibility.GetVisibility(item));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("apps.settings.json", "apps.aliases.json")]
    [DataRow("custom.json", "custom.aliases.json")]
    public void CommandAliases_UseASiblingFile(string settingsName, string aliasesName)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cmdpal-alias-path-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(directory, settingsName);
        Directory.CreateDirectory(directory);
        try
        {
            var settings = new AllAppsSettings(settingsPath);

            Assert.AreEqual(Path.Combine(directory, aliasesName), settings.AppCommandAliasesFilePath);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandAliases_ExistingSidecarIsAuthoritativeAndMigrationIsIdempotent(bool emptySidecar)
    {
        var settingsPath = TemporarySettingsPath();
        var aliasPath = AllAppsSettings.AppCommandAliasesPath(settingsPath);
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\",\"AppCommandAliases\":{\"stale_42\":\"app-v1-packaged-Old!App\"}}");
            var content = emptySidecar ? "{\"AppCommandAliases\":{}}" : "{\"AppCommandAliases\":{\"Current_42\":\"app-v1-packaged-Current!App\",\"ambiguous_42\":\"\"}}";
            File.WriteAllText(aliasPath, content);
            var settings = new AllAppsSettings(settingsPath);
            var aliases = settings.RetainAppCommandAliases([]);
            await settings.WaitForAliasSavesAsync();

            Assert.IsFalse(aliases.ContainsKey("stale_42"));
            Assert.AreEqual(emptySidecar ? 0 : 2, aliases.Count);
            if (!emptySidecar)
            {
                Assert.AreEqual("app-v1-packaged-Current!App", aliases["Current_42"]);
                Assert.AreEqual(string.Empty, aliases["ambiguous_42"]);
            }

            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.IsFalse(savedSettings.ContainsKey("AppCommandAliases"));
            Assert.AreEqual("preserved", savedSettings["futureSetting"]!.GetValue<string>());
            Assert.AreEqual(content, File.ReadAllText(aliasPath), "An authoritative sidecar must not be rewritten by legacy cleanup.");

            var reloaded = new AllAppsSettings(settingsPath);
            Assert.AreEqual(aliases.Count, reloaded.RetainAppCommandAliases([]).Count);
            await reloaded.WaitForAliasSavesAsync();
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
            var aliasPath = AllAppsSettings.AppCommandAliasesPath(settingsPath);
            File.WriteAllText(aliasPath, new JsonObject
            {
                ["AppCommandAliases"] = new JsonObject { ["Earlier Editor_42"] = AppIdentity.ForCommand(app.CatalogId) },
            }.ToJsonString());
            var settings = new AllAppsSettings(settingsPath);
            var aliases = settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();
            var row = new AppListItem(app, useThumbnails: false);
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
            const string editedPreferences = "{\"apps.SearchResultLimit\":\"1\",\"futureSetting\":\"edited\"}";
            File.WriteAllText(settingsPath, editedPreferences);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };

            settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();

            Assert.AreEqual(5, settings.EffectiveSearchResultLimit);
            Assert.AreEqual(editedPreferences, File.ReadAllText(settingsPath));
            Assert.IsTrue(File.Exists(settings.AppCommandAliasesFilePath));
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
    [DataRow("invalid json")]
    [DataRow("directory")]
    public async Task CommandAliases_UnreadableSidecarPreservesHistoryAndAllowsPreferenceSaves(string content)
    {
        var settingsPath = TemporarySettingsPath();
        var aliasPath = AllAppsSettings.AppCommandAliasesPath(settingsPath);
        try
        {
            File.WriteAllText(settingsPath, "{\"AppCommandAliases\":{\"stale_42\":\"app-v1-packaged-Old!App\"}}");
            if (content == "directory")
            {
                Directory.CreateDirectory(aliasPath);
            }
            else
            {
                File.WriteAllText(aliasPath, content);
            }

            var logger = new RecordingLogger<AllAppsSettings>();
            var settings = new AllAppsSettings(settingsPath, logger);
            Assert.AreEqual(6, logger.LastEventId?.Id);
            Assert.AreEqual("Failed to load Apps command aliases.", logger.LastMessage);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            Assert.IsFalse(settings.RetainAppCommandAliases([app]).ContainsKey("stale_42"));
            await settings.WaitForAliasSavesAsync();

            var form = (SettingsForm)settings.Settings.ToContent().Single();
            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.AreEqual("true", savedSettings["apps.HideAppDescriptions"]!.GetValue<string>());
            Assert.IsTrue(savedSettings.ContainsKey("AppCommandAliases"), "Unreadable sidecars must not discard the only readable copy of old aliases.");
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
            settings.LoadSettings();
            var recovered = settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();
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
            var aliasPath = AllAppsSettings.AppCommandAliasesPath(settingsPath);
            File.WriteAllText(settingsPath, "{\"AppCommandAliases\":{\"stale_42\":\"app-v1-packaged-Old!App\"}}");
            File.WriteAllText(aliasPath, new JsonObject
            {
                ["AppCommandAliases"] = new JsonObject
                {
                    [previousId] = currentId,
                    [ambiguityMarker] = string.Empty,
                },
            }.ToJsonString());
            using var lockedFile = new FileStream(aliasPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var logger = new RecordingLogger<AllAppsSettings>
            {
                OnLog = eventId =>
                {
                    if (eventId.Id == 8)
                    {
                        lockedFile.Dispose();
                    }
                },
            };
            var settings = new AllAppsSettings(settingsPath, logger);
            Assert.AreEqual(8, logger.LastEventId?.Id);
            Assert.AreEqual("Retrying Apps command alias read after an I/O failure.", logger.LastMessage);
            var aliases = settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();
            var row = new AppListItem(app, useThumbnails: false);
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
    public async Task CommandAliases_WriteFailurePreservesLegacyAliasesAndAllowsPreferenceSaves()
    {
        var settingsPath = TemporarySettingsPath();
        var logger = new RecordingLogger<AllAppsSettings>();
        var settings = new AllAppsSettings(settingsPath, logger);
        try
        {
            File.WriteAllText(settingsPath, "{\"AppCommandAliases\":{\"old_42\":\"app-v1-packaged-Old!App\"}}");
            Directory.CreateDirectory(settings.AppCommandAliasesFilePath);
            var app = new AppItem { CatalogId = @"win32:C:\Apps\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();

            Assert.AreEqual(7, logger.LastEventId?.Id);
            Assert.AreEqual("Failed to save Apps command aliases.", logger.LastMessage);
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            var savedSettings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            Assert.AreEqual("true", savedSettings["apps.HideAppDescriptions"]!.GetValue<string>());
            Assert.AreEqual("app-v1-packaged-Old!App", savedSettings["AppCommandAliases"]!["old_42"]!.GetValue<string>());
            Assert.IsTrue(Directory.Exists(settings.AppCommandAliasesFilePath));

            Directory.Delete(settings.AppCommandAliasesFilePath);
            settings.SaveSettings();
            Assert.IsTrue(File.Exists(settings.AppCommandAliasesFilePath));
            Assert.IsFalse(JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject().ContainsKey("AppCommandAliases"));
        }
        finally
        {
            await settings.WaitForAliasSavesAsync();
            if (Directory.Exists(settings.AppCommandAliasesFilePath))
            {
                Directory.Delete(settings.AppCommandAliasesFilePath);
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_InvalidPreferenceReloadRetriesUnsavedHiddenIdentityMove()
    {
        var settingsPath = TemporarySettingsPath();
        var settings = new AllAppsSettings(settingsPath);
        try
        {
            var original = new AppItem { CatalogId = @"win32:C:\Old\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            Assert.IsTrue(settings.SetAppHidden(original.CatalogId, hidden: true));
            settings.RetainAppCommandAliases([original]);
            await settings.WaitForAliasSavesAsync();
            Assert.IsTrue(new AllAppsSettings(settingsPath).IsAppHidden(original.CatalogId));

            File.WriteAllText(settingsPath, "[]");
            var moved = new AppItem { CatalogId = @"win32:D:\New\Editor.exe|args:", Name = original.Name, LaunchTarget = original.LaunchTarget };
            settings.RetainAppCommandAliases([moved]);
            await settings.WaitForAliasSavesAsync();
            Assert.IsTrue(settings.IsAppHidden(moved.CatalogId));
            Assert.AreEqual("[]", File.ReadAllText(settingsPath));
            var savedAliases = JsonNode.Parse(File.ReadAllText(settings.AppCommandAliasesFilePath))!["AppCommandAliases"]!.AsObject();
            Assert.AreEqual(AppIdentity.ForCommand(moved.CatalogId), savedAliases[AppIdentity.ForCommand(original.CatalogId)]!.GetValue<string>());

            settings.LoadSettings();
            File.WriteAllText(settingsPath, "{}");
            moved.Name = "Renamed Editor";
            settings.RetainAppCommandAliases([moved]);
            await settings.WaitForAliasSavesAsync();

            Assert.IsTrue(new AllAppsSettings(settingsPath).IsAppHidden(moved.CatalogId), "A failed reload must not mark the migrated hidden set as already saved.");
        }
        finally
        {
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandAliases_MigrateFlatStorageAndPersistLatestPublication()
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
                    flatAliases[alias] = commandId;
                }
            }

            flatAliases["CaseSensitiveAlias_42"] = AppIdentity.ForCommand(apps[0].CatalogId);
            flatAliases["casesensitivealias_42"] = AppIdentity.ForCommand(apps[1].CatalogId);
            var stickyLegacyId = AppCommand.GenerateId(apps[2].Name, apps[2].Subtitle, apps[2].LaunchTarget);
            flatAliases[stickyLegacyId] = string.Empty;
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
            await settings.WaitForAliasSavesAsync();
            var saved = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
            var savedAliases = JsonNode.Parse(File.ReadAllText(settings.AppCommandAliasesFilePath))!["AppCommandAliases"]!.AsObject();
            Assert.IsFalse(saved.ContainsKey("AppCommandAliases"), "Legacy aliases must be removed only after the sidecar has been written.");
            Assert.IsFalse(saved.ContainsKey("AppCommandAliasGroups"), "The grouped alias format from earlier pre-release builds must be cleared from the settings file.");
            Assert.AreEqual(aliases.Count, savedAliases.Count);
            Assert.IsTrue(apps.All(app => !savedAliases.ContainsKey(AppIdentity.ForCommand(app.CatalogId))), "Redundant self-aliases must not be persisted.");
            Assert.AreEqual("preserved", saved["futureSetting"]!.GetValue<string>());

            var reloaded = new AllAppsSettings(settingsPath);
            var reloadedAliases = reloaded.RetainAppCommandAliases(apps);
            await reloaded.WaitForAliasSavesAsync();
            foreach (var alias in aliases)
            {
                Assert.AreEqual(alias.Value, reloadedAliases[alias.Key]);
            }

            Assert.AreEqual(string.Empty, reloadedAliases["ambiguous_old-id"]);
            Assert.AreEqual(string.Empty, reloadedAliases[stickyLegacyId], "Migration must preserve a sticky ambiguity marker even when only one current app claims it.");
            Assert.AreEqual(AppIdentity.ForCommand(apps[0].CatalogId), reloadedAliases["CaseSensitiveAlias_42"]);
            Assert.AreEqual(AppIdentity.ForCommand(apps[1].CatalogId), reloadedAliases["casesensitivealias_42"]);
            var originalId = AppIdentity.ForCommand(apps[0].CatalogId);
            var legacyId = AppCommand.GenerateId(apps[0].Name, apps[0].Subtitle, apps[0].LaunchTarget);
            apps[0].Name = "Renamed App";
            settings.RetainAppCommandAliases(apps);
            apps[0].CatalogId = @"win32:D:\Moved\App0.exe|args:";
            settings.RetainAppCommandAliases(apps);
            await settings.WaitForAliasSavesAsync();
            reloaded = new AllAppsSettings(settingsPath);
            reloadedAliases = reloaded.RetainAppCommandAliases(apps);
            await reloaded.WaitForAliasSavesAsync();
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
            settings.RetainAppCommandAliases([app]);
            await settings.WaitForAliasSavesAsync();

            app.CatalogId = AppIdentity.ForPackaged(currentAumid);
            app.AppUserModelId = currentAumid;
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
            Assert.AreEqual(currentAumid, row.App.AppUserModelId);
        }
        finally
        {
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
                if (eventId.Id == 7)
                {
                    writerStarted.TrySetResult();
                    releaseWriter.Task.GetAwaiter().GetResult();
                }
            },
        };
        var settings = new AllAppsSettings(settingsPath, logger);
        try
        {
            var original = new AppItem { CatalogId = @"win32:C:\Old\Editor.exe|args:", Name = "Editor", LaunchTarget = @"C:\Links\Editor.lnk" };
            Directory.CreateDirectory(settings.AppCommandAliasesFilePath);
            settings.RetainAppCommandAliases([original]);
            var flush = settings.WaitForAliasSavesAsync();
            await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Directory.Delete(settings.AppCommandAliasesFilePath);
            var moved = new AppItem { CatalogId = @"win32:D:\New\Editor.exe|args:", Name = original.Name, LaunchTarget = original.LaunchTarget };
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
            Assert.IsTrue(renamedAliases.ContainsKey(AppCommand.GenerateId(moved.Name, moved.Subtitle, moved.LaunchTarget)));
        }
        finally
        {
            releaseWriter.TrySetResult();
            await settings.WaitForAliasSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (Directory.Exists(settings.AppCommandAliasesFilePath))
            {
                Directory.Delete(settings.AppCommandAliasesFilePath);
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
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
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
    }

    private static AppCatalogItem CreateCatalogItem(string identity)
    {
        var program = TestDataHelper.CreateTestWin32Metadata("Hidden app");
        return new AppCatalogItem(
            identity,
            priority: 0,
            new AppCatalogSourceReference("test", program.TargetPath),
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
