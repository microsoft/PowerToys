// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.Models.ResourceObjects;
using PowerToys.DSC.Resources;

namespace PowerToys.DSC.UnitTests;

[TestClass]
public sealed class SettingsResourceTest : BaseResourceTest
{
    private readonly SettingsUtils _settingsUtils = new();

    [TestMethod]
    public void Get_Success()
    {
        // Act
        var result = ExecuteCommand<GetCommand>("--resource", SettingsResource.ResourceName, "--module", "Awake");
        var state = result.OutputState<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.Contains(CreateGetResponse<AwakeSettings>(), result.Output);
        AssertStateAndSettingsAreEqual(GetSettings<AwakeSettings>(), state);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Act
        var result = ExecuteCommand<ExportCommand>("--resource", SettingsResource.ResourceName, "--module", "Awake");
        var state = result.OutputState<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.Contains(CreateGetResponse<AwakeSettings>(), result.Output);
        AssertStateAndSettingsAreEqual(GetSettings<AwakeSettings>(), state);
    }

    /// <summary>
    /// Get the settings for the specified module.
    /// </summary>
    /// <typeparam name="T">Settings type to retrieve.</typeparam>
    /// <returns>An instance of the settings type with the current configuration.</returns>
    private T GetSettings<T>()
        where T : ISettingsConfig, new()
    {
        var settingsConfig = new T();
        return _settingsUtils.GetSettingsOrDefault<T>(settingsConfig.GetModuleName());
    }

    /// <summary>
    /// Create the response for the Get operation.
    /// </summary>
    /// <returns>A JSON string representing the response.</returns>
    private string CreateGetResponse<T>()
        where T : ISettingsConfig, new()
    {
        return this.CreateResourceObject(GetSettings<T>());
    }

    /// <summary>
    /// Create the resource object for the operation.
    /// </summary>
    /// <param name="settings">Settings to include in the resource object.</param>
    /// <returns>A JSON string representing the resource object.</returns>
    private string CreateResourceObject<T>(T settings)
        where T : ISettingsConfig, new()
    {
        var resourceObject = new SettingsResourceObject<T>
        {
            Settings = settings,
        };
        return JsonSerializer.Serialize(resourceObject);
    }

    /// <summary>
    /// Asserts that the state and settings are equal.
    /// </summary>
    /// <param name="settings">Settings manifest to compare against.</param>
    /// <param name="state">Output state to compare.</param>
    private void AssertStateAndSettingsAreEqual<T>(T settings, SettingsResourceObject<T> state)
        where T : ISettingsConfig, new()
    {
        this.AssertSettingsAreEqual(settings, state.Settings);
    }

     /// <summary>
    /// Asserts that two settings manifests are equal.
    /// </summary>
    /// <param name="expected">Expected settings manifest.</param>
    /// <param name="actual">Actual settings manifest.</param>
    private void AssertSettingsAreEqual<T>(T expected, T actual)
        where T : ISettingsConfig, new()
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected);
        var actualJson = JsonSerializer.SerializeToNode(actual);
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson));
    }
}
