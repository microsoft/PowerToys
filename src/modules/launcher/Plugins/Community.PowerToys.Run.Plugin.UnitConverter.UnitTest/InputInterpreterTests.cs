using System.Globalization;
using NUnit.Framework;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestFixture]
    public class InputInterpreterTests
    {
        [TestCase(new string[] { "1,5'" }, new string[] { "1,5", "'" })]
        [TestCase(new string[] { "1.5'" }, new string[] { "1.5", "'" })]
        [TestCase(new string[] { "1'" }, new string[] { "1", "'" })]
        [TestCase(new string[] { "1'5\"" }, new string[] { "1", "'", "5", "\"" })]
        [TestCase(new string[] { "5\"" }, new string[] { "5", "\"" })]
        [TestCase(new string[] { "1'5" }, new string[] { "1", "'", "5" })]
        public void RegexSplitsInput(string[] input, string[] expectedResult)
        {
            string[] shortsplit = InputInterpreter.RegexSplitter(input);
            Assert.AreEqual(expectedResult, shortsplit);
        }

        [TestCase(new string[] { "1cm", "to", "mm" }, new string[] { "1", "cm", "to", "mm" })]
        public void InsertsSpaces(string[] input, string[] expectedResult)
        {
            InputInterpreter.InputSpaceInserter(ref input);
            Assert.AreEqual(expectedResult, input);
        }

        [TestCase(new string[] { "1'", "in", "cm" }, new string[] { "1", "foot", "in", "cm" })]
        [TestCase(new string[] { "1\"", "in", "cm" }, new string[] { "1", "inch", "in", "cm" })]
        [TestCase(new string[] { "1'6", "in", "cm" }, new string[] { "1.5", "foot", "in", "cm" })]
        [TestCase(new string[] { "1'6\"", "in", "cm" }, new string[] { "1.5", "foot", "in", "cm" })]
        public void HandlesShorthandFeetInchNotation(string[] input, string[] expectedResult)
        {
            InputInterpreter.ShorthandFeetInchHandler(ref input, CultureInfo.InvariantCulture);
            Assert.AreEqual(expectedResult, input);
        }

        [TestCase(new string[] { "5", "CeLsIuS", "in", "faHrenheiT" }, new string[] { "5", "DegreeCelsius", "in", "DegreeFahrenheit" })]
        [TestCase(new string[] { "5", "f", "in", "celsius" }, new string[] { "5", "°f", "in", "DegreeCelsius" })]
        [TestCase(new string[] { "5", "c", "in", "f" }, new string[] { "5", "°c", "in", "°f" })]
        [TestCase(new string[] { "5", "f", "in", "c" }, new string[] { "5", "°f", "in", "°c" })]
        public void PrefixesDegrees(string[] input, string[] expectedResult)
        {
            InputInterpreter.DegreePrefixer(ref input);
            Assert.AreEqual(expectedResult, input);
        }

        [TestCase("a f in c")]
        [TestCase("12 f in")]
        public void ParseInvalidQueries(string queryString)
        {
            Query query = new Query(queryString);
            var result = InputInterpreter.Parse(query);
            Assert.AreEqual(null, result);
        }

        [TestCase("12 f in c", 12)]
        [TestCase("10m to cm", 10)]
        public void ParseValidQueries(string queryString, double result)
        {
            Query query = new Query(queryString);
            var convertModel = InputInterpreter.Parse(query);
            Assert.AreEqual(result, convertModel.Value);
        }
    }
}
