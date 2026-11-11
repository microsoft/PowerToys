// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class RefreshRateSettingsTests
{
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, true)]
    [DataRow(60, true)]
    [DataRow(144, true)]
    public void IsSelectableRefreshRate_ExcludesHardwareDefaultSentinels(int frequency, bool expected)
    {
        Assert.AreEqual(expected, DisplayRefreshRateService.IsSelectableRefreshRate(frequency));
    }

    [TestMethod]
    public void MonitorInfo_SavedBeforeRefreshRateExisted_DefaultsToEnabled()
    {
        var monitor = JsonSerializer.Deserialize<MonitorInfo>("""{ "id": "x", "enableRotation": true }""");

        Assert.IsNotNull(monitor);
        Assert.IsTrue(monitor.EnableRefreshRate);
    }

    [TestMethod]
    public void MonitorInfo_ExplicitlyDisabledRefreshRate_StaysDisabled()
    {
        var monitor = JsonSerializer.Deserialize<MonitorInfo>("""{ "id": "x", "enableRefreshRate": false }""");

        Assert.IsNotNull(monitor);
        Assert.IsFalse(monitor.EnableRefreshRate);
    }
}
