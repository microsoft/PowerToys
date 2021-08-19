// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestClass]
    public class UnitHandlerTests
    {
        [TestMethod]
        public void HandleTemperature()
        {
            var convertModel = new ConvertModel(1, "DegreeCelsius", "DegreeFahrenheit");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Temperature);
            Assert.AreEqual(33.79999999999999d, result);
        }

        [TestMethod]
        public void HandleLength()
        {
            var convertModel = new ConvertModel(1, "meter", "centimeter");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Length);
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void HandlesByteCapitals()
        {
            var convertModel = new ConvertModel(1, "kB", "kb");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Information);
            Assert.AreEqual(8, result);
        }

        [TestMethod]
        public void HandleInvalidModel()
        {
            var convertModel = new ConvertModel(1, "aa", "bb");
            var results = UnitHandler.Convert(convertModel);
            Assert.AreEqual(0, results.Count());
        }
    }
}
