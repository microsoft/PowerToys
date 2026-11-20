// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsBackupAndRestoreUtilsTests
    {
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
    }
}
