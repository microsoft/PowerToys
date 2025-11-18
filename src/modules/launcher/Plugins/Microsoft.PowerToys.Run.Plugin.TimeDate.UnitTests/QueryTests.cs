// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;

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
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        [DataTestMethod]
        [DataRow("time", 2)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("date", 2)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("now", 3)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("current", 3)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("year", 0)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("time::10:10:10", 0)] // Setting 'Only Date, Time, Now on global results' is default on
        [DataRow("date::10/10/10", 0)] // Setting 'Only Date, Time, Now on global results' is default on
        public void CountWithoutPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result, "Result depends on default plugin settings!");
        }

        [DataTestMethod]
        [DataRow("(time", 18)]
        [DataRow("(date", 28)]
        [DataRow("(year", 8)]
        [DataRow("(now", 34)]
        [DataRow("(current", 34)]
        [DataRow("(", 34)]
        [DataRow("(now::10:10:10", 1)] // Windows file time
        [DataRow("(current::10:10:10", 0)]
        public void CountWithPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

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
            Mock<Main> main = new();
            Query expectedQuery = new(typedString);

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
        [DataRow("(unix epoch time in milli", "Unix epoch time in milliseconds -")]
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
        [DataRow("(days in mo", "Days in month -")]
        [DataRow("(Leap y", "Leap year -")]
        public void CanFindFormatResult(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

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
        [DataRow("(u+1646408119", "Date and time -")]
        [DataRow("(u-1646408119", "Date and time -")]
        [DataRow("(ums1646408119", "Date and time -")]
        [DataRow("(ums+1646408119", "Date and time -")]
        [DataRow("(ums-1646408119", "Date and time -")]
        [DataRow("(ft637820085517321977", "Date and time -")]
        public void DateTimeNumberOnlyInput(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

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
        [DataRow("(timtaaaetetaae::u1646408119")]
        [DataRow("(time:eeee")]
        [DataRow("(time::eeee")]
        [DataRow("(time//eeee")]
        public void InvalidInputNotShowsResults(string typedString)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault();

            // Assert
            Assert.IsNull(result, result?.ToString());
        }

        [DataTestMethod]
        [DataRow("(ug1646408119")] // Invalid prefix
        [DataRow("(u9999999999999")] // Unix number + prefix is longer than 12 characters
        [DataRow("(ums999999999999999")] // Unix number in milliseconds + prefix is longer than 17 characters
        [DataRow("(-u99999999999")] // Unix number with wrong placement of - sign
        [DataRow("(+ums9999999999")] // Unix number in milliseconds with wrong placement of + sign
        [DataRow("(0123456")] // Missing prefix
        [DataRow("(ft63782008ab55173dasdas21977")] // Number contains letters
        [DataRow("(ft63782008ab55173dasdas")] // Number contains letters at the end
        [DataRow("(ft12..548")] // Number contains wrong punctuation
        [DataRow("(ft12..54//8")] // Number contains wrong punctuation and other characters
        [DataRow("(time::ft12..54//8")] // Number contains wrong punctuation and other characters
        [DataRow("(ut2ed.5555")] // Number contains letters
        [DataRow("(12..54//8")] // Number contains punctuation and other characters, but no special prefix
        [DataRow("(ft::1288gg8888")] // Number contains delimiter and letters, but no special prefix
        [DataRow("(date::12::55")]
        [DataRow("(date::12:aa:55")]
        [DataRow("(10.aa.22")]
        [DataRow("(12::55")]
        [DataRow("(12:aa:55")]
        public void InvalidNumberInputShowsErrorMessage(string typedString)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().Title;

            // Assert
            Assert.IsTrue(result.StartsWith("Error:", StringComparison.CurrentCulture));
        }

        [DataTestMethod]
        [DataRow("(ft1 2..548")] // Input contains space
        [DataRow("(ft12..54 //8")] // Input contains space
        [DataRow("(time::ft12..54 //8")] // Input contains space
        [DataRow("(10.10aa")] // Input contains <Number>.<Number> (Can be part of a date.)
        [DataRow("(10:10aa")] // Input contains <Number>:<Number> (Can be part of a time.)
        [DataRow("(10/10aa")] // Input contains <Number>/<Number> (Can be part of a date.)
        public void InvalidInputNotShowsErrorMessage(string typedString)
        {
            // Setup
            Mock<Main> main = new();
            Query expectedQuery = new(typedString, "(");

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
