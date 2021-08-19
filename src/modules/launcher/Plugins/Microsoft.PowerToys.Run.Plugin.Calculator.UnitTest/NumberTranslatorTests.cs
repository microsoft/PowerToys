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
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => translator.Translate(input));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        public void Translate_WhenCalledEmpty(string input)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

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
        public void Translate_NoErrors_WhenCalled(string input, string expectedResult)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

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
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

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
            var sourceCulture = new CultureInfo("en-US")
            {
                NumberFormat =
                {
                    NumberDecimalSeparator = decimalSeparator,
                    NumberGroupSeparator = groupSeparator,
                },
            };
            var translator = NumberTranslator.Create(sourceCulture, new CultureInfo("en-US"));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResult, result);
        }
    }
}
