// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.System.UnitTests
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
        [DataRow("shutdown", "Images\\shutdown.dark.png")]
        [DataRow("restart", "Images\\restart.dark.png")]
        [DataRow("sign out", "Images\\logoff.dark.png")]
        [DataRow("lock", "Images\\lock.dark.png")]
        [DataRow("sleep", "Images\\sleep.dark.png")]
        [DataRow("hibernate", "Images\\sleep.dark.png")]
        [DataRow("empty recycle", "Images\\recyclebin.dark.png")]
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

        [DataTestMethod]
        [DataRow("shutdown", "Images\\shutdown.light.png")]
        [DataRow("restart", "Images\\restart.light.png")]
        [DataRow("sign out", "Images\\logoff.light.png")]
        [DataRow("lock", "Images\\lock.light.png")]
        [DataRow("sleep", "Images\\sleep.light.png")]
        [DataRow("hibernate", "Images\\sleep.light.png")]
        [DataRow("empty recycle", "Images\\recyclebin.light.png")]
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
