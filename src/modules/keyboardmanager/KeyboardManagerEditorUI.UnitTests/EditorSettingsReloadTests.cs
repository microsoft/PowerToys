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
    [TestClass]
    [DoNotParallelize]
    public class EditorSettingsReloadTests
    {
        [TestMethod]
        public void DiffersFromLoadedSettings_ShouldDetectRestoredMappingWithDifferentId()
        {
            const string loadedId = "00000000-0000-0000-0000-000000000001";
            const string restoredId = "00000000-0000-0000-0000-000000000002";
            EditorSettings originalSettings = SettingsManager.EditorSettings;
            try
            {
                var settings = CreateEditorSettings(loadedId);
                SettingsManager.NormalizeSettings(settings);
                SettingsManager.EditorSettings = settings;
                string restoredJson = JsonSerializer.Serialize(settings).Replace(loadedId, restoredId, StringComparison.Ordinal);

                Assert.IsTrue(SettingsManager.DiffersFromLoadedSettings(restoredJson));
            }
            finally
            {
                SettingsManager.EditorSettings = originalSettings;
            }
        }

        [TestMethod]
        public void DiffersFromLoadedSettings_ShouldIgnoreIdenticalAndEquivalentUnnormalizedJson()
        {
            EditorSettings originalSettings = SettingsManager.EditorSettings;
            try
            {
                var settings = CreateEditorSettings("00000000-0000-0000-0000-000000000001");
                string unnormalizedJson = JsonSerializer.Serialize(settings);
                SettingsManager.NormalizeSettings(settings);
                SettingsManager.EditorSettings = settings;

                Assert.IsFalse(SettingsManager.DiffersFromLoadedSettings(JsonSerializer.Serialize(settings)));
                Assert.IsFalse(SettingsManager.DiffersFromLoadedSettings(unnormalizedJson));
            }
            finally
            {
                SettingsManager.EditorSettings = originalSettings;
            }
        }

        // The editor's own edit re-appends the edited id to its operation index, leaving index order that
        // normalization rewrites; that must not look like an external change and force a reconcile.
        [TestMethod]
        public void DiffersFromLoadedSettings_ShouldIgnoreTheEditorsOwnEdit()
        {
            const string firstId = "00000000-0000-0000-0000-000000000001";
            const string secondId = "00000000-0000-0000-0000-000000000002";
            EditorSettings originalSettings = SettingsManager.EditorSettings;
            try
            {
                var settings = CreateEditorSettings(firstId);
                settings.ShortcutSettingsDictionary[secondId] = new ShortcutSettings
                {
                    Shortcut = new ShortcutKeyMapping { OperationType = ShortcutOperationType.RemapShortcut, OriginalKeys = "114", TargetKeys = "115" },
                };
                SettingsManager.NormalizeSettings(settings);
                var edited = new ShortcutKeyMapping { OperationType = ShortcutOperationType.RemapShortcut, OriginalKeys = "112", TargetKeys = "51" };
                Assert.IsTrue(SettingsManager.TryApplyShortcutKeyMapping(settings, edited, firstId));
                SettingsManager.EditorSettings = settings;

                Assert.IsFalse(SettingsManager.DiffersFromLoadedSettings(JsonSerializer.Serialize(settings)));
            }
            finally
            {
                SettingsManager.EditorSettings = originalSettings;
            }
        }

        private static EditorSettings CreateEditorSettings(string mappingId) =>
            new EditorSettings
            {
                ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>
                {
                    [mappingId] = new ShortcutSettings
                    {
                        Shortcut = new ShortcutKeyMapping
                        {
                            OperationType = ShortcutOperationType.RemapShortcut,
                            OriginalKeys = "112",
                            TargetKeys = "113",
                        },
                    },
                },
            };
    }
}
