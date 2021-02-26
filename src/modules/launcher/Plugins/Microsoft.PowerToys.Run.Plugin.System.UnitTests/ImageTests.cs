// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text;
using Mono.Collections.Generic;
using Moq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.System.UnitTests
{
    public class ImageTests
    {
        [SetUp]
        public void Setup()
        {
            StringMatcher.Instance = new StringMatcher();
        }

        [TestCase("shutdown", "Images\\shutdown.dark.png")]
        [TestCase("restart", "Images\\restart.dark.png")]
        [TestCase("sign out", "Images\\logoff.dark.png")]
        [TestCase("lock", "Images\\lock.dark.png")]
        [TestCase("sleep", "Images\\sleep.dark.png")]
        [TestCase("hibernate", "Images\\sleep.dark.png")]
        [TestCase("empty recycle", "Images\\recyclebin.dark.png")]
        public void IconThemeDarkTest(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "dark";
            Query expectedQuery = new Query(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [TestCase("shutdown", "Images\\shutdown.light.png")]
        [TestCase("restart", "Images\\restart.light.png")]
        [TestCase("sign out", "Images\\logoff.light.png")]
        [TestCase("lock", "Images\\lock.light.png")]
        [TestCase("sleep", "Images\\sleep.light.png")]
        [TestCase("hibernate", "Images\\sleep.light.png")]
        [TestCase("empty recycle", "Images\\recyclebin.light.png")]
        public void IconThemeLightTest(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "light";
            Query expectedQuery = new Query(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
