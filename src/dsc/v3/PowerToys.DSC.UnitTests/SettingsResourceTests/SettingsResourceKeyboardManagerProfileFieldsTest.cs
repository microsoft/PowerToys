// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

/// <summary>
/// The settings resource on Keyboard Manager, end to end: it applies the
/// module's own options and leaves the active profile and the profile list,
/// which the engine and the editor own, as they are.
/// </summary>
[TestClass]
public sealed class SettingsResourceKeyboardManagerProfileFieldsTest : BaseDscTest
{
    private const string OnlyUseNewEditor = "{\"settings\":{\"properties\":{\"useNewEditor\":false}}}";

    private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;

    private string _originalSettings;

    private static string Module => nameof(ModuleType.KeyboardManager);

    private static string SettingsPath => _settingsUtils.GetSettingsFilePath(KeyboardManagerSettings.ModuleName, "settings.json");

    [TestInitialize]
    public void TestInitialize()
    {
        // Save the actual settings, then start from: "work" active, three profiles
        _originalSettings = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;

        var settings = new KeyboardManagerSettings();
        settings.Properties.ActiveConfiguration.Value = "work";
        settings.Properties.KeyboardConfigurations.Value = ["default", "work", "home"];
        settings.Properties.UseNewEditor = true;
        _settingsUtils.SaveSettings(settings.ToJsonString(), KeyboardManagerSettings.ModuleName);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (_originalSettings != null)
        {
            File.WriteAllText(SettingsPath, _originalSettings);
        }
        else if (File.Exists(SettingsPath))
        {
            File.Delete(SettingsPath);
        }
    }

    [TestMethod]
    public void Set_OnlyUseNewEditor_KeepsTheActiveProfileAndTheProfileList()
    {
        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", OnlyUseNewEditor);
        var (state, _) = result.OutputStateAndDiff<SettingsResourceObject<KeyboardManagerSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        var stored = _settingsUtils.GetSettingsOrDefaultReadOnly<KeyboardManagerSettings>(KeyboardManagerSettings.ModuleName);
        Assert.IsFalse(stored.Properties.UseNewEditor);
        Assert.AreEqual("work", stored.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, stored.Properties.KeyboardConfigurations.Value);
        Assert.AreEqual("work", state.Settings.Properties.ActiveConfiguration.Value);
    }

    [TestMethod]
    public void Set_WithProfileFieldsInTheInput_DoesNotApplyThem()
    {
        // Arrange: e.g. an export from another machine
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"office\"},\"keyboardConfigurations\":{\"value\":[\"default\",\"office\"]},\"useNewEditor\":false}}}";

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);

        // Assert
        Assert.IsTrue(result.Success);
        var stored = _settingsUtils.GetSettingsOrDefaultReadOnly<KeyboardManagerSettings>(KeyboardManagerSettings.ModuleName);
        Assert.IsFalse(stored.Properties.UseNewEditor);
        Assert.AreEqual("work", stored.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, stored.Properties.KeyboardConfigurations.Value);

        // Not silently: one warning for each of the two fields
        var warnings = result.Messages().FindAll(message => message.Level == DscMessageLevel.Warning);
        Assert.AreEqual(2, warnings.Count);
        StringAssert.Contains(warnings[0].Message, "activeConfiguration");
        StringAssert.Contains(warnings[1].Message, "keyboardConfigurations");
    }

    [TestMethod]
    public void Test_OnlyUseNewEditor_AlreadyApplied_IsInDesiredState()
    {
        // Arrange
        var settings = _settingsUtils.GetSettingsOrDefaultReadOnly<KeyboardManagerSettings>(KeyboardManagerSettings.ModuleName);
        settings.Properties.UseNewEditor = false;
        _settingsUtils.SaveSettings(settings.ToJsonString(), KeyboardManagerSettings.ModuleName);

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", OnlyUseNewEditor);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<KeyboardManagerSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(state.InDesiredState);
        Assert.AreEqual(0, diff.Count);
    }
}
