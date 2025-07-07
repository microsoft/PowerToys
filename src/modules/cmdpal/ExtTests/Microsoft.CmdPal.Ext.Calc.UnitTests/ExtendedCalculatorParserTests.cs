// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

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

        [DataTestMethod]
        [DataRow("0X78AD+0o123", true)]
        [DataRow("0o9", false)]
        public void InputValid_TestValid_WhenCalled(string input, bool valid)
        {
            // Act
            var result = CalculateHelper.InputValid(input);

            // Assert
            Assert.AreEqual(valid, result);
        }
    }
