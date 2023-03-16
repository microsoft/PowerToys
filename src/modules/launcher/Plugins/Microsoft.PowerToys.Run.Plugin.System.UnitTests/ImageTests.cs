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
        [DataRow("recycle bin", "Images\\recyclebin.dark.png")]
        [DataRow("uefi firmware settings", "Images\\firmwareSettings.dark.png")]
        [DataRow("ip v4 addr", "Images\\networkAdapter.dark.png", true)]
        [DataRow("ip v6 addr", "Images\\networkAdapter.dark.png", true)]
        [DataRow("mac addr", "Images\\networkAdapter.dark.png", true)]
        public void IconThemeDarkTest(string typedString, string expectedResult, bool isDelayed = default)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "dark";
            main.Object.IsBootedInUefiMode = true; // Set to true that we can test, regardless of the environment we run on.
            Query expectedQuery = new Query(typedString);

            // Act
            var result = !isDelayed ? main.Object.Query(expectedQuery).FirstOrDefault().IcoPath : main.Object.Query(expectedQuery, true).FirstOrDefault().IcoPath;

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
        [DataRow("recycle bin", "Images\\recyclebin.light.png")]
        [DataRow("uefi firmware settings", "Images\\firmwareSettings.light.png")]
        [DataRow("ipv4 addr", "Images\\networkAdapter.light.png", true)]
        [DataRow("ipv6 addr", "Images\\networkAdapter.light.png", true)]
        [DataRow("mac addr", "Images\\networkAdapter.light.png", true)]
        public void IconThemeLightTest(string typedString, string expectedResult, bool isDelayed = default)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IconTheme = "light";
            main.Object.IsBootedInUefiMode = true; // Set to true that we can test, regardless of the environment we run on.
            Query expectedQuery = new Query(typedString);

            // Act
            var result = !isDelayed ? main.Object.Query(expectedQuery).FirstOrDefault().IcoPath : main.Object.Query(expectedQuery, true).FirstOrDefault().IcoPath;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
