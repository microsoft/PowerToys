// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class BasicTests
{
    [TestMethod]
    public void IconsHelperTest()
    {
        // Assert
        Assert.IsNotNull(Icons.FirmwareSettingsIcon);
        Assert.IsNotNull(Icons.LockIcon);
        Assert.IsNotNull(Icons.LogoffIcon);
        Assert.IsNotNull(Icons.NetworkAdapterIcon);
        Assert.IsNotNull(Icons.RecycleBinIcon);
        Assert.IsNotNull(Icons.RestartIcon);
        Assert.IsNotNull(Icons.RestartShellIcon);
        Assert.IsNotNull(Icons.ShutdownIcon);
        Assert.IsNotNull(Icons.SleepIcon);
    }

    [TestMethod]
    public void Win32HelpersTest()
    {
        // Setup & Act
        // These methods should not throw exceptions
        var firmwareType = Win32Helpers.GetSystemFirmwareType();

        // Assert
        // Just testing that they don't throw exceptions
        Assert.IsTrue(Enum.IsDefined(typeof(FirmwareType), firmwareType));
    }

    [TestMethod]
    public void NetworkConnectionPropertiesTest()
    {
        // Test that network connection properties can be accessed without throwing exceptions
        try
        {
            var networkPropertiesList = NetworkConnectionProperties.GetList();

            // If we have network connections, test accessing their properties
            if (networkPropertiesList.Count > 0)
            {
                var networkProperties = networkPropertiesList[0];

                // Access properties (these used to be methods)
                var ipv4 = networkProperties.IPv4;
                var ipv6 = networkProperties.IPv6Primary;
                var macAddress = networkProperties.PhysicalAddress;

                // Test passes if no exceptions are thrown
                Assert.IsTrue(true);
            }
            else
            {
                // If no network connections, test still passes
                Assert.IsTrue(true);
            }
        }
        catch
        {
            Assert.Fail("Network properties should not throw exceptions");
        }
    }
}
