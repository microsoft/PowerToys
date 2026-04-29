// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace ViewModelTests;

[TestClass]
public sealed class MacroViewModelTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteMacro(MacroDefinition def)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, $"{def.Id}.json"),
            MacroSerializer.Serialize(def));
    }

    [TestMethod]
    public void Constructor_LoadsMacrosFromDirectory()
    {
        WriteMacro(new MacroDefinition { Id = "a", Name = "Alpha" });
        WriteMacro(new MacroDefinition { Id = "b", Name = "Beta" });

        MacroViewModel vm = new(_tempDir);

        Assert.AreEqual(2, vm.Macros.Count);
        Assert.IsTrue(vm.Macros.Any(m => m.Name == "Alpha"));
        Assert.IsTrue(vm.Macros.Any(m => m.Name == "Beta"));
    }

    [TestMethod]
    public void Constructor_EmptyDirectory_EmptyCollection()
    {
        MacroViewModel vm = new(_tempDir);
        Assert.AreEqual(0, vm.Macros.Count);
    }

    [TestMethod]
    public void Constructor_MalformedFile_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "bad.json"), "{ not valid json");
        WriteMacro(new MacroDefinition { Id = "ok", Name = "Good" });

        MacroViewModel vm = new(_tempDir);

        Assert.AreEqual(1, vm.Macros.Count);
        Assert.AreEqual("Good", vm.Macros[0].Name);
    }

    [TestMethod]
    public async Task SaveMacroAsync_WritesJsonFile()
    {
        MacroViewModel vm = new(_tempDir);
        MacroEditViewModel editVm = new(new MacroDefinition { Id = "new-1", Name = "My Macro" });

        await vm.SaveMacroAsync(editVm);

        string expectedPath = Path.Combine(_tempDir, "new-1.json");
        Assert.IsTrue(File.Exists(expectedPath));
        string json = File.ReadAllText(expectedPath);
        MacroDefinition restored = MacroSerializer.Deserialize(json);
        Assert.AreEqual("My Macro", restored.Name);
    }

    [TestMethod]
    public void DeleteMacro_RemovesFileAndItem()
    {
        MacroDefinition def = new() { Id = "del-1", Name = "ToDelete" };
        WriteMacro(def);

        MacroViewModel vm = new(_tempDir);
        Assert.AreEqual(1, vm.Macros.Count);

        vm.DeleteMacro(vm.Macros[0]);

        Assert.AreEqual(0, vm.Macros.Count);
        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "del-1.json")));
    }

    [TestMethod]
    public void MacroListItem_IsEnabled_Toggle_WritesJson()
    {
        MacroDefinition def = new() { Id = "en-1", Name = "Toggleable", IsEnabled = true };
        WriteMacro(def);

        MacroViewModel vm = new(_tempDir);
        MacroListItem item = vm.Macros[0];
        Assert.IsTrue(item.IsEnabled);

        item.IsEnabled = false;

        string json = File.ReadAllText(Path.Combine(_tempDir, "en-1.json"));
        MacroDefinition restored = MacroSerializer.Deserialize(json);
        Assert.IsFalse(restored.IsEnabled);
    }
}
