// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library.Attributes;

namespace CommonLibTest
{
    [TestClass]
    public class MouseWithoutBordersSettingsTests
    {
        [TestMethod]
        public void NonConsoleSessionsDefaultToFalseWithTypedJsonShape()
        {
            var settings = new MouseWithoutBordersSettings();

            Assert.IsFalse(settings.Properties.AllowNonConsoleSessions.Value);

            using var document = JsonDocument.Parse(settings.ToJsonString());
            var property = document.RootElement.GetProperty("properties").GetProperty("AllowNonConsoleSessions");

            Assert.AreEqual(JsonValueKind.Object, property.ValueKind);
            Assert.IsFalse(property.GetProperty("value").GetBoolean());
            Assert.AreEqual("1.1", settings.Version);
        }

        [TestMethod]
        [DataRow("{}")]
        [DataRow("{\"properties\":{}}")]
        [DataRow("{\"properties\":{\"AllowNonConsoleSessions\":{\"value\":false}}}")]
        public void MissingOrFalseNonConsoleSettingRemainsDisabled(string json)
        {
            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MouseWithoutBordersSettings);

            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.Properties.AllowNonConsoleSessions.Value);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NonConsoleSettingRoundTripSurvivesOrdinarySettingsEdit(bool allowNonConsoleSessions)
        {
            var configuredSettings = new MouseWithoutBordersSettings();
            configuredSettings.Properties.AllowNonConsoleSessions.Value = allowNonConsoleSessions;

            var settings = JsonSerializer.Deserialize(configuredSettings.ToJsonString(), SettingsSerializationContext.Default.MouseWithoutBordersSettings);

            Assert.IsNotNull(settings);
            settings.Properties.WrapMouse = false;

            var deserialized = JsonSerializer.Deserialize(settings.ToJsonString(), SettingsSerializationContext.Default.MouseWithoutBordersSettings);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(allowNonConsoleSessions, deserialized.Properties.AllowNonConsoleSessions.Value);
            Assert.IsFalse(deserialized.Properties.WrapMouse);
            Assert.AreEqual("1.1", deserialized.Version);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ClonedPropertiesSaveRetainsNonConsoleSetting(bool allowNonConsoleSessions)
        {
            var settings = new MouseWithoutBordersSettings();
            settings.Properties.AllowNonConsoleSessions.Value = allowNonConsoleSessions;

            var clonedSettings = new MouseWithoutBordersSettings
            {
                Properties = (MouseWithoutBordersProperties)settings.Properties.Clone(),
            };
            var settingsUtils = new SettingsUtils(new MockFileSystem());

            clonedSettings.Save(settingsUtils);

            var deserialized = settingsUtils.GetSettings<MouseWithoutBordersSettings>(MouseWithoutBordersSettings.ModuleName);

            Assert.AreEqual(allowNonConsoleSessions, deserialized.Properties.AllowNonConsoleSessions.Value);
        }

        [TestMethod]
        public void NonConsoleSettingIsExcludedFromCommandLineConfiguration()
        {
            var property = typeof(MouseWithoutBordersProperties).GetProperty(nameof(MouseWithoutBordersProperties.AllowNonConsoleSessions));

            Assert.IsNotNull(property);
            Assert.IsTrue(property.IsDefined(typeof(CmdConfigureIgnoreAttribute), inherit: false));
        }

        [TestMethod]
        [DataRow("{\"properties\":{\"AllowNonConsoleSessions\":true}}")]
        [DataRow("{\"properties\":{\"AllowNonConsoleSessions\":{\"value\":\"true\"}}}")]
        [DataRow("{\"properties\":{\"AllowNonConsoleSessions\":{\"value\":1}}}")]
        public void MalformedNonConsoleSettingIsNotSilentlyCoerced(string json)
        {
            Assert.ThrowsException<JsonException>(() => JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MouseWithoutBordersSettings));
        }
    }
}
