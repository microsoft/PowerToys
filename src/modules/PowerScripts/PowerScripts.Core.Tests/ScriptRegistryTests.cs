// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
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

    private void WriteScript(string fileName, object? metadata = null, string? id = null, string? title = null)
    {
        var entry = Path.Combine(_root, fileName + ".ps1");
        File.WriteAllText(entry, "# noop\n");
        File.WriteAllText(entry + ".tool.json", JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["name"] = id ?? fileName,
            ["title"] = title ?? fileName,
            ["inputSchema"] = new { type = "object", properties = new { } },
            ["x-powerscript"] = metadata ?? new { },
        }));
    }

    [TestMethod]
    public void Load_Skips_Invalid_And_Records_Error()
    {
        WriteScript("good");

        WriteScript("bad");
        File.WriteAllText(Path.Combine(_root, "bad.ps1.tool.json"), """{ "title": "Bad" }""");

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual("good", registry.Scripts[0].Id);
        Assert.AreEqual(1, registry.Errors.Count);
    }

    [TestMethod]
    public void Load_ResolvesPackageRelativeIcon()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllBytes(Path.Combine(_root, "assets", "script icon.ico"), Convert.FromBase64String(
            "AAABAAEAAQEAAAEAIAAwAAAAFgAAACgAAAABAAAAAgAAAAEAIAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAD///8AAAAAAA=="));
        WriteScript("icon", new { icon = @"assets\script icon.ico" });

        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(Path.Combine(_root, "assets", "script icon.ico"), registry.Get("icon")!.IconPath);
        Assert.AreEqual(0, registry.Errors.Count);
    }

    [DataTestMethod]
    [DataRow(1048576, true)]
    [DataRow(1048577, false)]
    public void Load_EnforcesIconSizeLimitWithoutDroppingTheScript(int size, bool accepted)
    {
        var bytes = new byte[size];
        Convert.FromBase64String(
            "AAABAAEAAQEAAAEAIAAwAAAAFgAAACgAAAABAAAAAgAAAAEAIAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAD///8AAAAAAA==")
            .CopyTo(bytes, 0);
        File.WriteAllBytes(Path.Combine(_root, "script.ico"), bytes);
        WriteScript("icon", new { icon = "script.ico" });
        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual(accepted, registry.Get("icon")!.IconPath is not null);
        Assert.AreEqual(accepted ? 0 : 1, registry.Errors.Count);
    }

    [DataTestMethod]
    [DataRow(@"..\outside.ico")]
    [DataRow(@"C:\outside.ico")]
    [DataRow(@"\\server\share\icon.ico")]
    [DataRow("https://example.com/icon.ico")]
    [DataRow("missing.ico")]
    [DataRow("script.png")]
    [DataRow("invalid.ico")]
    public void Load_InvalidOptionalIconKeepsScriptAndSurfacesWarning(string icon)
    {
        File.WriteAllText(Path.Combine(_root, "invalid.ico"), "not an icon");
        WriteScript("icon", new { icon });

        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.IsNull(registry.Get("icon")!.IconPath);
        Assert.IsFalse(string.IsNullOrEmpty(registry.Get("icon")!.IconWarning));
        Assert.AreEqual(1, registry.Errors.Count);
    }

    [TestMethod]
    public void Load_Allows_IdDecoupledFromFileName()
    {
        // The file name differs from the id; the script is still loaded and keyed by its id.
        WriteScript("some-file", id: "portable.id", title: "Portable");

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
        WriteScript("file-a", id: "dup", title: "First");
        WriteScript("file-b", id: "dup", title: "Second");

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
        WriteScript("png-only", new { extensions = new[] { ".png" } });
        WriteScript("any-file", new { extensions = new[] { "*" } });

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
        WriteScript("single-png", new { extensions = new[] { ".png" }, minFiles = 1, maxFiles = 1 });

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
    public void SystemScripts_ExcludesFileScripts()
    {
        WriteScript("sys");
        WriteScript("file", new { extensions = new[] { "*" } });

        var registry = new ScriptRegistry(_root);
        registry.Load();

        // "System" is now derived: any script whose resolved input shape is not files.
        var system = registry.SystemScripts.Select(s => s.Id).ToList();
        CollectionAssert.AreEqual(new[] { "sys" }, system);
    }

    [TestMethod]
    public void Load_IgnoresInlineMetadataWithoutADescriptor()
    {
        File.WriteAllText(Path.Combine(_root, "legacy.ps1"), """
            # @powerscript.id legacy
            # @powerscript.name Legacy
            Write-Output 'not registered'
            """);
        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(0, registry.Scripts.Count);
        Assert.AreEqual(0, registry.Errors.Count);
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
