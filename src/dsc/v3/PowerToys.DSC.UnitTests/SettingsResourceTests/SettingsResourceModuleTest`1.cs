// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    private readonly SettingsUtils _settingsUtils = new();

    private TSettingsConfig _originalSettings;

    protected TSettingsConfig DefaultSettings => new();

    protected string Module { get; }

    public SettingsResourceModuleTest(string module)
    {
        Module = module;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _originalSettings = GetSettings();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        SaveSettings(_originalSettings);
    }

    [TestMethod]
    public void Get_Success()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.Contains(CreateGetResponse(), result.Output);
        AssertStateAndSettingsAreEqual(GetSettings(), state);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<TSettingsConfig>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.Contains(CreateGetResponse(), result.Output);
        AssertStateAndSettingsAreEqual(GetSettings(), state);
    }

    /// <summary>
    /// Resets the settings to default values.
    /// </summary>
    protected void ResetSettingsToDefaultValues()
    {
        SaveSettings(DefaultSettings);
    }

    /// <summary>
    /// Get the settings for the specified module.
    /// </summary>
    /// <returns>An instance of the settings type with the current configuration.</returns>
    protected TSettingsConfig GetSettings()
    {
        return _settingsUtils.GetSettingsOrDefault<TSettingsConfig>(DefaultSettings.GetModuleName());
    }

    /// <summary>
    /// Saves the settings for the specified module.
    /// </summary>
    /// <param name="settings">Settings to save.</param>
    protected void SaveSettings(TSettingsConfig settings)
    {
        _settingsUtils.SaveSettings(JsonSerializer.Serialize(settings), DefaultSettings.GetModuleName());
    }

    /// <summary>
    /// Create the resource object for the operation.
    /// </summary>
    /// <param name="settings">Settings to include in the resource object.</param>
    /// <returns>A JSON string representing the resource object.</returns>
    protected string CreateResourceObject(TSettingsConfig settings)
    {
        var resourceObject = new SettingsResourceObject<TSettingsConfig>
        {
            Settings = settings,
        };
        return JsonSerializer.Serialize(resourceObject);
    }

    /// <summary>
    /// Create the response for the Get operation.
    /// </summary>
    /// <returns>A JSON string representing the response.</returns>
    protected string CreateGetResponse()
    {
        return CreateResourceObject(GetSettings());
    }

    /// <summary>
    /// Asserts that the state and settings are equal.
    /// </summary>
    /// <param name="settings">Settings manifest to compare against.</param>
    /// <param name="state">Output state to compare.</param>
    protected void AssertStateAndSettingsAreEqual(TSettingsConfig settings, SettingsResourceObject<TSettingsConfig> state)
    {
        AssertSettingsAreEqual(settings, state.Settings);
    }

     /// <summary>
    /// Asserts that two settings manifests are equal.
    /// </summary>
    /// <param name="expected">Expected settings.</param>
    /// <param name="actual">Actual settings.</param>
    protected virtual void AssertSettingsAreEqual(TSettingsConfig expected, TSettingsConfig actual)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected);
        var actualJson = JsonSerializer.SerializeToNode(actual);
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson));
    }
}
