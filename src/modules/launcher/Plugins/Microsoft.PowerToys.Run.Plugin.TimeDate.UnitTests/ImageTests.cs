// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
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
        [DataRow("time", "Time -", "Images\\time.dark.png")]
        [DataRow("time u", "Time UTC -", "Images\\time.dark.png")]
        [DataRow("date", "Date -", "Images\\calendar.dark.png")]
        [DataRow("now", "Now -", "Images\\timeDate.dark.png")]
        [DataRow("now u", "Now UTC -", "Images\\timeDate.dark.png")]
        [DataRow("unix", "Unix epoch time -", "Images\\timeDate.dark.png")]
        [DataRow("hour", "Hour -", "Images\\time.dark.png")]
        [DataRow("minute", "Minute -", "Images\\time.dark.png")]
        [DataRow("second", "Second -", "Images\\time.dark.png")]
        [DataRow("millisecond", "Millisecond -", "Images\\time.dark.png")]
        [DataRow("file", "Windows file time (Int64 number) -", "Images\\timeDate.dark.png")]
        [DataRow("day", "Day (Week day) -", "Images\\calendar.dark.png")]
        [DataRow("day of week", "Day of the week (Week day) -", "Images\\calendar.dark.png")]
        [DataRow("day of month", "Day of the month -", "Images\\calendar.dark.png")]
        [DataRow("day of year", "Day of the year -", "Images\\calendar.dark.png")]
        [DataRow("week of month", "Week of the month -", "Images\\calendar.dark.png")]
        [DataRow("week of year", "Week of the year (Calendar week, Week number) -", "Images\\calendar.dark.png")]
        [DataRow("month", "Month -", "Images\\calendar.dark.png")]
        [DataRow("month of year", "Month of the year -", "Images\\calendar.dark.png")]
        [DataRow("month and", "Month and day -", "Images\\calendar.dark.png")]
        [DataRow("year", "Year -", "Images\\calendar.dark.png")]
        [DataRow("era", "Era -", "Images\\calendar.dark.png")]
        [DataRow("era abb", "Era abbreviation -", "Images\\calendar.dark.png")]
        [DataRow("month and", "Month and year -", "Images\\calendar.dark.png")]
        [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss -", "Images\\timeDate.dark.png")]
        [DataRow("iso", "ISO 8601 -", "Images\\timeDate.dark.png")]
        [DataRow("iso utc", "ISO 8601 UTC - ", "Images\\timeDate.dark.png")]
        [DataRow("iso zone", "ISO 8601 with time zone - ", "Images\\timeDate.dark.png")]
        [DataRow("iso utc zone", "ISO 8601 UTC with time zone - ", "Images\\timeDate.dark.png")]
        [DataRow("rfc", "RFC1123 -", "Images\\timeDate.dark.png")]
        public void IconThemeDarkTest(string typedString, string subTitleMatch, string expectedResult)
        {
            // Setup
            Mock<Main> main = new();
            main.Object.IconTheme = "dark";
            Query expectedQuery = new("(" + typedString, "(");

            // Act
            string result = main.Object.Query(expectedQuery).FirstOrDefault(predicate: x => x.SubTitle.StartsWith(subTitleMatch, System.StringComparison.CurrentCulture)).IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("time", "Time -", "Images\\time.light.png")]
        [DataRow("time u", "Time UTC -", "Images\\time.light.png")]
        [DataRow("date", "Date -", "Images\\calendar.light.png")]
        [DataRow("now", "Now -", "Images\\timeDate.light.png")]
        [DataRow("now u", "Now UTC -", "Images\\timeDate.light.png")]
        [DataRow("unix", "Unix epoch time -", "Images\\timeDate.light.png")]
        [DataRow("hour", "Hour -", "Images\\time.light.png")]
        [DataRow("minute", "Minute -", "Images\\time.light.png")]
        [DataRow("second", "Second -", "Images\\time.light.png")]
        [DataRow("millisecond", "Millisecond -", "Images\\time.light.png")]
        [DataRow("file", "Windows file time (Int64 number) -", "Images\\timeDate.light.png")]
        [DataRow("day", "Day (Week day) -", "Images\\calendar.light.png")]
        [DataRow("day of week", "Day of the week (Week day) -", "Images\\calendar.light.png")]
        [DataRow("day of month", "Day of the month -", "Images\\calendar.light.png")]
        [DataRow("day of year", "Day of the year -", "Images\\calendar.light.png")]
        [DataRow("week of month", "Week of the month -", "Images\\calendar.light.png")]
        [DataRow("week of year", "Week of the year (Calendar week, Week number) -", "Images\\calendar.light.png")]
        [DataRow("month", "Month -", "Images\\calendar.light.png")]
        [DataRow("month of year", "Month of the year -", "Images\\calendar.light.png")]
        [DataRow("month and", "Month and day -", "Images\\calendar.light.png")]
        [DataRow("year", "Year -", "Images\\calendar.light.png")]
        [DataRow("era", "Era -", "Images\\calendar.light.png")]
        [DataRow("era abb", "Era abbreviation -", "Images\\calendar.light.png")]
        [DataRow("Month and", "Month and year -", "Images\\calendar.light.png")]
        [DataRow("universal", "Universal time format: YYYY-MM-DD hh:mm:ss -", "Images\\timeDate.light.png")]
        [DataRow("iso", "ISO 8601 -", "Images\\timeDate.light.png")]
        [DataRow("iso utc", "ISO 8601 UTC - ", "Images\\timeDate.light.png")]
        [DataRow("iso zone", "ISO 8601 with time zone - ", "Images\\timeDate.light.png")]
        [DataRow("iso utc zone", "ISO 8601 UTC with time zone - ", "Images\\timeDate.light.png")]
        [DataRow("rfc", "RFC1123 -", "Images\\timeDate.light.png")]
        public void IconThemeLightTest(string typedString, string subTitleMatch, string expectedResult)
        {
            // Setup
            Mock<Main> main = new();
            main.Object.IconTheme = "light";
            Query expectedQuery = new("(" + typedString, "(");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault(x => x.SubTitle.StartsWith(subTitleMatch, System.StringComparison.CurrentCulture)).IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
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
