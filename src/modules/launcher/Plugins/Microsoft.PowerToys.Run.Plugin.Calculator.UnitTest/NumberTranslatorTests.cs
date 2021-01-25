// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using NUnit.Framework;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestFixture]
    public class NumberTranslatorTests
    {
        [TestCase(null, "en-US")]
        [TestCase("de-DE", null)]
        public void Create_ThrowError_WhenCalledNullOrEmpty(string sourceCultureName, string targetCultureName)
        {
            // Arrange
            CultureInfo sourceCulture = sourceCultureName != null ? new CultureInfo(sourceCultureName) : null;
            CultureInfo targetCulture = targetCultureName != null ? new CultureInfo(targetCultureName) : null;

            // Act
            Assert.Catch<ArgumentNullException>(() => NumberTranslator.Create(sourceCulture, targetCulture));
        }

        [TestCase("en-US", "en-US")]
        [TestCase("en-EN", "en-US")]
        [TestCase("de-DE", "en-US")]
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

        [TestCase(null)]
        public void Translate_ThrowError_WhenCalledNull(string input)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

            // Act
            Assert.Catch<ArgumentNullException>(() => translator.Translate(input));
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Translate_WhenCalledEmpty(string input)
        {
            // Arrange
            var translator = NumberTranslator.Create(new CultureInfo("de-DE"), new CultureInfo("en-US"));

            // Act
            var result = translator.Translate(input);

            // Assert
            Assert.AreEqual(input, result);
        }

        [TestCase("2,0 * 2", "2.0 * 2")]
        [TestCase("4 * 3,6 + 9", "4 * 3.6 + 9")]
        [TestCase("5,2+6", "5.2+6")]
        [TestCase("round(2,5)", "round(2.5)")]
        [TestCase("3,3333", "3.3333")]
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

        [TestCase("2.0 * 2", "2,0 * 2")]
        [TestCase("4 * 3.6 + 9", "4 * 3,6 + 9")]
        [TestCase("5.2+6", "5,2+6")]
        [TestCase("round(2.5)", "round(2,5)")]
        [TestCase("3.3333", "3,3333")]
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

        [TestCase(".", ",", "2,000,000", "2000000")]
        [TestCase(".", ",", "2,000,000.6", "2000000.6")]
        [TestCase(",", ".", "2.000.000", "2000000")]
        [TestCase(",", ".", "2.000.000,6", "2000000.6")]
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
