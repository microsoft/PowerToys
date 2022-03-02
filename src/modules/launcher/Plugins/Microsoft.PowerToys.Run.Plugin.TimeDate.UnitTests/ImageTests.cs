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
        [DataRow("time", "Images\\time.dark.png")]
        [DataRow("date", "Images\\calendar.dark.png")]
        [DataRow("date and time", "Images\\timeDate.dark.png")]
        [DataRow("unix", "Images\\timeDate.dark.png")]
        [DataRow("day", "Images\\timeDate.dark.png")]
        [DataRow("day of week", "Images\\calendar.dark.png")]
        [DataRow("day of months", "Images\\calendar.dark.png")]
        [DataRow("day of year", "Images\\calendar.dark.png")]
        [DataRow("week of month", "Images\\calendar.dark.png")]
        [DataRow("week of year", "Images\\calendar.dark.png")]
        [DataRow("month", "Images\\calendar.dark.png")]
        [DataRow("month of year", "Images\\calendar.dark.png")]
        [DataRow("year", "Images\\calendar.dark.png")]
        public void IconThemeDarkTest(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "dark";
            string pluginKeyword = "(";
            Query expectedQuery = new Query(pluginKeyword + typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [DataTestMethod]
        [DataRow("time", "Images\\time.light.png")]
        [DataRow("date", "Images\\calendar.light.png")]
        [DataRow("date and time", "Images\\timeDate.light.png")]
        [DataRow("unix", "Images\\timeDate.light.png")]
        [DataRow("day", "Images\\timeDate.light.png")]
        [DataRow("day of week", "Images\\calendar.light.png")]
        [DataRow("day of months", "Images\\calendar.light.png")]
        [DataRow("day of year", "Images\\calendar.light.png")]
        [DataRow("week of month", "Images\\calendar.light.png")]
        [DataRow("week of year", "Images\\calendar.light.png")]
        [DataRow("month", "Images\\calendar.light.png")]
        [DataRow("month of year", "Images\\calendar.light.png")]
        [DataRow("year", "Images\\calendar.light.png")]
        public void IconThemeLightTest(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "light";
            string pluginKeyword = "(";
            Query expectedQuery = new Query(pluginKeyword + typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
