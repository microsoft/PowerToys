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
            Assert.AreEqual(3.0857000000000004E+25, result.ConvertedValue);
            Assert.AreEqual("3.0857e+25 nanometer", str);
        }

        [TestMethod]
        public void HandlesNanometerToParsec()
        {
            var convertModel = new ConvertModel(1, "nanometer", "parsec");
            var result = UnitHandler.Convert(convertModel).Single();
            var str = result.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(3.2408000000000005E-26, result.ConvertedValue);
            Assert.AreEqual("3.2408e-26 parsec", str);
        }

        [TestMethod]
        public void HandleInvalidModel()
        {
            var convertModel = new ConvertModel(1, "aa", "bb");
            var results = UnitHandler.Convert(convertModel);
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void RoundZero()
        {
            double result = UnitHandler.Round(0.0);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void RoundNormalValue()
        {
            double result = UnitHandler.Round(3.141592653589793);
            Assert.AreEqual(3.1416, result);
        }

        [TestMethod]
        public void RoundSmallValue()
        {
            double result = UnitHandler.Round(1.23456789012345E-16);
            Assert.AreEqual(1.2346E-16, result);
        }

        [TestMethod]
        public void RoundBigValue()
        {
            double result = UnitHandler.Round(1234567890123456.0);
            Assert.AreEqual(1234600000000000.0, result);
        }

        [TestMethod]
        public void RoundNegativeValue()
        {
            double result = UnitHandler.Round(-3.141592653589793);
            Assert.AreEqual(-3.1416, result);
        }

        [TestMethod]
        public void RoundNinesValue()
        {
            double result = UnitHandler.Round(999999999999.9998);
            Assert.AreEqual(1000000000000.0, result);
        }
    }
}
