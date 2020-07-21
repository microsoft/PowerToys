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
    public class PowerRename
    {
        public const string ModuleName = "PowerRename";
        public string schemaText = null;

        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            GeneralSettings generalSettings = new GeneralSettings();
            PowerRenameSettings powerRename = new PowerRenameSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
            SettingsUtils.SaveSettings(powerRename.ToJsonString(), powerRename.Name, "power-rename-settings.json");
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

            // delete folder created.
            if (SettingsUtils.SettingsFolderExists(ModuleName))
            {
                DeleteFolder(ModuleName);
            }
        }

        [TestMethod]
        public void IsEnabled_ShouldEnableModule_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.PowerRename);
            };

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void MRUEnabled_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.MRUEnabled.Value);
            };

            // act
            viewModel.MRUEnabled = true;
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            PowerRenameViewModel viewModel = new PowerRenameViewModel();
            ShellPage.DefaultSndMSGCallback = msg => { };

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOnGlobalAndMruShouldBeOff()
        {
            PowerRenameViewModel viewModel = new PowerRenameViewModel();
            ShellPage.DefaultSndMSGCallback = msg => { };

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = true;


            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            PowerRenameViewModel viewModel = new PowerRenameViewModel();
            ShellPage.DefaultSndMSGCallback = msg => { };

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOnGlobalAndMruShouldBeOn()
        {
            PowerRenameViewModel viewModel = new PowerRenameViewModel();
            ShellPage.DefaultSndMSGCallback = msg => { };

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = true;

            Assert.IsTrue(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void EnabledOnContextMenu_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
            };

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void EnabledOnContextExtendedMenu_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
            };

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void RestoreFlagsOnLaunch_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.PersistState.Value);
            };

            // act
            viewModel.RestoreFlagsOnLaunch = true;
        }

        [TestMethod]
        public void MaxDispListNum_ShouldSetMaxSuggListTo20_WhenSuccessful()
        {
            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.AreEqual(20, snd.Powertoys.PowerRename.Properties.MaxMRUSize.Value);
            };

            // act
            viewModel.MaxDispListNum = 20;
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }
    }
}
