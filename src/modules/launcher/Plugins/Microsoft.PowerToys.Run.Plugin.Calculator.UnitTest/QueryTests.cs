// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    public class QueryTests
    {
        [DataTestMethod]
        [DataRow("=pi(9+)", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=pi(9)", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=pi,", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=log()", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=0xf0x6", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=0xf,0x6", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=2^96", "Result value was either too large or too small for a decimal number")]
        [DataRow("=+()", "Calculation result is not a valid number (NaN)")]
        [DataRow("=[10,10]", "Unsupported use of square brackets")]
        public void ErrorResultOnInvalidKeywordQuery(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "=");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("pi(9+)")]
        [DataRow("pi(9)")]
        [DataRow("pi,")]
        [DataRow("log()")]
        [DataRow("0xf0x6")]
        [DataRow("0xf,0x6")]
        [DataRow("2^96")]
        [DataRow("+()")]
        [DataRow("[10,10]")]
        public void NoResultOnInvalidGlobalQuery(string typedString)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString);

            // Act
            var result = main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(result, 0);
        }
    }
}
