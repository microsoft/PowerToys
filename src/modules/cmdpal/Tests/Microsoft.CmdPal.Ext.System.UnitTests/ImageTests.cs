// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CmdPal.Ext.System.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

[TestClass]
public class ImageTests
{
    [DataTestMethod]
    [DataRow("shutdown", "ShutdownIcon")]
    [DataRow("restart", "RestartIcon")]
    [DataRow("sign out", "LogoffIcon")]
    [DataRow("lock", "LockIcon")]
    [DataRow("sleep", "SleepIcon")]
    [DataRow("hibernate", "SleepIcon")]
    [DataRow("recycle bin", "RecycleBinIcon")]
    [DataRow("uefi firmware settings", "FirmwareSettingsIcon")]
    [DataRow("IPv4 addr", "NetworkAdapterIcon")]
    [DataRow("IPV6 addr", "NetworkAdapterIcon")]
    [DataRow("MAC addr", "NetworkAdapterIcon")]
    public void IconThemeDarkTest(string typedString, string expectedIconPropertyName)
    {
        var systemPage = new SystemCommandPage(new SettingsManager());

        foreach (var item in systemPage.GetItems())
        {
            if (item.Title.Contains(typedString, StringComparison.OrdinalIgnoreCase) || item.Subtitle.Contains(typedString, StringComparison.OrdinalIgnoreCase))
            {
                var icon = item.Icon;
                Assert.IsNotNull(icon, $"Icon for '{typedString}' should not be null.");
                Assert.IsNotEmpty(icon.Dark.Icon, $"Icon for '{typedString}' should not be empty.");
            }
        }
    }

    [DataTestMethod]
    [DataRow("shutdown", "ShutdownIcon")]
    [DataRow("restart", "RestartIcon")]
    [DataRow("sign out", "LogoffIcon")]
    [DataRow("lock", "LockIcon")]
    [DataRow("sleep", "SleepIcon")]
    [DataRow("hibernate", "SleepIcon")]
    [DataRow("recycle bin", "RecycleBinIcon")]
    [DataRow("uefi firmware settings", "FirmwareSettingsIcon")]
    [DataRow("IPv4 addr", "NetworkAdapterIcon")]
    [DataRow("IPV6 addr", "NetworkAdapterIcon")]
    [DataRow("MAC addr", "NetworkAdapterIcon")]
    public void IconThemeLightTest(string typedString, string expectedIconPropertyName)
    {
        var systemPage = new SystemCommandPage(new SettingsManager());

        foreach (var item in systemPage.GetItems())
        {
            if (item.Title.Contains(typedString, StringComparison.OrdinalIgnoreCase) || item.Subtitle.Contains(typedString, StringComparison.OrdinalIgnoreCase))
            {
                var icon = item.Icon;
                Assert.IsNotNull(icon, $"Icon for '{typedString}' should not be null.");
                Assert.IsNotEmpty(icon.Light.Icon, $"Icon for '{typedString}' should not be empty.");
            }
        }
    }
}
