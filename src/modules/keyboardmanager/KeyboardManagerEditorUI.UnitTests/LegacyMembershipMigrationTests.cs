// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Upgrade from the single-profile editor. The released v0.100.2 editor stored a turned-off
    /// row in editorSettings.json with no profile membership and deleted its native mapping, so
    /// nothing on disk says which profile the row came from except the profile active at upgrade.
    /// </summary>
    [TestClass]
    public class LegacyMembershipMigrationTests
    {
        private const string RowId = "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11";

        private const string ActiveRowId = "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02";

        // editorSettings.json as the v0.100.2 editor wrote it after the user turned a remapping off:
        // the row has no profile, the profile index and the active profile are empty, and the
        // engine config holds no matching mapping (the toggle deleted it).
        private const string V0100DisabledRowJson = """
            {
              "ShortcutSettingsDictionary": {
                "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11": {
                  "Id": "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11",
                  "Shortcut": {
                    "OriginalKeys": "65",
                    "TargetKeys": "66",
                    "TargetApp": "",
                    "OperationType": 0,
                    "TargetText": "",
                    "ProgramPath": "",
                    "ProgramArgs": "",
                    "StartInDirectory": "",
                    "UriToOpen": ""
                  },
                  "Profiles": [],
                  "IsActive": false
                }
              },
              "ProfileDictionary": {},
              "ShortcutsByOperationType": {
                "RemapShortcut": [ "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11" ]
              },
              "ActiveProfile": ""
            }
            """;

        // The same file after v0.101 reconciled it under "default": the active row was tagged, the
        // turned-off legacy row matched nothing and stayed untagged.
        private const string V0101MixedJson = """
            {
              "ShortcutSettingsDictionary": {
                "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11": {
                  "Id": "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11",
                  "Shortcut": { "OriginalKeys": "65", "TargetKeys": "66", "OperationType": 0 },
                  "Profiles": [],
                  "IsActive": false
                },
                "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02": {
                  "Id": "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02",
                  "Shortcut": { "OriginalKeys": "70", "TargetKeys": "71", "OperationType": 0 },
                  "Profiles": [ "default" ],
                  "IsActive": true
                }
              },
              "ProfileDictionary": { "default": [ "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02" ] },
              "ShortcutsByOperationType": {
                "RemapShortcut": [ "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11", "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02" ]
              },
              "ActiveProfile": "default"
            }
            """;

        [TestMethod]
        public void StartupReconcile_AssignsLegacyDisabledRowToTheProfileActiveAtUpgrade()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            Assert.AreEqual(0, settings.SchemaVersion);

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "default", migrateLegacyRows: true));

            ShortcutSettings row = settings.ShortcutSettingsDictionary[RowId];
            CollectionAssert.AreEqual(new List<string> { "default" }, row.Profiles);
            Assert.IsFalse(row.IsActive);
            CollectionAssert.AreEqual(new List<string> { RowId }, settings.ProfileDictionary["default"]);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, settings.SchemaVersion);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, Load(JsonSerializer.Serialize(settings)).SchemaVersion);
        }

        // The reviewer's scenario: upgrade, create an empty Work profile, switch to it.
        [TestMethod]
        public void LegacyDisabledRow_IsNotInAnEmptyWorkProfile_AndWorkCannotRemoveOrClaimIt()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "default", migrateLegacyRows: true);

            // Switch to the new, empty Work profile (the editor reconciles against Work's empty config).
            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work");

            ShortcutSettings row = settings.ShortcutSettingsDictionary[RowId];
            Assert.IsFalse(SettingsManager.IsMappingInActiveProfile(row, "Work"), "Work must not list default's turned-off row.");
            Assert.IsTrue(SettingsManager.IsMappingInActiveProfile(row, "default"));

            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMappingRemoval(settings, RowId, "Work"), "Deleting from Work must not remove default's row.");
            Assert.IsTrue(settings.ShortcutSettingsDictionary.ContainsKey(RowId));

            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMapping(settings, Map("65", "67"), RowId, "Work"), "Editing from Work must not move default's row to Work.");
            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMappingActiveState(settings, RowId, true, "Work"), "Turning it on from Work must not claim it for Work.");
            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);

            // Back in default the row is still there and can be deleted.
            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMappingRemoval(settings, RowId, "default"));
            Assert.IsFalse(settings.ShortcutSettingsDictionary.ContainsKey(RowId));
        }

        [TestMethod]
        public void StartupReconcile_OfAV0101File_KeepsBothRowsInDefault()
        {
            EditorSettings settings = Load(V0101MixedJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { Map("70", "71") }, "default", migrateLegacyRows: true);

            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);
            Assert.IsFalse(settings.ShortcutSettingsDictionary[RowId].IsActive);
            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[ActiveRowId].Profiles);
            Assert.IsTrue(settings.ShortcutSettingsDictionary[ActiveRowId].IsActive);
            CollectionAssert.AreEquivalent(new List<string> { RowId, ActiveRowId }, settings.ProfileDictionary["default"]);
        }

        // A reconcile after a profile switch (picker, hotkey, auto-switch, file watcher) must not
        // migrate: the active profile then is not the one the legacy rows came from.
        [TestMethod]
        public void ReconcileAfterASwitch_LeavesLegacyRowsForTheStartupMigration()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work");

            Assert.AreEqual(0, settings.ShortcutSettingsDictionary[RowId].Profiles.Count);
            Assert.AreEqual(0, settings.SchemaVersion);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "default", migrateLegacyRows: true);
            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);
        }

        // The migration is one-time: once the file is marked, a later start under another profile
        // does not adopt rows for that profile.
        [TestMethod]
        public void StartupReconcile_OfAMigratedFile_DoesNotAssignUntaggedRowsAgain()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            settings.SchemaVersion = EditorSettings.CurrentSchemaVersion;

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", migrateLegacyRows: true);

            Assert.AreEqual(0, settings.ShortcutSettingsDictionary[RowId].Profiles.Count);
            Assert.IsFalse(settings.ProfileDictionary.ContainsKey("Work"));
        }

        private static EditorSettings Load(string json)
        {
            EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(json) ?? new EditorSettings();
            SettingsManager.NormalizeSettings(settings);
            return settings;
        }

        private static ShortcutKeyMapping Map(string originalKeys, string targetKeys) =>
            new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = originalKeys,
                TargetKeys = targetKeys,
                TargetText = string.Empty,
            };
    }
}
