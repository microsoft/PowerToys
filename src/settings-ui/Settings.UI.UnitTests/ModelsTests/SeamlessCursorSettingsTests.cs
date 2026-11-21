// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SeamlessCursorSettingsTests
    {
        [TestMethod]
        public void DefaultsMatchModuleIdentity()
        {
            var settings = new SeamlessCursorSettings();

            Assert.AreEqual(SeamlessCursorSettings.ModuleName, settings.Name);
            Assert.AreEqual("1.0", settings.Version);
            Assert.AreEqual(ModuleType.SeamlessCursor, settings.GetModuleType());
            Assert.IsNotNull(settings.Properties);
        }

        [TestMethod]
        public void ModuleIsDisabledByDefault()
        {
            Assert.IsFalse(new EnabledModules().SeamlessCursor);
        }

        [TestMethod]
        public void SerializesStopAtEmptyEdgesOnByDefault()
        {
            using var document = JsonDocument.Parse(new SeamlessCursorSettings().ToJsonString());
            var root = document.RootElement;

            Assert.AreEqual("SeamlessCursor", root.GetProperty("name").GetString());
            Assert.AreEqual("1.0", root.GetProperty("version").GetString());
            Assert.IsTrue(root.GetProperty("properties").GetProperty("stop_at_empty_edges").GetProperty("value").GetBoolean());
            Assert.IsTrue(root.GetProperty("properties").GetProperty("map_cursor_wrap").GetProperty("value").GetBoolean());
        }

        [TestMethod]
        public void SettingsRoundTripPreservesIdentity()
        {
            var deserialized = JsonSerializer.Deserialize<SeamlessCursorSettings>(new SeamlessCursorSettings().ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(SeamlessCursorSettings.ModuleName, deserialized.Name);
            Assert.AreEqual("1.0", deserialized.Version);
            Assert.IsNotNull(deserialized.Properties);
            Assert.IsFalse(deserialized.UpgradeSettingsConfiguration());
        }

        [TestMethod]
        public void UpgradeRestoresMissingStopAtEmptyEdges()
        {
            var settings = JsonSerializer.Deserialize<SeamlessCursorSettings>("{\"name\":\"SeamlessCursor\",\"version\":\"1.0\",\"properties\":{\"stop_at_empty_edges\":null}}");

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.UpgradeSettingsConfiguration());
            Assert.IsTrue(settings.Properties.StopAtEmptyEdges.Value);
        }

        [TestMethod]
        public void UpgradeRestoresMissingMapCursorWrap()
        {
            var settings = JsonSerializer.Deserialize<SeamlessCursorSettings>("{\"name\":\"SeamlessCursor\",\"version\":\"1.0\",\"properties\":{\"stop_at_empty_edges\":{\"value\":false},\"map_cursor_wrap\":null}}");

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.UpgradeSettingsConfiguration());
            Assert.IsTrue(settings.Properties.MapCursorWrap.Value);
            Assert.IsFalse(settings.Properties.StopAtEmptyEdges.Value);
        }

        [TestMethod]
        public void SettingsWithoutMapCursorWrapDefaultToOn()
        {
            var settings = JsonSerializer.Deserialize<SeamlessCursorSettings>("{\"name\":\"SeamlessCursor\",\"version\":\"1.0\",\"properties\":{\"stop_at_empty_edges\":{\"value\":true}}}");

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.Properties.MapCursorWrap.Value);
        }

        [TestMethod]
        public void UpgradeRestoresMissingProperties()
        {
            var settings = JsonSerializer.Deserialize<SeamlessCursorSettings>("{\"name\":\"SeamlessCursor\",\"version\":\"1.0\",\"properties\":null}");

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.UpgradeSettingsConfiguration());
            Assert.IsNotNull(settings.Properties);
        }
    }
}
