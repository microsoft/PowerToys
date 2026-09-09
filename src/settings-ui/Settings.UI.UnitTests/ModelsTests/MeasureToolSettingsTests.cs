// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class MeasureToolSettingsTests
    {
        [TestMethod]
        public void DefaultMeasurementUnitIsPixels()
        {
            var settings = new MeasureToolSettings();

            Assert.AreEqual(0, settings.Properties.UnitsOfMeasure.Value);
        }

        [TestMethod]
        [DataRow(0, "Pixels")]
        [DataRow(1, "Inches")]
        [DataRow(2, "Centimeters")]
        [DataRow(3, "Millimeters")]
        [DataRow(4, "Display-independent pixels")]
        public void MeasurementUnitRoundTripPreservesStableValue(int unitValue, string unitName)
        {
            var settings = new MeasureToolSettings();
            settings.Properties.UnitsOfMeasure.Value = unitValue;

            var deserialized = JsonSerializer.Deserialize<MeasureToolSettings>(settings.ToJsonString());

            Assert.IsNotNull(deserialized, $"Failed to deserialize {unitName} settings.");
            Assert.AreEqual(unitValue, deserialized.Properties.UnitsOfMeasure.Value, $"{unitName} value changed during serialization.");
        }
    }
}
