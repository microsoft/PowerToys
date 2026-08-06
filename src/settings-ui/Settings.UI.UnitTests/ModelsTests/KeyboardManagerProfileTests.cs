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
        public void Defaults_ShouldInitializeTextReplacements()
        {
            var profile = new KeyboardManagerProfile();

            Assert.IsNotNull(profile.TextReplacements);
            Assert.IsNotNull(profile.TextReplacements.InProcessTextReplacements);
            Assert.AreEqual(0, profile.TextReplacements.InProcessTextReplacements.Count);
        }

        [TestMethod]
        public void RoundTrip_WithTextReplacements_ShouldPreserveUnicodeValues()
        {
            var profile = new KeyboardManagerProfile();
            profile.TextReplacements.InProcessTextReplacements.Add(new TextReplacementDataModel
            {
                Trigger = "cafe",
                NewRemapString = "café 🌟",
            });

            var json = profile.ToJsonString();
            var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(1, roundTripped.TextReplacements.InProcessTextReplacements.Count);
            Assert.AreEqual("cafe", roundTripped.TextReplacements.InProcessTextReplacements[0].Trigger);
            Assert.AreEqual("café 🌟", roundTripped.TextReplacements.InProcessTextReplacements[0].NewRemapString);
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
        public void ToJsonString_ShouldUseNativeSchemaNames()
        {
            var profile = new KeyboardManagerProfile();
            profile.TextReplacements.InProcessTextReplacements.Add(new TextReplacementDataModel
            {
                Trigger = "sun",
                NewRemapString = "moon",
            });

            var json = profile.ToJsonString();

            StringAssert.Contains(json, "\"textReplacements\"");
            StringAssert.Contains(json, "\"inProcess\"");
            StringAssert.Contains(json, "\"trigger\":\"sun\"");
            StringAssert.Contains(json, "\"unicodeText\":\"moon\"");
        }
    }
}
