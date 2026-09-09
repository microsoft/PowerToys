// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
    public void UpdateShutdownFlagsTest()
    {
        // SHUTDOWN_INSTALL_UPDATES | SHUTDOWN_RESTART
        Assert.AreEqual(0x44u, WindowsUpdateHelper.GetUpdateShutdownFlags(restart: true));

        // SHUTDOWN_INSTALL_UPDATES | SHUTDOWN_POWEROFF
        Assert.AreEqual(0x48u, WindowsUpdateHelper.GetUpdateShutdownFlags(restart: false));
    }

    [TestMethod]
    public void UpdatePendingDetectionDoesNotThrowTest()
    {
        // The WUAPI query must never throw; on any failure it reports false. The actual
        // value depends on the machine's update state, but back-to-back calls within the
        // cache interval must agree (the second call takes the cached path).
        var first = WindowsUpdateHelper.IsUpdatePending();
        var second = WindowsUpdateHelper.IsUpdatePending();

        Assert.AreEqual(first, second, "Cached query should return the same value within the cache interval.");
    }

    [TestMethod]
    public void ExecuteCommandSurfacesFuncResult()
    {
        // A command built from a Func<CommandResult> must return that result rather than
        // always dismissing, so a failed action can keep the palette open.
        var command = new ExecuteCommand(() => CommandResult.ShowToast("failure"));

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
    }

    [TestMethod]
    public void ConfirmationCommandDirectPathSurfacesFailureResult()
    {
        // Confirmation off: invoking runs the action directly and must return its result
        // (here a failure toast) instead of silently dismissing.
        var command = new ExecuteCommandConfirmation("name", confirm: false, "message", () => CommandResult.ShowToast("failure"));

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
    }

    [TestMethod]
    public void ConfirmationCommandConfirmedPathSurfacesFailureResult()
    {
        // Confirmation on: Invoke returns a Confirm result, and the primary command it
        // carries must surface the same failure result when the user confirms.
        var command = new ExecuteCommandConfirmation("name", confirm: true, "message", () => CommandResult.ShowToast("failure"));

        var confirmResult = command.Invoke();
        Assert.AreEqual(CommandResultKind.Confirm, confirmResult.Kind);

        var args = confirmResult.Args as ConfirmationArgs;
        Assert.IsNotNull(args, "Confirm result should carry ConfirmationArgs.");

        var primary = args.PrimaryCommand as InvokableCommand;
        Assert.IsNotNull(primary, "Confirmation should carry an invokable primary command.");

        var innerResult = primary.Invoke();
        Assert.AreEqual(CommandResultKind.ShowToast, innerResult.Kind);
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
