// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class TextExpansionProfileTests
    {
        private static readonly string[] ExpectedPropertyNames = ["id", "sourceText", "activationKeys", "replacementText", "enabled"];
        private static readonly uint[] ExpectedActivationKeys = [17, 32];

        [TestMethod]
        public void RoundTrip_UsesCanonicalTextExpansionSchema()
        {
            const string Profile = """
                {
                  "textReplacements": {
                    "inProcess": [
                      {
                        "id": "2d6e35c0-7344-47ad-b1ba-0348f41fa21f",
                        "sourceText": "brb",
                        "activationKeys": [17, 32],
                        "replacementText": "be right back",
                        "enabled": true
                      }
                    ]
                  }
                }
                """;

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement mapping = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess")[0];

            CollectionAssert.AreEquivalent(
                ExpectedPropertyNames,
                mapping.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.AreEqual("2d6e35c0-7344-47ad-b1ba-0348f41fa21f", mapping.GetProperty("id").GetString());
            Assert.AreEqual("brb", mapping.GetProperty("sourceText").GetString());
            CollectionAssert.AreEqual(
                ExpectedActivationKeys,
                mapping.GetProperty("activationKeys").EnumerateArray().Select(key => key.GetUInt32()).ToArray());
            Assert.AreEqual("be right back", mapping.GetProperty("replacementText").GetString());
            Assert.IsTrue(mapping.GetProperty("enabled").GetBoolean());
        }

        [TestMethod]
        public void Deserialize_ProfileWithoutTextReplacements_UsesEmptyCollection()
        {
            const string Profile = """
                {
                  "remapKeys": { "inProcess": [] },
                  "remapKeysToText": { "inProcess": [] },
                  "remapShortcuts": { "global": [], "appSpecific": [] },
                  "remapShortcutsToText": { "global": [], "appSpecific": [] }
                }
                """;

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement mappings = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess");
            Assert.AreEqual(0, mappings.GetArrayLength());
        }

        [TestMethod]
        public void RoundTrip_PreservesDuplicateTriggersAndProfileOrder()
        {
            const string Profile = """
                {
                  "textReplacements": {
                    "inProcess": [
                      {
                        "id": "11111111-1111-4111-8111-111111111111",
                        "sourceText": "brb",
                        "activationKeys": [32],
                        "replacementText": "first",
                        "enabled": true
                      },
                      {
                        "id": "22222222-2222-4222-8222-222222222222",
                        "sourceText": "brb",
                        "activationKeys": [32],
                        "replacementText": "second",
                        "enabled": false
                      }
                    ]
                  }
                }
                """;

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement mappings = document.RootElement
                .GetProperty("textReplacements")
                .GetProperty("inProcess");

            Assert.AreEqual(2, mappings.GetArrayLength());
            Assert.AreEqual("11111111-1111-4111-8111-111111111111", mappings[0].GetProperty("id").GetString());
            Assert.AreEqual("first", mappings[0].GetProperty("replacementText").GetString());
            Assert.IsTrue(mappings[0].GetProperty("enabled").GetBoolean());
            Assert.AreEqual("22222222-2222-4222-8222-222222222222", mappings[1].GetProperty("id").GetString());
            Assert.AreEqual("second", mappings[1].GetProperty("replacementText").GetString());
            Assert.IsFalse(mappings[1].GetProperty("enabled").GetBoolean());
        }
    }
}
