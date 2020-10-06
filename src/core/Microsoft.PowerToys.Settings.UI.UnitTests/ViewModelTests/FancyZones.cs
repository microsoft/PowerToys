// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using CommonLibTest;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class FancyZones
    {
        public const string FancyZonesTestFolderName = "Test\\FancyZones";

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockFancyZonesSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockFancyZonesSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<FancyZonesSettings>();
        }

        [TestMethod]
        public void IsEnabledShouldDisableModuleWhenSuccessful()
        {
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsFalse(snd.GeneralSettings.Enabled.FancyZones);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.IsEnabled); // check if the module is enabled.

            // act
            viewModel.IsEnabled = false;
        }

        [TestMethod]
        public void ShiftDragShouldSetValue2FalseWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsFalse(snd.Powertoys.FancyZones.Properties.FancyzonesShiftDrag.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.ShiftDrag); // check if value was initialized to false.

            // act
            viewModel.ShiftDrag = false;
        }

        [TestMethod]
        public void OverrideSnapHotkeysShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesOverrideSnapHotkeys.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OverrideSnapHotkeys); // check if value was initialized to false.

            // act
            viewModel.OverrideSnapHotkeys = true;
        }

        [TestMethod]
        public void MoveWindowsBasedOnPositionShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMoveWindowsBasedOnPosition.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MoveWindowsBasedOnPosition); // check if value was initialized to false.

            // act
            viewModel.MoveWindowsBasedOnPosition = true;
        }

        [TestMethod]
        public void ZoneSetChangeFlashZonesShouldSetValue2FalseWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMakeDraggedWindowTransparent.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MakeDraggedWindowsTransparent); // check if value was initialized to false.

            // act
            viewModel.MakeDraggedWindowsTransparent = true;
        }

        [TestMethod]
        public void MouseSwitchShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMouseSwitch.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MouseSwitch); // check if value was initialized to false.

            // act
            viewModel.MouseSwitch = true;
        }

        [TestMethod]
        public void DisplayChangeMoveWindowsShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesDisplayChangeMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.DisplayChangeMoveWindows); // check if value was initialized to false.

            // act
            viewModel.DisplayChangeMoveWindows = true;
        }

        [TestMethod]
        public void ZoneSetChangeMoveWindowsShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesZoneSetChangeMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ZoneSetChangeMoveWindows); // check if value was initialized to false.

            // act
            viewModel.ZoneSetChangeMoveWindows = true;
        }

        [TestMethod]
        public void AppLastZoneMoveWindowsShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesAppLastZoneMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.AppLastZoneMoveWindows); // check if value was initialized to false.

            // act
            viewModel.AppLastZoneMoveWindows = true;
        }

        public void OpenWindowOnActiveMonitorShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesOpenWindowOnActiveMonitor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OpenWindowOnActiveMonitor); // check if value was initialized to false.

            // act
            viewModel.OpenWindowOnActiveMonitor = true;
        }

        [TestMethod]
        public void RestoreSizeShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesRestoreSize.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.RestoreSize); // check if value was initialized to false.

            // act
            viewModel.RestoreSize = true;
        }

        [TestMethod]
        public void UseCursorPosEditorStartupScreenShouldSetValue2FalseWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.UseCursorposEditorStartupscreen.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.UseCursorPosEditorStartupScreen); // check if value was initialized to false.

            // act
            viewModel.UseCursorPosEditorStartupScreen = true;
        }

        [TestMethod]
        public void ShowOnAllMonitorsShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesShowOnAllMonitors.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ShowOnAllMonitors); // check if value was initialized to false.

            // act
            viewModel.ShowOnAllMonitors = true;
        }

        [TestMethod]
        public void ZoneHighlightColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesZoneHighlightColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesZoneHighlightColor, viewModel.ZoneHighlightColor);

            // act
            viewModel.ZoneHighlightColor = "#FFFFFF";
        }

        [TestMethod]
        public void ZoneBorderColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesBorderColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyzonesBorderColor, viewModel.ZoneBorderColor);

            // act
            viewModel.ZoneBorderColor = "#FFFFFF";
        }

        [TestMethod]
        public void ZoneInActiveColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesInActiveColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesInActiveColor, viewModel.ZoneInActiveColor);

            // act
            viewModel.ZoneInActiveColor = "#FFFFFF";
        }

        [TestMethod]
        public void ExcludedAppsShouldSetColorValue2WhiteWhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("Sample", snd.Powertoys.FancyZones.Properties.FancyzonesExcludedApps.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(string.Empty, viewModel.ExcludedApps);

            // act
            viewModel.ExcludedApps = "Sample";
        }

        [TestMethod]
        public void HighlightOpacityShouldSetOpacityValueTo60WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual(60, snd.Powertoys.FancyZones.Properties.FancyzonesHighlightOpacity.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(50, viewModel.HighlightOpacity);

            // act
            viewModel.HighlightOpacity = 60;
        }
    }
}
