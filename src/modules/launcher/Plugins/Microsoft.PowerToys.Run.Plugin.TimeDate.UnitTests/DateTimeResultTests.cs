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
        [DataRow("ISO 8601 (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("ISO 8601 with time zone (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("RFC1123 (Date and time)", "Wed, 02 Mar 2022 10:30:45 GMT")]
        public void LocalFormatsWithShortTimeAndShortDate(string formatLabel, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            if (string.IsNullOrEmpty(expectedResult))
            {
                Assert.IsNotNull(result);
            }
            else
            {
                Assert.AreEqual(expectedResult, result?.Value);
            }

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
        [DataRow("ISO 8601 (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("ISO 8601 with time zone (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("RFC1123 (Date and time)", "Wed, 02 Mar 2022 10:30:45 GMT")]
        public void LocalFormatsWithShortTimeAndLongDate(string formatLabel, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

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
        [DataRow("ISO 8601 (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("ISO 8601 with time zone (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("RFC1123 (Date and time)", "Wed, 02 Mar 2022 10:30:45 GMT")]
        public void LocalFormatsWithLongTimeAndShortDate(string formatLabel, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

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
        [DataRow("ISO 8601 (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("ISO 8601 with time zone (Date and time)", "2022-03-02T10:30:45")]
        [DataRow("RFC1123 (Date and time)", "Wed, 02 Mar 2022 10:30:45 GMT")]
        public void LocalFormatsWithLongTimeAndLongDate(string formatLabel, string expectedResult)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time utc", "t")]
        [DataRow("date and time utc", "g")]
        [DataRow("ISO 8601 UTC (Date and time)", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone (Date and time)", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss (Date and time)", "u")]
        public void UtcFormatsWithShortTimeAndShortDate(string formatLabel, string expectedFormat)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = new DateTime(2022, 03, 02, 10, 30, 45).ToUniversalTime().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time utc", "t")]
        [DataRow("date and time utc", "f")]
        [DataRow("ISO 8601 UTC (Date and time)", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone (Date and time)", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss (Date and time)", "u")]
        public void UtcFormatsWithShortTimeAndLongDate(string formatLabel, string expectedFormat)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = new DateTime(2022, 03, 02, 10, 30, 45).ToUniversalTime().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time utc", "T")]
        [DataRow("date and time utc", "G")]
        [DataRow("ISO 8601 UTC (Date and time)", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone (Date and time)", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss (Date and time)", "u")]
        public void UtcFormatsWithLongTimeAndShortDate(string formatLabel, string expectedFormat)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, false, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = new DateTime(2022, 03, 02, 10, 30, 45).ToUniversalTime().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [DataTestMethod]
        [DataRow("time utc", "T")]
        [DataRow("date and time utc", "F")]
        [DataRow("ISO 8601 UTC (Date and time)", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone (Date and time)", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss (Date and time)", "u")]
        public void UtcFormatsWithLongTimeAndLongDate(string formatLabel, string expectedFormat)
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, true, true, new DateTime(2022, 03, 02, 10, 30, 45));
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = new DateTime(2022, 03, 02, 10, 30, 45).ToUniversalTime().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [TestMethod]
        public void UnixTimestampFormat()
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");
            string formatLabel = "Unix Timestamp (Date and time)";
            DateTime timeValue = DateTime.Now.ToUniversalTime();

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, false, timeValue);
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = (long)timeValue.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            // Assert
            Assert.AreEqual(expectedResult.ToString(CultureInfo.CurrentCulture), result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        [TestMethod]
        public void WindowsFileTimeFormat()
        {
            // Setup
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");
            string formatLabel = "Windows file time (Date and time as Int64 number)";
            DateTime timeValue = DateTime.Now;

            // Act
            var helperResults = ResultHelper.GetAvailableResults(true, false, false, timeValue);
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));
            var expectedResult = timeValue.Ticks.ToString(CultureInfo.CurrentCulture);

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);

            // Finalize
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
