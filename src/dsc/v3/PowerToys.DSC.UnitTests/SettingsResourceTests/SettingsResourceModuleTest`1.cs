// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

public abstract class SettingsResourceModuleTest<TSettingsConfig> : BaseDscTest
    where TSettingsConfig : ISettingsConfig, new()
{
    private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
    private TSettingsConfig _originalSettings;

    protected TSettingsConfig DefaultSettings => new();

    protected string Module { get; }

    protected List<string> DiffSettings { get; } = [SettingsResourceObject<AwakeSettings>.SettingsJsonPropertyName];

    protected List<string> DiffEmpty { get; } = [];

    public SettingsResourceModuleTest(string module)
    {
        Module = module;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _originalSettings = GetSettings();
        ResetSettingsToDefaultValues();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        SaveSettings(_originalSettings);
    }

    [TestMethod]
    public void Get_Success()
    {
        // Arrange
        var settingsBeforeExecute = GetSettings();

        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(settingsBeforeExecute, GetSettings());
        AssertStateAndSettingsAreEqual(settingsBeforeExecute, state);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Arrange
        var settingsBeforeExecute = GetSettings();

        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(settingsBeforeExecute, GetSettings());
        AssertStateAndSettingsAreEqual(settingsBeforeExecute, state);
    }

    [TestMethod]
    public void SetWithDiff_Success()
    {
        // Arrange
        var settingsModifier = GetSettingsModifier();
        var input = CreateInputResourceObject(settingsModifier);

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsHasChanged(settingsModifier);
        AssertStateAndSettingsAreEqual(GetSettings(), state);
        CollectionAssert.AreEqual(DiffSettings, diff);
    }

    [TestMethod]
    public void SetWithoutDiff_Success()
    {
        // Arrange
        var settingsModifier = GetSettingsModifier();
        UpdateSettings(settingsModifier);
        var settingsBeforeExecute = GetSettings();
        var input = CreateInputResourceObject(settingsModifier);

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(settingsBeforeExecute, GetSettings());
        AssertStateAndSettingsAreEqual(settingsBeforeExecute, state);
        CollectionAssert.AreEqual(DiffEmpty, diff);
    }

    [TestMethod]
    public void TestWithDiff_Success()
    {
        // Arrange
        var settingsModifier = GetSettingsModifier();
        var settingsBeforeExecute = GetSettings();
        var input = CreateInputResourceObject(settingsModifier);

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(settingsBeforeExecute, GetSettings());
        AssertStateAndSettingsAreEqual(settingsBeforeExecute, state);
        CollectionAssert.AreEqual(DiffSettings, diff);
        Assert.IsFalse(state.InDesiredState);
    }

    [TestMethod]
    public void TestWithoutDiff_Success()
    {
        // Arrange
        var settingsModifier = GetSettingsModifier();
        UpdateSettings(settingsModifier);
        var settingsBeforeExecute = GetSettings();
        var input = CreateInputResourceObject(settingsModifier);

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(settingsBeforeExecute, GetSettings());
        AssertStateAndSettingsAreEqual(settingsBeforeExecute, state);
        CollectionAssert.AreEqual(DiffEmpty, diff);
        Assert.IsTrue(state.InDesiredState);
    }

    /// <summary>
    /// Gets the settings modifier action for the specific settings configuration.
    /// </summary>
    /// <returns>An action that modifies the settings configuration.</returns>
    protected abstract Action<TSettingsConfig> GetSettingsModifier();

    /// <summary>
    /// Resets the settings to default values.
    /// </summary>
    private void ResetSettingsToDefaultValues()
    {
        SaveSettings(DefaultSettings);
    }

    /// <summary>
    /// Get the settings for the specified module.
    /// </summary>
    /// <returns>An instance of the settings type with the current configuration.</returns>
    private TSettingsConfig GetSettings()
    {
        return _settingsUtils.GetSettingsOrDefault<TSettingsConfig>(DefaultSettings.GetModuleName());
    }

    /// <summary>
    /// Saves the settings for the specified module.
    /// </summary>
    /// <param name="settings">Settings to save.</param>
    private void SaveSettings(TSettingsConfig settings)
    {
        _settingsUtils.SaveSettings(JsonSerializer.Serialize(settings), DefaultSettings.GetModuleName());
    }

    /// <summary>
    /// Create the resource object for the operation.
    /// </summary>
    /// <param name="settings">Settings to include in the resource object.</param>
    /// <returns>A JSON string representing the resource object.</returns>
    private string CreateResourceObject(TSettingsConfig settings)
    {
        var resourceObject = new SettingsResourceObject<TSettingsConfig>
        {
            Settings = settings,
        };
        return JsonSerializer.Serialize(resourceObject);
    }

    private string CreateInputResourceObject(Action<TSettingsConfig> settingsModifier)
    {
        var settings = DefaultSettings;
        settingsModifier(settings);
        return CreateResourceObject(settings);
    }

    /// <summary>
    /// Create the response for the Get operation.
    /// </summary>
    /// <returns>A JSON string representing the response.</returns>
    private string CreateGetResponse()
    {
        return CreateResourceObject(GetSettings());
    }

    /// <summary>
    /// Asserts that the state and settings are equal.
    /// </summary>
    /// <param name="settings">Settings manifest to compare against.</param>
    /// <param name="state">Output state to compare.</param>
    private void AssertStateAndSettingsAreEqual(TSettingsConfig settings, SettingsResourceObject<TSettingsConfig> state)
    {
        AssertSettingsAreEqual(settings, state.Settings);
    }

    /// <summary>
    /// Asserts that two settings manifests are equal.
    /// </summary>
    /// <param name="expected">Expected settings.</param>
    /// <param name="actual">Actual settings.</param>
    private void AssertSettingsAreEqual(TSettingsConfig expected, TSettingsConfig actual)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected) as JsonObject;
        var actualJson = JsonSerializer.SerializeToNode(actual) as JsonObject;
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson));
    }

    /// <summary>
    /// Asserts that the current settings have changed.
    /// </summary>
    /// <param name="action">Action to prepare the default settings.</param>
    private void AssertSettingsHasChanged(Action<TSettingsConfig> action)
    {
        var currentSettings = GetSettings();
        var defaultSettings = DefaultSettings;
        action(defaultSettings);
        AssertSettingsAreEqual(defaultSettings, currentSettings);
    }

    /// <summary>
    /// Updates the settings.
    /// </summary>
    /// <param name="action">Action to modify the settings.</param>
    private void UpdateSettings(Action<TSettingsConfig> action)
    {
        var settings = GetSettings();
        action(settings);
        SaveSettings(settings);
    }
}
