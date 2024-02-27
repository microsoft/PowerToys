// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    public class QueryTests
    {
        private static Mock<Main> _main;
        private static Mock<IPublicAPI> _api;

        [TestInitialize]
        public void Initialize()
        {
            _api = new Mock<IPublicAPI>();
            _api.Setup(api => api.ChangeQuery(It.IsAny<string>(), It.IsAny<bool>())).Verifiable();

            _main = new Mock<Main>();
            _main.Object.Init(new PluginInitContext() { API = _api.Object });
        }

        [DataTestMethod]
        [DataRow("=pi(9+)", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=pi,", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=log()", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=0xf0x6", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=0xf,0x6", "Expression wrong or incomplete (Did you forget some parentheses?)")]
        [DataRow("=2^96", "Result value was either too large or too small for a decimal number")]
        [DataRow("=+()", "Calculation result is not a valid number (NaN)")]
        [DataRow("=[10,10]", "Unsupported use of square brackets")]
        [DataRow("=5/0", "Expression contains division by zero")]
        [DataRow("=5 / 0", "Expression contains division by zero")]
        [DataRow("10+(8*9)/0+7", "Expression contains division by zero")]
        [DataRow("10+(8*9)/0*7", "Expression contains division by zero")]
        public void ErrorResultOnInvalidKeywordQuery(string typedString, string expectedResult)
        {
            Query expectedQuery = new(typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("pi(9+)")]
        [DataRow("pi,")]
        [DataRow("log()")]
        [DataRow("0xf0x6")]
        [DataRow("0xf,0x6")]
        [DataRow("2^96")]
        [DataRow("+()")]
        [DataRow("[10,10]")]
        [DataRow("5/0")]
        [DataRow("5 / 0")]
        [DataRow("10+(8*9)/0+7")]
        [DataRow("10+(8*9)/0*7")]
        public void NoResultOnInvalidGlobalQuery(string typedString)
        {
            // Setup
            Query expectedQuery = new(typedString);

            // Act
            var result = _main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(result, 0);
        }

        [DataTestMethod]
        [DataRow("9+")]
        [DataRow("9-")]
        [DataRow("9*")]
        [DataRow("9|")]
        [DataRow("9\\")]
        [DataRow("9^")]
        [DataRow("9=")]
        [DataRow("9&")]
        [DataRow("9/")]
        [DataRow("9%")]
        public void NoResultIfQueryEndsWithBinaryOperator(string typedString)
        {
            // Setup
            Query expectedQuery = new(typedString);
            Query expectedQueryWithKeyword = new("=" + typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).Count;
            var resultWithKeyword = _main.Object.Query(expectedQueryWithKeyword).Count;

            // Assert
            Assert.AreEqual(result, 0);
            Assert.AreEqual(resultWithKeyword, 0);
        }

        [DataTestMethod]
        [DataRow("10+(8*9)/0,5")] // German decimal digit separator
        [DataRow("10+(8*9)/0.5")]
        [DataRow("10+(8*9)/1,5")] // German decimal digit separator
        [DataRow("10+(8*9)/1.5")]
        public void NoErrorForDivisionByNumberWithDecimalDigits(string typedString)
        {
            // Setup
            Query expectedQuery = new(typedString);
            Query expectedQueryWithKeyword = new("=" + typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;
            var resultWithKeyword = _main.Object.Query(expectedQueryWithKeyword).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual(result, "Copy this number to the clipboard");
            Assert.AreEqual(resultWithKeyword, "Copy this number to the clipboard");
        }

        [DataTestMethod]
        [DataRow("pie", "pi * e")]
        [DataRow("eln(100)", "e * ln(100)")]
        [DataRow("pi(1+1)", "pi * (1+1)")]
        [DataRow("2pi", "2 * pi")]
        [DataRow("2log10(100)", "2 * log10(100)")]
        [DataRow("2(3+4)", "2 * (3+4)")]
        [DataRow("sin(pi)cos(pi)", "sin(pi) * cos(pi)")]
        [DataRow("log10(100)(2+3)", "log10(100) * (2+3)")]
        [DataRow("(1+1)cos(pi)", "(1+1) * cos(pi)")]
        [DataRow("(1+1)(2+2)", "(1+1) * (2+2)")]
        [DataRow("2(1+1)", "2 * (1+1)")]
        [DataRow("pi(1+1)", "pi * (1+1)")]
        [DataRow("pilog(100)", "pi * log(100)")]
        [DataRow("3log(100)", "3 * log(100)")]
        [DataRow("2e", "2 * e")]
        [DataRow("(1+1)(3+2)", "(1+1) * (3+2)")]
        [DataRow("(1+1)cos(pi)", "(1+1) * cos(pi)")]
        [DataRow("sin(pi)cos(pi)", "sin(pi) * cos(pi)")]
        [DataRow("2 (1+1)", "2 * (1+1)")]
        [DataRow("pi  (1+1)", "pi * (1+1)")]
        [DataRow("pi  log(100)", "pi * log(100)")]
        [DataRow("3   log(100)", "3 * log(100)")]
        [DataRow("2 e", "2 * e")]
        [DataRow("(1+1)  (3+2)", "(1+1) * (3+2)")]
        [DataRow("(1+1)  cos(pi)", "(1+1) * cos(pi)")]
        [DataRow("sin  (pi)  cos(pi)", "sin  (pi) * cos(pi)")]
        [DataRow("2picos(pi)(1+1)", "2 * pi * cos(pi) * (1+1)")]
        [DataRow("pilog(100)log(1000)", "pi * log(100) * log(1000)")]
        [DataRow("pipipie", "pi * pi * pi * e")]
        [DataRow("(1+1)(3+2)(1+1)(1+1)", "(1+1) * (3+2) * (1+1) * (1+1)")]
        [DataRow("(1+1) (3+2)  (1+1)(1+1)", "(1+1) * (3+2) * (1+1) * (1+1)")]
        [DataRow("1.0E2", "(1.0 * 10^(2))")]
        [DataRow("-1.0E-2", "(-1.0 * 10^(-2))")]
        [DataRow("1.2E2", "(1.2 * 10^(2))")]
        [DataRow("5/1.0E2", "5/(1.0 * 10^(2))")]
        [DataRow("0.1E2", "(0.1 * 10^(2))")]
        [DataRow(".1E2", "(.1 * 10^(2))")]
        [DataRow(".1E2", "(.1 * 10^(2))")]
        [DataRow("5/5E3", "5/(5 * 10^(3))")]
        [DataRow("1.E2", "(1. * 10^(2))")]
        public void RightHumanMultiplicationExpressionTransformation(string typedString, string expectedQuery)
        {
            // Setup

            // Act
            var result = CalculateHelper.FixHumanMultiplicationExpressions(typedString);

            // Assert
            Assert.AreEqual(expectedQuery, result);
        }

        [DataTestMethod]
        [DataRow("2(1+1)")]
        [DataRow("pi(1+1)")]
        [DataRow("pilog(100)")]
        [DataRow("3log(100)")]
        [DataRow("2e")]
        [DataRow("(1+1)(3+2)")]
        [DataRow("(1+1)cos(pi)")]
        [DataRow("sin(pi)cos(pi)")]
        [DataRow("2 (1+1)")]
        [DataRow("pi  (1+1)")]
        [DataRow("pi  log(100)")]
        [DataRow("3   log(100)")]
        [DataRow("2 e")]
        [DataRow("(1+1)  (3+2)")]
        [DataRow("(1+1)  cos(pi)")]
        [DataRow("sin  (pi)  cos(pi)")]
        [DataRow("2picos(pi)(1+1)")]
        [DataRow("pilog(100)log(1000)")]
        [DataRow("pipipie")]
        [DataRow("(1+1)(3+2)(1+1)(1+1)")]
        [DataRow("(1+1) (3+2)  (1+1)(1+1)")]
        public void NoErrorForHumanMultiplicationExpressions(string typedString)
        {
            // Setup
            Query expectedQuery = new(typedString);
            Query expectedQueryWithKeyword = new("=" + typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).FirstOrDefault()?.SubTitle;
            var resultWithKeyword = _main.Object.Query(expectedQueryWithKeyword).FirstOrDefault()?.SubTitle;

            // Assert
            Assert.AreEqual("Copy this number to the clipboard", result);
            Assert.AreEqual("Copy this number to the clipboard", resultWithKeyword);
        }

        [DataTestMethod]
        [DataRow("2(1+1)", 4)]
        [DataRow("pi(1+1)", 6.2831853072)]
        [DataRow("pilog(100)", 6.2831853072)]
        [DataRow("3log(100)", 6)]
        [DataRow("2e", 5.4365636569)]
        [DataRow("2E2", 200)]
        [DataRow("2E-2", 0.02)]
        [DataRow("1.2E2", 120)]
        [DataRow("1.2E-1", 0.12)]
        [DataRow("5/5E3", 0.001)]
        [DataRow("-5/5E3", -0.001)]
        [DataRow("(1+1)(3+2)", 10)]
        [DataRow("(1+1)cos(pi)", -2)]
        [DataRow("log(100)cos(pi)", -2)]
        public void RightAnswerForHumanMultiplicationExpressions(string typedString, double answer)
        {
            // Setup
            Query expectedQuery = new(typedString);
            Query expectedQueryWithKeyword = new("=" + typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).FirstOrDefault()?.Title;
            var resultWithKeyword = _main.Object.Query(expectedQueryWithKeyword).FirstOrDefault()?.Title;

            // Assert
            Assert.AreEqual(answer.ToString(CultureInfo.CurrentCulture), result);
            Assert.AreEqual(answer.ToString(CultureInfo.CurrentCulture), resultWithKeyword);
        }

        [DataTestMethod]
        [DataRow("0x1234+0x1234", 9320)]
        [DataRow("0x1234-0x1234", 0)]
        [DataRow("0x12345678+0x12345678", 610839792)]
        [DataRow("0xf4572220-0xf4572410", -496)]
        public void RightAnswerForLargeHexadecimalNumbers(string typedString, double answer)
        {
            // Setup
            Query expectedQuery = new(typedString);
            Query expectedQueryWithKeyword = new("=" + typedString, "=");

            // Act
            var result = _main.Object.Query(expectedQuery).FirstOrDefault()?.Title;
            var resultWithKeyword = _main.Object.Query(expectedQueryWithKeyword).FirstOrDefault()?.Title;

            // Assert
            Assert.AreEqual(answer.ToString(CultureInfo.CurrentCulture), result);
            Assert.AreEqual(answer.ToString(CultureInfo.CurrentCulture), resultWithKeyword);
        }

        [DataTestMethod]
        [DataRow("#", "#1+1=", true)]
        [DataRow("#", "#1+1", false)]
        [DataRow("#", "#1/0=", false)]
        [DataRow("", "1+1=", false)]
        public void HandleInputReplace(string actionKeyword, string query, bool shouldChangeQuery)
        {
            // Setup
            Query expectedQuery = new(query, actionKeyword);
            _main.Object.UpdateSettings(new PowerLauncherPluginSettings()); // Default settings

            // Act
            _main.Object.Query(expectedQuery);

            // Assert
            _api.Verify(api => api.ChangeQuery(It.IsAny<string>(), It.IsAny<bool>()), shouldChangeQuery ? Times.Once : Times.Never);
        }
    }
}
