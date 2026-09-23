// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
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
        [DataRow(4, 4)]
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
            measureToolSettings.Properties.UnitsOfMeasure.Value = 8;

            var measureToolSettingsRepository = new Mock<ISettingsRepository<MeasureToolSettings>>();
            measureToolSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(measureToolSettings);

            var viewModel = new MeasureToolViewModel(
                settingsUtils.Object,
                generalSettingsRepository.Object,
                measureToolSettingsRepository.Object,
                _ => 0);

            // Confirm that the legacy value was translated to a valid index and
            // persisted successfully as millimeters (index 3).
            Assert.AreEqual(3, viewModel.UnitsOfMeasure);
            Assert.AreEqual(3, measureToolSettings.Properties.UnitsOfMeasure.Value);
            settingsUtils.Verify(
                utils => utils.SaveSettings(It.IsAny<string>(), MeasureToolSettings.ModuleName, SettingsUtils.DefaultFileName),
                Times.Once);
        }

        [TestMethod]
        public void MaximumUnitsOfMeasureIndexMatchesComboBoxItemCount()
        {
            // Find the repo root by walking up from the test base directory
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PowerToys.slnx")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate PowerToys repository root.");

            var xamlPath = Path.Combine(
                dir.FullName,
                @"src\settings-ui\Settings.UI\SettingsXAML\Views\MeasureToolPage.xaml");

            Assert.IsTrue(File.Exists(xamlPath), $"Could not find XAML file at {xamlPath}");

            var doc = XDocument.Load(xamlPath);

            var comboBox = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ComboBox" &&
                    e.Attributes().Any(a => a.Name.LocalName == "AutomationProperties.AutomationId" &&
                                            a.Value == "ComboBox_ScreenRuler_UnitsOfMeasure"));

            Assert.IsNotNull(comboBox, "ComboBox_ScreenRuler_UnitsOfMeasure was not found in MeasureToolPage.xaml");

            var itemCount = comboBox.Elements().Count(e => e.Name.LocalName == "ComboBoxItem");

            Assert.AreEqual(
                MeasureToolViewModel.MaximumUnitsOfMeasureIndex,
                itemCount - 1,
                "MaximumUnitsOfMeasureIndex must match the highest selectable ComboBoxItem index.");
        }
    }
}
