// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Frameworks;

namespace ViewModelTests
{
    [TestClass]
    public class General
    {
        public const string generalSettings_file_name = "Test\\GenealSettings";

        [TestMethod]
        public void IsElevated_ShouldUpdateRunasAdminStatusAttrs_WhenSuccessful()
        {
            // Arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);

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
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Startup);
                return 0;
            };

            // Arrange
            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.Startup);

            // act
            viewModel.Startup = true;
        }

        [TestMethod]
        public void RunElevated_ShouldEnableAlwaysRunElevated_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.RunElevated);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };

            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);

            Assert.IsFalse(viewModel.RunElevated);

            // act
            viewModel.RunElevated = true;
        }

        [TestMethod]
        public void IsLightThemeRadioButtonChecked_ShouldThemeToLight_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = null;
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("light", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.IsLightThemeRadioButtonChecked);

            // act
            viewModel.IsLightThemeRadioButtonChecked = true;
        }

        [TestMethod]
        public void IsDarkThemeRadioButtonChecked_ShouldThemeToDark_WhenSuccessful()
        {
            // Arrange
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("dark", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.IsDarkThemeRadioButtonChecked);



            // act
            viewModel.IsDarkThemeRadioButtonChecked = true;
        }

        [TestMethod]
        public void AllModulesAreEnabledByDefault()
        {
            //arrange 
            EnabledModules modules = new EnabledModules();


            //Assert
            Assert.IsTrue(modules.FancyZones);
            Assert.IsTrue(modules.ImageResizer);
            Assert.IsTrue(modules.FileExplorerPreview);
            Assert.IsTrue(modules.ShortcutGuide);
            Assert.IsTrue(modules.PowerRename);
            Assert.IsTrue(modules.KeyboardManager);
            Assert.IsTrue(modules.PowerLauncher);
            Assert.IsTrue(modules.ColorPicker);
        }

        public int UpdateUIThemeMethod(string themeName)
        {
            return 0;
        }
    }
}
