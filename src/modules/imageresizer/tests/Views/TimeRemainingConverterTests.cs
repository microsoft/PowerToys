// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ImageResizer.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Views
{
    [TestClass]
    public class TimeRemainingConverterTests
    {
        [TestMethod]
        public void Convert_ReturnsEmptyString_WhenTimeSpanIsMaxValue()
        {
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                TimeSpan.MaxValue,
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Convert_ReturnsEmptyString_WhenTotalSecondsLessThanOne()
        {
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                TimeSpan.FromSeconds(0.5),
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Convert_ReturnsEmptyString_WhenZeroTimeSpan()
        {
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                TimeSpan.Zero,
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Convert_ReturnsFormattedString_WhenValidTimeSpan()
        {
            var converter = new TimeRemainingConverter();
            var timeRemaining = new TimeSpan(0, 5, 30);

            var result = converter.Convert(
                timeRemaining,
                targetType: null,
                parameter: null,
                language: string.Empty);

            // The result should contain the time remaining information
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.AreNotEqual(string.Empty, result);
        }

        [TestMethod]
        public void Convert_ReturnsEmptyString_WhenValueIsNotTimeSpan()
        {
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                "not a timespan",
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Convert_ReturnsEmptyString_WhenValueIsNull()
        {
            var converter = new TimeRemainingConverter();

            var result = converter.Convert(
                null,
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ConvertBack_ReturnsValueUnchanged()
        {
            var converter = new TimeRemainingConverter();
            var input = "test value";

            var result = converter.ConvertBack(
                input,
                targetType: null,
                parameter: null,
                language: string.Empty);

            Assert.AreEqual(input, result);
        }
    }
}
