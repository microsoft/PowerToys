using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Windows.UI.Popups;

namespace ViewModelTests
{
    [TestClass]
    public class ShortcutGuide
    {
        private const string ModuleName = "Shortcut Guide";

        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            // Test base path:
            // C:\Users\<user name>\AppData\Local\Packages\08e1807b-8b6d-4bfa-adc4-79c64aae8e78_9abkseg265h2m\LocalState\Microsoft\PowerToys\
            GeneralSettings generalSettings = new GeneralSettings();
            ShortcutGuideSettings shortcutGuide = new ShortcutGuideSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
            SettingsUtils.SaveSettings(shortcutGuide.ToJsonString(), shortcutGuide.Name);
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

            if (SettingsUtils.SettingsFolderExists(ModuleName))
            {
                DeleteFolder(ModuleName);
            }
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }

        [TestMethod]
        public void IsEnabled_ShouldEnableModule_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ShortcutGuide);
            };

            // Act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void ThemeIndex_ShouldSetThemeToDark_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();
            Assert.AreEqual(1, viewModel.ThemeIndex);

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);
                Assert.AreEqual("dark", snd.Powertoys.ShortcutGuide.Properties.Theme.Value);
            };

            // Act
            viewModel.ThemeIndex = 0;
        }

        [TestMethod]
        public void PressTime_ShouldSetPressTimeToOneHundred_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();
            Assert.AreEqual(900, viewModel.PressTime);

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);
                Assert.AreEqual(100, snd.Powertoys.ShortcutGuide.Properties.PressTime.Value);
            };

            // Act
            viewModel.PressTime = 100;
        }

        [TestMethod]
        public void OverlayOpacity_ShouldSeOverlayOpacityToOneHundred_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();
            Assert.AreEqual(90, viewModel.OverlayOpacity);

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);
                // Serialisation not working as expected in the test project:
                Assert.AreEqual(100, snd.Powertoys.ShortcutGuide.Properties.OverlayOpacity.Value);
            };

            // Act
            viewModel.OverlayOpacity = 100;
        }
    }
}