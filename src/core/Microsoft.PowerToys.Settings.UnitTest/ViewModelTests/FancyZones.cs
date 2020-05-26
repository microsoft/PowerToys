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
using Windows.UI;

namespace ViewModelTests
{
    [TestClass]
    public class FancyZones
    {
        public const string ModuleName = "FancyZones";

        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            GeneralSettings generalSettings = new GeneralSettings();
            FZConfigProperties fZConfigProperties = new FZConfigProperties();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
            SettingsUtils.SaveSettings(fZConfigProperties.ToJsonString(), ModuleName);
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
        public void IsEnabled_ShouldDisableModule_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsTrue(viewModel.IsEnabled); // check if the module is enabled.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsFalse(snd.GeneralSettings.Enabled.FancyZones);
            };

            // act
            viewModel.IsEnabled = false;
        }

        [TestMethod]
        public void ShiftDrag_ShouldSetValue2False_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsTrue(viewModel.ShiftDrag); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsFalse(snd.Powertoys.FancyZones.Properties.FancyzonesShiftDrag.Value);
            };

            // act
            viewModel.ShiftDrag = false;
        }

        [TestMethod]
        public void OverrideSnapHotkeys_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.OverrideSnapHotkeys); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesOverrideSnapHotkeys.Value);
            };

            // act
            viewModel.OverrideSnapHotkeys = true;
        }

        [TestMethod]
        public void ZoneSetChangeFlashZones_ShouldSetValue2False_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.MakeDraggedWindowsTransparent); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMakeDraggedWindowTransparent.Value);
            };

            // act
            viewModel.MakeDraggedWindowsTransparent = true;
        }

        [TestMethod]
        public void MouseSwitch_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.MouseSwitch); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMouseSwitch.Value);
            };

            // act
            viewModel.MouseSwitch = true;
        }

        [TestMethod]
        public void DisplayChangeMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.DisplayChangeMoveWindows); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesDisplayChangeMoveWindows.Value);
            };

            // act
            viewModel.DisplayChangeMoveWindows = true;
        }

        [TestMethod]
        public void ZoneSetChangeMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.ZoneSetChangeMoveWindows); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesZoneSetChangeMoveWindows.Value);
            };

            // act
            viewModel.ZoneSetChangeMoveWindows = true;
        }

        [TestMethod]
        public void AppLastZoneMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.AppLastZoneMoveWindows); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesAppLastZoneMoveWindows.Value);
            };

            // act
            viewModel.AppLastZoneMoveWindows = true;
        }

        [TestMethod]
        public void UseCursorPosEditorStartupScreen_ShouldSetValue2False_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsTrue(viewModel.UseCursorPosEditorStartupScreen); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.UseCursorposEditorStartupscreen.Value);
            };

            // act
            viewModel.UseCursorPosEditorStartupScreen = true;
        }

        [TestMethod]
        public void ShowOnAllMonitors_ShouldSetValue2True_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.IsFalse(viewModel.ShowOnAllMonitors); // check if value was initialized to false.

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesShowOnAllMonitors.Value);
            };

            // act
            viewModel.ShowOnAllMonitors = true;
        }

        [TestMethod]
        public void ZoneHighlightColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesZoneHighlightColor, ToRGBHex(viewModel.ZoneHighlightColor)); 

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#E1E1E1", snd.Powertoys.FancyZones.Properties.FancyzonesZoneHighlightColor.Value);
            };

            // act
            viewModel.ZoneHighlightColor = Color.FromArgb(0, 225,225,225);
        }

        [TestMethod]
        public void ZoneBorderColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.AreEqual(ConfigDefaults.DefaultFancyzonesBorderColor, ToRGBHex(viewModel.ZoneBorderColor)); 

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#E1E1E1", snd.Powertoys.FancyZones.Properties.FancyzonesBorderColor.Value);
            };

            // act
            viewModel.ZoneBorderColor = Color.FromArgb(0, 225, 225, 225);
        }

        [TestMethod]
        public void ZoneInActiveColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesInActiveColor, ToRGBHex(viewModel.ZoneInActiveColor)); 

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#E1E1E1", snd.Powertoys.FancyZones.Properties.FancyzonesInActiveColor.Value);
            };

            // act
            viewModel.ZoneInActiveColor = Color.FromArgb(0, 225, 225, 225);
        }

        [TestMethod]
        public void ExcludedApps_ShouldSetColorValue2White_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.AreEqual("", viewModel.ExcludedApps);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("Sample", snd.Powertoys.FancyZones.Properties.FancyzonesExcludedApps.Value);
            };

            // act
            viewModel.ExcludedApps = "Sample";
        }

        [TestMethod]
        public void HighlightOpacity_ShouldSetOpacityValueTo60_WhenSuccessful()
        {
            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel();
            Assert.AreEqual(50, viewModel.HighlightOpacity);

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual(60, snd.Powertoys.FancyZones.Properties.FancyzonesHighlightOpacity.Value);
            };

            // act
            viewModel.HighlightOpacity = 60;
        }

        private String ToRGBHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }
    }
}
