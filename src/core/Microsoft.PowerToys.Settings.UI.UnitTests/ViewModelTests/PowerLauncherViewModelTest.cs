// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PowerLauncherViewModelTest
    {
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
