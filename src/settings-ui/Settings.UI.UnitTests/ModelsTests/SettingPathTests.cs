// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions.TestingHelpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingPathTests
    {
        [TestMethod]
        public void GetSettingsPath_WithEmptyPowertoy_ReturnsRootSettingsPath()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);
            string expectedPath = mockFileSystem.Path.Combine(Helper.LocalApplicationDataFolder(), "Microsoft", "PowerToys", "settings.json");

            // Act
            string actualPath = settingPath.GetSettingsPath(string.Empty);

            // Assert
            Assert.AreEqual(expectedPath, actualPath);
        }

        [TestMethod]
        public void GetSettingsPath_WithPowertoyModule_ReturnsModuleSettingsPath()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);
            string expectedPath = mockFileSystem.Path.Combine(Helper.LocalApplicationDataFolder(), "Microsoft", "PowerToys", "FancyZones", "settings.json");

            // Act
            string actualPath = settingPath.GetSettingsPath("FancyZones");

            // Assert
            Assert.AreEqual(expectedPath, actualPath);
        }

        [TestMethod]
        public void SettingsFolderExists_WhenFolderDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);

            // Act
            bool exists = settingPath.SettingsFolderExists("FancyZones");

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void SettingsFolderExists_WhenFolderExists_ReturnsTrue()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);
            string folderPath = mockFileSystem.Path.Combine(Helper.LocalApplicationDataFolder(), "Microsoft", "PowerToys", "FancyZones");
            mockFileSystem.AddDirectory(folderPath);

            // Act
            bool exists = settingPath.SettingsFolderExists("FancyZones");

            // Assert
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public void CreateSettingsFolder_CreatesFolderInFileSystem()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);
            string folderPath = mockFileSystem.Path.Combine(Helper.LocalApplicationDataFolder(), "Microsoft", "PowerToys", "FancyZones");

            // Act
            settingPath.CreateSettingsFolder("FancyZones");

            // Assert
            Assert.IsTrue(mockFileSystem.Directory.Exists(folderPath));
        }

        [TestMethod]
        public void DeleteSettings_DeletesFolderInFileSystem()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingPath = new SettingPath(mockFileSystem.Directory, mockFileSystem.Path);
            string folderPath = mockFileSystem.Path.Combine(Helper.LocalApplicationDataFolder(), "Microsoft", "PowerToys", "FancyZones");
            mockFileSystem.AddDirectory(folderPath);

            // Act
            settingPath.DeleteSettings("FancyZones");

            // Assert
            Assert.IsFalse(mockFileSystem.Directory.Exists(folderPath));
        }
    }
}
