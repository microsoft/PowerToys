// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    public class ExtendedCalculatorParserTests
    {
        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void InputValid_ThrowError_WhenCalledNullOrEmpty(string input)
        {
            // Act
            Assert.ThrowsException<ArgumentNullException>(() => CalculateHelper.InputValid(input));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void Interpret_ThrowError_WhenCalledNullOrEmpty(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => engine.Interpret(input, CultureInfo.CurrentCulture, out _));
        }

        [DataTestMethod]
        [DataRow("test")]
        [DataRow("[10,10]")] // '[10,10]' is interpreted as array by mages engine
        public void Interpret_NoResult_WhenCalled(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            var result = engine.Interpret(input, CultureInfo.CurrentCulture, out _);

            // Assert
            Assert.AreEqual(default(CalculateResult), result);
        }

        private static IEnumerable<object[]> Interpret_NoErrors_WhenCalledWithRounding_Data =>
            new[]
            {
                new object[] { "2 * 2", 4M },
                new object[] { "-2 ^ 2", 4M },
                new object[] { "-(2 ^ 2)", -4M },
                new object[] { "2 * pi", 6.28318530717959M },
                new object[] { "round(2 * pi)", 6M },
                new object[] { "1 == 2", default(decimal) },
                new object[] { "pi * ( sin ( cos ( 2)))", -1.26995475603563M },
                new object[] { "5.6/2", 2.8M },
                new object[] { "123 * 4.56", 560.88M },
                new object[] { "1 - 9.0 / 10", 0.1M },
                new object[] { "0.5 * ((2*-395.2)+198.2)", -296.1M },
                new object[] { "2+2.11", 4.11M },
                new object[] { "8.43 + 4.43 - 12.86", 0M },
                new object[] { "8.43 + 4.43 - 12.8", 0.06M },
                new object[] { "exp(5)", 148.413159102577M },
                new object[] { "e^5", 148.413159102577M },
                new object[] { "e*2", 5.43656365691809M },
                new object[] { "ln(3)",  1.09861228866810M },
                new object[] { "log(3)", 0.47712125471966M },
                new object[] { "log2(3)", 1.58496250072116M },
                new object[] { "log10(3)", 0.47712125471966M },
                new object[] { "ln(e)", 1M },
                new object[] { "cosh(0)", 1M },
            };

        [DataTestMethod]
        [DynamicData(nameof(Interpret_NoErrors_WhenCalledWithRounding_Data))]
        public void Interpret_NoErrors_WhenCalledWithRounding(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture, out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(CalculateEngine.Round(expectedResult), result.RoundedResult);
        }

        private static IEnumerable<object[]> Interpret_QuirkOutput_WhenCalled_Data =>
            new[]
            {
                new object[] { "123 456", 56088M }, // BUG: Framework accepts ' ' as multiplication
            };

        [DynamicData(nameof(Interpret_QuirkOutput_WhenCalled_Data))]
        [DataTestMethod]
        public void Interpret_QuirkOutput_WhenCalled(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture, out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }

        private static IEnumerable<object[]> Interpret_GreaterPrecision_WhenCalled_Data =>
    new[]
    {
                new object[] { "0.100000000000000000000", 0.1M },
                new object[] { "0.200000000000000000000000", 0.2M },
    };

        [DynamicData(nameof(Interpret_GreaterPrecision_WhenCalled_Data))]
        [DataTestMethod]
        public void Interpret_GreaterPrecision_WhenCalled(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture, out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }

        private static IEnumerable<object[]> Interpret_DifferentCulture_WhenCalled_Data =>
            new[]
            {
                new object[] { "4.5/3", 1.5M, "nl-NL" },
                new object[] { "4.5/3", 1.5M, "en-EN" },
                new object[] { "4.5/3", 1.5M, "de-DE" },
            };

        [DataTestMethod]
        [DynamicData(nameof(Interpret_DifferentCulture_WhenCalled_Data))]
        public void Interpret_DifferentCulture_WhenCalled(string input, decimal expectedResult, string cultureName)
        {
            // Arrange
            var cultureInfo = CultureInfo.GetCultureInfo(cultureName);
            var engine = new CalculateEngine();

            // Act
            var result = engine.Interpret(input, cultureInfo, out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(CalculateEngine.Round(expectedResult), result.RoundedResult);
        }

        [DataTestMethod]
        [DataRow("log(3)", true)]
        [DataRow("ln(3)", true)]
        [DataRow("log2(3)", true)]
        [DataRow("log10(3)", true)]
        [DataRow("log2", false)]
        [DataRow("log10", false)]
        [DataRow("log", false)]
        [DataRow("ln", false)]
        [DataRow("ceil(2 * (pi ^ 2))", true)]
        [DataRow("((1 * 2)", false)]
        [DataRow("(1 * 2)))", false)]
        [DataRow("abcde", false)]
        [DataRow("1 + 2 +", false)]
        [DataRow("1+2*", false)]
        [DataRow("1+2/", false)]
        [DataRow("1+2%", false)]
        [DataRow("1 && 3 &&", false)]
        [DataRow("sqrt( 36)", true)]
        [DataRow("max 4", false)]
        [DataRow("sin(0)", true)]
        [DataRow("sinh(1)", true)]
        [DataRow("tanh(0)", true)]
        [DataRow("artanh(pi/2)", true)]
        [DataRow("cosh", false)]
        [DataRow("cos", false)]
        [DataRow("abs", false)]
        [DataRow("1+1.1e3", true)]
        public void InputValid_TestValid_WhenCalled(string input, bool valid)
        {
            // Act
            var result = CalculateHelper.InputValid(input);

            // Assert
            Assert.AreEqual(valid, result);
        }

        [DataTestMethod]
        [DataRow("1-1")]
        [DataRow("sin(0)")]
        [DataRow("sinh(0)")]
        public void Interpret_MustReturnResult_WhenResultIsZero(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture, out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0.0M, result.Result);
        }

        private static IEnumerable<object[]> Interpret_MustReturnExpectedResult_WhenCalled_Data =>
           new[]
           {
               new object[] { "factorial(5)", 120M },
               new object[] { "sign(-2)", -1M },
               new object[] { "sign(2)", +1M },
               new object[] { "abs(-2)", 2M },
               new object[] { "abs(2)", 2M },
               new object[] { "0+(1*2)/(0+1)", 2M }, // Validate that division by "(0+1)" is not interpret as division by zero.
               new object[] { "0+(1*2)/0.5", 4M }, // Validate that division by  number with decimal digits is not interpret as division by zero.
           };

        [DataTestMethod]
        [DynamicData(nameof(Interpret_MustReturnExpectedResult_WhenCalled_Data))]
        public void Interpret_MustReturnExpectedResult_WhenCalled(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using en-us culture to have a fixed number style
            var result = engine.Interpret(input, new CultureInfo("en-us", false), out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }

        private static IEnumerable<object[]> Interpret_TestScientificNotation_WhenCalled_Data =>
           new[]
           {
               new object[] { "0.2E1", "en-US", 2M },
               new object[] { "0,2E1", "pt-PT", 2M },
           };

        [DataTestMethod]
        [DynamicData(nameof(Interpret_TestScientificNotation_WhenCalled_Data))]
        public void Interpret_TestScientificNotation_WhenCalled(string input, string sourceCultureName, decimal expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo(sourceCultureName, false), new CultureInfo("en-US", false));
            var engine = new CalculateEngine();

            // Act
            // Using en-us culture to have a fixed number style
            var translatedInput = translator.Translate(input);
            var result = engine.Interpret(translatedInput, new CultureInfo("en-US", false), out _);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }
    }
}
