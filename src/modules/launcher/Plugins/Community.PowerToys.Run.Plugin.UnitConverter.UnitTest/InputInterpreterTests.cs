using System.Globalization;
using NUnit.Framework;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestFixture]
    public class DegreePrefixingTests {
        [TestCase(new string[] {"1,5'"}, new string[] { "1,5", "'"})]
        [TestCase(new string[] { "1.5'" }, new string[] { "1.5", "'" })]
        [TestCase(new string[] { "1'" }, new string[] { "1", "'" })]
        [TestCase(new string[] { "1'5\"" }, new string[] { "1", "'", "5", "\"" })]
        [TestCase(new string[] { "5\"" }, new string[] { "5", "\"" })]
        [TestCase(new string[] { "1'5" }, new string[] { "1", "'", "5"})]
        public void RegexSplitsInput(string[] input, string[] expectedResult) {
            string[] shortsplit = InputInterpreter.RegexSplitter(ref input);
            Assert.AreEqual(expectedResult, shortsplit);
        }

        [TestCase(new string[] { "1'", "in", "cm" }, new string[] {"1", "foot", "in", "cm" })]
        [TestCase(new string[] { "1\"", "in", "cm" }, new string[] { "1", "inch", "in", "cm" })]
        [TestCase(new string[] { "1'6", "in", "cm" }, new string[] { "1,5", "foot", "in", "cm" })]
        [TestCase(new string[] { "1'6\"", "in", "cm" }, new string[] { "1,5", "foot", "in", "cm" })]
        public void HandlesShorthandFeetInchNotation(string[] input, string[] expectedResult) {
            InputInterpreter.ShorthandFeetInchHandler(ref input, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, input);
        }

        [TestCase(new string[] { "5", "celsius", "in", "fahrenheit" }, new string[] { "5", "degreeCelsius", "in", "degreeFahrenheit" })]
        [TestCase(new string[] { "5", "fahrenheit", "in", "celsius" }, new string[] { "5", "degreeFahrenheit", "in", "degreeCelsius" })]
        [TestCase(new string[] { "5", "c", "in", "f" }, new string[] { "5", "°c", "in", "°f" })]
        [TestCase(new string[] { "5", "f", "in", "c" }, new string[] { "5", "°f", "in", "°c" })]
        public void PrefixesDegrees(string[] input, string[] expectedResult) {
            InputInterpreter.DegreePrefixer(ref input);
            Assert.AreEqual(expectedResult, input);
        }
    }
}