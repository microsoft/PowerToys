// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowWalker.UnitTests;

[TestClass]
public class PluginSettingsTests
{
    [DataTestMethod]
    [DataRow("ResultsFromVisibleDesktopOnly")]
    [DataRow("SubtitleShowPid")]
    [DataRow("SubtitleShowDesktopName")]
    [DataRow("ConfirmKillProcess")]
    [DataRow("KillProcessTree")]
    [DataRow("OpenAfterKillAndClose")]
    [DataRow("HideKillProcessOnElevatedProcesses")]
    [DataRow("HideExplorerSettingInfo")]
    [DataRow("InMruOrder")]
    public void DoesSettingExist(string name)
    {
        // Setup
        Type settings = SettingsManager.Instance?.GetType();

        // Act
        var result = settings?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

        // Assert
        Assert.IsNotNull(result);
    }

    [DataTestMethod]
    [DataRow("ResultsFromVisibleDesktopOnly", false)]
    [DataRow("SubtitleShowPid", false)]
    [DataRow("SubtitleShowDesktopName", true)]
    [DataRow("ConfirmKillProcess", true)]
    [DataRow("KillProcessTree", false)]
    [DataRow("OpenAfterKillAndClose", false)]
    [DataRow("HideKillProcessOnElevatedProcesses", false)]
    [DataRow("HideExplorerSettingInfo", true)]
    [DataRow("InMruOrder", true)]
    public void DefaultValues(string name, bool valueExpected)
    {
        // Setup
        SettingsManager setting = SettingsManager.Instance;

        // Act
        PropertyInfo propertyInfo = setting?.GetType()?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        var result = propertyInfo?.GetValue(setting);

        // Assert
        Assert.AreEqual(valueExpected, result);
    }
}
