// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.FunctionData;
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.ProfileResourceTests;

[TestClass]
public sealed class ProfileResourceKeyboardManagerTest : BaseDscTest
{
    private const string DefaultProfileFileName = "default.json";
    private const string WorkProfileFileName = "work.json";

    private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;

    private readonly Dictionary<string, string> _originalFiles = [];

    private static string Module => nameof(ModuleType.KeyboardManager);

    [TestInitialize]
    public void TestInitialize()
    {
        // Save the actual settings and profile files, then reset to defaults
        foreach (var fileName in new[] { "settings.json", DefaultProfileFileName, WorkProfileFileName })
        {
            var path = _settingsUtils.GetSettingsFilePath(KeyboardManagerSettings.ModuleName, fileName);
            _originalFiles[fileName] = File.Exists(path) ? File.ReadAllText(path) : null;
        }

        _settingsUtils.SaveSettings(new KeyboardManagerSettings().ToJsonString(), KeyboardManagerSettings.ModuleName);
        _settingsUtils.SaveSettings(JsonSerializer.Serialize(new KeyboardManagerProfile()), KeyboardManagerSettings.ModuleName, DefaultProfileFileName);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        foreach (var (fileName, content) in _originalFiles)
        {
            var path = _settingsUtils.GetSettingsFilePath(KeyboardManagerSettings.ModuleName, fileName);
            if (content != null)
            {
                File.WriteAllText(path, content);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public void Get_Success()
    {
        // Arrange
        var profile = CreateSampleProfileModel();
        SaveProfile(KbmProfileConverter.ToProfile(profile), DefaultProfileFileName);

        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", ProfileResource.ResourceName, "--module", Module);
        var state = result.OutputState<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertProfilesAreEqual(KbmProfileConverter.Canonicalize(profile), state.Profile);
    }

    [TestMethod]
    public void Export_Success()
    {
        // Arrange
        var profile = CreateSampleProfileModel();
        SaveProfile(KbmProfileConverter.ToProfile(profile), DefaultProfileFileName);

        // Act
        var result = ExecuteDscCommand<ExportCommand>("--resource", ProfileResource.ResourceName, "--module", Module);
        var state = result.OutputState<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertProfilesAreEqual(KbmProfileConverter.Canonicalize(profile), state.Profile);
    }

    [TestMethod]
    public void SetWithDiff_Success()
    {
        // Arrange
        var input = CreateInputResourceObject(CreateSampleProfileModel());

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new List<string> { ProfileResourceObject.ProfileJsonPropertyName }, diff);
        AssertProfilesAreEqual(KbmProfileConverter.Canonicalize(CreateSampleProfileModel()), state.Profile);

        // The stored profile uses the exact editor encoding
        var stored = GetProfile(DefaultProfileFileName);
        Assert.AreEqual("20", stored.RemapKeys.InProcessRemapKeys[0].OriginalKeys);
        Assert.AreEqual("27", stored.RemapKeys.InProcessRemapKeys[0].NewRemapKeys);
        Assert.AreEqual("17;16;65", stored.RemapShortcuts.GlobalRemapShortcuts[0].OriginalKeys);
        Assert.AreEqual("17;86", stored.RemapShortcuts.GlobalRemapShortcuts[0].NewRemapKeys);
    }

    [TestMethod]
    public void SetTwice_SecondSetHasNoDiff()
    {
        // Arrange
        var input = CreateInputResourceObject(CreateSampleProfileModel());

        // Act
        var firstResult = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var secondResult = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var (_, firstDiff) = firstResult.OutputStateAndDiff<ProfileResourceObject>();
        var (_, secondDiff) = secondResult.OutputStateAndDiff<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(firstResult.Success);
        Assert.IsTrue(secondResult.Success);
        CollectionAssert.AreEqual(new List<string> { ProfileResourceObject.ProfileJsonPropertyName }, firstDiff);
        CollectionAssert.AreEqual(new List<string>(), secondDiff);
    }

    [TestMethod]
    public void TestWithDiff_Success()
    {
        // Arrange
        var input = CreateInputResourceObject(CreateSampleProfileModel());

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsFalse(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string> { ProfileResourceObject.ProfileJsonPropertyName }, diff);

        // Test must not modify the profile
        Assert.AreEqual(0, GetProfile(DefaultProfileFileName).RemapKeys.InProcessRemapKeys.Count);
    }

    [TestMethod]
    public void TestWithoutDiff_Success()
    {
        // Arrange
        var profile = CreateSampleProfileModel();
        SaveProfile(KbmProfileConverter.ToProfile(profile), DefaultProfileFileName);
        var input = CreateInputResourceObject(profile);

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<ProfileResourceObject>();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(state.InDesiredState);
        CollectionAssert.AreEqual(new List<string>(), diff);
    }

    [TestMethod]
    public void Set_InvalidProfile_FailsAndLeavesFileUntouched()
    {
        // Arrange
        var input = /*lang=json,strict*/ """{"profile":{"keys":[{"from":"CapsLok","to":"Esc"}]}}""";

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        StringAssert.Contains(messages[0].Message, "Invalid key name 'CapsLok'");
        Assert.AreEqual(0, GetProfile(DefaultProfileFileName).RemapKeys.InProcessRemapKeys.Count);
    }

    [TestMethod]
    public void Set_RespectsActiveConfiguration()
    {
        // Arrange
        var settings = new KeyboardManagerSettings();
        settings.Properties.ActiveConfiguration.Value = Path.GetFileNameWithoutExtension(WorkProfileFileName);
        _settingsUtils.SaveSettings(settings.ToJsonString(), KeyboardManagerSettings.ModuleName);
        var input = CreateInputResourceObject(CreateSampleProfileModel());

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(_settingsUtils.SettingsExists(KeyboardManagerSettings.ModuleName, WorkProfileFileName));
        Assert.AreEqual("20", GetProfile(WorkProfileFileName).RemapKeys.InProcessRemapKeys[0].OriginalKeys);
        Assert.AreEqual(0, GetProfile(DefaultProfileFileName).RemapKeys.InProcessRemapKeys.Count);
    }

    [TestMethod]
    public void Set_SignalsSettingsChangedEvent()
    {
        // Arrange
        using var settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ProfileFunctionData.SettingsEventName);
        settingsEvent.Reset();
        var input = CreateInputResourceObject(CreateSampleProfileModel());

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", ProfileResource.ResourceName, "--module", Module, "--input", input);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(settingsEvent.WaitOne(TimeSpan.FromSeconds(5)), "The Keyboard Manager settings event was not signaled");
    }

    /// <summary>
    /// Creates the sample friendly profile used by the tests.
    /// </summary>
    private static KbmProfileModel CreateSampleProfileModel()
    {
        return new KbmProfileModel
        {
            Keys = [new() { From = "CapsLock", To = "Esc" }],
            Shortcuts =
            [
                new() { From = "Ctrl+Shift+A", To = "Ctrl+V" },
                new() { From = "Win+O, K", ToText = "chord" },
                new() { From = "Ctrl+Alt+N", To = "Ctrl+S", TargetApp = "notepad.exe", ExactMatch = true },
            ],
        };
    }

    private static string CreateInputResourceObject(KbmProfileModel profile)
    {
        return JsonSerializer.Serialize(new ProfileResourceObject { Profile = profile });
    }

    private static KeyboardManagerProfile GetProfile(string fileName)
    {
        return _settingsUtils.GetSettingsOrDefault<KeyboardManagerProfile>(KeyboardManagerSettings.ModuleName, fileName);
    }

    private static void SaveProfile(KeyboardManagerProfile profile, string fileName)
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        _settingsUtils.SaveSettings(JsonSerializer.Serialize(profile, options), KeyboardManagerSettings.ModuleName, fileName);
    }

    private static void AssertProfilesAreEqual(KbmProfileModel expected, KbmProfileModel actual)
    {
        var expectedJson = JsonSerializer.SerializeToNode(expected);
        var actualJson = JsonSerializer.SerializeToNode(actual);
        Assert.IsTrue(JsonNode.DeepEquals(expectedJson, actualJson), $"{expectedJson} != {actualJson}");
    }
}
