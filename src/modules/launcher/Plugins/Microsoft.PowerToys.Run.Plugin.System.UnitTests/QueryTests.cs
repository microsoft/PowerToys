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
    public class QueryTests
    {
        [TestInitialize]
        public void Setup()
        {
            StringMatcher.Instance = new StringMatcher();
        }

        [DataTestMethod]
        [DataRow("shutdown", "Shutdown computer")]
        [DataRow("restart", "Restart computer")]
        [DataRow("sign out", "Sign out of computer")]
        [DataRow("lock", "Lock computer")]
        [DataRow("sleep", "Put computer to sleep")]
        [DataRow("hibernate", "Hibernate computer")]
        [DataRow("empty recycle", "Empty Recycle Bin")]
        public void EnvironmentIndependentQueryResults(string typedString, string expectedResult)
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            Query expectedQuery = new Query(typedString);

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [TestMethod]
        public void UefiCommandIsAvailableOnUefiSystems()
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IsBootedInUefiMode = true; // Simulate system with UEFI.
            Query expectedQuery = new Query("uefi firm");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault().SubTitle;

            // Assert
            Assert.AreEqual("Reboot computer into UEFI Firmware Settings (Requires administrative permissions.)", result);
        }

        [TestMethod]
        public void UefiCommandIsNotAvailableOnSystemsWithoutUefi()
        {
            // Setup
            Mock<Main> main = new Mock<Main>();
            main.Object.IsBootedInUefiMode = false; // Simulate system without UEFI.
            Query expectedQuery = new Query("uefi firm");

            // Act
            var result = main.Object.Query(expectedQuery).FirstOrDefault();

            // Assert
            Assert.IsNull(result);
        }
    }
}
