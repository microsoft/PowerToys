// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class PowerLauncherViewModelTest
    {
        private class SendCallbackMock
        {
            public int TimesSent { get; set; }

            // PowerLauncherSettings is unused, but required according to SendCallback's signature.
            // Naming parameter with discard symbol to suppress FxCop warnings.
            [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "We actually don't validate setting, just calculate it was sent")]
            public void OnSend(PowerLauncherSettings _)
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
        [DataRow("v0.18.2", "settings.json")]
        [DataRow("v0.19.2", "settings.json")]
        [DataRow("v0.20.1", "settings.json")]
        [DataRow("v0.21.1", "settings.json")]
        [DataRow("v0.22.0", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var settingPathMock = new Mock<ISettingsPath>();

            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, PowerLauncherSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object, settingPathMock.Object);
            PowerLauncherSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();

            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerLauncherViewModel viewModel = new PowerLauncherViewModel(mockSettingsUtils, generalSettingsRepository, sendMockIPCConfigMSG, 32, () => true);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.PowerLauncher, viewModel.EnablePowerLauncher);
            Assert.AreEqual(originalSettings.Properties.ClearInputOnLaunch, viewModel.ClearInputOnLaunch);
            Assert.AreEqual(originalSettings.Properties.CopyPathLocation.ToString(), viewModel.CopyPathLocation.ToString());
            Assert.AreEqual(originalSettings.Properties.IgnoreHotkeysInFullscreen, viewModel.IgnoreHotkeysInFullScreen);
            Assert.AreEqual(originalSettings.Properties.MaximumNumberOfResults, viewModel.MaximumNumberOfResults);
            Assert.AreEqual(originalSettings.Properties.OpenPowerLauncher.ToString(), viewModel.OpenPowerLauncher.ToString());
            Assert.AreEqual(originalSettings.Properties.OverrideWinkeyR, viewModel.OverrideWinRKey);
            Assert.AreEqual(originalSettings.Properties.OverrideWinkeyS, viewModel.OverrideWinSKey);
            Assert.AreEqual(originalSettings.Properties.SearchResultPreference, viewModel.SearchResultPreference);
            Assert.AreEqual(originalSettings.Properties.SearchTypePreference, viewModel.SearchTypePreference);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, PowerLauncherSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void SearchPreferenceShouldUpdatePreferences()
        {
            viewModel.SearchResultPreference = "SearchOptionsAreNotValidated";
            viewModel.SearchTypePreference = "SearchOptionsAreNotValidated";

            Assert.AreEqual(sendCallbackMock.TimesSent, 2);
            Assert.IsTrue(mockSettings.Properties.SearchResultPreference == "SearchOptionsAreNotValidated");
            Assert.IsTrue(mockSettings.Properties.SearchTypePreference == "SearchOptionsAreNotValidated");
        }

        public static void AssertHotkeySettings(HotkeySettings setting, bool win, bool ctrl, bool alt, bool shift, int code)
        {
            if (setting != null)
            {
                Assert.AreEqual(win, setting.Win);
                Assert.AreEqual(ctrl, setting.Ctrl);
                Assert.AreEqual(alt, setting.Alt);
                Assert.AreEqual(shift, setting.Shift);
                Assert.AreEqual(code, setting.Code);
            }
            else
            {
                Assert.Fail("setting parameter is null");
            }
        }

        [TestMethod]
        public void HotkeysShouldUpdateHotkeys()
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
        public void OverrideShouldUpdateOverrides()
        {
            viewModel.OverrideWinRKey = true;
            viewModel.OverrideWinSKey = false;

            Assert.AreEqual(1, sendCallbackMock.TimesSent);

            Assert.IsTrue(mockSettings.Properties.OverrideWinkeyR);
            Assert.IsFalse(mockSettings.Properties.OverrideWinkeyS);
        }
    }
}
