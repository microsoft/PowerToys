// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.CmdPal.Ext.System.Helpers;
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
    [DataRow("ip v4 addr", "NetworkAdapterIcon")]
    [DataRow("ip v6 addr", "NetworkAdapterIcon")]
    [DataRow("mac addr", "NetworkAdapterIcon")]
    public void IconThemeDarkTest(string typedString, string expectedIconPropertyName)
    {
        // Setup
        var iconProperty = typeof(Icons).GetProperty(expectedIconPropertyName);
        var iconInfo = iconProperty?.GetValue(null) as IconInfo;

        // Act
        var result = iconInfo.Dark;

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Icon);
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
    [DataRow("ip v4 addr", "NetworkAdapterIcon")]
    [DataRow("ip v6 addr", "NetworkAdapterIcon")]
    [DataRow("mac addr", "NetworkAdapterIcon")]
    public void IconThemeLightTest(string typedString, string expectedIconPropertyName)
    {
        // Setup
        var iconProperty = typeof(Icons).GetProperty(expectedIconPropertyName);
        var iconInfo = iconProperty?.GetValue(null) as IconInfo;

        // Act
        var result = iconInfo.Light;

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Icon);
    }
}
