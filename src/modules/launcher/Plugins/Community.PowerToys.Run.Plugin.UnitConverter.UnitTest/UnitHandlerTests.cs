using System.Globalization;
using NUnit.Framework;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestFixture]
    public class UnitHandlerTests {
        [TestCase(new string[] { "1", "meter", "in", "centimeter" }, UnitsNet.QuantityType.Length, UnitHandler.Abbreviated.Neither)]
        [TestCase(new string[] { "1", "m", "in", "cm" }, UnitsNet.QuantityType.Length, UnitHandler.Abbreviated.Both)]
        [TestCase(new string[] { "1", "m", "in", "centimeter" }, UnitsNet.QuantityType.Length, UnitHandler.Abbreviated.First)]
        [TestCase(new string[] { "1", "meter", "in", "cm" }, UnitsNet.QuantityType.Length, UnitHandler.Abbreviated.Second)]
        public void ParsesInputForAbbreviations(string[] input, UnitsNet.QuantityType qType, UnitHandler.Abbreviated expectedResult) {
            (UnitHandler.Abbreviated abbreviated, UnitsNet.QuantityInfo quantityInfo) result = UnitHandler.ParseInputForAbbreviation(input, qType);
            Assert.AreEqual(expectedResult, result.abbreviated);
        }

        [TestCase(new string[] { "1", "meter", "in", "centimeter" }, 100d)]
        public void ConvertsInput(string[] input, double expectedResult) {
            double result = UnitHandler.ConvertInput(input, UnitsNet.QuantityType.Length, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, result);
        }

        [TestCase(new string[] { "1", "kB", "in", "kb" }, 8)]
        public void HandlesByteCapitals(string[] input, double expectedResult) { 
            double result = UnitHandler.ConvertInput(input, UnitsNet.QuantityType.Information, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, result);
        }
    }
}