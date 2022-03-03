// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class DateTimeResultTests
    {
        [DataTestMethod]
        [DataRow("time", "10:30 AM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("date and time", "3/2/2022 10:30 AM")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of the week", "3")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithShortTimeAndShortDate(string formatLable, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLable, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30 AM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("date and time", "Wednesday, March 2, 2022 10:30 AM")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of the week", "3")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithShortTimeAndLongDate(string formatLable, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLable, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 AM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("date and time", "3/2/2022 10:30:45 AM")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of the week", "3")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithLongTimeAndShortDate(string formatLable, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLable, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 AM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("date and time", "Wednesday, March 2, 2022 10:30:45 AM")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of the week", "3")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithLongTimeAndLongDate(string formatLable, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLable, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
