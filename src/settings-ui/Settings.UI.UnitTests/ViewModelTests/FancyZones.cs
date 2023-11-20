// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class FancyZones
    {
        public const string FancyZonesTestFolderName = "Test\\FancyZones";

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

            var fileMock = BackCompatTestProperties.GetModuleIOProvider(version, FancyZonesSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(fileMock.Object, settingPathMock.Object);
            FancyZonesSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<FancyZonesSettings>(FancyZonesSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();

            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);
            var fancyZonesRepository = new BackCompatTestProperties.MockSettingsRepository<FancyZonesSettings>(mockSettingsUtils);

            // Initialise View Model with test Config files
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils, generalSettingsRepository, fancyZonesRepository, ColorPickerIsEnabledByDefault_IPC);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.FancyZones, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.FancyzonesAppLastZoneMoveWindows.Value, viewModel.AppLastZoneMoveWindows);
            Assert.AreEqual(originalSettings.Properties.FancyzonesBorderColor.Value, viewModel.ZoneBorderColor);
            Assert.AreEqual(originalSettings.Properties.FancyzonesDisplayOrWorkAreaChangeMoveWindows.Value, viewModel.DisplayOrWorkAreaChangeMoveWindows);
            Assert.AreEqual(originalSettings.Properties.FancyzonesEditorHotkey.Value.ToString(), viewModel.EditorHotkey.ToString());
            Assert.AreEqual(originalSettings.Properties.FancyzonesWindowSwitching.Value, viewModel.WindowSwitching);
            Assert.AreEqual(originalSettings.Properties.FancyzonesNextTabHotkey.Value.ToString(), viewModel.NextTabHotkey.ToString());
            Assert.AreEqual(originalSettings.Properties.FancyzonesPrevTabHotkey.Value.ToString(), viewModel.PrevTabHotkey.ToString());
            Assert.AreEqual(originalSettings.Properties.FancyzonesExcludedApps.Value, viewModel.ExcludedApps);
            Assert.AreEqual(originalSettings.Properties.FancyzonesHighlightOpacity.Value, viewModel.HighlightOpacity);
            Assert.AreEqual(originalSettings.Properties.FancyzonesInActiveColor.Value, viewModel.ZoneInActiveColor);
            Assert.AreEqual(originalSettings.Properties.FancyzonesMakeDraggedWindowTransparent.Value, viewModel.MakeDraggedWindowsTransparent);
            Assert.AreEqual(originalSettings.Properties.FancyzonesMouseSwitch.Value, viewModel.MouseSwitch);
            Assert.AreEqual(originalSettings.Properties.FancyzonesMoveWindowsAcrossMonitors.Value, viewModel.MoveWindowsAcrossMonitors);
            Assert.AreEqual(originalSettings.Properties.FancyzonesMoveWindowsBasedOnPosition.Value, viewModel.MoveWindowsBasedOnPosition);
            Assert.AreEqual(originalSettings.Properties.FancyzonesOpenWindowOnActiveMonitor.Value, viewModel.OpenWindowOnActiveMonitor);
            Assert.AreEqual(originalSettings.Properties.FancyzonesOverrideSnapHotkeys.Value, viewModel.OverrideSnapHotkeys);
            Assert.AreEqual(originalSettings.Properties.FancyzonesRestoreSize.Value, viewModel.RestoreSize);
            Assert.AreEqual(originalSettings.Properties.FancyzonesShiftDrag.Value, viewModel.ShiftDrag);
            Assert.AreEqual(originalSettings.Properties.FancyzonesShowOnAllMonitors.Value, viewModel.ShowOnAllMonitors);
            Assert.AreEqual(originalSettings.Properties.FancyzonesSpanZonesAcrossMonitors.Value, viewModel.SpanZonesAcrossMonitors);
            Assert.AreEqual(originalSettings.Properties.FancyzonesZoneHighlightColor.Value, viewModel.ZoneHighlightColor);
            Assert.AreEqual(originalSettings.Properties.FancyzonesZoneSetChangeMoveWindows.Value, viewModel.ZoneSetChangeMoveWindows);
            Assert.AreEqual(originalSettings.Properties.UseCursorposEditorStartupscreen.Value, viewModel.UseCursorPosEditorStartupScreen);
            Assert.AreEqual(originalSettings.Properties.FancyzonesAllowChildWindowSnap.Value, viewModel.AllowChildWindowSnap);
            Assert.AreEqual(originalSettings.Properties.FancyzonesDisableRoundCornersOnSnap.Value, viewModel.DisableRoundCornersOnWindowSnap);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(fileMock, FancyZonesSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        private int ColorPickerIsEnabledByDefault_IPC(string msg)
        {
            OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
            Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            return 0;
        }

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockFancyZonesSettingsUtils;

        private Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockFancyZonesSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<FancyZonesSettings>();
        }

        [TestMethod]
        public void IsEnabledShouldDisableModuleWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsFalse(snd.GeneralSettings.Enabled.FancyZones);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.IsEnabled); // check if the module is enabled.

            // act
            viewModel.IsEnabled = false;
        }

        [TestMethod]
        public void ShiftDragShouldSetValue2FalseWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.ShiftDrag); // check if value was initialized to false.

            // act
            viewModel.ShiftDrag = false;

            // assert
            var expected = viewModel.ShiftDrag;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesShiftDrag.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void OverrideSnapHotkeysShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OverrideSnapHotkeys); // check if value was initialized to false.

            // act
            viewModel.OverrideSnapHotkeys = true;

            // assert
            var expected = viewModel.OverrideSnapHotkeys;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesOverrideSnapHotkeys.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MoveWindowsAcrossMonitorsShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MoveWindowsAcrossMonitors); // check if value was initialized to false.

            // act
            viewModel.MoveWindowsAcrossMonitors = true;

            // assert
            var expected = viewModel.MoveWindowsAcrossMonitors;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesMoveWindowsAcrossMonitors.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MoveWindowsBasedOnPositionShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MoveWindowsBasedOnPosition); // check if value was initialized to false.
            Assert.IsTrue(viewModel.MoveWindowsBasedOnZoneIndex); // check if value was initialized to true.

            // act
            viewModel.MoveWindowsBasedOnPosition = true;

            // assert
            var basedOnPositionExpected = viewModel.MoveWindowsBasedOnPosition;
            var basedOnPositionActual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesMoveWindowsBasedOnPosition.Value;
            Assert.AreEqual(basedOnPositionExpected, basedOnPositionActual);

            var basedOnZoneIndexExpected = viewModel.MoveWindowsBasedOnZoneIndex;
            var basedOnZoneIndexActual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesMoveWindowsBasedOnPosition.Value;
            Assert.AreNotEqual(basedOnZoneIndexExpected, basedOnZoneIndexActual);
        }

        [TestMethod]
        public void QuickLayoutSwitchShouldSetValue2FalseWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.QuickLayoutSwitch); // check if value was initialized to true.

            // act
            viewModel.QuickLayoutSwitch = false;

            // assert
            var expected = viewModel.QuickLayoutSwitch;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesQuickLayoutSwitch.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FlashZonesOnQuickSwitchShouldSetValue2FalseWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.FlashZonesOnQuickSwitch); // check if value was initialized to true.

            // act
            viewModel.FlashZonesOnQuickSwitch = false;

            // assert
            var expected = viewModel.FlashZonesOnQuickSwitch;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesFlashZonesOnQuickSwitch.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MakeDraggedWindowsTransparentShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MakeDraggedWindowsTransparent); // check if value was initialized to false.

            // act
            viewModel.MakeDraggedWindowsTransparent = true;

            // assert
            var expected = viewModel.MakeDraggedWindowsTransparent;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesMakeDraggedWindowTransparent.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MouseSwitchShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MouseSwitch); // check if value was initialized to false.

            // act
            viewModel.MouseSwitch = true;

            // assert
            var expected = viewModel.MouseSwitch;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesMouseSwitch.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DisplayOrWorkAreaChangeMoveWindowsShouldSetValue2FalseWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.DisplayOrWorkAreaChangeMoveWindows); // check if value was initialized to true.

            // act
            viewModel.DisplayOrWorkAreaChangeMoveWindows = false;

            // assert
            var expected = viewModel.DisplayOrWorkAreaChangeMoveWindows;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesDisplayOrWorkAreaChangeMoveWindows.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ZoneSetChangeMoveWindowsShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ZoneSetChangeMoveWindows); // check if value was initialized to false.

            // act
            viewModel.ZoneSetChangeMoveWindows = true;

            // assert
            var expected = viewModel.ZoneSetChangeMoveWindows;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesZoneSetChangeMoveWindows.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AppLastZoneMoveWindowsShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.AppLastZoneMoveWindows); // check if value was initialized to false.

            // act
            viewModel.AppLastZoneMoveWindows = true;

            // assert
            var expected = viewModel.AppLastZoneMoveWindows;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesAppLastZoneMoveWindows.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void OpenWindowOnActiveMonitorShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OpenWindowOnActiveMonitor); // check if value was initialized to false.

            // act
            viewModel.OpenWindowOnActiveMonitor = true;

            // assert
            var expected = viewModel.OpenWindowOnActiveMonitor;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesOpenWindowOnActiveMonitor.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void RestoreSizeShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.RestoreSize); // check if value was initialized to false.

            // act
            viewModel.RestoreSize = true;

            // assert
            var expected = viewModel.RestoreSize;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesRestoreSize.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void UseCursorPosEditorStartupScreenShouldSetValue2FalseWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.UseCursorPosEditorStartupScreen); // check if value was initialized to false.

            // act
            viewModel.UseCursorPosEditorStartupScreen = true;

            // assert
            var expected = viewModel.UseCursorPosEditorStartupScreen;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.UseCursorposEditorStartupscreen.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ShowOnAllMonitorsShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ShowOnAllMonitors); // check if value was initialized to false.

            // act
            viewModel.ShowOnAllMonitors = true;

            // assert
            var expected = viewModel.ShowOnAllMonitors;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesShowOnAllMonitors.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpanZonesAcrossMonitorsShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.SpanZonesAcrossMonitors); // check if value was initialized to false.

            // act
            viewModel.SpanZonesAcrossMonitors = true;

            // assert
            var expected = viewModel.SpanZonesAcrossMonitors;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesSpanZonesAcrossMonitors.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void OverlappingZonesAlgorithmIndexShouldSetValue2AnotherWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(0, viewModel.OverlappingZonesAlgorithmIndex); // check if value was initialized to false.

            // act
            viewModel.OverlappingZonesAlgorithmIndex = 1;

            // assert
            var expected = viewModel.OverlappingZonesAlgorithmIndex;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesOverlappingZonesAlgorithm.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AllowChildWindowsToSnapShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.AllowChildWindowSnap); // check if value was initialized to false.

            // act
            viewModel.AllowChildWindowSnap = true;

            // assert
            var expected = viewModel.AllowChildWindowSnap;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesAllowChildWindowSnap.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DisableRoundCornersOnSnapShouldSetValue2TrueWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.DisableRoundCornersOnWindowSnap); // check if value was initialized to false.

            // act
            viewModel.DisableRoundCornersOnWindowSnap = true;

            // assert
            var expected = viewModel.DisableRoundCornersOnWindowSnap;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesDisableRoundCornersOnSnap.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ZoneHighlightColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesZoneHighlightColor, viewModel.ZoneHighlightColor);

            // act
            viewModel.ZoneHighlightColor = "#FFFFFF";

            // assert
            var expected = viewModel.ZoneHighlightColor;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesZoneHighlightColor.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ZoneBorderColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyzonesBorderColor, viewModel.ZoneBorderColor);

            // act
            viewModel.ZoneBorderColor = "#FFFFFF";

            // assert
            var expected = viewModel.ZoneBorderColor;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesBorderColor.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ZoneInActiveColorShouldSetColorValue2WhiteWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesInActiveColor, viewModel.ZoneInActiveColor);

            // act
            viewModel.ZoneInActiveColor = "#FFFFFF";

            // assert
            var expected = viewModel.ZoneInActiveColor;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesInActiveColor.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ExcludedAppsShouldSetColorValue2WhiteWhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(string.Empty, viewModel.ExcludedApps);

            // act
            viewModel.ExcludedApps = "Sample";

            // assert
            var expected = viewModel.ExcludedApps;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesExcludedApps.Value;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightOpacityShouldSetOpacityValueTo60WhenSuccessful()
        {
            Mock<SettingsUtils> mockSettingsUtils = new Mock<SettingsUtils>();

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object), sendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(50, viewModel.HighlightOpacity);

            // act
            viewModel.HighlightOpacity = 60;

            // assert
            var expected = viewModel.HighlightOpacity;
            var actual = SettingsRepository<FancyZonesSettings>.GetInstance(mockFancyZonesSettingsUtils.Object).SettingsConfig.Properties.FancyzonesHighlightOpacity.Value;
            Assert.AreEqual(expected, actual);
        }
    }
}
