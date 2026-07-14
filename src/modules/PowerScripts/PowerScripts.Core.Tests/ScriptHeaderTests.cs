// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ScriptHeaderTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Init()
    {
        _root = Path.Combine(Path.GetTempPath(), "ps-header-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception)
        {
        }
    }

    [TestMethod]
    public void HeaderParser_Returns_Null_When_No_Directives()
    {
        var file = WriteFile("plain.ps1", "# just a comment\nWrite-Host 'hi'\n");
        Assert.IsNull(ScriptHeaderParser.TryParseFile(file));
    }

    [TestMethod]
    public void HeaderParser_Parses_Core_Fields_And_Infers_Runtime()
    {
        var file = WriteFile(
            "whats-my-ip.ps1",
            "# Copyright header line that is not a directive\n" +
            "# @powerscript.id whats-my-ip\n" +
            "# @powerscript.name What's my IP\n" +
            "# @powerscript.description Shows the public IP.\n" +
            "# @powerscript.kind action\n" +
            "Write-Host 'x'\n");

        var manifest = ScriptHeaderParser.TryParseFile(file);

        Assert.IsNotNull(manifest);
        Assert.AreEqual("whats-my-ip", manifest!.Id);
        Assert.AreEqual("What's my IP", manifest.Name);
        Assert.AreEqual("Shows the public IP.", manifest.Description);
        Assert.AreEqual(ScriptKind.System, manifest.Kind);
        Assert.AreEqual(ScriptRuntime.PowerShell, manifest.Runtime);
        Assert.AreEqual("whats-my-ip.ps1", manifest.Entry);
    }

    [TestMethod]
    public void HeaderParser_Infers_Python_Runtime_From_Extension()
    {
        var file = WriteFile(
            "hello.py",
            "# @powerscript.id hello-py\n" +
            "# @powerscript.name Hello\n" +
            "print('hi')\n");

        var manifest = ScriptHeaderParser.TryParseFile(file);
        Assert.IsNotNull(manifest);
        Assert.AreEqual(ScriptRuntime.Python, manifest!.Runtime);
    }

    [TestMethod]
    public void HeaderParser_Parses_File_Input_And_Params()
    {
        var file = WriteFile(
            "convert.ps1",
            "# @powerscript.id conv\n" +
            "# @powerscript.name Convert\n" +
            "# @powerscript.kind file\n" +
            "# @powerscript.extensions .md, .txt\n" +
            "# @powerscript.capability fileRead fileWrite\n" +
            "# @powerscript.param name=greeting type=string label=\"Greeting text\" default=Hello\n" +
            "# @powerscript.param name=mode type=choice options=upper,lower default=upper\n" +
            "Write-Host 'x'\n");

        var manifest = ScriptHeaderParser.TryParseFile(file);

        Assert.IsNotNull(manifest);
        Assert.AreEqual(ScriptKind.File, manifest!.Kind);
        CollectionAssert.AreEquivalent(new[] { ".md", ".txt" }, manifest.Input!.Extensions);
        CollectionAssert.AreEquivalent(new[] { "fileRead", "fileWrite" }, manifest.Capabilities);
        Assert.AreEqual(2, manifest.Parameters.Count);

        var greeting = manifest.Parameters[0];
        Assert.AreEqual("greeting", greeting.Name);
        Assert.AreEqual("string", greeting.Type);
        Assert.AreEqual("Greeting text", greeting.Label);
        Assert.AreEqual("Hello", greeting.Default);

        var mode = manifest.Parameters[1];
        Assert.IsTrue(mode.IsChoice);
        CollectionAssert.AreEquivalent(new[] { "upper", "lower" }, mode.Options);
    }

    [TestMethod]
    public void HeaderParser_Stops_At_First_Code_Line()
    {
        var file = WriteFile(
            "stop.ps1",
            "# @powerscript.id stop\n" +
            "# @powerscript.name Stop\n" +
            "Write-Host 'x'\n" +
            "# @powerscript.description should be ignored\n");

        var manifest = ScriptHeaderParser.TryParseFile(file);
        Assert.IsNotNull(manifest);
        Assert.AreEqual(string.Empty, manifest!.Description);
    }

    [TestMethod]
    public void SurfaceInference_Infers_ContextMenu_For_File()
    {
        var manifest = new PowerScriptManifest { Kind = ScriptKind.File };
        SurfaceInference.ApplyDefaults(manifest);
        CollectionAssert.AreEquivalent(new[] { SurfaceInference.ContextMenu }, manifest.Surfaces);
    }

    [TestMethod]
    public void SurfaceInference_Infers_Kbm_And_CmdPal_For_System()
    {
        var manifest = new PowerScriptManifest { Kind = ScriptKind.System };
        SurfaceInference.ApplyDefaults(manifest);
        CollectionAssert.AreEquivalent(
            new[] { SurfaceInference.KeyboardManager, SurfaceInference.CommandPalette },
            manifest.Surfaces);
    }

    [TestMethod]
    public void SurfaceInference_Does_Not_Override_Declared_Surfaces()
    {
        var manifest = new PowerScriptManifest { Kind = ScriptKind.System, Surfaces = { "contextMenu" } };
        SurfaceInference.ApplyDefaults(manifest);
        CollectionAssert.AreEquivalent(new[] { "contextMenu" }, manifest.Surfaces);
    }

    [TestMethod]
    public void Registry_Loads_Loose_Header_Script_With_Inferred_Surfaces()
    {
        WriteFile(
            "whats-my-ip.ps1",
            "# @powerscript.id whats-my-ip\n" +
            "# @powerscript.name What's my IP\n" +
            "# @powerscript.kind action\n" +
            "Write-Host 'x'\n");

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(0, registry.Errors.Count, string.Join("; ", registry.Errors.Select(e => e.Message)));
        var script = registry.Get("whats-my-ip");
        Assert.IsNotNull(script);
        Assert.AreEqual(ScriptKind.System, script!.Kind);
        CollectionAssert.AreEquivalent(
            new[] { SurfaceInference.KeyboardManager, SurfaceInference.CommandPalette },
            script.Surfaces);
    }

    [TestMethod]
    public void Registry_Loads_Header_Script_In_Folder_Without_Manifest()
    {
        var folder = Path.Combine(_root, "my-tool");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "run.ps1"),
            "# @powerscript.id my-tool\n" +
            "# @powerscript.name My Tool\n" +
            "# @powerscript.kind action\n" +
            "Write-Host 'x'\n");

        var registry = new ScriptRegistry(_root);
        registry.Load();

        var script = registry.Get("my-tool");
        Assert.IsNotNull(script);
        Assert.AreEqual("run.ps1", script!.Entry);
    }

    [TestMethod]
    public void Registry_Ignores_Loose_Files_Without_Directives()
    {
        WriteFile("helper.ps1", "# a shared helper, not a powerscript\nfunction Foo {}\n");

        var registry = new ScriptRegistry(_root);
        registry.Load();

        Assert.AreEqual(0, registry.Scripts.Count);
        Assert.AreEqual(0, registry.Errors.Count);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
