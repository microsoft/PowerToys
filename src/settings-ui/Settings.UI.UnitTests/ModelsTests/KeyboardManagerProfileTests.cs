// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class KeyboardManagerProfileTests
    {
        [TestMethod]
        public void RoundTrip_WithUnicodeTextReplacement_ShouldUseNativeSchema()
        {
            var profile = new KeyboardManagerProfile();
            profile.TextReplacements.InProcessTextReplacements.Add(new TextReplacementDataModel
            {
                Trigger = "café",
                NewRemapString = "café 🌟",
                TriggerKey = 0x0D,
            });

            var json = profile.ToJsonString();
            using var document = JsonDocument.Parse(json);
            var serializedReplacement = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess")[0];

            Assert.AreEqual("café", serializedReplacement.GetProperty("trigger").GetString());
            Assert.AreEqual("café 🌟", serializedReplacement.GetProperty("unicodeText").GetString());
            Assert.AreEqual(0x0D, serializedReplacement.GetProperty("triggerKey").GetInt32());

            var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(1, roundTripped.TextReplacements.InProcessTextReplacements.Count);
            Assert.AreEqual("café", roundTripped.TextReplacements.InProcessTextReplacements[0].Trigger);
            Assert.AreEqual("café 🌟", roundTripped.TextReplacements.InProcessTextReplacements[0].NewRemapString);
            Assert.AreEqual(0x0D, roundTripped.TextReplacements.InProcessTextReplacements[0].TriggerKey);
        }

        [TestMethod]
        public void Deserialize_LegacyProfileWithoutTextReplacements_ShouldUseEmptyCollection()
        {
            const string LegacyProfile = "{\"remapKeys\":{\"inProcess\":[]},\"remapKeysToText\":{\"inProcess\":[]},\"remapShortcuts\":{\"global\":[],\"appSpecific\":[]},\"remapShortcutsToText\":{\"global\":[],\"appSpecific\":[]}}";

            var profile = JsonSerializer.Deserialize(LegacyProfile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            Assert.IsNotNull(profile.TextReplacements);
            Assert.AreEqual(0, profile.TextReplacements.InProcessTextReplacements.Count);
        }

        [TestMethod]
        public void RoundTrip_LegacyTextReplacementWithoutTriggerKey_ShouldDefaultToSpaceAndStripOneTrailingSpace()
        {
            const string LegacyProfile = "{\"textReplacements\":{\"inProcess\":[{\"trigger\":\"brb \",\"unicodeText\":\"be right back \"}]}}";

            var profile = JsonSerializer.Deserialize(LegacyProfile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            Assert.AreEqual(1, profile.TextReplacements.InProcessTextReplacements.Count);
            TextReplacementDataModel replacement = profile.TextReplacements.InProcessTextReplacements[0];
            Assert.AreEqual("brb", replacement.Trigger);
            Assert.AreEqual("be right back ", replacement.NewRemapString);
            Assert.AreEqual(0x20, replacement.TriggerKey);

            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement serializedReplacement = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess")[0];
            Assert.AreEqual("brb", serializedReplacement.GetProperty("trigger").GetString());
            Assert.AreEqual(0x20, serializedReplacement.GetProperty("triggerKey").GetInt32());
        }

        [TestMethod]
        public void Deserialize_TextReplacementWithExplicitTriggerKey_ShouldNotStripTrailingSpace()
        {
            const string Profile = "{\"textReplacements\":{\"inProcess\":[{\"trigger\":\"brb \",\"unicodeText\":\"be right back\",\"triggerKey\":32}]}}";

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            Assert.AreEqual("brb ", profile.TextReplacements.InProcessTextReplacements[0].Trigger);
            Assert.AreEqual(0x20, profile.TextReplacements.InProcessTextReplacements[0].TriggerKey);
        }
    }
}
