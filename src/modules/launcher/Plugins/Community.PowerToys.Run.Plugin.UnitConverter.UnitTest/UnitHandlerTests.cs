using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestFixture]
    public class UnitHandlerTests
    {
        [Test]
        public void HandleTemperature()
        {
            var convertModel = new ConvertModel(1, "DegreeCelsius", "DegreeFahrenheit");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Temperature);
            Assert.AreEqual(33.79999999999999d, result);
        }

        [Test]
        public void HandleLength()
        {
            var convertModel = new ConvertModel(1, "meter", "centimeter");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Length);
            Assert.AreEqual(100, result);
        }

        [Test]
        public void HandlesByteCapitals()
        {
            var convertModel = new ConvertModel(1, "kB", "kb");
            double result = UnitHandler.ConvertInput(convertModel, UnitsNet.QuantityType.Information);
            Assert.AreEqual(8, result);
        }

        [Test]
        public void HandleInvalidModel()
        {
            var convertModel = new ConvertModel(1, "aa", "bb");
            var results = UnitHandler.Convert(convertModel);
            Assert.AreEqual(0, results.Count());
        }
    }
}
