// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
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
    }
}
