// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        public void CountWithoutPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new ();
            Query expectedQuery = new Query(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result);
        }

        [DataTestMethod]
        [DataRow("(time", 3)]
        [DataRow("(date", 3)]
        [DataRow("(year", 1)]
        [DataRow("(", 13)]
        public void CountWithPluginKeyword(string typedString, int expectedResultCount)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).Count;

            // Assert
            Assert.AreEqual(expectedResultCount, result);
        }

        [DataTestMethod]
        [DataRow("time", "Time -")]
        [DataRow("date", "Date -")]
        [DataRow("now", "Now (Current date and time) -")]
        [DataRow("unix", "Unix Timestamp (Current date and time) -")]
        [DataRow("day", "Day -")]
        [DataRow("day of week", "Day of the week -")]
        [DataRow("day of months", "Day of the month -")]
        [DataRow("day of year", "Day of the year -")]
        [DataRow("week of month", "Week of the month -")]
        [DataRow("week of year", "Week of the year -")]
        [DataRow("month", "Month -")]
        [DataRow("month of year", "Month of the year -")]
        [DataRow("year", "Year -")]
        public void CanFindResult(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            string pluginKeyword = "(";
            Query expectedQuery = new Query(pluginKeyword + typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.IsTrue(result.StartsWith(expectedResult), result);
        }
    }
}
