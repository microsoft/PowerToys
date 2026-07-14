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

    private void WriteScript(string id, string header, string entryFile = "run.ps1")
    {
        // Scripts are single self-contained files with a @powerscript.* comment header. Write each
        // one as a loose file under the root, using the id as the file name so ids stay unique.
        var ext = Path.GetExtension(entryFile);
        File.WriteAllText(Path.Combine(_root, id + ext), header + "\n# noop\n");
    }

    [TestMethod]
    public void Load_Skips_Invalid_And_Records_Error()
    {
        WriteScript("good", """
            # @powerscript.id good
            # @powerscript.name Good
            # @powerscript.kind system
            """);

        // Missing 'id' -> should be rejected.
        WriteScript("bad", """
            # @powerscript.name Bad
            # @powerscript.kind system
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual("good", registry.Scripts[0].Id);
        Assert.AreEqual(1, registry.Errors.Count);
    }

    [TestMethod]
    public void Load_Allows_IdDecoupledFromFileName()
    {
        // The file name differs from the id; the script is still loaded and keyed by its id.
        WriteScript("some-file", """
            # @powerscript.id portable.id
            # @powerscript.name Portable
            # @powerscript.kind system
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual("portable.id", registry.Scripts[0].Id);
        Assert.AreEqual(0, registry.Errors.Count);
        Assert.IsNotNull(registry.Get("portable.id"));
    }

    [TestMethod]
    public void Load_Rejects_DuplicateIds()
    {
        WriteScript("file-a", """
            # @powerscript.id dup
            # @powerscript.name First
            # @powerscript.kind system
            """);
        WriteScript("file-b", """
            # @powerscript.id dup
            # @powerscript.name Second
            # @powerscript.kind system
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        // Only the first wins; the collision is reported.
        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual(1, registry.Errors.Count);
        Assert.IsTrue(registry.Errors[0].Message.Contains("duplicate id"));
    }

    [TestMethod]
    public void FileScriptsFor_Matches_Extension_And_Wildcard()
    {
        WriteScript("png-only", """
            # @powerscript.id png-only
            # @powerscript.name PNG
            # @powerscript.kind file
            # @powerscript.extensions .png
            """);

        WriteScript("any-file", """
            # @powerscript.id any-file
            # @powerscript.name Any
            # @powerscript.kind file
            # @powerscript.extensions *
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
            # @powerscript.id single-png
            # @powerscript.name Single PNG
            # @powerscript.kind file
            # @powerscript.extensions .png
            # @powerscript.minfiles 1
            # @powerscript.maxfiles 1
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
            # @powerscript.id sys
            # @powerscript.name Sys
            # @powerscript.kind system
            """);
        WriteScript("file", """
            # @powerscript.id file
            # @powerscript.name File
            # @powerscript.kind file
            # @powerscript.extensions *
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
