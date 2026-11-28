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
    public class WindowTabs
    {
        private Mock<SettingsUtils> mockGeneralSettingsUtils;

        private Mock<SettingsUtils> mockWindowTabsSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockWindowTabsSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<WindowTabsSettings>();
        }

        private WindowTabsViewModel CreateViewModel(Mock<SettingsUtils> settingsUtilsMock, Func<string, int> ipcCallback = null)
        {
            ipcCallback ??= msg => 0;
            return new WindowTabsViewModel(
                settingsUtilsMock.Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                SettingsRepository<WindowTabsSettings>.GetInstance(mockWindowTabsSettingsUtils.Object),
                ipcCallback);
        }

        [TestMethod]
        public void Constructor_ShouldLoadDefaultShortcutsFromSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            Assert.IsNotNull(viewModel.NextTabShortcut);
            Assert.IsTrue(viewModel.NextTabShortcut.Win);
            Assert.IsFalse(viewModel.NextTabShortcut.Shift);
            Assert.AreEqual(0x22, viewModel.NextTabShortcut.Code);

            Assert.IsNotNull(viewModel.PreviousTabShortcut);
            Assert.IsTrue(viewModel.PreviousTabShortcut.Win);
            Assert.IsFalse(viewModel.PreviousTabShortcut.Shift);
            Assert.AreEqual(0x21, viewModel.PreviousTabShortcut.Code);
        }

        [TestMethod]
        public void IsEnabled_WhenSet_ShouldSendEnabledGeneralSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);

            var ipcInvoked = false;
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.WindowTabs);
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
        public void NextTabShortcut_WhenChanged_ShouldPersistSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            var newShortcut = new HotkeySettings(true, true, false, false, 0x4E); // Win+Ctrl+N
            viewModel.NextTabShortcut = newShortcut;

            Assert.AreSame(newShortcut, viewModel.NextTabShortcut);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => json.Contains("next_tab_shortcut")),
                    It.Is<string>(name => name == WindowTabsSettings.ModuleName),
                    It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void PreviousTabShortcut_WhenChanged_ShouldPersistSettings()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            var newShortcut = new HotkeySettings(true, true, false, true, 0x50); // Win+Ctrl+Shift+P
            viewModel.PreviousTabShortcut = newShortcut;

            Assert.AreSame(newShortcut, viewModel.PreviousTabShortcut);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => json.Contains("previous_tab_shortcut")),
                    It.Is<string>(name => name == WindowTabsSettings.ModuleName),
                    It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [TestMethod]
        public void NextTabShortcut_WhenSetToNull_ShouldFallBackToDefault()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var viewModel = CreateViewModel(settingsUtilsMock);

            viewModel.NextTabShortcut = null;

            Assert.IsNotNull(viewModel.NextTabShortcut);
            Assert.IsTrue(viewModel.NextTabShortcut.Win);
            Assert.IsFalse(viewModel.NextTabShortcut.Shift);
            Assert.AreEqual(0x22, viewModel.NextTabShortcut.Code);
        }

        [TestMethod]
        public void DisablingRestorationSendsNativeSettingsEnvelope()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
            var sent = false;
            var viewModel = CreateViewModel(settingsUtilsMock, message =>
            {
                using var document = JsonDocument.Parse(message);
                var properties = document.RootElement.GetProperty("powertoys").GetProperty("WindowTabs").GetProperty("properties");
                Assert.IsFalse(properties.GetProperty("restore_session").GetProperty("value").GetBoolean());
                sent = true;
                return 0;
            });
            viewModel.RestoreSession = false;
            Assert.IsTrue(sent);
            settingsUtilsMock.Verify(
                utils => utils.SaveSettings(It.IsAny<string>(), WindowTabsSettings.ModuleName, It.IsAny<string>()),
                Times.Once);
        }
    }
}
