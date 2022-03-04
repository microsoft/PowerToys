// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        [TestInitialize]
        public void Setup()
        {
            StringMatcher.Instance = new StringMatcher();
        }

        [DataTestMethod]
        [DataRow("time", 2)]
        [DataRow("date", 2)]
        [DataRow("year", 0)]
        [DataRow("", 0)]
        public void CountWithoutPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new Query(typedString);

            // Act
            // using where to get only result with full word match
            var result = main.Object.Query(expectedQuery).Where(x => x.SubTitle.Contains(expectedQuery.Search, StringComparison.OrdinalIgnoreCase)).ToList().Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result, "Result depends on default plugin settings!");
        }

        [DataTestMethod]
        [DataRow("(time", 12)]
        [DataRow("(date", 11)]
        [DataRow("(year", 4)]
        [DataRow("(", 22)]
        public void CountWithPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString, "(");

            // Act
            // using where to get only result with full word match
            var result = main.Object.Query(expectedQuery).Where(x => x.SubTitle.Contains(expectedQuery.Search, StringComparison.OrdinalIgnoreCase)).ToList().Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result);
        }

        [DataTestMethod]
        [DataRow("(time", "Time -")]
        [DataRow("(time u", "Time UTC -")]
        [DataRow("(date", "Date -")]
        [DataRow("(now", "Now (Current date and time) -")]
        [DataRow("(now u", "Now UTC (Current date and time) -")]
        [DataRow("(unix", "Unix Timestamp (Current date and time) -")]
        [DataRow("(file", "Windows file time (Current date and time")]
        [DataRow("(day", "Day -")]
        [DataRow("(day of week", "Day of the week -")]
        [DataRow("(day of month", "Day of the month -")]
        [DataRow("(day of year", "Day of the year -")]
        [DataRow("(week of month", "Week of the month -")]
        [DataRow("(week of year", "Week of the year -")]
        [DataRow("(month", "Month -")]
        [DataRow("(month of year", "Month of the year -")]
        [DataRow("(year", "Year -")]
        [DataRow("(universal", "Universal time format: YYYY-MM-DD hh:mm:ss (Date and time) -")]
        [DataRow("(iso date", "ISO 8601 (Date and time) -")]
        [DataRow("(iso utc date", "ISO 8601 UTC (Date and time) -")]
        [DataRow("(iso zone", "ISO 8601 with time zone (Date and time) - ")]
        [DataRow("(iso utc zone", "ISO 8601 UTC with time zone (Date and time) -")]
        [DataRow("(rfc", "RFC1123 (Date and time) -")]
        [DataRow("(time::12:30", "Time -")]
        [DataRow("(date::10.10.2022", "Date -")]
        [DataRow("(12:30", "Time -")]
        [DataRow("(10.10.2022", "Date -")]
        [DataRow("(u1646408119", "Date and time -")]
        [DataRow("(time::u1646408119", "Time -")]
        [DataRow("(ft637820085517321977", "Date and time -")]
        [DataRow("(time::ft637820085517321977", "Time -")]
        public void CanFindResult(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault(x => x.SubTitle.StartsWith(expectedResult));

            // Assert
            Assert.IsNotNull(result, $"Failed for '{typedString}'='{expectedResult}'");
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
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString, "(");

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
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().Title;

            // Assert
            Assert.IsTrue(result.StartsWith("Error:"));
        }

        [DataTestMethod]
        [DataRow("(ft12..548")] // Number contains punctuation
        [DataRow("(ft12..54//8")] // Number contains punctuation and other characters
        [DataRow("(12..54//8")] // Number contains punctuation and other characters
        [DataRow("(ft::1288gg8888")] // Number contains delimiter and other characters
        public void InvalidNumberInputNotShowsErrorMessage(string typedString)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault();

            // Assert
            Assert.IsNull(result);
        }
    }
}
