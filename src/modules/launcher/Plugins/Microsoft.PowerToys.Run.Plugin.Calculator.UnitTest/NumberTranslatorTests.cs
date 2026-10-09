// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    public class NumberTranslatorTests
    {
        [DataTestMethod]
        [DataRow(null, "en-US")]
        [DataRow("de-DE", null)]
        public void Create_ThrowError_WhenCalledNullOrEmpty(string sourceCultureName, string targetCultureName)
        {
            // Arrange
            CultureInfo sourceCulture = sourceCultureName != null ? new CultureInfo(sourceCultureName) : null;
            CultureInfo targetCulture = targetCultureName != null ? new CultureInfo(targetCultureName) : null;

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => NumberTranslator.Create(sourceCulture, targetCulture));
        }

        [DataTestMethod]
        [DataRow("en-US", "en-US")]
        [DataRow("en-EN", "en-US")]
        [DataRow("de-DE", "en-US")]
        public void Create_WhenCalled(string sourceCultureName, string targetCultureName)
        {
            // Arrange
            CultureInfo sourceCulture = new CultureInfo(sourceCultureName);
            CultureInfo targetCulture = new CultureInfo(targetCultureName);

            // Act
            var translator = NumberTranslator.Create(sourceCulture, targetCulture);

            // Assert
            Assert.IsNotNull(translator);
        }

        [DataTestMethod]
        [DataRow(null)]
        public void Translate_ThrowError_WhenCalledNull(string input)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE", false), new CultureInfo("en-US", false));

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => translator.Translate(input));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        public void Translate_WhenCalledEmpty(string input)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE", false), new CultureInfo("en-US", false));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.AreEqual(input, result);
        }

        [DataTestMethod]
        [DataRow("2,0 * 2", "2.0 * 2")]
        [DataRow("4 * 3,6 + 9", "4 * 3.6 + 9")]
        [DataRow("5,2+6", "5.2+6")]
        [DataRow("round(2,5)", "round(2.5)")]
        [DataRow("3,3333", "3.3333")]
        [DataRow("max(2;3)", "max(2,3)")]
        public void Translate_NoErrors_WhenCalled(string input, string expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE", false), new CultureInfo("en-US", false));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("2.0 * 2", "2,0 * 2")]
        [DataRow("4 * 3.6 + 9", "4 * 3,6 + 9")]
        [DataRow("5.2+6", "5,2+6")]
        [DataRow("round(2.5)", "round(2,5)")]
        [DataRow("3.3333", "3,3333")]
        public void TranslateBack_NoErrors_WhenCalled(string input, string expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE", false), new CultureInfo("en-US", false));

            // Act
            var result = translator.TranslateBack(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow(".", ",", "2,000,000", "2000000")]
        [DataRow(".", ",", "2,000,000.6", "2000000.6")]
        [DataRow(",", ".", "2.000.000", "2000000")]
        [DataRow(",", ".", "2.000.000,6", "2000000.6")]
        public void Translate_RemoveNumberGroupSeparator_WhenCalled(string decimalSeparator, string groupSeparator, string input, string expectedResult)
        {
            // Arrange
            var sourceCulture = new CultureInfo("en-US", false)
            {
                NumberFormat =
                {
                    NumberDecimalSeparator = decimalSeparator,
                    NumberGroupSeparator = groupSeparator,
                },
            };
            var translator = NumberTranslator.Create(sourceCulture, new CultureInfo("en-US", false));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("de-DE", "12,0004", "12.0004")]
        [DataRow("de-DE", "0xF000", "0xF000")]
        [DataRow("de-DE", "0", "0")]
        [DataRow("de-DE", "00", "0")]
        [DataRow("de-DE", "12.004", "12004")] // . is the group separator in de-DE
        [DataRow("de-DE", "12.04", "1204")]
        [DataRow("de-DE", "12.4", "124")]
        [DataRow("de-DE", "3.004.044.444,05", "3004044444.05")]
        [DataRow("de-DE", "123.01 + 52.30", "12301 + 5230")]
        [DataRow("de-DE", "123.001 + 52.30", "123001 + 5230")]
        [DataRow("fr-FR", "0", "0")]
        [DataRow("fr-FR", "00", "0")]
        [DataRow("fr-FR", "12.004", "12.004")] // . is not decimal or group separator in fr-FR
        [DataRow("fr-FR", "12.04", "12.04")]
        [DataRow("fr-FR", "12.4", "12.4")]
        [DataRow("fr-FR", "12.0004", "12.0004")]
        [DataRow("fr-FR", "123.01 + 52.30", "123.01 + 52.30")]
        [DataRow("fr-FR", "123.001 + 52.30", "123.001 + 52.30")]
        public void Translate_NoRemovalOfLeadingZeroesOnEdgeCases(string sourceCultureName, string input, string expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo(sourceCultureName, false), new CultureInfo("en-US", false));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("en-US", "0xF000", "0xF000")]
        [DataRow("en-US", "0xf4572220", "4099351072")]
        [DataRow("en-US", "0x12345678", "305419896")]
        public void Translate_LargeHexadecimalNumbersToDecimal(string sourceCultureName, string input, string expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo(sourceCultureName, false), new CultureInfo("en-US", false));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }
    }
}
