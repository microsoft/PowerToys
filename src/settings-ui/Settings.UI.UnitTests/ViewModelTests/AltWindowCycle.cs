// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class AltWindowCycle
    {
        private Mock<SettingsUtils> mockGeneralSettingsUtils;

        private Mock<SettingsUtils> mockAltWindowCycleSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockAltWindowCycleSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<AltWindowCycleSettings>();
        }

        private AltWindowCycleViewModel CreateViewModel(Mock<SettingsUtils> settingsUtilsMock, Func<string, int> ipcCallback = null)
        {
            ipcCallback ??= msg => 0;
            return new AltWindowCycleViewModel(
                settingsUtilsMock.Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                SettingsRepository<AltWindowCycleSettings>.GetInstance(mockAltWindowCycleSettingsUtils.Object),
                ipcCallback);
        }

        [TestMethod]
        public void Constructor_ShouldLoadDefaultShortcutsFromSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            Assert.IsNotNull(viewModel.NextWindowShortcut);
            Assert.IsTrue(viewModel.NextWindowShortcut.Alt);
            Assert.IsFalse(viewModel.NextWindowShortcut.Shift);
            Assert.AreEqual(0xC0, viewModel.NextWindowShortcut.Code);

            Assert.IsNotNull(viewModel.PreviousWindowShortcut);
            Assert.IsTrue(viewModel.PreviousWindowShortcut.Alt);
            Assert.IsTrue(viewModel.PreviousWindowShortcut.Shift);
            Assert.AreEqual(0xC0, viewModel.PreviousWindowShortcut.Code);
        }

        [TestMethod]
        public void IsEnabled_WhenSet_ShouldSendEnabledGeneralSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);

            var ipcInvoked = false;
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.AltWindowCycle);
                ipcInvoked = true;
                return 0;
            };

            var viewModel = CreateViewModel(settingsUtilsMock, sendMockIPCConfigMSG);

            viewModel.IsEnabled = true;

            Assert.IsTrue(ipcInvoked, "Enabling the module should send an OutGoingGeneralSettings IPC message.");
            Assert.IsTrue(viewModel.IsEnabled);
        }

        [TestMethod]
        public void GpoNotConfigured_ShouldNotReportPolicyManaged()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            // With no enterprise policy applied in the test environment, the module must be
            // user-controllable (not reported as GPO-managed).
            Assert.IsFalse(viewModel.IsEnabledGpoConfigured);
        }

        [TestMethod]
        public void NextWindowShortcut_WhenChanged_ShouldPersistSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            var newShortcut = new HotkeySettings(true, true, false, false, 0x4E); // Win+Ctrl+N
            viewModel.NextWindowShortcut = newShortcut;

            Assert.AreSame(newShortcut, viewModel.NextWindowShortcut);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => json.Contains("next_window_shortcut")),
                    It.Is<string>(name => name == AltWindowCycleSettings.ModuleName),
                    It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void PreviousWindowShortcut_WhenChanged_ShouldPersistSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            var newShortcut = new HotkeySettings(true, true, false, true, 0x50); // Win+Ctrl+Shift+P
            viewModel.PreviousWindowShortcut = newShortcut;

            Assert.AreSame(newShortcut, viewModel.PreviousWindowShortcut);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => json.Contains("previous_window_shortcut")),
                    It.Is<string>(name => name == AltWindowCycleSettings.ModuleName),
                    It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void NextWindowShortcut_WhenSetToNull_ShouldFallBackToDefault()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            viewModel.NextWindowShortcut = null;

            Assert.IsNotNull(viewModel.NextWindowShortcut);
            Assert.IsTrue(viewModel.NextWindowShortcut.Alt);
            Assert.IsFalse(viewModel.NextWindowShortcut.Shift);
            Assert.AreEqual(0xC0, viewModel.NextWindowShortcut.Code);
        }
    }
}
