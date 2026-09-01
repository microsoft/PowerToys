// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class MeasureTool
    {
        [TestMethod]
        [DataRow(-1, 0)]
        [DataRow(0, 0)]
        [DataRow(3, 3)]
        [DataRow(4, 2)]
        [DataRow(8, 3)]
        public void NormalizeUnitsOfMeasureIndexReturnsValidComboBoxIndex(int value, int expected)
        {
            Assert.AreEqual(expected, MeasureToolViewModel.NormalizeUnitsOfMeasureIndex(value));
        }

        [TestMethod]
        public void ConstructorRepairsInvalidPersistedUnitsOfMeasureIndex()
        {
            var settingsUtils = new Mock<SettingsUtils>(new FileSystem(), null);
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(new GeneralSettings());

            var measureToolSettings = new MeasureToolSettings();
            measureToolSettings.Properties.UnitsOfMeasure.Value = 4;

            var measureToolSettingsRepository = new Mock<ISettingsRepository<MeasureToolSettings>>();
            measureToolSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(measureToolSettings);

            var viewModel = new MeasureToolViewModel(
                settingsUtils.Object,
                generalSettingsRepository.Object,
                measureToolSettingsRepository.Object,
                _ => 0);

            Assert.AreEqual(2, viewModel.UnitsOfMeasure);
            Assert.AreEqual(2, measureToolSettings.Properties.UnitsOfMeasure.Value);
            settingsUtils.Verify(
                utils => utils.SaveSettings(It.IsAny<string>(), MeasureToolSettings.ModuleName, SettingsUtils.DefaultFileName),
                Times.Once);
        }
    }
}
