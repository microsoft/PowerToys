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
        public void RoundTrip_PreservesSingleKeyRemapCondition()
        {
            const string Profile = """
                {
                  "remapKeys": {
                    "inProcess": [
                      {
                        "originalKeys": "65",
                        "newRemapKeys": "66",
                        "condition": "alone"
                      }
                    ]
                  }
                }
                """;

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            Assert.AreEqual("alone", profile.RemapKeys.InProcessRemapKeys[0].Condition);

            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement mapping = document.RootElement
                .GetProperty("remapKeys")
                .GetProperty("inProcess")[0];
            Assert.AreEqual("alone", mapping.GetProperty("condition").GetString());
        }

        [TestMethod]
        public void RoundTrip_LegacySingleKeyRemapWithoutCondition_PreservesOmission()
        {
            const string Profile = """
                {
                  "remapKeys": {
                    "inProcess": [
                      {
                        "originalKeys": "65",
                        "newRemapKeys": "66"
                      }
                    ]
                  }
                }
                """;

            var profile = JsonSerializer.Deserialize(Profile, SettingsSerializationContext.Default.KeyboardManagerProfile);

            Assert.IsNotNull(profile);
            Assert.IsNull(profile.RemapKeys.InProcessRemapKeys[0].Condition);

            using var document = JsonDocument.Parse(profile.ToJsonString());
            JsonElement mapping = document.RootElement
                .GetProperty("remapKeys")
                .GetProperty("inProcess")[0];
            Assert.IsFalse(mapping.TryGetProperty("condition", out _));
        }
    }
}
