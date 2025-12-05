// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CmdPal.Ext.System.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [DataTestMethod]
    [DataRow("shutdown", "Shutdown")]
    [DataRow("restart", "Restart")]
    [DataRow("sign out", "Sign out")]
    [DataRow("lock", "Lock")]
    [DataRow("sleep", "Sleep")]
    [DataRow("hibernate", "Hibernate")]
    [DataRow("open recycle", "Open Recycle Bin")]
    [DataRow("empty recycle", "Empty Recycle Bin")]
    [DataRow("uefi", "UEFI firmware settings")]
    public void TopLevelPageQueryTest(string input, string matchedTitle)
    {
        var settings = new Settings();
        var pages = new SystemCommandPage(settings);
        var allCommands = pages.GetItems();

        var result = Query(input, allCommands);

        // Empty recycle bin command should exist
        Assert.IsNotNull(result);

        var firstItem = result.FirstOrDefault();

        Assert.IsNotNull(firstItem, "No items matched the query.");
        Assert.AreEqual(matchedTitle, firstItem.Title, $"Expected to match '{input}' but got '{firstItem.Title}'");
    }

    [TestMethod]
    public void RecycleBinCommandTest()
    {
        var settings = new Settings(hideEmptyRecycleBin: true);
        var pages = new SystemCommandPage(settings);
        var allCommands = pages.GetItems();

        var result = Query("recycle", allCommands);

        // Empty recycle bin command should exist
        Assert.IsNotNull(result);

        foreach (var item in result)
        {
            if (item.Title.Contains("Open Recycle Bin") || item.Title.Contains("Empty Recycle Bin"))
            {
                Assert.Fail("Recycle Bin commands should not be available when hideEmptyRecycleBin is true.");
            }
        }

        var firstItem = result.FirstOrDefault();
        Assert.IsNotNull(firstItem, "No items matched the query.");
        Assert.IsTrue(
            firstItem.Title.Contains("Recycle Bin", StringComparison.OrdinalIgnoreCase),
            $"Expected to match 'Recycle Bin' but got '{firstItem.Title}'");
    }

    [TestMethod]
    public void NetworkCommandsTest()
    {
        var settings = new Settings();
        var pages = new SystemCommandPage(settings);
        var allCommands = pages.GetItems();

        var ipv4Result = Query("IPv4", allCommands);

        Assert.IsNotNull(ipv4Result);
        Assert.IsTrue(ipv4Result.Length > 0, "No IPv4 commands matched the query.");

        var ipv6Result = Query("IPv6", allCommands);
        Assert.IsNotNull(ipv6Result);
        Assert.IsTrue(ipv6Result.Length > 0, "No IPv6 commands matched the query.");

        var macResult = Query("MAC", allCommands);
        Assert.IsNotNull(macResult);
        Assert.IsTrue(macResult.Length > 0, "No MAC commands matched the query.");

        var findDisconnectedMACResult = false;
        foreach (var item in macResult)
        {
            if (item.Details.Body.Contains("Disconnected"))
            {
                findDisconnectedMACResult = true;
                break;
            }
        }

        Assert.IsTrue(findDisconnectedMACResult, "No disconnected MAC address found in the results.");
    }

    [TestMethod]
    public void HideDisconnectedNetworkInfoTest()
    {
        var settings = new Settings(hideDisconnectedNetworkInfo: true);
        var pages = new SystemCommandPage(settings);
        var allCommands = pages.GetItems();

        var macResult = Query("MAC", allCommands);
        Assert.IsNotNull(macResult);
        Assert.IsTrue(macResult.Length > 0, "No MAC commands matched the query.");

        var findDisconnectedMACResult = false;
        foreach (var item in macResult)
        {
            if (item.Details.Body.Contains("Disconnected"))
            {
                findDisconnectedMACResult = true;
                break;
            }
        }

        Assert.IsTrue(!findDisconnectedMACResult, "Disconnected MAC address found in the results.");
    }

    [TestMethod]
    [DataRow(FirmwareType.Uefi, true)]
    [DataRow(FirmwareType.Bios, false)]
    [DataRow(FirmwareType.Max, false)]
    [DataRow(FirmwareType.Unknown, false)]
    public void FirmwareSettingsTest(FirmwareType firmwareType, bool hasCommand)
    {
        var settings = new Settings(firmwareType: firmwareType);
        var pages = new SystemCommandPage(settings);
        var allCommands = pages.GetItems();
        var result = Query("UEFI", allCommands);

        // UEFI Firmware Settings command should exist
        Assert.IsNotNull(result);
        var firstItem = result.FirstOrDefault();
        var firstItemIsUefiCommand = firstItem?.Title.Contains("UEFI", StringComparison.OrdinalIgnoreCase) ?? false;
        Assert.AreEqual(hasCommand, firstItemIsUefiCommand, $"Expected to match (or not match) 'UEFI firmware settings' but got '{firstItem?.Title}'");
    }
}
