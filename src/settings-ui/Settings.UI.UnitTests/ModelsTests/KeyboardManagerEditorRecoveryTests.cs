// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class KeyboardManagerEditorRecoveryTests
    {
        private static readonly string[] ExpectedMergedProfiles = ["legacy", "current"];
        private static readonly string[] ExpectedLegacyProfileMappings = ["before", "canonical", "middle", "after"];
        private static readonly string[] ExpectedCurrentProfileMappings = ["canonical"];

        [TestMethod]
        public void RecoveryMetadataValidation_RejectsEmptyAndInconsistentCaches()
        {
            Assert.IsFalse(EditorSettingsRecoveryValidator.IsValid(new EditorSettings()));

            EditorSettings settings = CreateSettings();
            AddCachedReplacement(settings, "cached", "brb", 0x20, "target", true);
            Assert.IsTrue(EditorSettingsRecoveryValidator.IsValid(settings));

            settings.ShortcutsByOperationType[ShortcutOperationType.RemapText].Add("missing");
            Assert.IsFalse(EditorSettingsRecoveryValidator.IsValid(settings));
        }

        [TestMethod]
        public void Reconcile_NativeUpdateBeforeEditorCacheWrite_UpdatesExistingTriggerIdentity()
        {
            EditorSettings settings = CreateSettings();
            ShortcutSettings cached = AddCachedReplacement(settings, "cached", "brb", 0x20, "old value", true);

            bool changed = TextReplacementMappingReconciler.Reconcile(
                settings,
                new[] { new TextReplacement { Trigger = "brb", TriggerKey = 0x09, TargetText = "new value" } });

            Assert.IsTrue(changed);
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(cached, settings.ShortcutSettingsDictionary["cached"]);
            Assert.AreEqual(0x09, cached.Shortcut.TriggerKey);
            Assert.AreEqual("new value", cached.Shortcut.TargetText);
            Assert.AreEqual("new value", cached.Shortcut.TargetKeys);
            Assert.IsTrue(cached.IsActive);
        }

        [TestMethod]
        public void Reconcile_NativeAddAndDeleteBeforeEditorCacheWrite_RecoversBothSides()
        {
            EditorSettings settings = CreateSettings();
            ShortcutSettings deleted = AddCachedReplacement(settings, "deleted", "old", 0x20, "old target", true);

            bool changed = TextReplacementMappingReconciler.Reconcile(
                settings,
                new[] { new TextReplacement { Trigger = "new", TriggerKey = 0x0D, TargetText = "new target" } });

            Assert.IsTrue(changed);
            Assert.IsFalse(deleted.IsActive);
            ShortcutSettings added = settings.ShortcutSettingsDictionary.Values.Single(mapping => mapping.Shortcut.TriggerText == "new");
            Assert.IsTrue(added.IsActive);
            Assert.AreEqual(0x0D, added.Shortcut.TriggerKey);
            Assert.AreEqual("new target", added.Shortcut.TargetText);
        }

        [TestMethod]
        public void Reconcile_OldDuplicateRows_MergesProfilesIntoNativeCanonicalRow()
        {
            EditorSettings settings = CreateSettings();
            ShortcutSettings stale = AddCachedReplacement(settings, "stale", "brb", 0x20, "old value", true);
            stale.Profiles.Add("legacy");
            ShortcutSettings canonical = AddCachedReplacement(settings, "canonical", "brb", 0x09, "new value", false);
            canonical.Profiles.Add("current");
            settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = new List<string>
            {
                "before",
                "stale",
                "middle",
                "canonical",
                "canonical",
                "after",
            };
            settings.ProfileDictionary["legacy"] = new List<string> { "before", "stale", "middle", "canonical", "canonical", "after" };
            settings.ProfileDictionary["current"] = new List<string> { "canonical", "canonical" };

            bool changed = TextReplacementMappingReconciler.Reconcile(
                settings,
                new[] { new TextReplacement { Trigger = "brb", TriggerKey = 0x09, TargetText = "new value" } });

            Assert.IsTrue(changed);
            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(canonical, settings.ShortcutSettingsDictionary["canonical"]);
            Assert.IsTrue(canonical.IsActive);
            CollectionAssert.AreEquivalent(ExpectedMergedProfiles, canonical.Profiles);
            CollectionAssert.AreEqual(ExpectedLegacyProfileMappings, settings.ProfileDictionary["legacy"]);
            CollectionAssert.AreEqual(ExpectedCurrentProfileMappings, settings.ProfileDictionary["current"]);
            CollectionAssert.AreEqual(
                ExpectedLegacyProfileMappings,
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapText]);
        }

        [TestMethod]
        public void Reconcile_WithoutExactNativeRow_PrefersActiveThenOperationOrder()
        {
            EditorSettings settings = CreateSettings();
            AddCachedReplacement(settings, "inactive", "brb", 0x20, "one", false);
            ShortcutSettings expected = AddCachedReplacement(settings, "active-first", "brb", 0x09, "two", true);
            AddCachedReplacement(settings, "active-second", "brb", 0x0D, "three", true);
            settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = new List<string>
            {
                "inactive",
                "active-first",
                "active-second",
            };

            TextReplacementMappingReconciler.Reconcile(
                settings,
                new[] { new TextReplacement { Trigger = "brb", TriggerKey = 0x20, TargetText = "authoritative" } });

            Assert.AreEqual(1, settings.ShortcutSettingsDictionary.Count);
            Assert.AreSame(expected, settings.ShortcutSettingsDictionary["active-first"]);
            Assert.AreEqual("authoritative", expected.Shortcut.TargetText);
        }

        private static EditorSettings CreateSettings()
        {
            var settings = new EditorSettings();
            settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = new List<string>();
            return settings;
        }

        private static ShortcutSettings AddCachedReplacement(
            EditorSettings settings,
            string id,
            string trigger,
            int triggerKey,
            string target,
            bool isActive)
        {
            var shortcutSettings = new ShortcutSettings
            {
                Id = id,
                IsActive = isActive,
                Shortcut = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    TriggerText = trigger,
                    TriggerKey = triggerKey,
                    TargetKeys = target,
                    TargetText = target,
                },
            };
            settings.ShortcutSettingsDictionary[id] = shortcutSettings;
            settings.ShortcutsByOperationType[ShortcutOperationType.RemapText].Add(id);
            return shortcutSettings;
        }
    }
}
