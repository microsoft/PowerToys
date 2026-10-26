// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public class DescriptorTests
{
    [TestMethod]
    public void SchemasResolvePrimaryIoAndNestedTypedParametersWithoutRuntimeOrFunctionMetadata()
    {
        var descriptor = TestTool.Descriptor("text", "files");
        descriptor["inputSchema"]!["properties"]!["parameters"] = JsonNode.Parse("""
            {
              "type":"object", "additionalProperties":false,
              "properties":{
                "count":{"type":"integer","minimum":0,"maximum":5,"default":0},
                "shout":{"type":"boolean","default":false},
                "tone":{"type":"string","enum":["Hi","Hello"],"default":"Hi"},
                "file":{"type":"string","format":"file-path"}
              },
              "required":["file"]
            }
            """);
        var manifest = TestTool.Parse(descriptor, "script.py");
        Assert.AreEqual(ScriptRuntime.Python, manifest.Runtime);
        Assert.AreEqual((PowerScriptDataFormat.Text, PowerScriptDataFormat.Files), ScriptIo.Resolve(manifest));
        Assert.AreEqual(4, manifest.Parameters.Count);
        Assert.AreEqual("0", manifest.Parameters.Single(parameter => parameter.Name == "count").Default);
        Assert.AreEqual("false", manifest.Parameters.Single(parameter => parameter.Name == "shout").Default);
        Assert.IsTrue(manifest.Parameters.Single(parameter => parameter.Name == "tone").IsChoice);
        Assert.IsTrue(manifest.Parameters.Single(parameter => parameter.Name == "file").IsFile);
        Assert.IsTrue(manifest.Parameters.Single(parameter => parameter.Name == "file").IsRequired);
    }

    [TestMethod]
    public void InputValidationAppliesDefaultsAndPreservesJsonTypes()
    {
        var descriptor = TestTool.Descriptor();
        descriptor["inputSchema"]!["properties"]!["parameters"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"properties":{
              "count":{"type":"integer","default":0}, "enabled":{"type":"boolean","default":false},
              "text":{"type":"string","default":""}
            }}
            """);
        var manifest = TestTool.Parse(descriptor);
        var input = PythonTransformInput.Parse(manifest, """{"parameters":{}}""");
        var request = input.CreateRequest(manifest);
        Assert.AreEqual(0, request["parameters"]!["count"]!.GetValue<int>());
        Assert.IsFalse(request["parameters"]!["enabled"]!.GetValue<bool>());
        Assert.AreEqual(string.Empty, request["parameters"]!["text"]!.GetValue<string>());
        Assert.ThrowsException<InvalidDataException>(() =>
            PythonTransformInput.Parse(manifest, """{"parameters":{"count":"0"}}"""));
        Assert.ThrowsException<InvalidDataException>(() =>
            PythonTransformInput.Parse(manifest, """{"parameters":{"other":true}}"""));
    }

    [TestMethod]
    public void FileRestrictionsAreSchemaLocalAndEnforcedOnInputAndOutput()
    {
        var descriptor = TestTool.Descriptor("files", "files");
        foreach (var name in new[] { "inputSchema", "outputSchema" })
        {
            descriptor[name]!["properties"]!["files"]!["minItems"] = 1;
            descriptor[name]!["properties"]!["files"]!["maxItems"] = 2;
            descriptor[name]!["properties"]!["files"]!["items"]!["x-powerscript"] = JsonNode.Parse("""{"extensions":[".txt"]}""");
        }

        var manifest = TestTool.Parse(descriptor);
        CollectionAssert.AreEqual(new[] { ".txt" }, manifest.Input!.Extensions);
        Assert.AreEqual(1, manifest.Input.MinFiles);
        Assert.AreEqual(2, manifest.Input.MaxFiles);
        _ = PythonTransformInput.Parse(manifest, """{"files":["a.TXT"]}""");
        foreach (var json in new[] { """{"files":[]}""", """{"files":["a.png"]}""", """{"files":["a.txt","b.txt","c.txt"]}""" })
        {
            Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, json));
            using var document = JsonDocument.Parse(json);
            Assert.ThrowsException<InvalidDataException>(() => ScriptSchema.Validate(manifest.OutputSchema, document.RootElement, "output"));
        }
    }

    [TestMethod]
    public void SchemaConstraintsCannotBeBypassedByScalarOrFileParameters()
    {
        var descriptor = TestTool.Descriptor();
        descriptor["inputSchema"]!["properties"]!["parameters"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"properties":{
              "name":{"type":"string","minLength":2,"maxLength":4,"pattern":"^[a-z]+$"},
              "count":{"type":"integer","minimum":1,"maximum":3},
              "file":{"type":"string","format":"file-path","x-powerscript":{"extensions":[".csv"]}}
            },"required":["name"]}
            """);
        var manifest = TestTool.Parse(descriptor);
        _ = PythonTransformInput.Parse(manifest, """{"parameters":{"name":"good","count":2,"file":"data.csv"}}""");
        foreach (var json in new[]
        {
            """{"parameters":{}}""", """{"parameters":{"name":"x"}}""", """{"parameters":{"name":"TOOLONG"}}""",
            """{"parameters":{"name":"good","count":0}}""", """{"parameters":{"name":"good","file":"data.txt"}}""",
        })
        {
            Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, json));
        }
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("function")]
    [DataRow("input")]
    [DataRow("output")]
    [DataRow("extensions")]
    [DataRow("minFiles")]
    [DataRow("mxc")]
    public void RemovedMetadataIsRejectedInsteadOfOverridingSchemas(string name)
    {
        var descriptor = TestTool.Descriptor();
        descriptor["x-powerscript"] = new JsonObject { [name] = "obsolete" };
        Assert.ThrowsException<JsonException>(() => TestTool.Parse(descriptor));
    }

    [TestMethod]
    public void MandatoryClosedObjectSchemasAndSinglePrimaryShapeAreValidatedBeforeDiscovery()
    {
        var descriptor = TestTool.Descriptor();
        descriptor.Remove("outputSchema");
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = TestTool.Descriptor();
        descriptor["inputSchema"]!["type"] = "array";
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = TestTool.Descriptor("text");
        descriptor["inputSchema"]!["properties"]!["files"] = JsonNode.Parse("""{"type":"array","items":{"type":"string"}}""");
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = TestTool.Descriptor();
        descriptor["inputSchema"]!["additionalProperties"] = true;
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = TestTool.Descriptor();
        descriptor["inputSchema"]!["oneOf"] = new JsonArray();
        Assert.ThrowsException<JsonException>(() => TestTool.Parse(descriptor));
    }

    [TestMethod]
    public void NoneContractsAreEmptyObjectsAndUnknownPayloadFieldsAreRejected()
    {
        var manifest = TestTool.Parse(TestTool.Descriptor("none", "none"));
        _ = PythonTransformInput.Parse(manifest, "{}");
        foreach (var json in new[] { """{"file_paths":[]}""", """{"params":{}}""", """{"entry":"main"}""", """{"text":""}""" })
        {
            Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, json));
        }
    }

    [TestMethod]
    public void RegistryDoesNotReadEntryFunctionsOrScriptSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var entry = Path.Combine(root, "script.py");
            File.WriteAllText(entry, "print('top-level')\n");
            File.WriteAllText(entry + ".tool.json", TestTool.Descriptor().ToJsonString());
            var registry = new ScriptRegistry(root);
            registry.Load();
            Assert.AreEqual(1, registry.Scripts.Count);
            Assert.AreEqual(0, registry.Errors.Count);
            Assert.AreEqual(ScriptRuntime.Python, registry.Scripts[0].Runtime);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UnicodeLengthNullDefaultsAndZeroFileLimitsUseSchemaSemantics()
    {
        var descriptor = TestTool.Descriptor("text");
        descriptor["inputSchema"]!["properties"]!["text"]!["minLength"] = 1;
        descriptor["inputSchema"]!["properties"]!["text"]!["maxLength"] = 1;
        var manifest = TestTool.Parse(descriptor);
        _ = PythonTransformInput.Parse(manifest, "{\"text\":\"\\ud83d\\ude00\"}");
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, """{"text":"ab"}"""));
        descriptor["inputSchema"]!["properties"]!["text"]!["default"] = null;
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));

        descriptor = TestTool.Descriptor("files");
        descriptor["inputSchema"]!["properties"]!["files"]!["maxItems"] = 0;
        manifest = TestTool.Parse(descriptor);
        _ = PythonTransformInput.Parse(manifest, """{"files":[]}""");
        Assert.AreEqual(0, manifest.Input!.MaxFiles);
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, """{"files":["a.txt"]}"""));
        descriptor["inputSchema"]!["properties"]!["files"]!.AsObject().Remove("maxItems");
        manifest = TestTool.Parse(descriptor);
        Assert.IsNull(manifest.Input!.MaxFiles);
        _ = PythonTransformInput.Parse(manifest, """{"files":["a.txt","b.txt"]}""");
    }

    [TestMethod]
    public void PowerShellReceivesNativeBooleansDefaultsAndCompleteFileArrays()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var descriptor = TestTool.Descriptor("files");
            descriptor["inputSchema"]!["properties"]!["parameters"] = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"properties":{
                  "enabled":{"type":"boolean","default":false},
                  "count":{"type":"integer","default":0},
                  "label":{"type":"string","default":""}
                },"required":["label"]}
                """);
            var manifest = TestTool.Parse(descriptor);
            manifest.FolderPath = root;
            File.WriteAllText(manifest.EntryFullPath, """
                param([string[]]$Files, [bool]$Enabled, [int]$Count, [string]$Label)
                [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
                @{ files = @($Files); enabled = $Enabled; count = $Count; label = $Label } | ConvertTo-Json -Compress
                """);
            var paths = new[] { Path.Combine(root, "a 'quote'.txt"), Path.Combine(root, "b space.txt") };
            var result = new ScriptExecutor(new MxcSettings { Enabled = false, RiskAccepted = true })
                .Execute(manifest, paths);
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
            using var output = JsonDocument.Parse(result.StdOut);
            Assert.IsFalse(output.RootElement.GetProperty("enabled").GetBoolean());
            Assert.AreEqual(0, output.RootElement.GetProperty("count").GetInt32());
            Assert.AreEqual(string.Empty, output.RootElement.GetProperty("label").GetString());
            CollectionAssert.AreEqual(paths, output.RootElement.GetProperty("files").EnumerateArray().Select(path => path.GetString()).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ArgvRecipesReceivePrimaryTextAndMappedNamedOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-argv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var descriptor = TestTool.Descriptor("text");
            descriptor["x-execute"] = JsonNode.Parse("""
                {"command":["powershell","-NoProfile","-NonInteractive","-File","script.ps1"],"argMap":{"text":"-Value"}}
                """);
            var manifest = TestTool.Parse(descriptor);
            manifest.FolderPath = root;
            File.WriteAllText(manifest.EntryFullPath, "param([string]$Value)\nWrite-Output $Value\n");
            var result = new ScriptExecutor(new MxcSettings { Enabled = false, RiskAccepted = true })
                .Execute(manifest, text: "literal 'value' with spaces");
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
            Assert.AreEqual("literal 'value' with spaces", result.StdOut.Trim());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ProcessRunnerNonElevatedCapturesOutput()
    {
        var start = new ProcessStartInfo("cmd.exe");
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("echo hello");
        var result = ProcessRunner.SecureLauncher.Run(start, null, 5000);
        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        StringAssert.Contains(result.StdOut, "hello");
    }
}
