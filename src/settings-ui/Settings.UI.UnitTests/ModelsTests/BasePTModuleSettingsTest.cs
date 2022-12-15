// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class BasePTModuleSettingsTest
    {
        [TestMethod]
        [ObsoleteAttribute("This test method is obsolete.", true)]
        public void ToJsonStringShouldReturnValidJSONOfModelWhenSuccessful()
        {
            // Mock Disk access
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);

            // Arrange
            string file_name = "test\\BasePTModuleSettingsTest";
            string testSettingsConfigs = new BasePTSettingsTest().ToJsonString();
            settingsUtils.SaveSettings(testSettingsConfigs, file_name);

            // Act
            JsonDocument doc = JsonDocument.Parse(settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name).ToJsonString());
            BasePTSettingsTest settings = doc.Deserialize<BasePTSettingsTest>();

            // Assert
            Assert.IsNotNull(settings.Name);
            Assert.IsNotNull(settings.Version);
        }
    }
}
