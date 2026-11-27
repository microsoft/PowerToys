// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class PowerOcrSettingsTests
    {
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void DefaultCaptureModeRoundTripPreservesValue(int value)
        {
            var settings = new PowerOcrSettings();
            settings.Properties.DefaultCaptureMode = (PowerOcrCaptureMode)value;

            var json = settings.ToJsonString();
            var deserialized = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PowerOcrSettings);

            StringAssert.Contains(json, $"\"DefaultCaptureMode\":{value}", "The mode must be stored as a number.");
            Assert.IsNotNull(deserialized);
            Assert.AreEqual((PowerOcrCaptureMode)value, deserialized.Properties.DefaultCaptureMode);
        }

        [TestMethod]
        public void SettingsWithoutDefaultCaptureModeLoadAsNormal()
        {
            // Shape of a settings.json written before DefaultCaptureMode existed.
            const string json = """{"name":"TextExtractor","version":"1","properties":{"PreferredLanguage":"English"}}""";

            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PowerOcrSettings);

            Assert.IsNotNull(settings);
            Assert.AreEqual(PowerOcrCaptureMode.Normal, settings.Properties.DefaultCaptureMode);
            Assert.AreEqual("English", settings.Properties.PreferredLanguage);
        }

        [TestMethod]
        public void InvalidDefaultCaptureModeIsNormalizedToNormal()
        {
            const string json = """{"name":"TextExtractor","version":"1","properties":{"DefaultCaptureMode":7}}""";
            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PowerOcrSettings);
            Assert.IsNotNull(settings);

            Assert.IsFalse(settings.UpgradeSettingsConfiguration(), "Normalizing must not rewrite the settings file.");
            Assert.AreEqual(PowerOcrCaptureMode.Normal, settings.Properties.DefaultCaptureMode);
        }
    }
}
