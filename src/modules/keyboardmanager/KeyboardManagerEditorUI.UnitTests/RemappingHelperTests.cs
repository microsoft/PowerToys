// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class RemappingHelperTests
    {
        // Test that modifier keys pressed in reverse order (Shift, Alt, Ctrl, Win)
        // are sorted into the standard display order (Win, Ctrl, Alt, Shift)
        [TestMethod]
        public void SortModifierKeys_ShouldSortInCanonicalOrder_WhenPressedInReverseOrder()
        {
            // Arrange - keys in reverse of standard order
            var keys = new List<VirtualKey>
            {
                VirtualKey.Shift,
                VirtualKey.Menu,        // Alt
                VirtualKey.Control,
                VirtualKey.LeftWindows,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - should be Win, Ctrl, Alt, Shift
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Control, keys[1]);
            Assert.AreEqual(VirtualKey.Menu, keys[2]);
            Assert.AreEqual(VirtualKey.Shift, keys[3]);
        }

        // Test the specific bug scenario: Win+Shift+S where Shift was pressed before Win
        [TestMethod]
        public void SortModifierKeys_ShouldShowWinBeforeShift_WhenShiftPressedFirst()
        {
            // Arrange - Shift pressed before Win (the reported bug scenario)
            var keys = new List<VirtualKey>
            {
                VirtualKey.Shift,
                VirtualKey.LeftWindows,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Win should come before Shift
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Shift, keys[1]);
        }

        // Test that keys already in the correct order remain unchanged
        [TestMethod]
        public void SortModifierKeys_ShouldPreserveOrder_WhenAlreadyInCanonicalOrder()
        {
            // Arrange - already in correct order
            var keys = new List<VirtualKey>
            {
                VirtualKey.LeftWindows,
                VirtualKey.Control,
                VirtualKey.Menu,
                VirtualKey.Shift,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - order should be unchanged
            Assert.AreEqual(VirtualKey.LeftWindows, keys[0]);
            Assert.AreEqual(VirtualKey.Control, keys[1]);
            Assert.AreEqual(VirtualKey.Menu, keys[2]);
            Assert.AreEqual(VirtualKey.Shift, keys[3]);
        }

        // Test that left/right variants of the same modifier maintain their relative
        // position but are grouped correctly relative to other modifier types
        [TestMethod]
        public void SortModifierKeys_ShouldSortCorrectly_WithLeftRightVariants()
        {
            // Arrange - right shift before left control
            var keys = new List<VirtualKey>
            {
                VirtualKey.RightShift,
                VirtualKey.LeftControl,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Ctrl should come before Shift
            Assert.AreEqual(VirtualKey.LeftControl, keys[0]);
            Assert.AreEqual(VirtualKey.RightShift, keys[1]);
        }

        // Test with a single modifier key (should not throw)
        [TestMethod]
        public void SortModifierKeys_ShouldHandleSingleModifier()
        {
            // Arrange
            var keys = new List<VirtualKey> { VirtualKey.Control };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(VirtualKey.Control, keys[0]);
        }

        // Test with an empty list (should not throw)
        [TestMethod]
        public void SortModifierKeys_ShouldHandleEmptyList()
        {
            // Arrange
            var keys = new List<VirtualKey>();

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert
            Assert.AreEqual(0, keys.Count);
        }

        // Test GetModifierSortOrder returns correct values
        [TestMethod]
        public void GetModifierSortOrder_ShouldReturnCorrectOrder_ForAllModifierTypes()
        {
            // Win keys should return 0
            Assert.AreEqual(0, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftWindows));
            Assert.AreEqual(0, RemappingHelper.GetModifierSortOrder(VirtualKey.RightWindows));

            // Ctrl keys should return 1
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.Control));
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftControl));
            Assert.AreEqual(1, RemappingHelper.GetModifierSortOrder(VirtualKey.RightControl));

            // Alt keys should return 2
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.Menu));
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftMenu));
            Assert.AreEqual(2, RemappingHelper.GetModifierSortOrder(VirtualKey.RightMenu));

            // Shift keys should return 3
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.Shift));
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.LeftShift));
            Assert.AreEqual(3, RemappingHelper.GetModifierSortOrder(VirtualKey.RightShift));
        }

        // Test that non-modifier keys get the highest sort order (4)
        [TestMethod]
        public void GetModifierSortOrder_ShouldReturnFour_ForNonModifierKeys()
        {
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.A));
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.Space));
            Assert.AreEqual(4, RemappingHelper.GetModifierSortOrder(VirtualKey.Enter));
        }

        // Test that two modifiers pressed out of order (Alt then Ctrl) get corrected
        [TestMethod]
        public void SortModifierKeys_ShouldSortCorrectly_WhenAltPressedBeforeCtrl()
        {
            // Arrange
            var keys = new List<VirtualKey>
            {
                VirtualKey.Menu,        // Alt
                VirtualKey.Control,
            };

            // Act
            RemappingHelper.SortModifierKeys(keys);

            // Assert - Ctrl should come before Alt
            Assert.AreEqual(VirtualKey.Control, keys[0]);
            Assert.AreEqual(VirtualKey.Menu, keys[1]);
        }

        [TestMethod]
        public void TryApplyShortcutKeyMapping_ShouldReplaceExistingMappingWithoutDuplicatingId()
        {
            const string mappingId = "mapping-id";
            var settings = CreateEditorSettings(mappingId, ShortcutOperationType.RemapShortcut, "65", "66");
            var replacement = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "67");
            replacement.ExactMatch = true;

            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMapping(settings, replacement, mappingId));
            Assert.AreSame(replacement, settings.ShortcutSettingsDictionary[mappingId].Shortcut);
            Assert.IsTrue(settings.ShortcutSettingsDictionary[mappingId].IsActive);
            Assert.IsTrue(settings.ShortcutSettingsDictionary[mappingId].Shortcut.ExactMatch);
            CollectionAssert.AreEqual(new[] { mappingId }, settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut]);
        }

        [TestMethod]
        public void TryApplyShortcutKeyMapping_ShouldMoveIdToReplacementOperationType()
        {
            const string mappingId = "mapping-id";
            var settings = CreateEditorSettings(mappingId, ShortcutOperationType.RemapShortcut, "65", "66");
            var replacement = CreateMapping(ShortcutOperationType.RemapText, "65", "replacement text");

            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMapping(settings, replacement, mappingId));
            Assert.AreEqual(0, settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut].Count);
            CollectionAssert.AreEqual(new[] { mappingId }, settings.ShortcutsByOperationType[ShortcutOperationType.RemapText]);
        }

        [TestMethod]
        public void TryApplyShortcutKeyMapping_ShouldLeaveSettingsUnchangedForUnknownId()
        {
            const string mappingId = "mapping-id";
            var original = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "66");
            var settings = CreateEditorSettings(mappingId, original);
            var replacement = CreateMapping(ShortcutOperationType.RemapText, "65", "replacement text");

            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMapping(settings, replacement, "missing-id"));
            Assert.AreSame(original, settings.ShortcutSettingsDictionary[mappingId].Shortcut);
            CollectionAssert.AreEqual(new[] { mappingId }, settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut]);
            Assert.IsFalse(settings.ShortcutsByOperationType.ContainsKey(ShortcutOperationType.RemapText));
        }

        [TestMethod]
        public void TryApplyShortcutKeyMapping_ShouldSplitSharedMappingBeforeProfileReplacement()
        {
            const string mappingId = "mapping-id";
            var original = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "66");
            var replacement = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "67");
            var settings = CreateEditorSettings(mappingId, original);
            settings.ShortcutSettingsDictionary[mappingId].Profiles.AddRange(new List<string> { "profile-one", "profile-two" });
            settings.ProfileDictionary["profile-one"] = new List<string> { mappingId };
            settings.ProfileDictionary["profile-two"] = new List<string> { mappingId };

            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMapping(settings, replacement, mappingId, "profile-two"));
            Assert.AreEqual(2, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(original, settings.ShortcutSettingsDictionary[mappingId].Shortcut);
            Assert.IsFalse(settings.ShortcutSettingsDictionary[mappingId].IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, settings.ShortcutSettingsDictionary[mappingId].Profiles);

            KeyValuePair<string, ShortcutSettings> replacementEntry = settings.ShortcutSettingsDictionary.Single(entry => entry.Key != mappingId);
            Assert.AreSame(replacement, replacementEntry.Value.Shortcut);
            Assert.IsTrue(replacementEntry.Value.IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-two" }, replacementEntry.Value.Profiles);
            CollectionAssert.AreEqual(new List<string> { mappingId }, settings.ProfileDictionary["profile-one"]);
            CollectionAssert.AreEqual(new List<string> { replacementEntry.Key }, settings.ProfileDictionary["profile-two"]);
        }

        [TestMethod]
        public void NormalizeSettings_ShouldRepairNullCollectionsAndDiscardInvalidMappings()
        {
            var validMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "66");
            validMapping.TargetKeys = null!;
            validMapping.TargetApp = null!;
            validMapping.ProgramArgs = null!;
            var invalidTrigger = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "67");
            invalidTrigger.OriginalKeys = null!;
            var settings = new EditorSettings
            {
                ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>
                {
                    ["valid-id"] = new ShortcutSettings
                    {
                        Id = "wrong-id",
                        Shortcut = validMapping,
                        Profiles = null!,
                    },
                    ["null-settings"] = null!,
                    ["null-shortcut"] = new ShortcutSettings { Shortcut = null! },
                    ["null-trigger"] = new ShortcutSettings { Shortcut = invalidTrigger },
                },
                ProfileDictionary = new Dictionary<string, List<string>>
                {
                    ["profile-one"] = new List<string> { "valid-id", null!, "missing-id" },
                    ["profile-null"] = null!,
                },
                ShortcutsByOperationType = new Dictionary<ShortcutOperationType, List<string>>
                {
                    [ShortcutOperationType.RemapShortcut] = null!,
                },
                ActiveProfile = null!,
            };

            Assert.IsTrue(SettingsManager.NormalizeSettings(settings));
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreEqual("valid-id", settings.ShortcutSettingsDictionary["valid-id"].Id);
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, settings.ShortcutSettingsDictionary["valid-id"].Profiles);
            Assert.AreEqual(string.Empty, settings.ShortcutSettingsDictionary["valid-id"].Shortcut.TargetKeys);
            Assert.AreEqual(string.Empty, settings.ShortcutSettingsDictionary["valid-id"].Shortcut.TargetApp);
            Assert.AreEqual(string.Empty, settings.ShortcutSettingsDictionary["valid-id"].Shortcut.ProgramArgs);
            CollectionAssert.AreEqual(new List<string> { "valid-id" }, settings.ProfileDictionary["profile-one"]);
            Assert.IsFalse(settings.ProfileDictionary.ContainsKey("profile-null"));
            CollectionAssert.AreEqual(new List<string> { "valid-id" }, settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut]);
            Assert.AreEqual(string.Empty, settings.ActiveProfile);
        }

        [TestMethod]
        public void NormalizeSettings_ShouldDiscardCaseVariantDuplicateIds()
        {
            var retainedMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "66");
            var duplicateMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "65", "67");
            var settings = new EditorSettings
            {
                ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>(StringComparer.Ordinal)
                {
                    ["mapping-id"] = new ShortcutSettings
                    {
                        Id = "mapping-id",
                        Shortcut = retainedMapping,
                    },
                    ["MAPPING-ID"] = new ShortcutSettings
                    {
                        Id = "MAPPING-ID",
                        Shortcut = duplicateMapping,
                    },
                },
            };

            Assert.IsTrue(SettingsManager.NormalizeSettings(settings));
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreEqual("mapping-id", settings.ShortcutSettingsDictionary.Keys.Single());
            Assert.AreSame(retainedMapping, settings.ShortcutSettingsDictionary["MAPPING-ID"].Shortcut);
            CollectionAssert.AreEqual(new List<string> { "mapping-id" }, settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut]);
        }

        [TestMethod]
        public void TryApplyShortcutKeyMappingRemoval_ShouldDetachOnlyActiveProfileFromSharedMapping()
        {
            const string mappingId = "mapping-id";
            var settings = CreateEditorSettings(mappingId, ShortcutOperationType.RemapShortcut, "65", "66");
            settings.ShortcutSettingsDictionary[mappingId].Profiles.AddRange(new List<string> { "profile-one", "profile-two" });
            settings.ProfileDictionary["profile-one"] = new List<string> { mappingId };
            settings.ProfileDictionary["profile-two"] = new List<string> { mappingId };

            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMappingRemoval(settings, mappingId, "profile-two"));
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.IsFalse(settings.ShortcutSettingsDictionary[mappingId].IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, settings.ShortcutSettingsDictionary[mappingId].Profiles);
            CollectionAssert.AreEqual(new List<string> { mappingId }, settings.ProfileDictionary["profile-one"]);
            Assert.IsFalse(settings.ProfileDictionary.ContainsKey("profile-two"));
        }

        [TestMethod]
        public void TryApplyShortcutKeyMappingActiveState_ShouldRejectForeignProfileAndClaimLegacyMetadata()
        {
            const string mappingId = "mapping-id";
            var foreignSettings = CreateEditorSettings(mappingId, ShortcutOperationType.RemapShortcut, "65", "66");
            foreignSettings.ShortcutSettingsDictionary[mappingId].Profiles.Add("profile-one");
            foreignSettings.ProfileDictionary["profile-one"] = new List<string> { mappingId };

            Assert.IsFalse(SettingsManager.TryApplyShortcutKeyMappingActiveState(foreignSettings, mappingId, true, "profile-two"));
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, foreignSettings.ShortcutSettingsDictionary[mappingId].Profiles);

            var legacySettings = CreateEditorSettings(mappingId, ShortcutOperationType.RemapShortcut, "65", "66");
            Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMappingActiveState(legacySettings, mappingId, true, "profile-two"));
            Assert.IsTrue(legacySettings.ShortcutSettingsDictionary[mappingId].IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-two" }, legacySettings.ShortcutSettingsDictionary[mappingId].Profiles);
            CollectionAssert.AreEqual(new List<string> { mappingId }, legacySettings.ProfileDictionary["profile-two"]);
        }

        [TestMethod]
        public void ActionTargetKeys_ShouldUseSyntheticNativeKeysAndCanonicalEmptyReadback()
        {
            var runProgram = CreateMapping(ShortcutOperationType.RunProgram, "162;80", string.Empty);
            var openUri = CreateMapping(ShortcutOperationType.OpenUri, "162;85", string.Empty);
            var remapText = CreateMapping(ShortcutOperationType.RemapText, "162;84", string.Empty);
            remapText.TargetText = "replacement text";

            Assert.AreEqual("162;80", KeyboardMappingService.GetNativeTargetKeys(runProgram));
            Assert.AreEqual("162;85", KeyboardMappingService.GetNativeTargetKeys(openUri));
            Assert.AreEqual("replacement text", KeyboardMappingService.GetNativeTargetKeys(remapText));
            Assert.AreEqual(
                string.Empty,
                KeyboardMappingService.CanonicalizeTargetKeys(ShortcutOperationType.RunProgram, "162;80"));
            Assert.AreEqual(
                string.Empty,
                KeyboardMappingService.CanonicalizeTargetKeys(ShortcutOperationType.OpenUri, "162;85"));
            Assert.AreEqual(
                string.Empty,
                KeyboardMappingService.CanonicalizeTargetKeys(ShortcutOperationType.RemapText, "replacement text"));
            Assert.AreEqual(
                "162;66",
                KeyboardMappingService.CanonicalizeTargetKeys(ShortcutOperationType.RemapShortcut, "162;66"));
        }

        [TestMethod]
        public void ReconcileMappings_ShouldReplaceEveryPersistedActionFieldAndCanonicalizeTargets()
        {
            var oldProgram = CreateMapping(ShortcutOperationType.RunProgram, "162;80", "162;80");
            oldProgram.ProgramPath = "old.exe";
            var oldUri = CreateMapping(ShortcutOperationType.OpenUri, "162;85", "162;85");
            oldUri.UriToOpen = "https://old.example";
            var settings = CreateEditorSettings(
                ("program-id", oldProgram, true),
                ("uri-id", oldUri, true));

            var persistedProgram = CreateMapping(ShortcutOperationType.RunProgram, "162;80", string.Empty);
            persistedProgram.ProgramPath = "new.exe";
            persistedProgram.ProgramArgs = "--new";
            persistedProgram.StartInDirectory = "C:\\New";
            persistedProgram.Elevation = ShortcutKeyMapping.ElevationLevel.Elevated;
            persistedProgram.IfRunningAction = ShortcutKeyMapping.ProgramAlreadyRunningAction.StartAnother;
            persistedProgram.Visibility = ShortcutKeyMapping.StartWindowType.Maximized;
            persistedProgram.ExactMatch = true;
            var persistedUri = CreateMapping(ShortcutOperationType.OpenUri, "162;85", string.Empty);
            persistedUri.UriToOpen = "https://new.example";

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { persistedProgram, persistedUri }));
            Assert.AreSame(persistedProgram, settings.ShortcutSettingsDictionary["program-id"].Shortcut);
            Assert.AreSame(persistedUri, settings.ShortcutSettingsDictionary["uri-id"].Shortcut);
            Assert.AreEqual(string.Empty, settings.ShortcutSettingsDictionary["program-id"].Shortcut.TargetKeys);
            Assert.AreEqual(string.Empty, settings.ShortcutSettingsDictionary["uri-id"].Shortcut.TargetKeys);
            Assert.IsTrue(settings.ShortcutSettingsDictionary["program-id"].IsActive);
            Assert.IsTrue(settings.ShortcutSettingsDictionary["uri-id"].IsActive);
            CollectionAssert.AreEqual(new List<string> { "program-id" }, settings.ShortcutsByOperationType[ShortcutOperationType.RunProgram]);
            CollectionAssert.AreEqual(new List<string> { "uri-id" }, settings.ShortcutsByOperationType[ShortcutOperationType.OpenUri]);
        }

        [TestMethod]
        public void ReconcileMappings_ShouldReactivateExactMatchAndDeactivateDuplicateAndStaleMetadata()
        {
            var persisted = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;66");
            var duplicate = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;67");
            var stale = CreateMapping(ShortcutOperationType.RemapText, "162;68", "stale");
            var settings = CreateEditorSettings(
                ("matching-id", persisted, false),
                ("duplicate-id", duplicate, true),
                ("stale-id", stale, true));

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { persisted }));
            Assert.IsTrue(settings.ShortcutSettingsDictionary["matching-id"].IsActive);
            Assert.IsFalse(settings.ShortcutSettingsDictionary["duplicate-id"].IsActive);
            Assert.IsFalse(settings.ShortcutSettingsDictionary["stale-id"].IsActive);
            CollectionAssert.AreEqual(
                new List<string> { "matching-id", "duplicate-id" },
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut]);
            CollectionAssert.AreEqual(
                new List<string> { "stale-id" },
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapText]);
        }

        [TestMethod]
        public void ReconcileMappings_ShouldNotRewriteCanonicalMetadata()
        {
            var persisted = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;66");
            var settings = CreateEditorSettings(("mapping-id", persisted, true));

            Assert.IsFalse(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { persisted }));
            Assert.AreSame(persisted, settings.ShortcutSettingsDictionary["mapping-id"].Shortcut);
        }

        [TestMethod]
        public void ReconcileMappings_ShouldPreserveInactiveMetadataOwnedByAnotherProfile()
        {
            var profileOneMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;66");
            var profileTwoMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;67");
            var settings = CreateEditorSettings(("profile-one-id", profileOneMapping, false));
            settings.ShortcutSettingsDictionary["profile-one-id"].Profiles.Add("profile-one");
            settings.ProfileDictionary["profile-one"] = new List<string> { "profile-one-id" };
            settings.ActiveProfile = "profile-one";

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { profileTwoMapping }, "profile-two"));
            Assert.AreEqual(2, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(profileOneMapping, settings.ShortcutSettingsDictionary["profile-one-id"].Shortcut);
            Assert.IsFalse(settings.ShortcutSettingsDictionary["profile-one-id"].IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, settings.ShortcutSettingsDictionary["profile-one-id"].Profiles);

            KeyValuePair<string, ShortcutSettings> activeEntry = settings.ShortcutSettingsDictionary.Single(entry => entry.Value.IsActive);
            Assert.AreSame(profileTwoMapping, activeEntry.Value.Shortcut);
            CollectionAssert.AreEqual(new List<string> { "profile-two" }, activeEntry.Value.Profiles);
            CollectionAssert.AreEqual(new List<string> { "profile-one-id" }, settings.ProfileDictionary["profile-one"]);
            CollectionAssert.AreEqual(new List<string> { activeEntry.Key }, settings.ProfileDictionary["profile-two"]);
            Assert.AreEqual("profile-two", settings.ActiveProfile);
        }

        [TestMethod]
        public void ReconcileMappings_ShouldSplitSharedMetadataBeforeChangingOneProfile()
        {
            var sharedMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;66");
            var profileTwoMapping = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;67");
            var settings = CreateEditorSettings(("shared-id", sharedMapping, true));
            settings.ShortcutSettingsDictionary["shared-id"].Profiles.AddRange(new List<string> { "profile-one", "profile-two" });
            settings.ProfileDictionary["profile-one"] = new List<string> { "shared-id" };
            settings.ProfileDictionary["profile-two"] = new List<string> { "shared-id" };
            settings.ActiveProfile = "profile-two";

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { profileTwoMapping }, "profile-two"));
            Assert.AreEqual(2, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(sharedMapping, settings.ShortcutSettingsDictionary["shared-id"].Shortcut);
            Assert.IsFalse(settings.ShortcutSettingsDictionary["shared-id"].IsActive);
            CollectionAssert.AreEqual(new List<string> { "profile-one" }, settings.ShortcutSettingsDictionary["shared-id"].Profiles);

            KeyValuePair<string, ShortcutSettings> activeEntry = settings.ShortcutSettingsDictionary.Single(entry => entry.Value.IsActive);
            Assert.AreSame(profileTwoMapping, activeEntry.Value.Shortcut);
            CollectionAssert.AreEqual(new List<string> { "profile-two" }, activeEntry.Value.Profiles);
            CollectionAssert.AreEqual(new List<string> { "shared-id" }, settings.ProfileDictionary["profile-one"]);
            CollectionAssert.AreEqual(new List<string> { activeEntry.Key }, settings.ProfileDictionary["profile-two"]);
        }

        [TestMethod]
        public void MappingCollectionsEqual_ShouldRejectDelimiterCollision()
        {
            var first = new List<KeyToTextMapping>
            {
                new KeyToTextMapping { OriginalKey = 65, TargetText = "x\u001e66|y" },
            };
            var second = new List<KeyToTextMapping>
            {
                new KeyToTextMapping { OriginalKey = 65, TargetText = "x" },
                new KeyToTextMapping { OriginalKey = 66, TargetText = "y" },
            };

            Assert.IsFalse(KeyboardMappingService.MappingCollectionsEqual(
                new List<KeyMapping>(),
                new List<KeyMapping>(),
                first,
                second,
                new List<ShortcutKeyMapping>(),
                new List<ShortcutKeyMapping>()));
        }

        [TestMethod]
        public void ReconcileMappings_ShouldRepairNullGlobalScopeInPlace()
        {
            var metadata = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;66");
            metadata.TargetApp = null;
            var persisted = CreateMapping(ShortcutOperationType.RemapShortcut, "162;65", "162;67");
            var settings = CreateEditorSettings(("mapping-id", metadata, true));

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { persisted }));
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(persisted, settings.ShortcutSettingsDictionary["mapping-id"].Shortcut);
            Assert.IsTrue(settings.ShortcutSettingsDictionary["mapping-id"].IsActive);
        }

        [TestMethod]
        public void ReconcileMappings_ShouldCanonicalizeProfilelessLegacyMetadataInPlace()
        {
            var legacyMetadata = CreateMapping(ShortcutOperationType.RemapText, "162;65", "hello");
            legacyMetadata.TargetText = "hello";
            var persisted = CreateMapping(ShortcutOperationType.RemapText, "162;65", string.Empty);
            persisted.TargetText = "hello";
            var settings = CreateEditorSettings(("legacy-id", legacyMetadata, false));

            Assert.IsTrue(SettingsManager.ReconcileMappings(settings, new List<ShortcutKeyMapping> { persisted }, "default"));
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(persisted, settings.ShortcutSettingsDictionary["legacy-id"].Shortcut);
            Assert.IsTrue(settings.ShortcutSettingsDictionary["legacy-id"].IsActive);
            CollectionAssert.AreEqual(new List<string> { "default" }, settings.ShortcutSettingsDictionary["legacy-id"].Profiles);
            CollectionAssert.AreEqual(new List<string> { "legacy-id" }, settings.ProfileDictionary["default"]);
        }

        private static EditorSettings CreateEditorSettings(string mappingId, ShortcutOperationType operationType, string originalKeys, string targetKeys) =>
            CreateEditorSettings(mappingId, CreateMapping(operationType, originalKeys, targetKeys));

        private static EditorSettings CreateEditorSettings(string mappingId, ShortcutKeyMapping mapping) =>
            new EditorSettings
            {
                ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>
                {
                    [mappingId] = new ShortcutSettings
                    {
                        Id = mappingId,
                        Shortcut = mapping,
                        IsActive = false,
                    },
                },
                ShortcutsByOperationType = new Dictionary<ShortcutOperationType, List<string>>
                {
                    [mapping.OperationType] = new List<string> { mappingId },
                },
            };

        private static EditorSettings CreateEditorSettings(params (string Id, ShortcutKeyMapping Mapping, bool IsActive)[] mappings)
        {
            var settings = new EditorSettings();
            foreach ((string id, ShortcutKeyMapping mapping, bool isActive) in mappings)
            {
                settings.ShortcutSettingsDictionary[id] = new ShortcutSettings
                {
                    Id = id,
                    Shortcut = mapping,
                    IsActive = isActive,
                };

                if (!settings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out List<string> ids))
                {
                    ids = new List<string>();
                    settings.ShortcutsByOperationType[mapping.OperationType] = ids;
                }

                ids.Add(id);
            }

            return settings;
        }

        private static ShortcutKeyMapping CreateMapping(ShortcutOperationType operationType, string originalKeys, string targetKeys) =>
            new ShortcutKeyMapping
            {
                OperationType = operationType,
                OriginalKeys = originalKeys,
                TargetKeys = targetKeys,
                TargetText = operationType == ShortcutOperationType.RemapText ? targetKeys : string.Empty,
            };
    }
}
