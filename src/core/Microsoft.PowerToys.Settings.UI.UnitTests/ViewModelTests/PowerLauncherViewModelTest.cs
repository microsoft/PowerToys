// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Moq;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;

namespace ViewModelTests
{
    [TestClass]
    public class PowerLauncherViewModelTest
    {
        // This should not be changed. 
        // Changing it will causes user's to lose their local settings configs.
        public const string OriginalModuleName = "PowerToys Run";

        private class SendCallbackMock
        {
            public int TimesSent { get; set; }

            public void OnSend(PowerLauncherSettings settings)
            {
                TimesSent++;
            }
        }

        private PowerLauncherViewModel viewModel;
        private PowerLauncherSettings mockSettings;
        private SendCallbackMock sendCallbackMock;
        [TestInitialize]
        public void Initialize()
        {
            mockSettings = new PowerLauncherSettings();
            sendCallbackMock = new SendCallbackMock();
            viewModel = new PowerLauncherViewModel(
                mockSettings,
                new PowerLauncherViewModel.SendCallback(sendCallbackMock.OnSend));
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            PowerLauncherSettings originalSettings = mockSettingsUtils.GetSettings<PowerLauncherSettings>(OriginalModuleName);
            GeneralSettings originalGeneralSettings = mockSettingsUtils.GetSettings<GeneralSettings>();

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerLauncherViewModel viewModel = new PowerLauncherViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockSettingsUtils), SendMockIPCConfigMSG, 32);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.PowerLauncher, viewModel.EnablePowerLauncher);
            Assert.AreEqual(originalSettings.Properties.ClearInputOnLaunch, viewModel.ClearInputOnLaunch);
            Assert.AreEqual(originalSettings.Properties.CopyPathLocation.ToString(), viewModel.CopyPathLocation.ToString());
            Assert.AreEqual(originalSettings.Properties.DisableDriveDetectionWarning, viewModel.DisableDriveDetectionWarning);
            Assert.AreEqual(originalSettings.Properties.IgnoreHotkeysInFullscreen, viewModel.IgnoreHotkeysInFullScreen);
            Assert.AreEqual(originalSettings.Properties.MaximumNumberOfResults, viewModel.MaximumNumberOfResults);
            Assert.AreEqual(originalSettings.Properties.OpenPowerLauncher.ToString(), viewModel.OpenPowerLauncher.ToString());
            Assert.AreEqual(originalSettings.Properties.OverrideWinkeyR, viewModel.OverrideWinRKey);
            Assert.AreEqual(originalSettings.Properties.OverrideWinkeyS, viewModel.OverrideWinSKey);
            Assert.AreEqual(originalSettings.Properties.SearchResultPreference, viewModel.SearchResultPreference);
            Assert.AreEqual(originalSettings.Properties.SearchTypePreference, viewModel.SearchTypePreference);
        }

        [TestMethod]
        public void SearchPreference_ShouldUpdatePreferences()
        {
            viewModel.SearchResultPreference = "SearchOptionsAreNotValidated";
            viewModel.SearchTypePreference = "SearchOptionsAreNotValidated";

            Assert.AreEqual(sendCallbackMock.TimesSent, 2);
            Assert.IsTrue(mockSettings.Properties.SearchResultPreference == "SearchOptionsAreNotValidated");
            Assert.IsTrue(mockSettings.Properties.SearchTypePreference == "SearchOptionsAreNotValidated");
        }

        public void AssertHotkeySettings(HotkeySettings setting, bool win, bool ctrl, bool alt, bool shift, int code)
        {
            Assert.AreEqual(win, setting.Win);
            Assert.AreEqual(ctrl, setting.Ctrl);
            Assert.AreEqual(alt, setting.Alt);
            Assert.AreEqual(shift, setting.Shift);
            Assert.AreEqual(code, setting.Code);
        }

        [TestMethod]
        public void Hotkeys_ShouldUpdateHotkeys()
        {
            var openPowerLauncher = new HotkeySettings();
            openPowerLauncher.Win = true;
            openPowerLauncher.Code = 83;

            var openFileLocation = new HotkeySettings();
            openFileLocation.Ctrl = true;
            openFileLocation.Code = 65;

            var openConsole = new HotkeySettings();
            openConsole.Alt = true;
            openConsole.Code = 68;

            var copyFileLocation = new HotkeySettings();
            copyFileLocation.Shift = true;
            copyFileLocation.Code = 70;

            viewModel.OpenPowerLauncher = openPowerLauncher;
            viewModel.OpenFileLocation = openFileLocation;
            viewModel.CopyPathLocation = copyFileLocation;

            Assert.AreEqual(3, sendCallbackMock.TimesSent);

            AssertHotkeySettings(
                mockSettings.Properties.OpenPowerLauncher,
                true,
                false,
                false,
                false,
                83);
            AssertHotkeySettings(
                mockSettings.Properties.OpenFileLocation,
                false,
                true,
                false,
                false,
                65);
            AssertHotkeySettings(
                mockSettings.Properties.CopyPathLocation,
                false,
                false,
                false,
                true,
                70);
        }

        [TestMethod]
        public void Override_ShouldUpdateOverrides()
        {
            viewModel.OverrideWinRKey = true;
            viewModel.OverrideWinSKey = false;

            Assert.AreEqual(1, sendCallbackMock.TimesSent);

            Assert.IsTrue(mockSettings.Properties.OverrideWinkeyR);
            Assert.IsFalse(mockSettings.Properties.OverrideWinkeyS);
        }

        [TestMethod]
        public void DriveDetectionViewModel_WhenSet_MustUpdateOverrides()
        {
            // Act
            viewModel.DisableDriveDetectionWarning = true;

            // Assert
            Assert.AreEqual(1, sendCallbackMock.TimesSent);
            Assert.IsTrue(mockSettings.Properties.DisableDriveDetectionWarning);
        }
    }
}
