// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Upgrade from the single-profile editor. The released v0.100.2 editor stored a turned-off
    /// row in editorSettings.json with no profile membership and deleted its native mapping. Those
    /// editors could only use "default", so that is where such rows go, whichever profile is active
    /// when they are migrated. A version-0 file with rows tagged with another profile comes from a
    /// pre-release profile-aware build; its untagged rows go to every existing profile.
    /// </summary>
    [TestClass]
    public class LegacyMembershipMigrationTests
    {
        internal const string RowId = "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11";

        private const string ActiveRowId = "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02";

        // editorSettings.json as the v0.100.2 editor wrote it after the user turned a remapping off:
        // the row has no profile, the profile index and the active profile are empty, and the
        // engine config holds no matching mapping (the toggle deleted it).
        internal const string V0100DisabledRowJson = """
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

        // A file written by v0.101/v0.102 (which only ever had "default") with ActiveProfile set to
        // another profile: the row came from a profile the user had made by hand.
        private const string RecordedHomeJson = """
            {
              "ShortcutSettingsDictionary": {
                "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11": {
                  "Id": "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11",
                  "Shortcut": { "OriginalKeys": "65", "TargetKeys": "66", "OperationType": 0 },
                  "Profiles": [],
                  "IsActive": false
                }
              },
              "ProfileDictionary": {},
              "ShortcutsByOperationType": {
                "RemapShortcut": [ "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11" ]
              },
              "ActiveProfile": "Home"
            }
            """;

        // A file from a pre-release profile-aware build: no SchemaVersion, most rows tagged, a few
        // untagged rows (shown in every profile by that build), and a recorded active profile other
        // than default.
        internal const string DevBuildJson = """
            {
              "ShortcutSettingsDictionary": {
                "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11": {
                  "Id": "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11",
                  "Shortcut": { "OriginalKeys": "65", "TargetKeys": "66", "OperationType": 0 },
                  "Profiles": [],
                  "IsActive": true
                },
                "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02": {
                  "Id": "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02",
                  "Shortcut": { "OriginalKeys": "70", "TargetKeys": "71", "OperationType": 0 },
                  "Profiles": [ "Apple Magic Keyboard" ],
                  "IsActive": true
                }
              },
              "ProfileDictionary": { "Apple Magic Keyboard": [ "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02" ] },
              "ShortcutsByOperationType": {
                "RemapShortcut": [ "6f1a3c52-0d4e-4b8a-9a51-2a7d5c0e9b11", "0b7e2d19-5c3a-4f6e-8d21-7e4a9c3b5f02" ]
              },
              "ActiveProfile": "Apple Magic Keyboard"
            }
            """;

        private static readonly Func<IEnumerable<string>> AllProfiles = () => new[] { "default", "Home", "Work", "Apple Magic Keyboard" };

        [TestMethod]
        public void StartupReconcile_AssignsLegacyDisabledRowToTheOriginalProfile()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            Assert.AreEqual(0, settings.SchemaVersion);

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "default", AllProfiles));

            ShortcutSettings row = settings.ShortcutSettingsDictionary[RowId];
            CollectionAssert.AreEqual(new List<string> { "default" }, row.Profiles);
            Assert.IsFalse(row.IsActive);
            CollectionAssert.AreEqual(new List<string> { RowId }, settings.ProfileDictionary["default"]);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, settings.SchemaVersion);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, Load(JsonSerializer.Serialize(settings)).SchemaVersion);
        }

        // Scenario: upgrade, create an empty Work profile, switch to it.
        [TestMethod]
        public void LegacyDisabledRow_IsNotInAnEmptyWorkProfile_AndWorkCannotRemoveOrClaimIt()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "default", AllProfiles);

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

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { Map("70", "71") }, "default", AllProfiles);

            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);
            Assert.IsFalse(settings.ShortcutSettingsDictionary[RowId].IsActive);
            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[ActiveRowId].Profiles);
            Assert.IsTrue(settings.ShortcutSettingsDictionary[ActiveRowId].IsActive);
            CollectionAssert.AreEquivalent(new List<string> { RowId, ActiveRowId }, settings.ProfileDictionary["default"]);
        }

        // The first reconcile that sees a version-0 file may run while another profile is active
        // (a start-up reconcile that failed, a switch, auto-switch before the editor started, a
        // restored old backup). The rows still go to the profile they came from, not to Work.
        [TestMethod]
        public void Reconcile_WhileWorkIsActive_AssignsLegacyRowsToDefault()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", AllProfiles);

            ShortcutSettings row = settings.ShortcutSettingsDictionary[RowId];
            CollectionAssert.AreEqual(new List<string> { "default" }, row.Profiles);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, settings.SchemaVersion);
            Assert.AreEqual("Work", settings.ActiveProfile);
            Assert.IsFalse(SettingsManager.IsMappingInActiveProfile(row, "Work"), "Work must not list default's turned-off row.");
            Assert.IsFalse(settings.ProfileDictionary.ContainsKey("Work"));
            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMappingRemoval(settings, RowId, "Work"));
        }

        // All rows of a v0.100 file are untagged, the turned-on ones too. Migrated under Work, they
        // all go to default; Work's own native mappings get rows of their own.
        [TestMethod]
        public void Reconcile_WhileWorkIsActive_KeepsDefaultsTurnedOnRowsInDefault()
        {
            EditorSettings settings = Load(V0101MixedJson);
            settings.ShortcutSettingsDictionary[ActiveRowId].Profiles.Clear();
            settings.ProfileDictionary.Clear();

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { Map("70", "71") }, "Work", AllProfiles);

            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[ActiveRowId].Profiles);
            Assert.IsFalse(settings.ShortcutSettingsDictionary[ActiveRowId].IsActive);
            CollectionAssert.AreEquivalent(new List<string> { RowId, ActiveRowId }, settings.ProfileDictionary["default"]);
            Assert.AreEqual(1, settings.ProfileDictionary["Work"].Count, "Work's native mapping gets its own row.");
        }

        // A file without any tagged row comes from a released single-profile editor: its rows go to
        // default even when the file records another profile that exists. The recorded value can
        // come from a write made while another profile was active, so it is not trusted.
        [TestMethod]
        public void Reconcile_OfAFileWithoutTaggedRows_IgnoresTheRecordedActiveProfile()
        {
            EditorSettings settings = Load(RecordedHomeJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", AllProfiles);

            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);
            Assert.AreEqual("Work", settings.ActiveProfile);
        }

        [TestMethod]
        public void Reconcile_OfAFileWithoutTaggedRows_AssignsLegacyRowsToDefault_WhenProfilesAreUnknown()
        {
            EditorSettings settings = Load(RecordedHomeJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work");

            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary[RowId].Profiles);
        }

        // A version-0 file with rows tagged with a profile other than default (v0.101 and v0.102 only
        // tag default) was written by a pre-release profile-aware build, which
        // showed untagged rows in every profile. They are tagged with every existing profile, not
        // with the recorded one, so each profile keeps listing them.
        [TestMethod]
        public void Reconcile_OfAProfileAwareDevFile_TagsUntaggedRowsWithEveryExistingProfile()
        {
            EditorSettings settings = Load(DevBuildJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { Map("65", "66"), Map("70", "71") }, "Apple Magic Keyboard", AllProfiles);

            CollectionAssert.AreEquivalent(
                new List<string> { "default", "Home", "Work", "Apple Magic Keyboard" },
                settings.ShortcutSettingsDictionary[RowId].Profiles);
            CollectionAssert.AreEqual(new List<string> { "Apple Magic Keyboard" }, settings.ShortcutSettingsDictionary[ActiveRowId].Profiles);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, settings.SchemaVersion);
            foreach (string profile in AllProfiles())
            {
                Assert.IsTrue(SettingsManager.IsMappingInActiveProfile(settings.ShortcutSettingsDictionary[RowId], profile), profile);
                CollectionAssert.Contains(settings.ProfileDictionary[profile], RowId, profile);
            }
        }

        // The outcome does not depend on which profile is active when the migration runs.
        [TestMethod]
        public void Reconcile_OfAProfileAwareDevFile_WhileWorkIsActive_TagsTheSameProfiles()
        {
            EditorSettings settings = Load(DevBuildJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", AllProfiles);

            CollectionAssert.AreEquivalent(
                new List<string> { "default", "Home", "Work", "Apple Magic Keyboard" },
                settings.ShortcutSettingsDictionary[RowId].Profiles);
        }

        // Once migrated, deleting the row from one profile only removes that profile's membership.
        [TestMethod]
        public void MigratedDevFileRow_DeletedFromWork_StaysInTheOtherProfiles()
        {
            EditorSettings settings = Load(DevBuildJson);
            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", AllProfiles);

            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMappingRemoval(settings, RowId, "Work"));

            Assert.IsTrue(settings.ShortcutSettingsDictionary.ContainsKey(RowId));
            CollectionAssert.AreEquivalent(
                new List<string> { "default", "Home", "Apple Magic Keyboard" },
                settings.ShortcutSettingsDictionary[RowId].Profiles);
            Assert.IsFalse(SettingsManager.IsMappingInActiveProfile(settings.ShortcutSettingsDictionary[RowId], "Work"));
        }

        // Without the list of existing profiles, a dev file's untagged rows go to default and to the
        // profiles the file itself names.
        [TestMethod]
        public void Reconcile_OfAProfileAwareDevFile_WhenProfilesAreUnknown_UsesDefaultAndTheNamedProfiles()
        {
            EditorSettings settings = Load(DevBuildJson);

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work");

            CollectionAssert.AreEquivalent(
                new List<string> { "default", "Apple Magic Keyboard" },
                settings.ShortcutSettingsDictionary[RowId].Profiles);
        }

        // The migration is one-time: once the file is marked, a later reconcile under another
        // profile does not adopt rows for any profile.
        [TestMethod]
        public void Reconcile_OfAMigratedFile_DoesNotAssignUntaggedRowsAgain()
        {
            EditorSettings settings = Load(V0100DisabledRowJson);
            settings.SchemaVersion = EditorSettings.CurrentSchemaVersion;

            SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping>(), "Work", AllProfiles);

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
