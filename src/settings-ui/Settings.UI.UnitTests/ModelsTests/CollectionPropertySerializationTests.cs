// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class CollectionPropertySerializationTests
    {
        [TestMethod]
        public void ImageResizerSizesDeserializesInitOnlyCollection()
        {
            const string json = """
                {
                  "value": [
                    {
                      "Id": 1,
                      "name": "Small"
                    }
                  ]
                }
                """;

            var sizes = JsonSerializer.Deserialize<ImageResizerSizes>(json);

            Assert.IsNotNull(sizes);
            Assert.HasCount(1, sizes.Value);
            Assert.AreEqual(1, sizes.Value[0].Id);
            Assert.AreEqual("Small", sizes.Value[0].Name);
        }

        [TestMethod]
        public void KeyboardManagerModelsDeserializeInitOnlyCollections()
        {
            const string remapKeysJson = """
                {
                  "inProcess": [
                    {
                      "originalKeys": "65",
                      "newRemapKeys": "66"
                    }
                  ]
                }
                """;
            const string shortcutsJson = """
                {
                  "global": [],
                  "appSpecific": []
                }
                """;

            var remapKeys = JsonSerializer.Deserialize<RemapKeysDataModel>(remapKeysJson);
            var shortcuts = JsonSerializer.Deserialize<ShortcutsKeyDataModel>(shortcutsJson);

            Assert.IsNotNull(remapKeys);
            Assert.HasCount(1, remapKeys.InProcessRemapKeys);
            Assert.IsNotNull(shortcuts);
            Assert.IsEmpty(shortcuts.GlobalRemapShortcuts);
            Assert.IsEmpty(shortcuts.AppSpecificRemapShortcuts);
        }

        [TestMethod]
        public void ColorPickerPropertiesDeserializeInitOnlyCollections()
        {
            const string json = """
                {
                  "colorhistory": ["#FFFFFF"],
                  "visiblecolorformats": {
                    "HEX": {
                      "Key": true,
                      "Value": "HEX"
                    }
                  }
                }
                """;

            var properties = JsonSerializer.Deserialize<ColorPickerProperties>(json);

            Assert.IsNotNull(properties);
            Assert.HasCount(1, properties.ColorHistory);
            Assert.AreEqual("#FFFFFF", properties.ColorHistory[0]);
            Assert.HasCount(1, properties.VisibleColorFormats);
            Assert.IsTrue(properties.VisibleColorFormats["HEX"].Key);
        }

        [TestMethod]
        public void ColorPickerVersion1CollectionsDeserializeAndUpgrade()
        {
            const string json = """
                {
                  "properties": {
                    "colorhistory": ["#FFFFFF"],
                    "visiblecolorformats": {
                      "HEX": true,
                      "RGB": false
                    },
                    "copiedcolorrepresentation": 0
                  }
                }
                """;

            var version1 = JsonSerializer.Deserialize<ColorPickerSettingsVersion1>(json);

            Assert.IsNotNull(version1);
            Assert.HasCount(1, version1.Properties.ColorHistory);
            Assert.HasCount(2, version1.Properties.VisibleColorFormats);

            var upgraded = (ColorPickerSettings)ColorPickerSettings.UpgradeSettings(version1);

            Assert.HasCount(2, upgraded.Properties.VisibleColorFormats);
            Assert.IsTrue(upgraded.Properties.VisibleColorFormats["HEX"].Key);
            Assert.IsFalse(upgraded.Properties.VisibleColorFormats["RGB"].Key);
            Assert.AreEqual("HEX", upgraded.Properties.CopiedColorRepresentation);
        }

        [TestMethod]
        public void SettingsCollectionsDeserializeInitOnlyProperties()
        {
            var awake = JsonSerializer.Deserialize<AwakeProperties>("""
                { "customTrayTimes": { "Morning": 30 } }
                """);
            var customActions = JsonSerializer.Deserialize<AdvancedPasteCustomActions>("""
                { "value": [] }
                """);
            var shortcutConflicts = JsonSerializer.Deserialize<ShortcutConflictProperties>("""
                { "ignored_shortcuts": [] }
                """);

            Assert.IsNotNull(awake);
            Assert.AreEqual(30u, awake.CustomTrayTimes["Morning"]);
            Assert.IsNotNull(customActions);
            Assert.IsEmpty(customActions.Value);
            Assert.IsNotNull(shortcutConflicts);
            Assert.IsEmpty(shortcutConflicts.IgnoredShortcuts);
        }

        [TestMethod]
        public void AwakeSettingsMissingCustomTrayTimesKeepsDefaultAndCanClone()
        {
            const string json = """
                {
                  "name": "Awake",
                  "properties": {
                    "mode": 1,
                    "keepDisplayOn": true
                  }
                }
                """;

            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.AwakeSettings);

            Assert.IsNotNull(settings);
            Assert.IsEmpty(settings.Properties.CustomTrayTimes);

            var clone = (AwakeSettings)settings.Clone();
            Assert.IsEmpty(clone.Properties.CustomTrayTimes);
        }

        [TestMethod]
        public void AwakeSettingsNullCustomTrayTimesNormalizesToEmpty()
        {
            const string json = """
                {
                  "name": "Awake",
                  "properties": {
                    "customTrayTimes": null
                  }
                }
                """;

            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.AwakeSettings);

            Assert.IsNotNull(settings);
            Assert.IsEmpty(settings.Properties.CustomTrayTimes);
        }

        [TestMethod]
        public void HotkeyConflictCollectionsDeserializeInitOnlyProperties()
        {
            var allConflicts = JsonSerializer.Deserialize<AllHotkeyConflictsData>("""
                { "InAppConflicts": [], "SystemConflicts": [] }
                """);
            var moduleConflicts = JsonSerializer.Deserialize<ModuleConflictsData>("""
                { "InAppConflicts": [], "SystemConflicts": [] }
                """);
            var group = JsonSerializer.Deserialize<HotkeyConflictGroupData>("""
                { "Modules": [] }
                """);
            var info = JsonSerializer.Deserialize<HotkeyConflictInfo>("""
                { "AllConflictingModules": ["FancyZones:1"] }
                """);

            Assert.IsNotNull(allConflicts);
            Assert.IsEmpty(allConflicts.InAppConflicts);
            Assert.IsEmpty(allConflicts.SystemConflicts);
            Assert.IsNotNull(moduleConflicts);
            Assert.IsEmpty(moduleConflicts.InAppConflicts);
            Assert.IsEmpty(moduleConflicts.SystemConflicts);
            Assert.IsNotNull(group);
            Assert.IsEmpty(group.Modules);
            Assert.IsNotNull(info);
            CollectionAssert.Contains(info.AllConflictingModules, "FancyZones:1");
        }
    }
}
