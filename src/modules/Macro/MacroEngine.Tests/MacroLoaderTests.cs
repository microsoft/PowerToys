// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class MacroLoaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init() =>
        _tempDir = Path.Combine(Path.GetTempPath(), "MacroLoaderTests_" + Guid.NewGuid());

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private async Task WriteJsonAsync(MacroDefinition macro)
    {
        Directory.CreateDirectory(_tempDir);
        await MacroSerializer.SerializeFileAsync(macro, Path.Combine(_tempDir, $"{macro.Id}.json"));
    }

    [TestMethod]
    public async Task LoadAllAsync_SingleFile_LoadsMacro()
    {
        var macro = new MacroDefinition { Id = "abc", Name = "Test" };
        await WriteJsonAsync(macro);
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(1, loader.Macros.Count);
        Assert.AreEqual("Test", loader.Macros["abc"].Name);
    }

    [TestMethod]
    public async Task LoadAllAsync_MalformedFile_IsSkipped()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "bad.json"), "{ not valid json {{");
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(0, loader.Macros.Count);
    }

    [TestMethod]
    public async Task LoadAllAsync_EmptyDirectory_ReturnsEmpty()
    {
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(0, loader.Macros.Count);
    }

    [TestMethod]
    public async Task LoadAllAsync_MultipleFiles_LoadsAll()
    {
        await WriteJsonAsync(new MacroDefinition { Id = "id1", Name = "M1" });
        await WriteJsonAsync(new MacroDefinition { Id = "id2", Name = "M2" });
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(2, loader.Macros.Count);
    }
}
