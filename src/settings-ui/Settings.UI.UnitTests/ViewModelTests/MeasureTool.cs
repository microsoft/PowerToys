// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Settings.UI.Library.Enumerations;

namespace ViewModelTests
{
    [TestClass]
    public class MeasureTool
    {
        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(9)]
        [DataRow(int.MaxValue)]
        public void ToolbarPosition_ConstructorNormalizesAndPersistsInvalidStoredValue(int invalidValue)
        {
            var viewModel = CreateViewModel(invalidValue, out var settings, out var settingsUtils);

            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, viewModel.ToolbarPosition);
            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, settings.Properties.ToolbarPosition.Value);
            VerifyPersistedTopCenter(settingsUtils);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(9)]
        [DataRow(int.MaxValue)]
        public void ToolbarPosition_GetterNeverReturnsInvalidValue(int invalidValue)
        {
            var viewModel = CreateViewModel(
                (int)MeasureToolToolbarPosition.TopCenter,
                out var settings,
                out _);
            settings.Properties.ToolbarPosition.Value = invalidValue;

            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, viewModel.ToolbarPosition);
            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, settings.Properties.ToolbarPosition.Value);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(6)]
        [DataRow(9)]
        [DataRow(int.MaxValue)]
        public void ToolbarPosition_SetterNormalizesAndPersistsInvalidValue(int invalidValue)
        {
            var viewModel = CreateViewModel(
                (int)MeasureToolToolbarPosition.TopCenter,
                out var settings,
                out var settingsUtils);

            viewModel.ToolbarPosition = invalidValue;

            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, viewModel.ToolbarPosition);
            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, settings.Properties.ToolbarPosition.Value);
            VerifyPersistedTopCenter(settingsUtils);
        }

        [DataTestMethod]
        [DataRow(0, MeasureToolToolbarPosition.TopLeft)]
        [DataRow(1, MeasureToolToolbarPosition.TopCenter)]
        [DataRow(2, MeasureToolToolbarPosition.TopRight)]
        [DataRow(3, MeasureToolToolbarPosition.BottomLeft)]
        [DataRow(4, MeasureToolToolbarPosition.BottomCenter)]
        [DataRow(5, MeasureToolToolbarPosition.BottomRight)]
        public void ToolbarPosition_SetterMapsSelectedIndexToPersistedValue(int selectedIndex, MeasureToolToolbarPosition expectedPosition)
        {
            MeasureToolToolbarPosition initialPosition = expectedPosition == MeasureToolToolbarPosition.TopCenter
                ? MeasureToolToolbarPosition.TopLeft
                : MeasureToolToolbarPosition.TopCenter;
            var viewModel = CreateViewModel(
                (int)initialPosition,
                out var settings,
                out var settingsUtils);

            viewModel.ToolbarPosition = selectedIndex;

            Assert.AreEqual(selectedIndex, viewModel.ToolbarPosition);
            Assert.AreEqual((int)expectedPosition, settings.Properties.ToolbarPosition.Value);
            settingsUtils.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => HasToolbarPosition(json, expectedPosition)),
                    MeasureToolSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(4)]
        [DataRow(int.MaxValue)]
        public void UnitsOfMeasure_GetterFallsBackWithoutOverwritingUnsupportedStoredValue(int unsupportedValue)
        {
            var viewModel = CreateViewModel(
                (int)MeasureToolToolbarPosition.TopCenter,
                out var settings,
                out var settingsUtils,
                unitsOfMeasure: unsupportedValue);

            Assert.AreEqual(0, viewModel.UnitsOfMeasure);
            Assert.AreEqual(unsupportedValue, settings.Properties.UnitsOfMeasure.Value);
            settingsUtils.Verify(
                x => x.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(4)]
        [DataRow(int.MaxValue)]
        public void UnitsOfMeasure_SetterNormalizesAndPersistsUnsupportedValue(int unsupportedValue)
        {
            var viewModel = CreateViewModel(
                (int)MeasureToolToolbarPosition.TopCenter,
                out var settings,
                out var settingsUtils,
                unitsOfMeasure: 1);

            viewModel.UnitsOfMeasure = unsupportedValue;

            Assert.AreEqual(0, viewModel.UnitsOfMeasure);
            Assert.AreEqual(0, settings.Properties.UnitsOfMeasure.Value);
            settingsUtils.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => HasUnitsOfMeasure(json, 0)),
                    MeasureToolSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(5)]
        [DataRow(int.MaxValue)]
        public void DefaultMeasureStyle_GetterFallsBackWithoutOverwritingUnsupportedStoredValue(int unsupportedValue)
        {
            var viewModel = CreateViewModel(
                (int)MeasureToolToolbarPosition.TopCenter,
                out var settings,
                out var settingsUtils,
                defaultMeasureStyle: unsupportedValue);

            Assert.AreEqual(0, viewModel.DefaultMeasureStyle);
            Assert.AreEqual(unsupportedValue, settings.Properties.DefaultMeasureStyle.Value);
            settingsUtils.Verify(
                x => x.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        private static MeasureToolViewModel CreateViewModel(
            int toolbarPosition,
            out MeasureToolSettings settings,
            out Mock<SettingsUtils> settingsUtils,
            int unitsOfMeasure = 0,
            int defaultMeasureStyle = 0)
        {
            settings = new MeasureToolSettings();
            settings.Properties.ToolbarPosition.Value = toolbarPosition;
            settings.Properties.UnitsOfMeasure.Value = unitsOfMeasure;
            settings.Properties.DefaultMeasureStyle.Value = defaultMeasureStyle;

            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(x => x.SettingsConfig).Returns(new GeneralSettings());

            var measureToolSettingsRepository = new Mock<ISettingsRepository<MeasureToolSettings>>();
            measureToolSettingsRepository.SetupGet(x => x.SettingsConfig).Returns(settings);

            settingsUtils = new Mock<SettingsUtils>(new FileSystem(), null);

            return new MeasureToolViewModel(
                settingsUtils.Object,
                generalSettingsRepository.Object,
                measureToolSettingsRepository.Object,
                _ => 0);
        }

        private static void VerifyPersistedTopCenter(Mock<SettingsUtils> settingsUtils)
        {
            settingsUtils.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => HasToolbarPosition(json, MeasureToolToolbarPosition.TopCenter)),
                    MeasureToolSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        private static bool HasToolbarPosition(string json, MeasureToolToolbarPosition expected)
        {
            var settings = JsonSerializer.Deserialize<MeasureToolSettings>(json);
            return settings?.Properties.ToolbarPosition.Value == (int)expected;
        }

        private static bool HasUnitsOfMeasure(string json, int expected)
        {
            var settings = JsonSerializer.Deserialize<MeasureToolSettings>(json);
            return settings?.Properties.UnitsOfMeasure.Value == expected;
        }
    }
}
