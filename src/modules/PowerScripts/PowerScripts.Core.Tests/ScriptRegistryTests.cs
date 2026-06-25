// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ScriptRegistryTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "powerscripts-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteScript(string id, string manifestJson, string entryFile = "run.ps1")
    {
        var folder = Path.Combine(_root, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "manifest.json"), manifestJson);
        File.WriteAllText(Path.Combine(folder, entryFile), "# noop");
    }

    [TestMethod]
    public void Load_Skips_Invalid_And_Records_Error()
    {
        WriteScript("good", """
            { "id": "good", "name": "Good", "kind": "system", "entry": "run.ps1" }
            """);

        // id does not match the folder name -> should be rejected.
        WriteScript("bad", """
            { "id": "mismatch", "name": "Bad", "kind": "system", "entry": "run.ps1" }
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual("good", registry.Scripts[0].Id);
        Assert.AreEqual(1, registry.Errors.Count);
    }

    [TestMethod]
    public void FileScriptsFor_Matches_Extension_And_Wildcard()
    {
        WriteScript("png-only", """
            { "id": "png-only", "name": "PNG", "kind": "file", "entry": "run.ps1",
              "input": { "extensions": [".png"], "minFiles": 1, "maxFiles": 0 } }
            """);

        WriteScript("any-file", """
            { "id": "any-file", "name": "Any", "kind": "file", "entry": "run.ps1",
              "input": { "extensions": ["*"], "minFiles": 1, "maxFiles": 0 } }
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        var forPng = registry.FileScriptsFor(".PNG").Select(s => s.Id).OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(new[] { "any-file", "png-only" }, forPng);

        var forTxt = registry.FileScriptsFor(".txt").Select(s => s.Id).ToList();
        CollectionAssert.AreEqual(new[] { "any-file" }, forTxt);
    }

    [TestMethod]
    public void FileScriptsForSelection_Respects_MinMax_And_MixedExtensions()
    {
        WriteScript("single-png", """
            { "id": "single-png", "name": "Single PNG", "kind": "file", "entry": "run.ps1",
              "input": { "extensions": [".png"], "minFiles": 1, "maxFiles": 1 } }
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        // Two files exceeds maxFiles=1.
        Assert.AreEqual(0, registry.FileScriptsForSelection(new[] { "a.png", "b.png" }).Count());

        // One file is fine.
        Assert.AreEqual(1, registry.FileScriptsForSelection(new[] { "a.png" }).Count());

        // Mixed extensions: not all match .png.
        Assert.AreEqual(0, registry.FileScriptsForSelection(new[] { "a.txt" }).Count());
    }

    [TestMethod]
    public void SystemScripts_Filters_ByKind()
    {
        WriteScript("sys", """
            { "id": "sys", "name": "Sys", "kind": "system", "entry": "run.ps1" }
            """);
        WriteScript("file", """
            { "id": "file", "name": "File", "kind": "file", "entry": "run.ps1",
              "input": { "extensions": ["*"] } }
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        var system = registry.SystemScripts.Select(s => s.Id).ToList();
        CollectionAssert.AreEqual(new[] { "sys" }, system);
    }

    [TestMethod]
    public void Load_EmptyRoot_YieldsNoScripts()
    {
        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(0, registry.Scripts.Count);
        Assert.AreEqual(0, registry.Errors.Count);
    }
}
