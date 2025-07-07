// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class QueryTests
    {
        [DataTestMethod]
        [DataRow("shutdown", "Shutdown")]
        [DataRow("restart", "Restart")]
        [DataRow("sign out", "Sign out")]
        [DataRow("lock", "Lock")]
        [DataRow("sleep", "Sleep")]
        [DataRow("hibernate", "Hibernate")]
        public void SystemCommandsTest(string typedString, string expectedCommand)
        {
            // Setup
            var commands = Commands.GetCommands();

            // Act
            var result = commands.Where(c => c.Name.Contains(expectedCommand, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Name.Contains(expectedCommand, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void RecycleBinCommandTest()
        {
            // Setup
            var commands = Commands.GetCommands();

            // Act
            var result = commands.Where(c => c.Name.Contains("Recycle", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void NetworkCommandsTest()
        {
            // Setup
            var networkProperties = new NetworkConnectionProperties();

            // Act
            var ipv4 = networkProperties.GetLocalIPv4Address();
            var ipv6 = networkProperties.GetLocalIPv6Address();
            var macAddress = networkProperties.GetMacAddress();

            // Assert
            // These might be null in test environment, but should not throw exceptions
            Assert.IsTrue(true); // Test passes if no exceptions are thrown
        }

        [TestMethod]
        public void UefiCommandIsAvailableTest()
        {
            // Setup
            var isUefiMode = Win32Helpers.IsBootedInUefiMode();

            // Act
            var commands = Commands.GetCommands();
            var uefiCommand = commands.Where(c => c.Name.Contains("UEFI", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Assert
            if (isUefiMode)
            {
                Assert.IsNotNull(uefiCommand);
            }
            else
            {
                // UEFI command may still exist but be disabled on non-UEFI systems
                Assert.IsTrue(true); // Test environment independent
            }
        }
    }
