// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Parsing;
using System.IO;
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
    private MockFileSystem mockFileSystem;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = new MockFileSystem();
        settingsUtils = new SettingsUtils(mockFileSystem);
    }

    [TestMethod]
    public void TestGetModulesAndStatus()
    {
        var modules = SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null);

        Assert.IsNotNull(modules);
        Assert.IsTrue(modules.Count > 0);
        Assert.IsTrue(modules.ContainsKey("FancyZones"));
        Assert.IsTrue(modules.ContainsKey("AlwaysOnTop"));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [TestMethod]
    public void TestReadOnlyModuleStatusDoesNotOverwriteInvalidSettings()
    {
        const string corruptSettings = "{";
        var settingsFilePath = settingsUtils.GetSettingsFilePath();
        mockFileSystem.AddFile(settingsFilePath, new MockFileData(corruptSettings));

        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null));

        Assert.AreEqual(corruptSettings, mockFileSystem.File.ReadAllText(settingsFilePath));
    }

    [TestMethod]
    public void TestGetModuleStatus()
    {
        var status = SettingsCliHelper.GetModuleStatus("fancyzones", settingsUtils, _ => null);

        Assert.AreEqual("FancyZones", status.ModuleName);
        Assert.IsNull(status.GroupPolicy);
    }

    [TestMethod]
    public void TestSetModuleEnabled()
    {
        var disabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: false, settingsUtils, _ => null, () => EmptyDisposable.Instance);
        Assert.IsFalse(disabledState.Enabled);

        var modulesAfterDisable = SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null);
        Assert.IsFalse(modulesAfterDisable["FancyZones"]);

        var enabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: true, settingsUtils, _ => null, () => EmptyDisposable.Instance);
        Assert.IsTrue(enabledState.Enabled);
    }

    [TestMethod]
    public void TestGroupPolicyOverridesEffectiveState()
    {
        var status = SettingsCliHelper.GetModuleStatus(
            "FancyZones",
            settingsUtils,
            _ => false);

        Assert.IsFalse(status.Enabled);
        Assert.AreEqual("Disabled", status.GroupPolicy);
    }

    [TestMethod]
    public void TestSetModuleEnabledRejectsGroupPolicyLockedModule()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: true,
                settingsUtils,
                _ => false,
                () => EmptyDisposable.Instance));
        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                settingsUtils,
                _ => true,
                () => EmptyDisposable.Instance));
    }

    [TestMethod]
    public void TestSetModuleEnabledPropagatesSaveFailure()
    {
        var failingSettingsUtils = new FailingSaveSettingsUtils();

        Assert.ThrowsException<IOException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                failingSettingsUtils,
                _ => null,
                () => EmptyDisposable.Instance));
    }

    [TestMethod]
    public void TestSetModuleEnabledRejectsWhenSettingsAreLocked()
    {
        Assert.ThrowsException<IOException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                settingsUtils,
                _ => null,
                () => throw new IOException("Settings lock is held.")));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    [DataTestMethod]
    [DataRow("enable")]
    [DataRow("disable")]
    [DataRow("status")]
    public void TestCommandParsingReportsMissingArguments(string command)
    {
        var parser = new Parser(Program.CreateRootCommand());

        var parseResult = parser.Parse([command]);

        Assert.IsTrue(parseResult.Errors.Count > 0);
    }

    private sealed class FailingSaveSettingsUtils : SettingsUtils
    {
        public FailingSaveSettingsUtils()
            : base(new MockFileSystem())
        {
        }

        public override void SaveSettingsOrThrow(string jsonSettings, string powertoy = "", string fileName = SettingsUtils.DefaultFileName)
        {
            throw new IOException("Simulated settings write failure.");
        }
    }
}
