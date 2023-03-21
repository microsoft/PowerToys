// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsUtilsTests
    {
        [TestMethod]
        public void SaveSettingsSaveSettingsToFileWhenFilePathExists()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);

            string file_name = "\\test";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            // Act
            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SaveSettingsShouldCreateFileWhenFilePathIsNotFound()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);
            string file_name = "test\\Test Folder";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SettingsFolderExistsShouldReturnFalseWhenFilePathIsNotFound()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);
            string file_name_random = "test\\" + RandomString();
            string file_name_exists = "test\\exists";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            // Act
            bool pathNotFound = settingsUtils.SettingsExists(file_name_random);

            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name_exists);
            bool pathFound = settingsUtils.SettingsExists(file_name_exists);

            // Assert
            Assert.IsFalse(pathNotFound);
            Assert.IsTrue(pathFound);
        }

        [TestMethod]
        public void SettingsUtilsMustReturnDefaultItemWhenFileIsCorrupt()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(mockFileSystem);

            // Act
            TestClass settings = mockSettingsUtils.GetSettingsOrDefault<TestClass>(string.Empty);

            // Assert
            Assert.AreEqual(settings.TestInt, 100);
            Assert.AreEqual(settings.TestString, "test");
        }

        public static string RandomString()
        {
            Random random = new Random();
            int length = 20;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private sealed partial class TestClass : ISettingsConfig
        {
            public int TestInt { get; set; } = 100;

            public string TestString { get; set; } = "test";

            public string GetModuleName()
            {
                throw new NotImplementedException();
            }

            public string ToJsonString()
            {
                return JsonSerializer.Serialize(this);
            }

            public bool UpgradeSettingsConfiguration()
            {
                throw new NotImplementedException();
            }
        }
    }
}
