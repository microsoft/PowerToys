// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class QueryTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            StringMatcher.Instance = new StringMatcher();

            // Set culture to 'en-us'
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us");
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us");
        }

        [DataTestMethod]
        [DataRow("time", 2)]
        [DataRow("date", 2)]
        [DataRow("now", 3)]
        [DataRow("current", 3)]
        [DataRow("year", 0)]
        [DataRow("", 0)]
        [DataRow("time::10:10:10", 0)]
        [DataRow("date::10/10/10", 0)]
        public void CountWithoutPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString);

            // Act
            var result = main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result, "Result depends on default plugin settings!");
        }

        [DataTestMethod]
        [DataRow("(time", 16)]
        [DataRow("(date", 24)]
        [DataRow("(year", 7)]
        [DataRow("(now", 30)]
        [DataRow("(current", 30)]
        [DataRow("(", 30)]
        [DataRow("(now::10:10:10", 1)] // Windows file time
        [DataRow("(current::10:10:10", 0)]
        public void CountWithPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery);

            // Assert
            Assert.AreEqual(expectedResultCount, result.Count, result.ToString());
        }

        [DataTestMethod]
        [DataRow("time", 2)] // Match if first word is a full word match
        [DataRow("ime", 0)] // Don't match if first word is not a full match
        [DataRow("and", 0)] // Don't match for only conjunctions
        [DataRow("and time", 1)] // match if term is conjunction and other words
        [DataRow("date and time", 1)] // Match if first word is a full word match
        [DataRow("ate and time", 0)] // Don't match if first word is not a full word match
        [DataRow("10/10/10", 0)] // Don't match number only input (Setting 'Only Date, Time, Now on global results' is default on)
        [DataRow("10:10:10", 0)] // Don't match number only input (Setting 'Only Date, Time, Now on global results' is default on)
        [DataRow("10 10 10", 0)] // Don't match number only input (Setting 'Only Date, Time, Now on global results' is default on)
        [DataRow("ft10", 1)] // Don't match number input with prefix (Setting 'Only Date, Time, Now on global results' is default on) => Test behave strange here.
        public void ValidateBehaviorOnGlobalQueries(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString);

            // Act
            var result = main.Object.Query(expectedQuery);

            // Assert
            Assert.AreEqual(expectedResultCount, result.Count, result.ToString());
        }

        [DataTestMethod]
        [DataRow("(time", "Time -")]
        [DataRow("(time u", "Time UTC -")]
        [DataRow("(date", "Date -")]
        [DataRow("(now", "Now -")]
        [DataRow("(now u", "Now UTC -")]
        [DataRow("(unix", "Unix epoch time -")]
        [DataRow("(file", "Windows file time (Int64 number) ")]
        [DataRow("(hour", "Hour -")]
        [DataRow("(minute", "Minute -")]
        [DataRow("(second", "Second -")]
        [DataRow("(millisecond", "Millisecond -")]
        [DataRow("(day", "Day (Week day) -")]
        [DataRow("(day of week", "Day of the week (Week day) -")]
        [DataRow("(day of month", "Day of the month -")]
        [DataRow("(day of year", "Day of the year -")]
        [DataRow("(week of month", "Week of the month -")]
        [DataRow("(week of year", "Week of the year (Calendar week, Week number) -")]
        [DataRow("(month", "Month -")]
        [DataRow("(month of year", "Month of the year -")]
        [DataRow("(month and d", "Month and day -")]
        [DataRow("(month and y", "Month and year -")]
        [DataRow("(year", "Year -")]
        [DataRow("(era", "Era -")]
        [DataRow("(era a", "Era abbreviation -")]
        [DataRow("(universal", "Universal time format: YYYY-MM-DD hh:mm:ss -")]
        [DataRow("(iso", "ISO 8601 -")]
        [DataRow("(iso utc", "ISO 8601 UTC -")]
        [DataRow("(iso zone", "ISO 8601 with time zone - ")]
        [DataRow("(iso utc zone", "ISO 8601 UTC with time zone -")]
        [DataRow("(rfc", "RFC1123 -")]
        [DataRow("(time::12:30", "Time -")]
        [DataRow("(date::10.10.2022", "Date -")]
        [DataRow("(time::u1646408119", "Time -")]
        [DataRow("(time::ft637820085517321977", "Time -")]
        [DataRow("(year", "Era -")]
        [DataRow("(date", "Era -")]
        [DataRow("(week day", "Day (Week day) -")]
        [DataRow("(week day", "Day of the week (Week day) -")]
        [DataRow("(cal week", "Week of the year (Calendar week, Week number) -")]
        [DataRow("(week num", "Week of the year (Calendar week, Week number) -")]
        public void CanFindFormatResult(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault(x => x.SubTitle.StartsWith(expectedResult, StringComparison.CurrentCulture));

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result?.Score >= 1, $"Score: {result?.Score}");
        }

        [DataTestMethod]
        [DataRow("(12:30", "Time -")]
        [DataRow("(10.10.2022", "Date -")]
        [DataRow("(u1646408119", "Date and time -")]
        [DataRow("(ft637820085517321977", "Date and time -")]
        public void DateTimeNumberOnlyInput(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault(x => x.SubTitle.StartsWith(expectedResult, StringComparison.CurrentCulture));

            // Assert
            Assert.IsNotNull(result);
        }

        // [DataRow("(::")] -> Behaves different to PT Run user interface
        // [DataRow("(time::")] -> Behaves different to PT Run user interface
        // [DataRow("(::time")] -> Behaves different to PT Run user interface
        [DataTestMethod]
        [DataRow("(abcdefg")]
        [DataRow("(timmmmeeee")]
        [DataRow("(10.10.20.aa.22")]
        [DataRow("(12::55")]
        [DataRow("(12:aa:55")]
        [DataRow("(timtaaaetetaae::u1646408119")]
        [DataRow("(time:eeee")]
        [DataRow("(time::eeee")]
        [DataRow("(time//eeee")]
        [DataRow("(date::12::55")]
        [DataRow("(date::12:aa:55")]
        public void InvalidInputNotShowsResults(string typedString)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault();

            // Assert
            Assert.IsNull(result, result?.ToString());
        }

        [DataTestMethod]
        [DataRow("(ug1646408119")] // Invalid prefix
        [DataRow("(u9999999999999")] // Unix number + prefix is longer than 12 characters
        [DataRow("(0123456")] // Missing prefix
        [DataRow("(ft63782008ab55173dasdas21977")] // Number contains letters
        public void InvalidNumberInputShowsErrorMessage(string typedString)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().Title;

            // Assert
            Assert.IsTrue(result.StartsWith("Error:", StringComparison.CurrentCulture));
        }

        [DataTestMethod]
        [DataRow("(ft12..548")] // Number contains punctuation
        [DataRow("(ft12..54//8")] // Number contains punctuation and other characters
        [DataRow("(12..54//8")] // Number contains punctuation and other characters
        [DataRow("(ft::1288gg8888")] // Number contains delimiter and other characters
        public void InvalidInputNotShowsErrorMessage(string typedString)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new (typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault();

            // Assert
            Assert.IsNull(result);
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
