// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Models.FunctionData;
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.KeyboardManager;

/// <summary>
/// Applying Keyboard Manager settings that leave out the active profile and
/// the profile list must not reset them. These tests work on objects only and
/// touch no settings file.
/// </summary>
[TestClass]
public sealed class KeyboardManagerSettingsInputTests
{
    private const string OnlyUseNewEditor = "{\"settings\":{\"properties\":{\"useNewEditor\":false}}}";

    [TestMethod]
    public void OnlyUseNewEditor_KeepsTheActiveProfileAndTheProfileList()
    {
        var current = CreateCurrent();
        var desired = Deserialize(OnlyUseNewEditor);
        Assert.AreEqual("default", desired.Properties.ActiveConfiguration.Value, "The model fills an omitted value with its default.");

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse(OnlyUseNewEditor), desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, desired.Properties.KeyboardConfigurations.Value);
        Assert.IsFalse(desired.Properties.UseNewEditor);
    }

    // With the two fields completed, an input that only repeats the current
    // useNewEditor value is already in the desired state.
    [TestMethod]
    public void OnlyUseNewEditor_AlreadyApplied_ComparesEqualToTheCurrentState()
    {
        var current = CreateCurrent();
        current.Properties.UseNewEditor = false;
        var desired = Deserialize(OnlyUseNewEditor);

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse(OnlyUseNewEditor), desired, current);

        Assert.IsTrue(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(desired), JsonSerializer.SerializeToNode(current)));
    }

    [TestMethod]
    public void ExplicitProfileFields_AreKeptAsGiven()
    {
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"home\"},\"keyboardConfigurations\":{\"value\":[\"default\",\"home\"]}}}}";
        var current = CreateCurrent();
        var desired = Deserialize(input);

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse(input), desired, current);

        Assert.AreEqual("home", desired.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "home" }, desired.Properties.KeyboardConfigurations.Value);
    }

    [TestMethod]
    public void OnlyOneOfTheTwoGiven_TheOtherIsKept()
    {
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"home\"}}}}";
        var current = CreateCurrent();
        var desired = Deserialize(input);

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse(input), desired, current);

        Assert.AreEqual("home", desired.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, desired.Properties.KeyboardConfigurations.Value);
    }

    [TestMethod]
    public void ExplicitNull_CountsAsOmitted()
    {
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":null,\"useNewEditor\":false}}}";
        var current = CreateCurrent();
        var desired = new KeyboardManagerSettings();

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse(input), desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
    }

    // The function data runs the same completion in GetState; here without
    // reading any file, through the object model only.
    [TestMethod]
    public void InputWithoutSettingsObject_KeepsBoth()
    {
        var current = CreateCurrent();
        var desired = new KeyboardManagerSettings();

        KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(JsonNode.Parse("{}"), desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
        Assert.AreEqual(3, desired.Properties.KeyboardConfigurations.Value.Count);
    }

    private static KeyboardManagerSettings CreateCurrent()
    {
        var settings = new KeyboardManagerSettings();
        settings.Properties.ActiveConfiguration.Value = "work";
        settings.Properties.KeyboardConfigurations.Value = ["default", "work", "home"];
        return settings;
    }

    private static KeyboardManagerSettings Deserialize(string input)
    {
        return JsonSerializer.Deserialize<SettingsResourceObject<KeyboardManagerSettings>>(input).Settings;
    }
}
