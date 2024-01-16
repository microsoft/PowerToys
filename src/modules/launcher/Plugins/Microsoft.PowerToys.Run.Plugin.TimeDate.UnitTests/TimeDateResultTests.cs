// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class TimeDateResultTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            // Set culture to 'en-us'
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        private DateTime GetDateTimeForTest(bool embedUtc = false)
        {
            var dateTime = new DateTime(2022, 03, 02, 22, 30, 45);
            if (embedUtc)
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }
            else
            {
                return dateTime;
            }
        }

        [DataTestMethod]
        [DataRow("time", "10:30 PM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("date and time", "3/2/2022 10:30 PM")]
        [DataRow("hour", "22")]
        [DataRow("minute", "30")]
        [DataRow("second", "45")]
        [DataRow("millisecond", "0")]
        [DataRow("day (week day)", "Wednesday")]
        [DataRow("day of the week (week day)", "4")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year (calendar week, week number)", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("month and day", "March 2")]
        [DataRow("year", "2022")]
        [DataRow("month and year", "March 2022")]
        [DataRow("ISO 8601", "2022-03-02T22:30:45")]
        [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
        [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
        [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
        public void LocalFormatsWithShortTimeAndShortDate(string formatLabel, string expectedResult)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, false, false, GetDateTimeForTest());

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value, $"Culture {CultureInfo.CurrentCulture.Name}, Culture UI: {CultureInfo.CurrentUICulture.Name}, Calendar: {CultureInfo.CurrentCulture.Calendar}, Region: {RegionInfo.CurrentRegion.Name}");
        }

        [DataTestMethod]
        [DataRow("time", "10:30 PM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("date and time", "Wednesday, March 2, 2022 10:30 PM")]
        [DataRow("hour", "22")]
        [DataRow("minute", "30")]
        [DataRow("second", "45")]
        [DataRow("millisecond", "0")]
        [DataRow("day (week day)", "Wednesday")]
        [DataRow("day of the week (week day)", "4")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year (calendar week, week number)", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("month and day", "March 2")]
        [DataRow("year", "2022")]
        [DataRow("month and year", "March 2022")]
        [DataRow("ISO 8601", "2022-03-02T22:30:45")]
        [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
        [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
        [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
        public void LocalFormatsWithShortTimeAndLongDate(string formatLabel, string expectedResult)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, false, true, GetDateTimeForTest());

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 PM")]
        [DataRow("date", "3/2/2022")]
        [DataRow("date and time", "3/2/2022 10:30:45 PM")]
        [DataRow("hour", "22")]
        [DataRow("minute", "30")]
        [DataRow("second", "45")]
        [DataRow("millisecond", "0")]
        [DataRow("day (week day)", "Wednesday")]
        [DataRow("day of the week (week day)", "4")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year (calendar week, week number)", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("month and day", "March 2")]
        [DataRow("year", "2022")]
        [DataRow("month and year", "March 2022")]
        [DataRow("ISO 8601", "2022-03-02T22:30:45")]
        [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
        [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
        [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
        public void LocalFormatsWithLongTimeAndShortDate(string formatLabel, string expectedResult)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, true, false, GetDateTimeForTest());

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time", "10:30:45 PM")]
        [DataRow("date", "Wednesday, March 2, 2022")]
        [DataRow("date and time", "Wednesday, March 2, 2022 10:30:45 PM")]
        [DataRow("hour", "22")]
        [DataRow("minute", "30")]
        [DataRow("second", "45")]
        [DataRow("millisecond", "0")]
        [DataRow("day (week day)", "Wednesday")]
        [DataRow("day of the week (week day)", "4")]
        [DataRow("day of the month", "2")]
        [DataRow("day of the year", "61")]
        [DataRow("week of the month", "1")]
        [DataRow("week of the year (calendar week, week number)", "10")]
        [DataRow("month", "March")]
        [DataRow("month of the year", "3")]
        [DataRow("month and day", "March 2")]
        [DataRow("year", "2022")]
        [DataRow("month and year", "March 2022")]
        [DataRow("ISO 8601", "2022-03-02T22:30:45")]
        [DataRow("ISO 8601 with time zone", "2022-03-02T22:30:45")]
        [DataRow("RFC1123", "Wed, 02 Mar 2022 22:30:45 GMT")]
        [DataRow("Date and time in filename-compatible format", "2022-03-02_22-30-45")]
        public void LocalFormatsWithLongTimeAndLongDate(string formatLabel, string expectedResult)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, true, true, GetDateTimeForTest());

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time utc", "t")]
        [DataRow("date and time utc", "g")]
        [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
        [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
        public void UtcFormatsWithShortTimeAndShortDate(string formatLabel, string expectedFormat)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, false, false, GetDateTimeForTest(true));
            var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time utc", "t")]
        [DataRow("date and time utc", "f")]
        [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
        [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
        public void UtcFormatsWithShortTimeAndLongDate(string formatLabel, string expectedFormat)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, false, true, GetDateTimeForTest(true));
            var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time utc", "T")]
        [DataRow("date and time utc", "G")]
        [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
        [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
        public void UtcFormatsWithLongTimeAndShortDate(string formatLabel, string expectedFormat)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, true, false, GetDateTimeForTest(true));
            var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [DataTestMethod]
        [DataRow("time utc", "T")]
        [DataRow("date and time utc", "F")]
        [DataRow("ISO 8601 UTC", "yyyy-MM-ddTHH:mm:ss")]
        [DataRow("ISO 8601 UTC with time zone", "yyyy-MM-ddTHH:mm:ss'Z'")]
        [DataRow("Universal time format: YYYY-MM-DD hh:mm:ss", "u")]
        [DataRow("Date and time in filename-compatible format", "yyyy-MM-dd_HH-mm-ss")]
        public void UtcFormatsWithLongTimeAndLongDate(string formatLabel, string expectedFormat)
        {
            // Setup
            var helperResults = AvailableResultsList.GetList(true, true, true, GetDateTimeForTest(true));
            var expectedResult = GetDateTimeForTest().ToString(expectedFormat, CultureInfo.CurrentCulture);

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [TestMethod]
        public void UnixTimestampSecondsFormat()
        {
            // Setup
            string formatLabel = "Unix epoch time";
            DateTime timeValue = DateTime.Now.ToUniversalTime();
            var helperResults = AvailableResultsList.GetList(true, false, false, timeValue);
            var expectedResult = (long)timeValue.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult.ToString(CultureInfo.CurrentCulture), result?.Value);
        }

        [TestMethod]
        public void UnixTimestampMillisecondsFormat()
        {
            // Setup
            string formatLabel = "Unix epoch time in milliseconds";
            DateTime timeValue = DateTime.Now.ToUniversalTime();
            var helperResults = AvailableResultsList.GetList(true, false, false, timeValue);
            var expectedResult = (long)timeValue.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult.ToString(CultureInfo.CurrentCulture), result?.Value);
        }

        [TestMethod]
        public void WindowsFileTimeFormat()
        {
            // Setup
            string formatLabel = "Windows file time (Int64 number)";
            DateTime timeValue = DateTime.Now;
            var helperResults = AvailableResultsList.GetList(true, false, false, timeValue);
            var expectedResult = timeValue.ToFileTime().ToString(CultureInfo.CurrentCulture);

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [TestMethod]
        public void ValidateEraResult()
        {
            // Setup
            string formatLabel = "Era";
            DateTime timeValue = DateTime.Now;
            var helperResults = AvailableResultsList.GetList(true, false, false, timeValue);
            var expectedResult = DateTimeFormatInfo.CurrentInfo.GetEraName(CultureInfo.CurrentCulture.Calendar.GetEra(timeValue));

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [TestMethod]
        public void ValidateEraAbbreviationResult()
        {
            // Setup
            string formatLabel = "Era abbreviation";
            DateTime timeValue = DateTime.Now;
            var helperResults = AvailableResultsList.GetList(true, false, false, timeValue);
            var expectedResult = DateTimeFormatInfo.CurrentInfo.GetAbbreviatedEraName(CultureInfo.CurrentCulture.Calendar.GetEra(timeValue));

            // Act
            var result = helperResults.FirstOrDefault(x => x.Label.Equals(formatLabel, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.AreEqual(expectedResult, result?.Value);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Set culture to original value
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
