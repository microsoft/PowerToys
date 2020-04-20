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

namespace Microsoft.PowerToys.Settings.UnitTest.ViewModelTests
{
    [TestClass]
    public class ShortcutGuideViewModelTest
    {
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
                Assert.IsTrue(snd.general.Enabled.ShortcutGuide);
            };

            // Act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void ThemeIndex_ShouldSetThemeToDark_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                SndModuleSettings<ShortcutGuideSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<ShortcutGuideSettings>>(msg);
                Assert.AreEqual("dark", snd.powertoys.Properties.Theme.Value);
                Assert.AreEqual("hey", msg);
            };

            // Act
            viewModel.ThemeIndex = 0;
        }

        [TestMethod]
        public void PressTime_ShouldSetPressTimeToOneHundred_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                SndModuleSettings<ShortcutGuideSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<ShortcutGuideSettings>>(msg);
                // https://stackoverflow.com/questions/59198417/deserialization-of-reference-types-without-parameterless-constructor-is-not-supp
                Assert.AreEqual(100, snd.powertoys.Properties.PressTime.Value);
            };

            // Act
            viewModel.PressTime = 100;
        }

        [TestMethod]
        public void OverlayOpacity_ShouldSeOverlayOpacityToOneHundred_WhenSuccessful()
        {
            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel();

            // Assert
            // Initilize mock function of sending IPC message.
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                SndModuleSettings<ShortcutGuideSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<ShortcutGuideSettings>>(msg);
                // Serialisation not working as expected in the test project:
                // https://stackoverflow.com/questions/59198417/deserialization-of-reference-types-without-parameterless-constructor-is-not-supp
                Assert.AreEqual(100, snd.powertoys.Properties.OverlayOpacity.Value);
            };

            // Act
            viewModel.OverlayOpacity = 100;
        }
    }
}
