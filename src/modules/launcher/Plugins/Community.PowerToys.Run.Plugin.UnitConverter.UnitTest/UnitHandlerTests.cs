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
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.Temperature.Info);
            Assert.AreEqual(33.79999999999999d, result);
        }

        [TestMethod]
        public void HandleLength()
        {
            var convertModel = new ConvertModel(1, "meter", "centimeter");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.Length.Info);
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void HandleNanometerToKilometer()
        {
            var convertModel = new ConvertModel(1, "nanometer", "kilometer");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.Length.Info);
            Assert.AreEqual(1E-12, result);
        }

        [TestMethod]
        public void HandlePlurals()
        {
            var convertModel = new ConvertModel(1, "meters", "centimeters");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.Length.Info);
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void HandlesByteCapitals()
        {
            var convertModel = new ConvertModel(1, "kB", "kb");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.Information.Info);
            Assert.AreEqual(8, result);
        }

        [TestMethod]
        public void HandlesParsecToNanometer()
        {
            var convertModel = new ConvertModel(1, "parsec", "nanometer");
            var result = UnitHandler.Convert(convertModel).Single();
            var str = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(3.08567758128E+25, result.ConvertedValue);
            Assert.AreEqual("3.08567758128E+25 nanometer", str);
        }

        [TestMethod]
        public void HandlesNanometerToParsec()
        {
            var convertModel = new ConvertModel(1, "nanometer", "parsec");
            var result = UnitHandler.Convert(convertModel).Single();
            var str = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(3.240779289666357E-26, result.ConvertedValue);
            Assert.AreEqual("3.2407792896664E-26 parsec", str);
        }

        [TestMethod]
        public void HandleInvalidModel()
        {
            var convertModel = new ConvertModel(1, "aa", "bb");
            var results = UnitHandler.Convert(convertModel);
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void RoundNormalValue()
        {
            var convertModel = new ConvertModel(3.14159265358979323, "stone", "kg");
            var result = UnitHandler.Convert(convertModel).Single();
            var str = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual("19.950018128979… kg", str);
        }
    }
}
