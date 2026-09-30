// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

/// <summary>
/// Covers the explicit <c>.tool.json</c> (MCP Tool shape) authoring path: parsing a descriptor into a
/// manifest, discovering descriptors in the registry, and running a descriptor script end-to-end
/// through the shared, de-elevation-aware process runner.
/// </summary>
[TestClass]
public class DescriptorTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "powerscripts-descriptor-" + Guid.NewGuid().ToString("N"));
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

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void Parser_Maps_Scalar_Inputs_To_System_Script_Parameters()
    {
        Write("greet.ps1", "param($who)\n");
        var descriptor = Write("greet.ps1.tool.json", """
            {
              "name": "greet",
              "title": "Greet",
              "description": "Greet someone.",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "who":   { "type": "string", "default": "World", "description": "Name" },
                  "count": { "type": "integer", "minimum": 1, "maximum": 5 },
                  "tone":  { "type": "string", "enum": ["Hello", "Hi"], "default": "Hello" }
                },
                "required": ["who"]
              }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);

        Assert.IsNotNull(manifest);
        Assert.AreEqual("greet", manifest!.Id);
        Assert.AreEqual("Greet", manifest.Name);
        Assert.AreEqual("Greet someone.", manifest.Description);
        Assert.AreEqual("greet.ps1", manifest.Entry);

        // No file input => a "system" (no-input) script; there is no separate kind to assert.
        Assert.IsNull(manifest.Input);
        Assert.AreEqual(PowerScriptDataFormat.None, ScriptIo.Resolve(manifest, null).Input);
        Assert.AreEqual(ScriptRuntime.PowerShell, manifest.Runtime);
        Assert.AreEqual(3, manifest.Parameters.Count);

        var who = manifest.Parameters.Single(p => p.Name == "who");
        Assert.AreEqual(ScriptParameter.ParameterTypeString, who.Type);
        Assert.AreEqual("World", who.Default);

        var count = manifest.Parameters.Single(p => p.Name == "count");
        Assert.AreEqual(ScriptParameter.ParameterTypeInt, count.Type);
        Assert.AreEqual(1, count.Min);
        Assert.AreEqual(5, count.Max);

        var tone = manifest.Parameters.Single(p => p.Name == "tone");
        Assert.AreEqual(ScriptParameter.ParameterTypeChoice, tone.Type);
        CollectionAssert.AreEqual(new[] { "Hello", "Hi" }, tone.Options);
    }

    [TestMethod]
    public void Parser_File_Input_Becomes_File_Script_Not_A_Parameter()
    {
        Write("hash.py", "print('x')\n");
        var descriptor = Write("hash.py.tool.json", """
            {
              "name": "hash-file",
              "description": "Hash files.",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "file": { "type": "string", "contentMediaType": "application/octet-stream" }
                },
                "required": ["file"]
              },
              "x-powerscript": { "extensions": [".png", ".jpg"], "capabilities": ["fileRead"] }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);

        Assert.IsNotNull(manifest);

        // A file-typed input property makes it file-driven (input shape = files); no "kind" needed.
        Assert.AreEqual(PowerScriptDataFormat.Files, ScriptIo.Resolve(manifest!, null).Input);
        Assert.AreEqual(ScriptRuntime.Python, manifest!.Runtime);
        Assert.IsNotNull(manifest.Input);
        CollectionAssert.AreEqual(new[] { ".png", ".jpg" }, manifest.Input!.Extensions);
        // The file-typed property is the input object, not a prompt parameter.
        Assert.AreEqual(0, manifest.Parameters.Count);
        CollectionAssert.Contains(manifest.Capabilities.ToList(), "fileRead");
    }

    [TestMethod]
    public void Parser_FilePath_Format_Becomes_File_Parameter_Not_File_Script()
    {
        Write("inspect.ps1", "param($file)\nWrite-Output $file\n");
        var descriptor = Write("inspect.ps1.tool.json", """
            {
              "name": "inspect-file",
              "description": "Inspect a selected file.",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "file": {
                    "type": "string",
                    "format": "file-path",
                    "contentMediaType": "application/octet-stream"
                  }
                },
                "required": ["file"]
              },
              "x-powerscript": { "capabilities": ["fileRead"] }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);

        Assert.IsNotNull(manifest);
        Assert.AreEqual(PowerScriptDataFormat.None, ScriptIo.Resolve(manifest!, null).Input);
        Assert.IsNull(manifest!.Input);
        var parameter = manifest.Parameters.Single();
        Assert.AreEqual("file", parameter.Name);
        Assert.AreEqual(ScriptParameter.ParameterTypeFile, parameter.Type);
        Assert.IsTrue(parameter.IsRequired);
    }

    [TestMethod]
    public void Registry_Discovers_Descriptor_And_Skips_Its_Sibling_As_Header()
    {
        Write("act.ps1", "param($who)\nWrite-Output $who\n");
        Write("act.ps1.tool.json", """
            { "name": "act", "description": "d",
              "inputSchema": { "type": "object", "properties": { "who": { "type": "string" } } },
              "x-execute": { "command": ["powershell.exe", "-File", "act.ps1"] } }
            """);

        var registry = new ScriptRegistry(_root);
        registry.Load();

        // Exactly one script: the descriptor. The sibling .ps1 (no @powerscript.* header) is not
        // registered a second time, so there is no duplicate-id error.
        Assert.AreEqual(1, registry.Scripts.Count);
        Assert.AreEqual("act", registry.Scripts[0].Id);
        Assert.AreEqual(0, registry.Errors.Count);
        Assert.IsNotNull(registry.Scripts[0].Execute);
    }

    [TestMethod]
    public void Descriptor_Script_Runs_And_Passes_Scalars_On_Argv()
    {
        // A language-agnostic descriptor over a .cmd script: parameters arrive on argv.
        Write("echo.cmd", "@echo off\r\necho GOT %*\r\n");
        var descriptor = Write("echo.cmd.tool.json", """
            {
              "name": "echoer",
              "description": "Echo args.",
              "inputSchema": { "type": "object", "properties": { "who": { "type": "string" } } },
              "x-execute": { "command": ["cmd.exe", "/c", "echo.cmd"], "argMap": { "who": "/who" } }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);
        Assert.IsNotNull(manifest);
        manifest!.FolderPath = _root;

        var result = new ScriptExecutor(DirectExecutionSettings()).Execute(
            manifest,
            files: null,
            parameters: new Dictionary<string, string?> { ["who"] = "World" });

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        StringAssert.Contains(result.StdOut, "GOT");
        StringAssert.Contains(result.StdOut, "/who");
        StringAssert.Contains(result.StdOut, "World");
    }

    [TestMethod]
    public void Descriptor_Script_Receives_Structured_Input_On_Json_Stdin()
    {
        // stdin:"json" delivers the whole input object as one JSON document (large/structured channel).
        Write("readstdin.ps1", "$in = [Console]::In.ReadToEnd()\r\nWrite-Output $in\r\n");
        var descriptor = Write("readstdin.ps1.tool.json", """
            {
              "name": "reader",
              "description": "Echo stdin.",
              "inputSchema": { "type": "object", "properties": { "note": { "type": "string" } } },
              "x-execute": {
                "command": ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "readstdin.ps1"],
                "stdin": "json"
              }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);
        Assert.IsNotNull(manifest);
        manifest!.FolderPath = _root;

        var result = new ScriptExecutor(DirectExecutionSettings()).Execute(
            manifest,
            files: null,
            parameters: new Dictionary<string, string?> { ["note"] = "ping-42" });

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        StringAssert.Contains(result.StdOut, "ping-42");
        StringAssert.Contains(result.StdOut, "note");
    }

    [TestMethod]
    public void ProcessRunner_NonElevated_Captures_Output()
    {
        // The common (non-elevated) launch path returns exit code and stdout unchanged. This is also
        // the path descriptor/PowerShell/Python executors share, so its behavior is the security seam
        // exercised by every run.
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("echo hi-there");

        var context = new ProcessExecutionContext
        {
            ScriptId = "process-runner-test",
            ScriptDirectory = _root,
            ScriptsRoot = _root,
        };
        var result = ProcessRunner.Run(psi, context, mxcSettings: DirectExecutionSettings());

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StdOut, "hi-there");
    }

    private static MxcSettings DirectExecutionSettings() =>
        new()
        {
            Enabled = false,
            RiskAccepted = true,
        };
}
