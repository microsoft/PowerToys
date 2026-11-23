// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsBackupAndRestoreUtilsTests
    {
        private static readonly string[] RetainedWork = { "Work" };

        // Mirrors the CustomRestoreSettings block in backup_restore_settings.json: an exact policy for
        // default.json (overwrite), an exact opt-out for the module settings.json (merge), and a
        // wildcard policy covering every other Keyboard Manager profile file (overwrite).
        private static JsonNode CustomRestoreSettings() => JsonNode.Parse(
            "{" +
            "\"\\\\Keyboard Manager\\\\default.json\": { \"overwrite\": true }," +
            "\"\\\\Keyboard Manager\\\\settings.json\": { \"overwrite\": false }," +
            "\"\\\\Keyboard Manager\\\\*.json\": { \"overwrite\": true }" +
            "}");

        [TestMethod]
        public void ShouldOverwriteOnRestore_ExactKeyMatch_ReturnsItsPolicy()
        {
            // default.json has an exact overwrite:true entry.
            Assert.IsTrue(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(CustomRestoreSettings(), "\\Keyboard Manager\\default.json"));
        }

        [TestMethod]
        public void ShouldOverwriteOnRestore_ExactKeyWinsOverWildcard()
        {
            // settings.json matches the "*.json" wildcard (overwrite:true) but its exact entry
            // (overwrite:false) must win, keeping the module settings file on the merge path.
            Assert.IsFalse(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(CustomRestoreSettings(), "\\Keyboard Manager\\settings.json"));
        }

        [TestMethod]
        public void ShouldOverwriteOnRestore_WildcardMatch_AppliesToUserNamedProfiles()
        {
            // Custom profile files have user-defined names with no exact key; the wildcard covers them.
            Assert.IsTrue(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(CustomRestoreSettings(), "\\Keyboard Manager\\Work.json"));
            Assert.IsTrue(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(CustomRestoreSettings(), "\\Keyboard Manager\\deviceProfiles.json"));
        }

        [TestMethod]
        public void ShouldOverwriteOnRestore_NoMatch_ReturnsFalse()
        {
            // A file outside the wildcard's folder has no policy and must fall through to merge.
            Assert.IsFalse(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(CustomRestoreSettings(), "\\FancyZones\\custom-layouts.json"));
        }

        [TestMethod]
        public void ShouldOverwriteOnRestore_NullPolicy_ReturnsFalse()
        {
            Assert.IsFalse(SettingsBackupAndRestoreUtils.ShouldOverwriteOnRestore(null, "\\Keyboard Manager\\Work.json"));
        }

        // Keyboard Manager profiles that exist only on this machine are not in the backup, so the
        // restore leaves their {id}.json alone. The stores and set-aside files are not profiles.
        [TestMethod]
        public void GetRetainedKeyboardManagerProfiles_ListsProfilesOnlyOnThisMachine()
        {
            string[] current =
            {
                "\\Keyboard Manager\\default.json",
                "\\Keyboard Manager\\Work.json",
                "\\Keyboard Manager\\home.json",
                "\\Keyboard Manager\\settings.json",
                "\\Keyboard Manager\\editorSettings.json",
                "\\Keyboard Manager\\deviceProfiles.json",
                "\\Keyboard Manager\\profileMetadata.json",
                "\\Keyboard Manager\\Old.deleting-0123456789abcdef.json",
                "\\FancyZones\\custom-layouts.json",
                "\\settings.json",
            };
            string[] backup =
            {
                "\\Keyboard Manager\\default.json",
                "\\Keyboard Manager\\Home.json",
                "\\Keyboard Manager\\editorSettings.json",
            };

            CollectionAssert.AreEquivalent(
                new List<string> { "Work" },
                SettingsBackupAndRestoreUtils.GetRetainedKeyboardManagerProfiles(current, backup).ToList());
        }

        // The reviewer's scenario: the backup's editorSettings.json replaces this machine's, but Work
        // is kept. Work's turned-off row exists only in the editor metadata, so it must be carried
        // over; Work's membership of a row also in the backup is kept; default's rows come from the
        // backup.
        [TestMethod]
        public void PreserveRetained_EditorSettings_KeepsRowsOfRetainedProfiles()
        {
            const string backup = """
                {
                  "ShortcutSettingsDictionary": {
                    "r2": { "Id": "r2", "Shortcut": { "OriginalKeys": "66", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true },
                    "r4": { "Id": "r4", "Shortcut": { "OriginalKeys": "68", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                  },
                  "ProfileDictionary": { "default": [ "r2", "r4" ] },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "r2", "r4" ] },
                  "ActiveProfile": "default",
                  "SchemaVersion": 1
                }
                """;
            const string current = """
                {
                  "ShortcutSettingsDictionary": {
                    "r1": { "Id": "r1", "Shortcut": { "OriginalKeys": "65", "OperationType": 3, "TargetText": "hi" }, "Profiles": [ "Work" ], "IsActive": false },
                    "r2": { "Id": "r2", "Shortcut": { "OriginalKeys": "66", "OperationType": 0 }, "Profiles": [ "default", "Work" ], "IsActive": true },
                    "r3": { "Id": "r3", "Shortcut": { "OriginalKeys": "67", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                  },
                  "ProfileDictionary": { "default": [ "r2", "r3" ], "Work": [ "r1", "r2" ] },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "r2", "r3" ], "RemapText": [ "r1" ] },
                  "ActiveProfile": "Work",
                  "SchemaVersion": 1
                }
                """;

            JsonNode result = JsonNode.Parse(SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles(
                "\\Keyboard Manager\\editorSettings.json", backup, current, RetainedWork));

            JsonNode rows = result["ShortcutSettingsDictionary"];
            CollectionAssert.AreEquivalent(new List<string> { "r1", "r2", "r4" }, rows.AsObject().Select(p => p.Key).ToList());
            CollectionAssert.AreEqual(new List<string> { "Work" }, Strings(rows["r1"]["Profiles"]));
            Assert.IsFalse(rows["r1"]["IsActive"].GetValue<bool>());
            Assert.AreEqual("hi", rows["r1"]["Shortcut"]["TargetText"].GetValue<string>());
            CollectionAssert.AreEquivalent(new List<string> { "default", "Work" }, Strings(rows["r2"]["Profiles"]));
            CollectionAssert.AreEquivalent(new List<string> { "r1", "r2" }, Strings(result["ProfileDictionary"]["Work"]));
            CollectionAssert.AreEquivalent(new List<string> { "r2", "r4" }, Strings(result["ProfileDictionary"]["default"]));
            CollectionAssert.AreEqual(new List<string> { "r1" }, Strings(result["ShortcutsByOperationType"]["RemapText"]));
            CollectionAssert.AreEqual(new List<string> { "r2", "r4" }, Strings(result["ShortcutsByOperationType"]["RemapShortcut"]));
            Assert.AreEqual("default", result["ActiveProfile"].GetValue<string>());
        }

        // Keyboard assignments to a retained profile are kept unless the backup assigns that
        // keyboard itself; the backup's flag and hotkey win.
        [TestMethod]
        public void PreserveRetained_DeviceProfiles_KeepsAssignmentsToRetainedProfiles()
        {
            const string backup = """{ "autoSwitchEnabled": false, "map": [ { "device": "k1", "profile": "default", "name": "K1" } ], "cycleHotkey": { "key": 112 } }""";
            const string current = """
                { "autoSwitchEnabled": true, "map": [
                  { "device": "k1", "profile": "Work", "name": "K1" },
                  { "device": "k2", "profile": "Work", "name": "K2" },
                  { "device": "k3", "profile": "default", "name": "K3" } ] }
                """;

            JsonNode result = JsonNode.Parse(SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles(
                "\\Keyboard Manager\\deviceProfiles.json", backup, current, RetainedWork));

            CollectionAssert.AreEqual(
                new List<string> { "k1=default", "k2=Work" },
                result["map"].AsArray().Select(e => $"{e["device"]}={e["profile"]}").ToList());
            Assert.IsFalse(result["autoSwitchEnabled"].GetValue<bool>());
            Assert.AreEqual(112, result["cycleHotkey"]["key"].GetValue<int>());
        }

        [TestMethod]
        public void PreserveRetained_ProfileMetadata_KeepsDisplayNamesOfRetainedProfiles()
        {
            const string backup = """{ "profiles": { "Home": { "name": "House" } } }""";
            const string current = """{ "profiles": { "Work": { "name": "Office" }, "Home": { "name": "Flat" }, "Gone": { "name": "x" } } }""";

            JsonNode result = JsonNode.Parse(SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles(
                "\\Keyboard Manager\\profileMetadata.json", backup, current, RetainedWork));

            Assert.AreEqual("Office", result["profiles"]["Work"]["name"].GetValue<string>());
            Assert.AreEqual("House", result["profiles"]["Home"]["name"].GetValue<string>());
            Assert.IsNull(result["profiles"]["Gone"]);
        }

        // Nothing changes for other files, for other modules, when no profile is retained, or when
        // either side cannot be parsed.
        [TestMethod]
        public void PreserveRetained_LeavesTheBackupUnchangedOtherwise()
        {
            const string backup = """{ "ShortcutSettingsDictionary": {} }""";
            const string current = """{ "ShortcutSettingsDictionary": { "r1": { "Profiles": [ "Work" ] } } }""";

            Assert.AreEqual(backup, SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles("\\Keyboard Manager\\Work.json", backup, current, RetainedWork));
            Assert.AreEqual(backup, SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles("\\FancyZones\\editorSettings.json", backup, current, RetainedWork));
            Assert.AreEqual(backup, SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles("\\Keyboard Manager\\editorSettings.json", backup, current, System.Array.Empty<string>()));
            Assert.AreEqual(backup, SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles("\\Keyboard Manager\\editorSettings.json", backup, "{ not json", RetainedWork));
        }

        // An older backup of this machine has the same row id. After the backup, the row was split
        // off to Work, edited there and turned off. The backup's row keeps default's content, and
        // Work's edited, turned-off row is kept under a new id for Work only.
        [TestMethod]
        public void PreserveRetained_EditorSettings_KeepsTheRetainedContentOfARowEditedAfterTheBackup()
        {
            const string backup = """
                {
                  "ShortcutSettingsDictionary": {
                    "g": { "Id": "g", "Shortcut": { "OriginalKeys": "162;65", "TargetKeys": "162;66", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                  },
                  "ProfileDictionary": { "default": [ "g" ] },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "g" ] },
                  "ActiveProfile": "default",
                  "SchemaVersion": 1
                }
                """;
            const string current = """
                {
                  "ShortcutSettingsDictionary": {
                    "g": { "Id": "g", "Shortcut": { "OriginalKeys": "162;65", "TargetKeys": "162;67", "OperationType": 0 }, "Profiles": [ "Work" ], "IsActive": false },
                    "n": { "Id": "n", "Shortcut": { "OriginalKeys": "162;65", "TargetKeys": "162;66", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                  },
                  "ProfileDictionary": { "default": [ "n" ], "Work": [ "g" ] },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "g", "n" ] },
                  "ActiveProfile": "Work",
                  "SchemaVersion": 1
                }
                """;

            JsonNode result = JsonNode.Parse(SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles(
                "\\Keyboard Manager\\editorSettings.json", backup, current, RetainedWork));

            JsonObject rows = result["ShortcutSettingsDictionary"].AsObject();
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("162;66", rows["g"]["Shortcut"]["TargetKeys"].GetValue<string>());
            Assert.IsTrue(rows["g"]["IsActive"].GetValue<bool>());
            CollectionAssert.AreEqual(new List<string> { "default" }, Strings(rows["g"]["Profiles"]));

            string workId = rows.Single(p => p.Key != "g").Key;
            JsonNode workRow = rows[workId];
            Assert.AreEqual(workId, workRow["Id"].GetValue<string>());
            Assert.AreEqual("162;67", workRow["Shortcut"]["TargetKeys"].GetValue<string>());
            Assert.IsFalse(workRow["IsActive"].GetValue<bool>());
            CollectionAssert.AreEqual(new List<string> { "Work" }, Strings(workRow["Profiles"]));
            CollectionAssert.AreEqual(new List<string> { workId }, Strings(result["ProfileDictionary"]["Work"]));
            CollectionAssert.AreEqual(new List<string> { "g" }, Strings(result["ProfileDictionary"]["default"]));
            CollectionAssert.AreEqual(new List<string> { "g", workId }, Strings(result["ShortcutsByOperationType"]["RemapShortcut"]));
        }

        // Same id, same mapping, but turned off in Work after the backup: that is a content change
        // too, so Work's row is kept separately and the backup's row stays on.
        [TestMethod]
        public void PreserveRetained_EditorSettings_TreatsAChangedOnOffStateAsEdited()
        {
            const string backup = """
                {
                  "ShortcutSettingsDictionary": {
                    "g": { "Id": "g", "Shortcut": { "OriginalKeys": "66", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                  },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "g" ] }
                }
                """;
            const string current = """
                {
                  "ShortcutSettingsDictionary": {
                    "g": { "Id": "g", "Shortcut": { "OriginalKeys": "66", "OperationType": 0 }, "Profiles": [ "Work" ], "IsActive": false }
                  },
                  "ShortcutsByOperationType": { "RemapShortcut": [ "g" ] }
                }
                """;

            JsonNode result = JsonNode.Parse(SettingsBackupAndRestoreUtils.PreserveRetainedKeyboardManagerProfiles(
                "\\Keyboard Manager\\editorSettings.json", backup, current, RetainedWork));

            JsonObject rows = result["ShortcutSettingsDictionary"].AsObject();
            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows["g"]["IsActive"].GetValue<bool>());
            CollectionAssert.AreEqual(new List<string> { "default" }, Strings(rows["g"]["Profiles"]));
            Assert.IsFalse(rows.Single(p => p.Key != "g").Value["IsActive"].GetValue<bool>());
        }

        // The whole restore of two folders: the backup has only default, this machine also has Work
        // with a turned-off row, a keyboard assignment and a display name. The retained profile's
        // file stays, and its entries in the shared stores survive; default's come from the backup.
        [TestMethod]
        public void RestoreSettingsFiles_KeepsTheSharedStoreEntriesOfARetainedProfile()
        {
            string root = Path.Combine(Path.GetTempPath(), "PowerToysRestoreTest-" + Guid.NewGuid().ToString("N"));
            string current = Path.Combine(root, "current");
            string backupFolder = Path.Combine(root, "backup");
            try
            {
                WriteKeyboardManagerFile(backupFolder, "default.json", """{ "remapKeys": { "inProcess": [] } }""");
                const string backupEditorSettings = """
                    {
                      "ShortcutSettingsDictionary": {
                        "d": { "Id": "d", "Shortcut": { "OriginalKeys": "66", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                      },
                      "ProfileDictionary": { "default": [ "d" ] },
                      "ShortcutsByOperationType": { "RemapShortcut": [ "d" ] },
                      "ActiveProfile": "default",
                      "SchemaVersion": 1
                    }
                    """;
                WriteKeyboardManagerFile(backupFolder, "editorSettings.json", backupEditorSettings);
                WriteKeyboardManagerFile(backupFolder, "deviceProfiles.json", """{ "autoSwitchEnabled": false, "map": [] }""");
                WriteKeyboardManagerFile(backupFolder, "profileMetadata.json", """{ "profiles": {} }""");

                WriteKeyboardManagerFile(current, "default.json", """{ "remapKeys": { "inProcess": [ { "originalKeys": "65", "newRemapKeys": "66" } ] } }""");
                WriteKeyboardManagerFile(current, "Work.json", """{ "remapKeys": { "inProcess": [] } }""");
                const string currentEditorSettings = """
                    {
                      "ShortcutSettingsDictionary": {
                        "w": { "Id": "w", "Shortcut": { "OriginalKeys": "67", "OperationType": 3, "TargetText": "hi" }, "Profiles": [ "Work" ], "IsActive": false },
                        "x": { "Id": "x", "Shortcut": { "OriginalKeys": "68", "OperationType": 0 }, "Profiles": [ "default" ], "IsActive": true }
                      },
                      "ProfileDictionary": { "default": [ "x" ], "Work": [ "w" ] },
                      "ShortcutsByOperationType": { "RemapShortcut": [ "x" ], "RemapText": [ "w" ] },
                      "ActiveProfile": "Work",
                      "SchemaVersion": 1
                    }
                    """;
                WriteKeyboardManagerFile(current, "editorSettings.json", currentEditorSettings);
                WriteKeyboardManagerFile(current, "deviceProfiles.json", """{ "autoSwitchEnabled": true, "map": [ { "device": "k1", "profile": "Work", "name": "K1" } ] }""");
                WriteKeyboardManagerFile(current, "profileMetadata.json", """{ "profiles": { "Work": { "name": "Office" } } }""");

                Assert.AreEqual(true, SettingsBackupAndRestoreUtils.RestoreSettingsFiles(RestoreConfiguration(), current, backupFolder));

                string folder = Path.Combine(current, "Keyboard Manager");
                Assert.IsTrue(File.Exists(Path.Combine(folder, "Work.json")));
                JsonNode editor = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "editorSettings.json")));
                CollectionAssert.AreEquivalent(new List<string> { "d", "w" }, editor["ShortcutSettingsDictionary"].AsObject().Select(p => p.Key).ToList());
                Assert.IsFalse(editor["ShortcutSettingsDictionary"]["w"]["IsActive"].GetValue<bool>());
                CollectionAssert.AreEqual(new List<string> { "w" }, Strings(editor["ProfileDictionary"]["Work"]));
                JsonNode devices = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "deviceProfiles.json")));
                CollectionAssert.AreEqual(new List<string> { "k1=Work" }, devices["map"].AsArray().Select(e => $"{e["device"]}={e["profile"]}").ToList());
                JsonNode metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "profileMetadata.json")));
                Assert.AreEqual("Office", metadata["profiles"]["Work"]["name"].GetValue<string>());
                JsonNode defaultConfig = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "default.json")));
                Assert.AreEqual(0, defaultConfig["remapKeys"]["inProcess"].AsArray().Count);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static List<string> Strings(JsonNode array) =>
            array.AsArray().Select(n => n.GetValue<string>()).ToList();

        // The Keyboard Manager part of backup_restore_settings.json.
        private static JsonNode RestoreConfiguration() => JsonNode.Parse(
            "{" +
            "\"RestartAfterRestore\": true," +
            "\"IncludeFiles\": [ \"*Keyboard Manager\\\\*.json\" ]," +
            "\"IgnoreFiles\": []," +
            "\"CustomRestoreSettings\": {" +
            "\"\\\\Keyboard Manager\\\\default.json\": { \"overwrite\": true }," +
            "\"\\\\Keyboard Manager\\\\editorSettings.json\": { \"overwrite\": true }," +
            "\"\\\\Keyboard Manager\\\\settings.json\": { \"overwrite\": false }," +
            "\"\\\\Keyboard Manager\\\\*.json\": { \"overwrite\": true }" +
            "}" +
            "}");

        private static void WriteKeyboardManagerFile(string baseFolder, string name, string json)
        {
            string folder = Path.Combine(baseFolder, "Keyboard Manager");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), json);
        }
    }
}
