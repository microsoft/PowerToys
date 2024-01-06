// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.UnitConverter.UnitTest
{
    [TestClass]
    public class InputInterpreterTests
    {
#pragma warning disable CA1861 // Avoid constant arrays as arguments
        [DataTestMethod]
        [DataRow(new string[] { "1,5'" }, new string[] { "1,5", "'" })]
        [DataRow(new string[] { "1.5'" }, new string[] { "1.5", "'" })]
        [DataRow(new string[] { "1'" }, new string[] { "1", "'" })]
        [DataRow(new string[] { "1'5\"" }, new string[] { "1", "'", "5", "\"" })]
        [DataRow(new string[] { "5\"" }, new string[] { "5", "\"" })]
        [DataRow(new string[] { "1'5" }, new string[] { "1", "'", "5" })]
        [DataRow(new string[] { "-1,5'" }, new string[] { "-1,5", "'" })]
        [DataRow(new string[] { "-1.5'" }, new string[] { "-1.5", "'" })]
        [DataRow(new string[] { "-1'" }, new string[] { "-1", "'" })]
        [DataRow(new string[] { "-1'5\"" }, new string[] { "-1", "'", "5", "\"" })]
        [DataRow(new string[] { "-5\"" }, new string[] { "-5", "\"" })]
        [DataRow(new string[] { "-1'5" }, new string[] { "-1", "'", "5" })]
        public void RegexSplitsInput(string[] input, string[] expectedResult)
        {
            string[] shortsplit = InputInterpreter.RegexSplitter(input);
            CollectionAssert.AreEqual(expectedResult, shortsplit);
        }

        [DataTestMethod]
        [DataRow(new string[] { "1cm", "to", "mm" }, new string[] { "1", "cm", "to", "mm" })]
        [DataRow(new string[] { "-1cm", "to", "mm" }, new string[] { "-1", "cm", "to", "mm" })]
        public void InsertsSpaces(string[] input, string[] expectedResult)
        {
            InputInterpreter.InputSpaceInserter(ref input);
            CollectionAssert.AreEqual(expectedResult, input);
        }

        [DataTestMethod]
        [DataRow(new string[] { "1'", "in", "cm" }, new string[] { "1", "foot", "in", "cm" })]
        [DataRow(new string[] { "1\"", "in", "cm" }, new string[] { "1", "inch", "in", "cm" })]
        [DataRow(new string[] { "1'6", "in", "cm" }, new string[] { "1.5", "foot", "in", "cm" })]
        [DataRow(new string[] { "1'6\"", "in", "cm" }, new string[] { "1.5", "foot", "in", "cm" })]
        [DataRow(new string[] { "-1'", "in", "cm" }, new string[] { "-1", "foot", "in", "cm" })]
        [DataRow(new string[] { "-1\"", "in", "cm" }, new string[] { "-1", "inch", "in", "cm" })]
        [DataRow(new string[] { "-1'6", "in", "cm" }, new string[] { "-1.5", "foot", "in", "cm" })]
        [DataRow(new string[] { "-1'6\"", "in", "cm" }, new string[] { "-1.5", "foot", "in", "cm" })]
        public void HandlesShorthandFeetInchNotation(string[] input, string[] expectedResult)
        {
            InputInterpreter.ShorthandFeetInchHandler(ref input, CultureInfo.InvariantCulture);
            CollectionAssert.AreEqual(expectedResult, input);
        }

        [DataTestMethod]
        [DataRow(new string[] { "1", "metre", "in", "metre" }, new string[] { "1", "meter", "in", "meter" })]
        [DataRow(new string[] { "1", "centimetre", "in", "kilometre" }, new string[] { "1", "centimeter", "in", "kilometer" })]
        [DataRow(new string[] { "1", "metres", "in", "kilometres" }, new string[] { "1", "meters", "in", "kilometers" })]
        public void HandlesMetreVsMeterNotation(string[] input, string[] expectedResult)
        {
            InputInterpreter.MetreToMeter(ref input);
            CollectionAssert.AreEqual(expectedResult, input);
        }

        [DataTestMethod]
        [DataRow(new string[] { "5", "CeLsIuS", "in", "faHrenheiT" }, new string[] { "5", "DegreeCelsius", "in", "DegreeFahrenheit" })]
        [DataRow(new string[] { "5", "f", "in", "celsius" }, new string[] { "5", "°f", "in", "DegreeCelsius" })]
        [DataRow(new string[] { "5", "c", "in", "f" }, new string[] { "5", "°c", "in", "°f" })]
        [DataRow(new string[] { "5", "f", "in", "c" }, new string[] { "5", "°f", "in", "°c" })]
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        public void PrefixesDegrees(string[] input, string[] expectedResult)
        {
            InputInterpreter.DegreePrefixer(ref input);
            CollectionAssert.AreEqual(expectedResult, input);
        }

        [DataTestMethod]
        [DataRow("a f in c")]
        [DataRow("12 f in")]
        [DataRow("1-2 f in c")]
        [DataRow("12- f in c")]
        public void ParseInvalidQueries(string queryString)
        {
            Query query = new Query(queryString);
            var result = InputInterpreter.Parse(query);
            Assert.AreEqual(null, result);
        }

        [DataTestMethod]
        [DataRow("12 f in c", 12)]
        [DataRow("10m to cm", 10)]
        [DataRow("-12 f in c", -12)]
        [DataRow("-10m to cm", -10)]
        public void ParseValidQueries(string queryString, double result)
        {
            Query query = new Query(queryString);
            var convertModel = InputInterpreter.Parse(query);
            Assert.AreEqual(result, convertModel.Value);
        }
    }
}
