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
        [DataRow("(iso utc date", "ISO 8601 UTC (Date and time) - ")]
        [DataRow("(iso zone", "ISO 8601 with time zone (Date and time) - ")]
        [DataRow("(iso utc zone", "ISO 8601 UTC with time zone (Date and time) - ")]
        [DataRow("(rfc", "RFC1123 (Date and time) -")]
        [DataRow("(time::12:30", "Time -")]
        [DataRow("(date::10.10.2022", "Date -")]
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
    }
}
