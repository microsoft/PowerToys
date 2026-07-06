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
    public void Missing_SettingsFile_Allows()
    {
        // No PowerToys governance (standalone/dev/test) -> allow.
        var path = Path.Combine(_folder, "does-not-exist.json");
        Assert.IsTrue(ModuleState.IsPowerScriptsEnabled(path));
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
    public void KeyAbsent_Allows_BecauseRunnerStripsUnknownModules()
    {
        // The runner rewrites settings.json and drops entries for modules it does not itself host,
        // so an absent PowerScripts key is ambiguous rather than a deliberate "off". The settings-file
        // overload therefore falls back to enabled; a deliberate "off" is expressed either as an
        // explicit false here or via the module-owned config.json override.
        var path = WriteSettings("{ \"enabled\": { \"Keyboard Manager\": true } }");
        Assert.IsTrue(ModuleState.IsPowerScriptsEnabled(path));
    }

    [TestMethod]
    public void Corrupt_SettingsFile_Allows_ToNotBreakBindings()
    {
        var path = WriteSettings("{ this is not valid json");
        Assert.IsTrue(ModuleState.IsPowerScriptsEnabled(path));
    }
}
