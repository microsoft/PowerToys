// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class BasicTests
    {
        [TestMethod]
        public void CommandsHelperTest()
        {
            // Setup & Act
            var commands = Commands.GetCommands();

            // Assert
            Assert.IsNotNull(commands);
            Assert.IsTrue(commands.Count > 0);
        }

        [TestMethod]
        public void IconsHelperTest()
        {
            // Setup & Act
            var shutdownIcon = Icons.GetIcon("shutdown");
            var restartIcon = Icons.GetIcon("restart");

            // Assert
            Assert.IsNotNull(shutdownIcon);
            Assert.IsNotNull(restartIcon);
        }

        [TestMethod]
        public void Win32HelpersTest()
        {
            // Setup & Act
            // These methods should not throw exceptions
            var isUefiMode = Win32Helpers.IsBootedInUefiMode();
            var isElevated = Win32Helpers.IsCurrentProcessElevated();

            // Assert
            // Just testing that they don't throw exceptions
            Assert.IsTrue(isUefiMode || !isUefiMode); // Boolean value is valid
            Assert.IsTrue(isElevated || !isElevated); // Boolean value is valid
        }

        [TestMethod]
        public void NetworkConnectionPropertiesTest()
        {
            // Setup
            var networkProperties = new NetworkConnectionProperties();

            // Act & Assert
            // These methods should not throw exceptions even if no network is available
            try
            {
                var ipv4 = networkProperties.GetLocalIPv4Address();
                var ipv6 = networkProperties.GetLocalIPv6Address();
                var macAddress = networkProperties.GetMacAddress();
                
                // Test passes if no exceptions are thrown
                Assert.IsTrue(true);
            }
            catch
            {
                Assert.Fail("Network properties methods should not throw exceptions");
            }
        }
    }
