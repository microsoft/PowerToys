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
            });

            var json = profile.ToJsonString();
            using var document = JsonDocument.Parse(json);
            var serializedReplacement = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess")[0];

            Assert.AreEqual("café", serializedReplacement.GetProperty("trigger").GetString());
            Assert.AreEqual("café 🌟", serializedReplacement.GetProperty("unicodeText").GetString());

            var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(1, roundTripped.TextReplacements.InProcessTextReplacements.Count);
            Assert.AreEqual("café", roundTripped.TextReplacements.InProcessTextReplacements[0].Trigger);
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
    }
}
