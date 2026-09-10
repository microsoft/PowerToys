// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Settings.Cli.Helpers;

namespace Settings.UI.UnitTests.Cmd;

[TestClass]
public class SettingsCliTests
{
    private SettingsUtils settingsUtils;

    [TestInitialize]
    public void Setup()
    {
        settingsUtils = new SettingsUtils(new MockFileSystem());
    }

    [TestMethod]
    public void TestGetModulesAndStatus()
    {
        var modules = SettingsCliHelper.GetModulesAndStatus(settingsUtils);

        Assert.IsNotNull(modules);
        Assert.IsTrue(modules.Count > 0);
        Assert.IsTrue(modules.ContainsKey("FancyZones"));
        Assert.IsTrue(modules.ContainsKey("AlwaysOnTop"));
    }

    [TestMethod]
    public void TestGetModuleSettings()
    {
        var fancyZonesSettings = SettingsCliHelper.GetModuleSettings("FancyZones", settingsUtils);

        Assert.IsNotNull(fancyZonesSettings);
        Assert.IsTrue(fancyZonesSettings.Count > 0);
        Assert.IsTrue(fancyZonesSettings.ContainsKey("FancyzonesShiftDrag"));
    }

    [TestMethod]
    public void TestGetAndSetSettingValue()
    {
        var defaultValue = SettingsCliHelper.GetSettingValue("AlwaysOnTop.SoundEnabled", settingsUtils);
        Assert.IsNotNull(defaultValue);

        SettingsCliHelper.SetSettingValue("AlwaysOnTop.SoundEnabled", "false", settingsUtils);

        var newValue = SettingsCliHelper.GetSettingValue("AlwaysOnTop.SoundEnabled", settingsUtils);
        Assert.AreEqual(false, newValue);
    }

    [TestMethod]
    public void TestToggleModule()
    {
        var modulesBefore = SettingsCliHelper.GetModulesAndStatus(settingsUtils);
        var initialFancyZonesStatus = modulesBefore["FancyZones"];

        var toggledState = SettingsCliHelper.ToggleModule("FancyZones", targetState: null, settingsUtils);
        Assert.AreEqual(!initialFancyZonesStatus, toggledState);

        var modulesAfter = SettingsCliHelper.GetModulesAndStatus(settingsUtils);
        Assert.AreEqual(!initialFancyZonesStatus, modulesAfter["FancyZones"]);

        // Explicit enable
        var enabledState = SettingsCliHelper.ToggleModule("FancyZones", targetState: true, settingsUtils);
        Assert.IsTrue(enabledState);
    }
}
