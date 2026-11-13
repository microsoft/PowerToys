// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Helpers;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class WindowPlacementStorageTests
    {
        [TestMethod]
        [TestCategory("Window.Persistence")]
        public void RoundTrip_PreservesOpaquePlacementAsAString()
        {
            const string placementKey = "WindowPersistance_WorkspacesEditor";
            byte[] placement = [1, 0, 0, 0, 0, 255, 128, 64, 32, 16];
            var original = new Dictionary<string, object>
            {
                [placementKey] = Convert.ToBase64String(placement),
            };

            var saved = WindowPlacementStorage.Serialize(original);
            var reloaded = WindowPlacementStorage.Deserialize(saved);

            Assert.IsInstanceOfType<string>(reloaded[placementKey]);
            CollectionAssert.AreEqual(placement, Convert.FromBase64String((string)reloaded[placementKey]));
        }

        [TestMethod]
        [TestCategory("Window.Persistence")]
        public void RoundTrip_PreservesEscapedKeysAndValues()
        {
            const string key = "Window\\\"\n工作区";
            const string value = "AQIDBA==\\\"\r\n";
            var original = new Dictionary<string, object>
            {
                [key] = value,
            };

            var reloaded = WindowPlacementStorage.Deserialize(WindowPlacementStorage.Serialize(original));

            Assert.AreEqual(value, reloaded[key]);
            Assert.AreEqual(1, reloaded.Count);
        }

        [TestMethod]
        [TestCategory("Window.Persistence")]
        public void Deserialize_InvalidEntriesDoNotDiscardValidPlacement()
        {
            var storage = WindowPlacementStorage.Deserialize("""
                {
                    "WindowPersistance_WorkspacesEditor": "AQIDBA==",
                    "number": 12,
                    "object": { "value": "AQIDBA==" },
                    "array": ["AQIDBA=="],
                    "null": null
                }
                """);

            Assert.AreEqual(1, storage.Count);
            Assert.AreEqual("AQIDBA==", storage["WindowPersistance_WorkspacesEditor"]);
        }

        [TestMethod]
        [TestCategory("Window.Persistence")]
        [DataRow("{")]
        [DataRow("[]")]
        [DataRow("null")]
        [DataRow("\"AQIDBA==\"")]
        public void Deserialize_InvalidStorageIsReportedToCaller(string json)
        {
            Assert.Throws<JsonException>(() => WindowPlacementStorage.Deserialize(json));
        }

        [TestMethod]
        [TestCategory("Window.Persistence")]
        public void Serialize_WritesOnlyPlacementStrings()
        {
            using var document = JsonDocument.Parse("\"AQIDBA==\"");
            var storage = new Dictionary<string, object>
            {
                ["WindowPersistance_WorkspacesEditor"] = "AQIDBA==",
                ["number"] = 12,
                ["jsonElement"] = document.RootElement,
            };

            var reloaded = WindowPlacementStorage.Deserialize(WindowPlacementStorage.Serialize(storage));

            Assert.AreEqual(1, reloaded.Count);
            Assert.AreEqual("AQIDBA==", reloaded["WindowPersistance_WorkspacesEditor"]);
        }
    }
}
