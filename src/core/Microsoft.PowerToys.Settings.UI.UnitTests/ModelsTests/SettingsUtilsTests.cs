// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsUtilsTests
    {

        /// <summary>
        /// This method mocks an IO provider to validate tests wich required saving to a file, and then reading the contents of that file, or verifying it exists
        /// </summary>
        /// <returns></returns>
        Mock<IIOProvider> GetMockIOProviderForSaveLoadExists( )
        {
            string savePath = string.Empty;
            string saveContent = string.Empty;
            var mockIOProvider = new Mock<IIOProvider>();
            mockIOProvider.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                          .Callback<string, string>((path, content) =>
                          {
                              savePath = path;
                              saveContent = content;
                          });
            mockIOProvider.Setup(x => x.ReadAllText(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(() => saveContent);

            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(true);
            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => !x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(false);

            return mockIOProvider;
        }

        [TestMethod]
        public void SaveSettings_SaveSettingsToFile_WhenFilePathExists()
        {
            // Arrange
            var mockIOProvider = GetMockIOProviderForSaveLoadExists();
            var settingsUtils = new SettingsUtils(mockIOProvider.Object);
            string file_name = "\\test";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            // Act
            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettings<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SaveSettings_ShouldCreateFile_WhenFilePathIsNotFound()
        {
            // Arrange
            var mockIOProvider = GetMockIOProviderForSaveLoadExists();
            var settingsUtils = new SettingsUtils(mockIOProvider.Object);
            string file_name = "test\\Test Folder";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettings<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SettingsFolderExists_ShouldReturnFalse_WhenFilePathIsNotFound()
        {
            // Arrange
            var mockIOProvider = GetMockIOProviderForSaveLoadExists();
            var settingsUtils = new SettingsUtils(mockIOProvider.Object);
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

        public static string RandomString()
        {
            Random random = new Random();
            int length = 20;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
