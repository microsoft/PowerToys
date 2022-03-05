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
    public class ImageTests
    {
        [TestInitialize]
        public void Setup()
        {
            StringMatcher.Instance = new StringMatcher();
        }

        [DataTestMethod]
        [DataRow("time", "Time -", "Images\\time.dark.png")]
        [DataRow("time u", "Time UTC -", "Images\\time.dark.png")]
        [DataRow("date", "Date -", "Images\\calendar.dark.png")]
        [DataRow("now", "Now (Current date and time) -", "Images\\timeDate.dark.png")]
        [DataRow("now u", "Now UTC (Current date and time) -", "Images\\timeDate.dark.png")]
        [DataRow("unix", "Unix Timestamp (Current date and time) -", "Images\\timeDate.dark.png")]
        [DataRow("file", "Windows file time (Current date and time", "Images\\timeDate.dark.png")]
        [DataRow("day", "Day -", "Images\\calendar.dark.png")]
        [DataRow("day of week", "Day of the week -", "Images\\calendar.dark.png")]
        [DataRow("day of month", "Day of the month -", "Images\\calendar.dark.png")]
        [DataRow("day of year", "Day of the year -", "Images\\calendar.dark.png")]
        [DataRow("week of month", "Week of the month -", "Images\\calendar.dark.png")]
        [DataRow("week of year", "Week of the year -", "Images\\calendar.dark.png")]
        [DataRow("month", "Month -", "Images\\calendar.dark.png")]
        [DataRow("month of year", "Month of the year -", "Images\\calendar.dark.png")]
        [DataRow("year", "Year -", "Images\\calendar.dark.png")]
        [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss (Date and time) -", "Images\\timeDate.dark.png")]
        [DataRow("iso date", "ISO 8601 (Date and time) -", "Images\\timeDate.dark.png")]
        [DataRow("iso utc date", "ISO 8601 UTC (Date and time) - ", "Images\\timeDate.dark.png")]
        [DataRow("iso zone", "ISO 8601 with time zone (Date and time) - ", "Images\\timeDate.dark.png")]
        [DataRow("iso utc zone", "ISO 8601 UTC with time zone (Date and time) - ", "Images\\timeDate.dark.png")]
        [DataRow("rfc", "RFC1123 (Date and time) -", "Images\\timeDate.dark.png")]

        public void IconThemeDarkTest(string typedString, string subTitleMatch, string expectedResult)
        {
            // Setup
            Mock<Main> main = new ();
            main.Object.IconTheme = "dark";
            string pluginKeyword = "(";
            Query expectedQuery = new (typedString, pluginKeyword);

            // Act
            string result = main.Object.Query(expectedQuery).FirstOrDefault(predicate: x => x.SubTitle.StartsWith(subTitleMatch, System.StringComparison.CurrentCulture)).IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("time", "Time -", "Images\\time.light.png")]
        [DataRow("time u", "Time UTC -", "Images\\time.light.png")]
        [DataRow("date", "Date -", "Images\\calendar.light.png")]
        [DataRow("now", "Now (Current date and time) -", "Images\\timeDate.light.png")]
        [DataRow("now u", "Now UTC (Current date and time) -", "Images\\timeDate.light.png")]
        [DataRow("unix", "Unix Timestamp (Current date and time) -", "Images\\timeDate.light.png")]
        [DataRow("file", "Windows file time (Current date and time", "Images\\timeDate.light.png")]
        [DataRow("day", "Day -", "Images\\calendar.light.png")]
        [DataRow("day of week", "Day of the week -", "Images\\calendar.light.png")]
        [DataRow("day of month", "Day of the month -", "Images\\calendar.light.png")]
        [DataRow("day of year", "Day of the year -", "Images\\calendar.light.png")]
        [DataRow("week of month", "Week of the month -", "Images\\calendar.light.png")]
        [DataRow("week of year", "Week of the year -", "Images\\calendar.light.png")]
        [DataRow("month", "Month -", "Images\\calendar.light.png")]
        [DataRow("month of year", "Month of the year -", "Images\\calendar.light.png")]
        [DataRow("year", "Year -", "Images\\calendar.light.png")]
        [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss (Date and time) -", "Images\\timeDate.light.png")]
        [DataRow("iso date", "ISO 8601 (Date and time) -", "Images\\timeDate.light.png")]
        [DataRow("iso utc date", "ISO 8601 UTC (Date and time) - ", "Images\\timeDate.light.png")]
        [DataRow("iso zone", "ISO 8601 with time zone (Date and time) - ", "Images\\timeDate.light.png")]
        [DataRow("iso utc zone", "ISO 8601 UTC with time zone (Date and time) - ", "Images\\timeDate.light.png")]
        [DataRow("rfc", "RFC1123 (Date and time) -", "Images\\timeDate.light.png")]
        public void IconThemeLightTest(string typedString, string subTitleMatch, string expectedResult)
        {
            // Setup
            Mock<Main> main = new ();
            main.Object.IconTheme = "light";
            string pluginKeyword = "(";
            Query expectedQuery = new (typedString, pluginKeyword);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault(x => x.SubTitle.StartsWith(subTitleMatch, System.StringComparison.CurrentCulture)).IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
