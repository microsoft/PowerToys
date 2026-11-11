// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [OSCondition(OperatingSystems.Windows)]
    [TestClass]
    public class AdvancedPasteSettingsTests
    {
        // The Advanced Paste module interface (AdvancedPasteModuleInterface/dllmain.cpp) assigns hotkey IDs in
        // this order, and the shortcut conflict UI indexes GetAllHotkeyAccessors() with those IDs.
        private static readonly string[] ExpectedHotkeySlots =
        [
            "PasteAsPlainText_Shortcut",
            "AdvancedPasteUI_Shortcut",
            "PasteAsMarkdown_Shortcut",
            "PasteAsJson_Shortcut",
            "ImageToText",
            "FixSpellingAndGrammar",
            "PasteAsTxtFile",
            "PasteAsPngFile",
            "PasteAsJpgFile",
            "PasteAsHtmlFile",
            "TranscodeToMp3",
            "TranscodeToMp4",
        ];

        private static readonly string[] ExpectedPasteAsFileChildren =
        [
            "paste-as-txt-file",
            "paste-as-png-file",
            "paste-as-jpg-file",
            "paste-as-html-file",
        ];

        private const string Shortcut = """{ "win": true, "ctrl": true, "alt": true, "shift": false, "code": 72, "key": "H" }""";

        // Settings written before "Paste as .jpg file" existed have no paste-as-jpg-file entry.
        private const string LegacySettingsJson = $$"""
            {
              "name": "AdvancedPaste",
              "version": "1",
              "properties": {
                "additional-actions": {
                  "image-to-text": { "isShown": true },
                  "fix-spelling-and-grammar": { "isShown": true },
                  "paste-as-file": {
                    "isShown": true,
                    "paste-as-txt-file": { "isShown": true },
                    "paste-as-png-file": { "isShown": true },
                    "paste-as-html-file": { "isShown": true, "shortcut": {{Shortcut}} }
                  },
                  "transcode": {
                    "isShown": true,
                    "transcode-to-mp3": { "isShown": true },
                    "transcode-to-mp4": { "isShown": true }
                  }
                }
              }
            }
            """;

        [TestMethod]
        public void LegacySettingsWithoutJpgKeepHotkeySlotsAligned()
        {
            var settings = JsonSerializer.Deserialize(LegacySettingsJson, SettingsSerializationContext.Default.AdvancedPasteSettings);
            Assert.IsNotNull(settings);

            var accessors = settings.GetAllHotkeyAccessors();

            Assert.AreSequenceEqual(ExpectedHotkeySlots, accessors.Select(accessor => accessor.LocalizationHeaderKey).ToArray());

            // The native module reserves an empty slot for the missing JPEG action, so the HTML shortcut
            // must still be found right after it.
            var jpgSlot = accessors[System.Array.IndexOf(ExpectedHotkeySlots, "PasteAsJpgFile")];
            Assert.AreEqual(0, jpgSlot.Value.Code);
            var htmlSlot = accessors[System.Array.IndexOf(ExpectedHotkeySlots, "PasteAsHtmlFile")];
            Assert.AreEqual(72, htmlSlot.Value.Code);
        }

        [TestMethod]
        public void LegacySettingsWithoutJpgQualityUseDefaultQuality()
        {
            var settings = JsonSerializer.Deserialize(LegacySettingsJson, SettingsSerializationContext.Default.AdvancedPasteSettings);
            Assert.IsNotNull(settings);

            Assert.AreEqual(AdvancedPasteProperties.DefaultPasteAsJpgQuality, settings.Properties.PasteAsJpgQuality.Value);
        }

        [TestMethod]
        public void SerializedSettingsKeepPasteAsFileChildrenInSlotOrder()
        {
            var json = JsonSerializer.Serialize(new AdvancedPasteSettings(), SettingsSerializationContext.Default.AdvancedPasteSettings);

            using var document = JsonDocument.Parse(json);
            var pasteAsFile = document.RootElement.GetProperty("properties").GetProperty("additional-actions").GetProperty("paste-as-file");
            var children = pasteAsFile.EnumerateObject().Select(property => property.Name).Where(name => name != "isShown").ToArray();

            Assert.AreSequenceEqual(ExpectedPasteAsFileChildren, children);
        }
    }
}
