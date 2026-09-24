// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Parsing;
using System.IO.Abstractions.TestingHelpers;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Settings.Cli;
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

    [TestMethod]
    public void TestCommandParsingReportsMissingArguments()
    {
        var parser = new Parser(Program.CreateRootCommand());

        var parseResult = parser.Parse(["toggle"]);

        Assert.IsTrue(parseResult.Errors.Count > 0);
    }
}
