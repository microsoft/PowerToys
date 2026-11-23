// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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

        private static List<string> Strings(JsonNode array) =>
            array.AsArray().Select(n => n.GetValue<string>()).ToList();
    }
}
