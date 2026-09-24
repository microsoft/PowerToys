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
    public void TestGetModuleStatus()
    {
        var status = SettingsCliHelper.GetModuleStatus("fancyzones", settingsUtils);

        Assert.AreEqual("FancyZones", status.ModuleName);
        Assert.IsNull(status.GroupPolicy);
    }

    [TestMethod]
    public void TestSetModuleEnabled()
    {
        var disabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: false, settingsUtils);
        Assert.IsFalse(disabledState.Enabled);

        var modulesAfterDisable = SettingsCliHelper.GetModulesAndStatus(settingsUtils);
        Assert.IsFalse(modulesAfterDisable["FancyZones"]);

        var enabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: true, settingsUtils);
        Assert.IsTrue(enabledState.Enabled);
    }

    [TestMethod]
    public void TestCommandParsingReportsMissingArguments()
    {
        var parser = new Parser(Program.CreateRootCommand());

        var parseResult = parser.Parse(["enable"]);

        Assert.IsTrue(parseResult.Errors.Count > 0);
    }
}
