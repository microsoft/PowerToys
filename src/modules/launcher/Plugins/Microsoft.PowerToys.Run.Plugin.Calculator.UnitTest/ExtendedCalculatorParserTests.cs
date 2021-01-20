// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using NUnit.Framework;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestFixture]
    public class ExtendedCalculatorParserTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void InputValid_ThrowError_WhenCalledNullOrEmpty(string input)
        {
            // Act
            Assert.Catch<ArgumentNullException>(() => CalculateHelper.InputValid(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Interpret_ThrowError_WhenCalledNullOrEmpty(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            Assert.Catch<ArgumentNullException>(() => engine.Interpret(input));
        }

        [TestCase("42")]
        [TestCase("test")]
        [TestCase("pi(2)")] // Incorrect input, constant is being treated as a function.
        [TestCase("e(2)")]
        public void Interpret_NoResult_WhenCalled(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            var result = engine.Interpret(input);

            // Assert
            Assert.AreEqual(default(CalculateResult), result);
        }

        [TestCase("2 * 2", 4D)]
        [TestCase("-2 ^ 2", 4D)]
        [TestCase("-(2 ^ 2)", -4D)]
        [TestCase("2 * pi", 6.28318530717959D)]
        [TestCase("round(2 * pi)", 6D)]
        [TestCase("1 == 2", default(double))]
        [TestCase("pi * ( sin ( cos ( 2)))", -1.26995475603563D)]
        [TestCase("5.6/2", 2.8D)]
        [TestCase("123 * 4.56", 560.88D)]
        [TestCase("1 - 9.0 / 10", 0.1D)]
        [TestCase("0.5 * ((2*-395.2)+198.2)", -296.1D)]
        [TestCase("2+2.11", 4.11D)]
        [TestCase("8.43 + 4.43 - 12.86", 0D)]
        [TestCase("8.43 + 4.43 - 12.8", 0.06D)]
        [TestCase("exp(5)", 148.413159102577D)]
        [TestCase("e^5", 148.413159102577D)]
        [TestCase("e*2", 5.43656365691809D)]
        [TestCase("log(e)", 1D)]
        [TestCase("cosh(0)", 1D)]
        public void Interpret_NoErrors_WhenCalledWithRounding(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(CalculateEngine.Round(expectedResult), result.RoundedResult);
        }

        [TestCase("0.100000000000000000000", 0.00776627963145224D)] // BUG: Because data structure
        [TestCase("0.200000000000000000000000", 0.000000400752841041379D)] // BUG: Because data structure
        [TestCase("123 456", 56088D)] // BUG: Framework accepts ' ' as multiplication
        public void Interpret_QuirkOutput_WhenCalled(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }

        [TestCase("4.5/3", 1.5D, "nl-NL")]
        [TestCase("4.5/3", 1.5D, "en-EN")]
        [TestCase("4.5/3", 1.5D, "de-DE")]
        public void Interpret_DifferentCulture_WhenCalled(string input, decimal expectedResult, string cultureName)
        {
            // Arrange
            var cultureInfo = CultureInfo.GetCultureInfo(cultureName);
            var engine = new CalculateEngine();

            // Act
            var result = engine.Interpret(input, cultureInfo);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(CalculateEngine.Round(expectedResult), result.RoundedResult);
        }

        [TestCase("ceil(2 * (pi ^ 2))", true)]
        [TestCase("((1 * 2)", false)]
        [TestCase("(1 * 2)))", false)]
        [TestCase("abcde", false)]
        [TestCase("1 + 2 +", false)]
        [TestCase("1+2*", false)]
        [TestCase("1 && 3 &&", false)]
        [TestCase("sqrt( 36)", true)]
        [TestCase("max 4", false)]
        [TestCase("sin(0)", true)]
        [TestCase("sinh(1)", true)]
        [TestCase("tanh(0)", true)]
        [TestCase("artanh(pi/2)", true)]
        [TestCase("cosh", false)]
        [TestCase("cos", false)]
        [TestCase("abs", false)]
        [TestCase("1+1.1e3", true)]
        public void InputValid_TestValid_WhenCalled(string input, bool valid)
        {
            // Arrange

            // Act
            var result = CalculateHelper.InputValid(input);

            // Assert
            Assert.AreEqual(valid, result);
        }

        [TestCase("1-1")]
        [TestCase("sin(0)")]
        [TestCase("sinh(0)")]
        public void Interpret_MustReturnResult_WhenResultIsZero(string input)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0.0, result.Result);
        }

        [TestCase("factorial(5)", 120)]
        [TestCase("sign(-2)", -1)]
        [TestCase("sign(2)", +1)]
        [TestCase("abs(-2)", 2)]
        [TestCase("abs(2)", 2)]
        public void Interpret_MustReturnExpectedResult_WhenCalled(string input, decimal expectedResult)
        {
            // Arrange
            var engine = new CalculateEngine();

            // Act
            // Using InvariantCulture since this is internal
            var result = engine.Interpret(input, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result.Result);
        }
    }
}
