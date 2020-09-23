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
        // This should not be changed. 
        // Changing it will causes user's to lose their local settings configs.
        public const string OriginalModuleName = "FancyZones";


        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {

            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            FancyZonesSettings originalSettings = mockSettingsUtils.GetSettings<FancyZonesSettings>(OriginalModuleName);
            GeneralSettings originalGeneralSettings = mockSettingsUtils.GetSettings<GeneralSettings>();

            // Initialise View Model with test Config files
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, ColorPickerIsEnabledByDefault_IPC);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.FancyZones, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.FancyzonesAppLastZoneMoveWindows.Value, viewModel.AppLastZoneMoveWindows);
            Assert.AreEqual(originalSettings.Properties.FancyzonesBorderColor.Value, viewModel.ZoneBorderColor);
            Assert.AreEqual(originalSettings.Properties.FancyzonesDisplayChangeMoveWindows.Value, viewModel.DisplayChangeMoveWindows);
            Assert.AreEqual(originalSettings.Properties.FancyzonesEditorHotkey.Value.ToString(), viewModel.EditorHotkey.ToString());
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
        }

        private int ColorPickerIsEnabledByDefault_IPC(string msg)
        {
            OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
            Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            return 0;
        }

        [TestMethod]
        public void IsEnabled_ShouldDisableModule_WhenSuccessful()
        {
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsFalse(snd.GeneralSettings.Enabled.FancyZones);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.IsEnabled); // check if the module is enabled.

            // act
            viewModel.IsEnabled = false;
        }

        [TestMethod]
        public void ShiftDrag_ShouldSetValue2False_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsFalse(snd.Powertoys.FancyZones.Properties.FancyzonesShiftDrag.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.ShiftDrag); // check if value was initialized to false.

            // act
            viewModel.ShiftDrag = false;
        }

        [TestMethod]
        public void OverrideSnapHotkeys_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesOverrideSnapHotkeys.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OverrideSnapHotkeys); // check if value was initialized to false.

            // act
            viewModel.OverrideSnapHotkeys = true;
        }

        [TestMethod]
        public void MoveWindowsBasedOnPosition_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMoveWindowsBasedOnPosition.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MoveWindowsBasedOnPosition); // check if value was initialized to false.

            // act
            viewModel.MoveWindowsBasedOnPosition = true;
        }

        [TestMethod]
        public void ZoneSetChangeFlashZones_ShouldSetValue2False_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMakeDraggedWindowTransparent.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MakeDraggedWindowsTransparent); // check if value was initialized to false.

            // act
            viewModel.MakeDraggedWindowsTransparent = true;
        }

        [TestMethod]
        public void MouseSwitch_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesMouseSwitch.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.MouseSwitch); // check if value was initialized to false.

            // act
            viewModel.MouseSwitch = true;
        }

        [TestMethod]
        public void DisplayChangeMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesDisplayChangeMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.DisplayChangeMoveWindows); // check if value was initialized to false.

            // act
            viewModel.DisplayChangeMoveWindows = true;
        }

        [TestMethod]
        public void ZoneSetChangeMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesZoneSetChangeMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ZoneSetChangeMoveWindows); // check if value was initialized to false.

            // act
            viewModel.ZoneSetChangeMoveWindows = true;
        }

        [TestMethod]
        public void AppLastZoneMoveWindows_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesAppLastZoneMoveWindows.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.AppLastZoneMoveWindows); // check if value was initialized to false.

            // act
            viewModel.AppLastZoneMoveWindows = true;
        }

        public void OpenWindowOnActiveMonitor_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesOpenWindowOnActiveMonitor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.OpenWindowOnActiveMonitor); // check if value was initialized to false.

            // act
            viewModel.OpenWindowOnActiveMonitor = true;
        }

        [TestMethod]
        public void RestoreSize_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesRestoreSize.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.RestoreSize); // check if value was initialized to false.

            // act
            viewModel.RestoreSize = true;
        }

        [TestMethod]
        public void UseCursorPosEditorStartupScreen_ShouldSetValue2False_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.UseCursorposEditorStartupscreen.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsTrue(viewModel.UseCursorPosEditorStartupScreen); // check if value was initialized to false.

            // act
            viewModel.UseCursorPosEditorStartupScreen = true;
        }

        [TestMethod]
        public void ShowOnAllMonitors_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.FancyZones.Properties.FancyzonesShowOnAllMonitors.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.IsFalse(viewModel.ShowOnAllMonitors); // check if value was initialized to false.

            // act
            viewModel.ShowOnAllMonitors = true;
        }

        [TestMethod]
        public void ZoneHighlightColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesZoneHighlightColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesZoneHighlightColor, viewModel.ZoneHighlightColor);

            // act
            viewModel.ZoneHighlightColor = "#FFFFFF";
        }

        [TestMethod]
        public void ZoneBorderColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesBorderColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyzonesBorderColor, viewModel.ZoneBorderColor);

            // act
            viewModel.ZoneBorderColor = "#FFFFFF";
        }

        [TestMethod]
        public void ZoneInActiveColor_ShouldSetColorValue2White_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("#FFFFFF", snd.Powertoys.FancyZones.Properties.FancyzonesInActiveColor.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(ConfigDefaults.DefaultFancyZonesInActiveColor, viewModel.ZoneInActiveColor);

            // act
            viewModel.ZoneInActiveColor = "#FFFFFF";
        }

        [TestMethod]
        public void ExcludedApps_ShouldSetColorValue2White_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual("Sample", snd.Powertoys.FancyZones.Properties.FancyzonesExcludedApps.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(string.Empty, viewModel.ExcludedApps);

            // act
            viewModel.ExcludedApps = "Sample";
        }

        [TestMethod]
        public void HighlightOpacity_ShouldSetOpacityValueTo60_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                FancyZonesSettingsIPCMessage snd = JsonSerializer.Deserialize<FancyZonesSettingsIPCMessage>(msg);
                Assert.AreEqual(60, snd.Powertoys.FancyZones.Properties.FancyzonesHighlightOpacity.Value);
                return 0;
            };

            // arrange
            FancyZonesViewModel viewModel = new FancyZonesViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, FancyZonesTestFolderName);
            Assert.AreEqual(50, viewModel.HighlightOpacity);

            // act
            viewModel.HighlightOpacity = 60;
        }

        private string ToRGBHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }
    }
}
