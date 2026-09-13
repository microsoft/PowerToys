// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models.FunctionData;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

/// <summary>
/// ZoomIt stores its settings in the registry, read and written through the
/// ZoomIt settings interop. The tests replace the interop with an in-memory
/// store so they never touch the registry of the machine running them.
/// </summary>
[TestClass]
public sealed class SettingsResourceZoomItModuleTest : BaseDscTest
{
    // The shape the interop produces: every property wrapped in a "value" object
    private const string InteropSettingsJson = /*lang=json,strict*/ """
        {
          "name": "ZoomIt",
          "version": "1.0",
          "properties": {
            "ToggleKey": { "value": { "win": false, "ctrl": true, "alt": false, "shift": false, "code": 49, "key": "1" } },
            "DrawToggleKey": { "value": { "win": false, "ctrl": true, "alt": false, "shift": false, "code": 50, "key": "2" } },
            "BreakTimeout": { "value": 10 },
            "ShowTrayIcon": { "value": true },
            "RecordFormat": { "value": "GIF" },
            "RecordScaling": { "value": 100 },
            "Font": { "value": "AAAAAAAAAAAAAAAAAAAAAA==" }
          }
        }
        """;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        MaxDepth = 0,
        IncludeFields = true,
    };

    private Func<string> _originalLoadSettingsJson;
    private Action<string> _originalSaveSettingsJson;
    private string _store;
    private List<string> _saved;

    private static string Module => nameof(ModuleType.ZoomIt);

    [TestInitialize]
    public void TestInitialize()
    {
        _originalLoadSettingsJson = ZoomItSettingsFunctionData.LoadSettingsJson;
        _originalSaveSettingsJson = ZoomItSettingsFunctionData.SaveSettingsJson;
        _store = InteropSettingsJson;
        _saved = [];
        ZoomItSettingsFunctionData.LoadSettingsJson = () => _store;
        ZoomItSettingsFunctionData.SaveSettingsJson = json =>
        {
            _saved.Add(json);
            _store = json;
        };
    }

    [TestCleanup]
    public void TestCleanup()
    {
        ZoomItSettingsFunctionData.LoadSettingsJson = _originalLoadSettingsJson;
        ZoomItSettingsFunctionData.SaveSettingsJson = _originalSaveSettingsJson;
    }

    [TestMethod]
    public void Get_ReturnsRegistryBackedSettings()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(49, state.Settings.Properties.ToggleKey.Value.Code);
        Assert.IsTrue(state.Settings.Properties.ToggleKey.Value.Ctrl);
        Assert.AreEqual(10, state.Settings.Properties.BreakTimeout.Value);
        Assert.IsTrue(state.Settings.Properties.ShowTrayIcon.Value);
        Assert.AreEqual("GIF", state.Settings.Properties.RecordFormat.Value);
        Assert.AreEqual(0, _saved.Count);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", SettingsResource.ResourceName, "--module", Module);
        var state = result.OutputState<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(10, state.Settings.Properties.BreakTimeout.Value);
        Assert.AreEqual(0, _saved.Count);
    }

    [TestMethod]
    public void SetWithDiff_WritesDeclaredPropertiesAndKeepsTheOthers()
    {
        // Arrange
        var input = CreateInput(properties =>
        {
            properties.BreakTimeout = new IntProperty(25);
            properties.ShowTrayIcon = new BoolProperty(false);
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string> { SettingsResourceObject<ZoomItSettings>.SettingsJsonPropertyName }, diff);
        Assert.AreEqual(25, state.Settings.Properties.BreakTimeout.Value);
        Assert.IsFalse(state.Settings.Properties.ShowTrayIcon.Value);

        // Properties the configuration does not declare keep their current value
        Assert.AreEqual(49, state.Settings.Properties.ToggleKey.Value.Code);
        Assert.AreEqual("GIF", state.Settings.Properties.RecordFormat.Value);

        // The interop receives the complete settings in its own shape
        Assert.AreEqual(1, _saved.Count);
        var saved = JsonNode.Parse(_saved[0]);
        Assert.AreEqual("ZoomIt", saved["name"].GetValue<string>());
        Assert.AreEqual(25, saved["properties"]["BreakTimeout"]["value"].GetValue<int>());
        Assert.IsFalse(saved["properties"]["ShowTrayIcon"]["value"].GetValue<bool>());
        Assert.AreEqual(49, saved["properties"]["ToggleKey"]["value"]["code"].GetValue<int>());
        Assert.AreEqual("AAAAAAAAAAAAAAAAAAAAAA==", saved["properties"]["Font"]["value"].GetValue<string>());
    }

    [TestMethod]
    public void SetTwice_SecondSetHasNoDiffAndDoesNotWrite()
    {
        // Arrange
        var input = CreateInput(properties => properties.BreakTimeout = new IntProperty(25));

        // Act
        var firstResult = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var secondResult = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (_, firstDiff) = firstResult.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();
        var (_, secondDiff) = secondResult.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(firstResult.Success);
        Assert.IsTrue(secondResult.Success);
        CollectionAssert.AreEqual(new List<string> { SettingsResourceObject<ZoomItSettings>.SettingsJsonPropertyName }, firstDiff);
        CollectionAssert.AreEqual(new List<string>(), secondDiff);
        Assert.AreEqual(1, _saved.Count);
    }

    [TestMethod]
    public void SetWithoutDiff_DoesNotWrite()
    {
        // Arrange: the desired value already matches the current one
        var input = CreateInput(properties => properties.BreakTimeout = new IntProperty(10));

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (_, diff) = result.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string>(), diff);
        Assert.AreEqual(0, _saved.Count);
    }

    [TestMethod]
    public void TestWithDiff_Success()
    {
        // Arrange
        var input = CreateInput(properties => properties.ToggleKey = new KeyboardKeysProperty(new HotkeySettings(true, false, false, true, 90)));

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsFalse(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string> { SettingsResourceObject<ZoomItSettings>.SettingsJsonPropertyName }, diff);
        Assert.AreEqual(0, _saved.Count);
    }

    [TestMethod]
    public void TestWithoutDiff_Success()
    {
        // Arrange
        var input = CreateInput(properties => properties.ToggleKey = new KeyboardKeysProperty(new HotkeySettings(false, true, false, false, 49)));

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<ZoomItSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string>(), diff);
    }

    [TestMethod]
    public void Set_SignalsRefreshSettingsEvent()
    {
        // Arrange: stand in for a running ZoomIt instance
        using var refreshEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ZoomItSettingsFunctionData.RefreshSettingsEventName);
        refreshEvent.Reset();
        var input = CreateInput(properties => properties.BreakTimeout = new IntProperty(25));

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(refreshEvent.WaitOne(TimeSpan.FromSeconds(5)), "The ZoomIt refresh settings event was not signaled");
    }

    [TestMethod]
    public void Interop_LoadSettingsJson_DeserializesIntoSettingsModel()
    {
        // Act: read the real registry-backed settings through the interop (read-only)
        var json = _originalLoadSettingsJson();
        var settings = JsonSerializer.Deserialize<ZoomItSettings>(json, _serializerOptions);

        // Assert
        Assert.IsNotNull(settings);
        Assert.AreEqual(ZoomItSettings.ModuleName, settings.Name);
        Assert.IsNotNull(settings.Properties.ToggleKey);
        Assert.IsNotNull(settings.Properties.ToggleKey.Value);
        Assert.IsNotNull(settings.Properties.BreakTimeout);
        Assert.IsNotNull(settings.Properties.RecordFormat);
    }

    private static string CreateInput(Action<ZoomItProperties> configure)
    {
        var settings = new ZoomItSettings();
        configure(settings.Properties);
        return JsonSerializer.Serialize(new SettingsResourceObject<ZoomItSettings> { Settings = settings }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }
}
