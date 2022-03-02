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
    public class HelperTests
    {
        [DataTestMethod]
        [DataRow("time", "10:30 AM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("now", "3/2/2022 10:30 AM")]
        [DataRow("unix", "1646213445")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of week", "3")]
        [DataRow("day of months", "2")]
        [DataRow("day of year", "61")]
        [DataRow("week of month", "1")]
        [DataRow("week of year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithShortTimeAndShortDate(string typedString, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetCommandList(true, false, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label == typedString);

            // Assert
            Assert.AreEqual(result.Value, expectedResult);

            // Finalyze
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30 AM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("now", "Wednesday, March 2, 2022 10:30 AM")]
        [DataRow("unix", "1646213445")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of week", "3")]
        [DataRow("day of months", "2")]
        [DataRow("day of year", "61")]
        [DataRow("week of month", "1")]
        [DataRow("week of year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithShortTimeAndLongDate(string typedString, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetCommandList(true, false, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label == typedString);

            // Assert
            Assert.AreEqual(result.Value, expectedResult);

            // Finalyze
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 AM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("now", "3/2/2022 10:30:45 AM")]
        [DataRow("unix", "1646213445")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of week", "3")]
        [DataRow("day of months", "2")]
        [DataRow("day of year", "61")]
        [DataRow("week of month", "1")]
        [DataRow("week of year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithLongTimeAndShortDate(string typedString, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetCommandList(true, true, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label == typedString);

            // Assert
            Assert.AreEqual(result.Value, expectedResult);

            // Finalyze
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 AM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("now", "Wednesday, March 2, 2022 10:30:45 AM")]
        [DataRow("unix", "1646213445")]
        [DataRow("day", "Wednesday")]
        [DataRow("day of week", "3")]
        [DataRow("day of months", "2")]
        [DataRow("day of year", "61")]
        [DataRow("week of month", "1")]
        [DataRow("week of year", "10")]
        [DataRow("month", "March")]
        [DataRow("month of year", "3")]
        [DataRow("year", "2022")]
        public void ValidateWithLongTimeAndLongDate(string typedString, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetCommandList(true, true, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label == typedString);

            // Assert
            Assert.AreEqual(result.Value, expectedResult);

            // Finalyze
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
