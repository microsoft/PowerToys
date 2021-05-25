using System.Globalization;
using NUnit.Framework;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestFixture]
    public class UnitHandlerTests
    {
        [TestCase(new string[] { "1", "meter", "in", "centimeter" }, UnitsNet.QuantityType.Length, 100d)]
        [TestCase(new string[] { "1", "DegreeCelsius", "in", "DegreeFahrenheit" }, UnitsNet.QuantityType.Temperature, 33.79999999999999d)]
        public void ConvertsInput(string[] input, UnitsNet.QuantityType unit, double expectedResult)
        {
            double result = UnitHandler.ConvertInput(input, unit, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, result);
        }

        [TestCase(new string[] { "1", "kB", "in", "kb" }, 8)]
        public void HandlesByteCapitals(string[] input, double expectedResult)
        {
            double result = UnitHandler.ConvertInput(input, UnitsNet.QuantityType.Information, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, result);
        }
    }
}
