// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ModuleStateTests
{
    private string _folder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "powerscripts-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string WriteSettings(string json)
    {
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [TestMethod]
    public void Missing_SettingsFile_DefaultsOff()
    {
        var path = Path.Combine(_folder, "does-not-exist.json");
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void MissingSettingsFileUsesLegacyPreference(bool legacyEnabled)
    {
        Assert.AreEqual(legacyEnabled, ModuleState.IsPowerScriptsEnabled(Path.Combine(_folder, "missing.json"), legacyEnabled));
        Assert.AreEqual(legacyEnabled, ModuleState.IsPowerScriptsEnabled(Path.Combine(_folder, "missing-directory", "settings.json"), legacyEnabled));
    }

    [TestMethod]
    public void UnreadableSettingsPathRefusesEvenWithLegacyPreference()
    {
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(_folder, legacyEnabled: true));
        var path = WriteSettings("""{"enabled":{"PowerScripts":false}}""");
        using var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path, legacyEnabled: true));
    }

    [TestMethod]
    public void ExplicitlyEnabled_Allows()
    {
        var path = WriteSettings("{ \"enabled\": { \"PowerScripts\": true } }");
        Assert.IsTrue(ModuleState.IsPowerScriptsEnabled(path));
    }

    [TestMethod]
    public void ExplicitlyDisabled_Refuses()
    {
        var path = WriteSettings("{ \"enabled\": { \"PowerScripts\": false } }");
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path));
    }

    [TestMethod]
    public void KeyAbsent_DefaultsOff()
    {
        var path = WriteSettings("{ \"enabled\": { \"Keyboard Manager\": true } }");
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path));
    }

    [TestMethod]
    public void Corrupt_SettingsFile_RefusesEvenWithLegacyPreference()
    {
        var path = WriteSettings("{ this is not valid json");
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path, legacyEnabled: true));
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void GeneralPreferenceWinsOverLegacyPreference(bool enabled, bool legacyEnabled)
    {
        var path = WriteSettings($$"""{ "enabled": { "PowerScripts": {{enabled.ToString().ToLowerInvariant()}} } }""");
        Assert.AreEqual(enabled, ModuleState.IsPowerScriptsEnabled(path, legacyEnabled));
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("""{"enabled":{"Keyboard Manager":true}}""")]
    public void MissingGeneralPreferencePreservesLegacyPreference(string settings)
    {
        var path = WriteSettings(settings);
        Assert.IsTrue(ModuleState.IsPowerScriptsEnabled(path, legacyEnabled: true));
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path, legacyEnabled: false));
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("""{"enabled":null}""")]
    [DataRow("""{"enabled":{"PowerScripts":"true"}}""")]
    public void MalformedGeneralPreferenceRefusesInsteadOfUsingLegacyPreference(string settings)
    {
        var path = WriteSettings(settings);
        Assert.IsFalse(ModuleState.IsPowerScriptsEnabled(path, legacyEnabled: true));
    }
}
