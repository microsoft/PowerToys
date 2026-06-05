// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class MeasureToolSettingsTests
    {
        [TestMethod]
        public void Deserialization_FromPartialJson_PreservesProvidedValues()
        {
            const string json = "{\"ContinuousCapture\":{\"value\":true},\"PixelTolerance\":{\"value\":50}}";

            var deserialized = JsonSerializer.Deserialize<MeasureToolProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.ContinuousCapture);
            Assert.AreEqual(50, deserialized.PixelTolerance.Value);
            Assert.IsTrue(deserialized.DrawFeetOnCross);
            Assert.AreEqual(0, deserialized.UnitsOfMeasure.Value);
            Assert.AreEqual("#FF4500", deserialized.MeasureCrossColor.Value);
        }
    }
}
