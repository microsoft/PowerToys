// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class GrabAndMoveSettingsTests
    {
        [TestMethod]
        public void Defaults_ShouldNotBringToFrontOnGrab()
        {
            var settings = new GrabAndMoveSettings();

            Assert.AreEqual(GrabAndMoveSettings.ModuleName, settings.Name);
            Assert.IsNotNull(settings.Properties);
            Assert.IsFalse(settings.Properties.BringToFrontOnGrab.Value);
        }

        [TestMethod]
        public void ToJsonString_ShouldContainBringToFrontOnGrabKey()
        {
            var settings = new GrabAndMoveSettings();

            var json = settings.ToJsonString();

            StringAssert.Contains(json, "\"bringToFrontOnGrab\"");
        }

        [TestMethod]
        public void RoundTrip_ShouldPreserveBringToFrontOnGrab()
        {
            var original = new GrabAndMoveSettings();
            original.Properties.BringToFrontOnGrab.Value = true;

            var deserialized = JsonSerializer.Deserialize<GrabAndMoveSettings>(original.ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.Properties.BringToFrontOnGrab.Value);
        }

        [TestMethod]
        public void Deserialize_WithoutBringToFrontOnGrab_ShouldUseDefault()
        {
            // Settings files written before the key existed must still load.
            const string json = "{\"name\":\"GrabAndMove\",\"version\":\"1.0\",\"properties\":{\"showGeometry\":{\"value\":true}}}";

            var deserialized = JsonSerializer.Deserialize<GrabAndMoveSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.Properties.ShowGeometry.Value);
            Assert.IsFalse(deserialized.Properties.BringToFrontOnGrab.Value);
        }
    }
}
