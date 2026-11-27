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
/// The settings resource does not manage Keyboard Manager's active profile
/// and profile list: the desired state takes them from the current state.
/// These tests work on objects only and touch no settings file.
/// </summary>
[TestClass]
public sealed class KeyboardManagerSettingsInputTests
{
    private const string OnlyUseNewEditor = "{\"settings\":{\"properties\":{\"useNewEditor\":false}}}";

    // The input leaves both out.
    [TestMethod]
    public void OnlyUseNewEditor_KeepsTheActiveProfileAndTheProfileList()
    {
        var current = CreateCurrent();
        var desired = Deserialize(OnlyUseNewEditor);
        Assert.AreEqual("default", desired.Properties.ActiveConfiguration.Value, "The model fills an omitted value with its default.");

        KeyboardManagerSettingsInput.UseCurrentProfileFields(desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, desired.Properties.KeyboardConfigurations.Value);
        Assert.IsFalse(desired.Properties.UseNewEditor);
    }

    // An input that only repeats the current useNewEditor value is already
    // in the desired state.
    [TestMethod]
    public void OnlyUseNewEditor_AlreadyApplied_ComparesEqualToTheCurrentState()
    {
        var current = CreateCurrent();
        current.Properties.UseNewEditor = false;
        var desired = Deserialize(OnlyUseNewEditor);

        KeyboardManagerSettingsInput.UseCurrentProfileFields(desired, current);

        Assert.IsTrue(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(desired), JsonSerializer.SerializeToNode(current)));
    }

    // An export from another machine (or an older one from this machine)
    // names a profile and a list. Applying it must not select a profile whose
    // file may not exist here, nor drop profiles created since.
    [TestMethod]
    public void ProfileFieldsInTheInput_AreNotApplied()
    {
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"office\"},\"keyboardConfigurations\":{\"value\":[\"default\",\"office\"]},\"useNewEditor\":false}}}";
        var current = CreateCurrent();
        var desired = Deserialize(input);
        Assert.AreEqual("office", desired.Properties.ActiveConfiguration.Value);

        KeyboardManagerSettingsInput.UseCurrentProfileFields(desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
        CollectionAssert.AreEqual(new List<string> { "default", "work", "home" }, desired.Properties.KeyboardConfigurations.Value);
        Assert.IsFalse(desired.Properties.UseNewEditor);
    }

    // The caller is told which of the two the input named with another value.
    [TestMethod]
    public void GetProfileFieldsNotApplied_NamesTheFieldsThatDiffer()
    {
        var current = CreateCurrent();

        const string both = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"office\"},\"keyboardConfigurations\":{\"value\":[\"default\",\"office\"]},\"useNewEditor\":false}}}";
        CollectionAssert.AreEqual(
            new List<string> { "activeConfiguration", "keyboardConfigurations" },
            new List<string>(KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(both, current)));

        const string activeOnly = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"office\"}}}}";
        CollectionAssert.AreEqual(
            new List<string> { "activeConfiguration" },
            new List<string>(KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(activeOnly, current)));
    }

    // Nothing to report: the input leaves them out, repeats the current
    // values (an export applied to the same state), or has no values.
    [TestMethod]
    public void GetProfileFieldsNotApplied_IsEmpty_WhenTheInputDoesNotDiffer()
    {
        var current = CreateCurrent();

        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(OnlyUseNewEditor, current).Count);
        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(null, current).Count);
        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied("not json", current).Count);

        const string same = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"Work\"},\"keyboardConfigurations\":{\"value\":[\"default\",\"work\",\"home\"]}}}}";
        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(same, current).Count);

        const string empty = "{\"settings\":{\"properties\":{\"activeConfiguration\":{},\"keyboardConfigurations\":{}}}}";
        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(empty, current).Count);

        // A shape the node reader rejects (a property given twice) must not make the warning throw.
        const string twice = "{\"settings\":{\"properties\":{\"activeConfiguration\":{\"value\":\"office\"},\"activeConfiguration\":{\"value\":\"office\"}}}}";
        Assert.AreEqual(0, KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(twice, current).Count);
    }

    // A member without a value would otherwise be written as "value": null.
    [TestMethod]
    public void EmptyProfileMembersInTheInput_AreNotApplied()
    {
        const string input = "{\"settings\":{\"properties\":{\"activeConfiguration\":{},\"keyboardConfigurations\":{}}}}";
        var current = CreateCurrent();
        var desired = Deserialize(input);

        KeyboardManagerSettingsInput.UseCurrentProfileFields(desired, current);

        Assert.AreEqual("work", desired.Properties.ActiveConfiguration.Value);
        Assert.AreEqual(3, desired.Properties.KeyboardConfigurations.Value.Count);
    }

    // The active configuration becomes a file name; anything that is not a
    // plain file name falls back to the default profile.
    [TestMethod]
    public void GetProfileFileName_OnlyAcceptsAPlainFileName()
    {
        Assert.AreEqual("work.json", ProfileFunctionData.GetProfileFileName("work"));
        Assert.AreEqual("My profile 2.json", ProfileFunctionData.GetProfileFileName("My profile 2"));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName(null));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName(string.Empty));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName("..\\FancyZones\\settings"));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName("../x"));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName("a:b"));
        Assert.AreEqual("default.json", ProfileFunctionData.GetProfileFileName(".."));
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
