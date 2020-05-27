using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsUtilsTests
    {
        public SettingsUtilsTests()
        {
            string file_name = "\\test";
            if (SettingsUtils.SettingsFolderExists(file_name))
            {
                DeleteFolder(file_name);
            }
        }

        [TestCleanup()]
        public void Cleanup()
        {
            string file_name = "\\test";
            if (SettingsUtils.SettingsFolderExists(file_name))
            {
                DeleteFolder(file_name);
            }
        }

        [TestMethod]
        public void SaveSettings_SaveSettingsToFile_WhenFilePathExists()
        {
            // Arrange
            string file_name = "\\test";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            // Act
            SettingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = SettingsUtils.GetSettings<BasePTSettingsTest>(file_name);

            // Assert
            Assert.IsTrue(actual_json.Equals(actual_json));
        }

        [TestMethod]
        public async Task SaveSettings_ShouldCreateFile_WhenFilePathIsNotFoundAsync()
        {
            // Arrange
            string file_name = "test\\Test Folder";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            // Act
            if(SettingsUtils.SettingsFolderExists(file_name))
            {
                DeleteFolder(file_name);
            }

            SettingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = SettingsUtils.GetSettings<BasePTSettingsTest>(file_name);

            // Assert
            Assert.IsTrue(actual_json.Equals(actual_json));
        }

        [TestMethod]
        public void SettingsFolderExists_ShouldReturnFalse_WhenFilePathIsNotFound()
        {
            // Arrange
            string file_name_random = "test\\"+ RandomString();
            string file_name_exists = "test\\exists";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            // Act
            bool pathNotFound = SettingsUtils.SettingsFolderExists(file_name_random);

            SettingsUtils.SaveSettings(file_contents_correct_json_content, file_name_exists);
            bool pathFound = SettingsUtils.SettingsFolderExists(file_name_exists);

            // Assert
            Assert.IsFalse(pathNotFound);
            Assert.IsTrue(pathFound);
        }

        [TestMethod]
        public void CreateSettingsFolder_ShouldCreateFolder_WhenSuccessful()
        {
            // Arrange
            string file_name = "test\\" + RandomString();

            // Act
            SettingsUtils.CreateSettingsFolder(file_name);

            // Assert
            Assert.IsTrue(SettingsUtils.SettingsFolderExists(file_name));
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
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
