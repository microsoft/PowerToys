using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ViewModelTests
{
    [TestClass]
    public class General
    {
        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            GeneralSettings generalSettings = new GeneralSettings();
            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
        }

        [TestCleanup]
        public void CleanUp()
        {
            // delete folder created.
            string generalSettings_file_name = string.Empty;
            if (SettingsUtils.SettingsFolderExists(generalSettings_file_name))
            {
                DeleteFolder(generalSettings_file_name);
            }

            if (SettingsUtils.SettingsFolderExists(string.Empty))
            {
                DeleteFolder(string.Empty);
            }
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }

        [TestMethod]
        public void IsElevated_ShouldUpdateRunasAdminStatusAttrs_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
                        
            Assert.AreEqual(viewModel.RunningAsUserDefaultText, viewModel.RunningAsText);
            Assert.IsFalse(viewModel.IsElevated);
            
            // Act
            viewModel.IsElevated = true;

            // Assert
            Assert.AreEqual(viewModel.RunningAsAdminDefaultText, viewModel.RunningAsText);
            Assert.IsTrue(viewModel.IsElevated);
        }

        [TestMethod]
        public void Startup_ShouldEnableRunOnStartUp_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
            Assert.IsFalse(viewModel.Startup);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Startup);
            };

            // act
            viewModel.Startup = true;
        }

        [TestMethod]
        public void RunElevated_ShouldEnableAlwaysRunElevated_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
            Assert.IsFalse(viewModel.RunElevated);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.RunElevated);
            };

            // act
            viewModel.RunElevated = true;
        }

        [TestMethod]
        public void AutoDownloadUpdates_ShouldEnableAutoDownloadUpdates_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
            Assert.IsFalse(viewModel.AutoDownloadUpdates);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.AutoDownloadUpdates);
            };

            // act
            viewModel.AutoDownloadUpdates = true;
        }

        [TestMethod]
        public void IsLightThemeRadioButtonChecked_ShouldThemeToLight_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
            Assert.IsFalse(viewModel.IsLightThemeRadioButtonChecked);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("light", snd.GeneralSettings.Theme);
            };

            // act
            viewModel.IsLightThemeRadioButtonChecked = true;
        }

        [TestMethod]
        public void IsDarkThemeRadioButtonChecked_ShouldThemeToDark_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();
            Assert.IsFalse(viewModel.IsDarkThemeRadioButtonChecked);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("dark", snd.GeneralSettings.Theme);
            };

            // act
            viewModel.IsDarkThemeRadioButtonChecked = true;
        }
    }
}
