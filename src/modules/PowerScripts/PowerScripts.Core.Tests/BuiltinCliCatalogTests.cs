// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class BuiltinCliCatalogTests
{
    [TestMethod]
    public void EmbeddedCataloguePreservesFriendlyParameterMetadataAndIsolationRequirements()
    {
        var catalog = BuiltinCliCatalog.Load();
        var file = catalog.Get("filelocksmith.query")!;
        var path = file.Parameters.Single(parameter => parameter.Name == "path");
        Assert.AreEqual("Find processes using a file", file.Name);
        Assert.AreEqual("filelocksmith", file.ModuleId);
        Assert.AreEqual("File Locksmith", file.Group);
        Assert.AreEqual("FileLocksmithCLI.exe", file.Executable);
        Assert.IsTrue(path.IsFile);
        Assert.IsTrue(path.IsRequired);
        Assert.AreEqual("File", path.Label);
        Assert.AreEqual("An existing file to inspect.", path.Description);
        var format = file.Parameters.Single(parameter => parameter.Name == "json");
        Assert.AreEqual("Output format", format.Label);
        Assert.IsTrue(format.IsBool);
        Assert.AreEqual("true", format.Default);
        Assert.AreEqual("choice", file.ParameterUi["json"].Control);
        Assert.AreEqual("JSON", file.ParameterUi["json"].TrueLabel);
        Assert.AreEqual("Text", file.ParameterUi["json"].FalseLabel);
        CollectionAssert.AreEqual(new[] { "json", "path" }, file.Bindings.Select(binding => binding.Parameter).ToArray());
        Assert.IsTrue(file.RequiresHostProcessVisibility);
        Assert.IsFalse(file.RequiresHostDesktop);

        foreach (var id in new[] { "fancyzones.get-layouts", "fancyzones.get-monitors" })
        {
            var command = catalog.Get(id)!;
            Assert.AreEqual("fancyzones", command.ModuleId);
            Assert.AreEqual("FancyZones", command.Group);
            Assert.AreEqual("FancyZonesCLI.exe", command.Executable);
            Assert.AreEqual(id["fancyzones.".Length..], command.FixedArguments.Single());
            Assert.AreEqual(0, command.Parameters.Count);
            Assert.IsFalse(command.RequiresHostProcessVisibility);
            Assert.IsTrue(command.RequiresHostDesktop);
        }

        foreach (var command in catalog.Commands)
        {
            CollectionAssert.AreEqual(new[] { "fileRead" }, command.RequiredCapabilities.ToArray());
        }
    }

    [TestMethod]
    public void ModulesFlattenIntoStableOperationsWithInheritedDisplayNames()
    {
        var root = Catalog();
        var other = Module(root).DeepClone().AsObject();
        other["id"] = "other";
        other["title"] = "Other module";
        other["operations"]![0]!["id"] = "other.query";
        root["modules"]!.AsArray().Add(other);

        var catalog = BuiltinCliCatalog.Parse(root.ToJsonString());
        CollectionAssert.AreEqual(new[] { "test.query", "other.query" }, catalog.Commands.Select(command => command.Id).ToArray());
        Assert.AreEqual("test", catalog.Commands[0].ModuleId);
        Assert.AreEqual("Test", catalog.Commands[0].Group);
        Assert.AreEqual("other", catalog.Commands[1].ModuleId);
        Assert.AreEqual("Other module", catalog.Commands[1].Group);
        Assert.AreEqual("Test operation", catalog.Commands[1].Name);
    }

    [TestMethod]
    public void BooleanChoiceLabelsDoNotTurnFalseIntoAStringOrEnableItsSwitch()
    {
        var command = BuiltinCliCatalog.Load().Get("filelocksmith.query")!;
        var values = new Dictionary<string, string?> { ["path"] = "literal.txt", ["json"] = "false" };
        var start = CliActionProvider.CreateStartInfo(command, "FileLocksmithCLI.exe", Path.GetTempPath(), values);

        CollectionAssert.AreEqual(new[] { "literal.txt" }, start.ArgumentList.ToArray());
        values["json"] = "true";
        start = CliActionProvider.CreateStartInfo(command, "FileLocksmithCLI.exe", Path.GetTempPath(), values);
        CollectionAssert.AreEqual(new[] { "--json", "literal.txt" }, start.ArgumentList.ToArray());
    }

    [TestMethod]
    public void ACallerCannotMutateTheNextInstalledCatalogueLoad()
    {
        var catalog = BuiltinCliCatalog.Load();
        catalog.Get("filelocksmith.query")!.Parameters[0].Label = "Changed by a consumer";

        Assert.AreEqual("File", BuiltinCliCatalog.Load().Get("FILELOCKSMITH.QUERY")!.Parameters[0].Label);
    }

    [TestMethod]
    public void JsonSchemaSuppliesTypedDefaultsConstraintsAndArgumentOrder()
    {
        var root = Catalog();
        var command = Command(root);
        command["inputSchema"] = JsonNode.Parse("""
            {
              "type":"object", "additionalProperties":false,
              "properties": {
                "file":{"type":"string","format":"file-path","title":"File"},
                "count":{"type":"integer","title":"Count","minimum":0,"maximum":9,"default":0},
                "mode":{"type":"string","title":"Mode","enum":["fast","full"],"default":"fast"},
                "label":{"type":"string","title":"Label","default":""},
                "enabled":{"type":"boolean","title":"Enabled","default":false}
              },
              "required":["file"]
            }
            """);
        command["x-execute"]!["arguments"] = JsonNode.Parse("""
            [
              {"parameter":"enabled","kind":"switch","flag":"--enabled"},
              {"parameter":"count","kind":"option","flag":"--count"},
              {"parameter":"mode","kind":"option","flag":"--mode"},
              {"parameter":"label","kind":"option","flag":"--label","attachedValue":true},
              {"parameter":"file","kind":"positional"}
            ]
            """);

        var parsed = BuiltinCliCatalog.Parse(root.ToJsonString()).Commands.Single();
        var parameters = parsed.Parameters.ToDictionary(parameter => parameter.Name);
        Assert.IsTrue(parameters["file"].IsFile);
        Assert.IsTrue(parameters["file"].IsRequired);
        Assert.AreEqual(ScriptParameter.ParameterTypeInt, parameters["count"].Type);
        Assert.AreEqual("0", parameters["count"].Default);
        Assert.AreEqual(0, parameters["count"].Min);
        Assert.AreEqual(9, parameters["count"].Max);
        Assert.IsTrue(parameters["mode"].IsChoice);
        CollectionAssert.AreEqual(new[] { "fast", "full" }, parameters["mode"].Options);
        Assert.AreEqual(string.Empty, parameters["label"].Default);
        Assert.AreEqual("false", parameters["enabled"].Default);
        Assert.IsTrue(parsed.Bindings.Single(binding => binding.Parameter == "label").AttachedValue);
        Assert.AreEqual("enabled", parsed.Bindings[0].Parameter);
        Assert.AreEqual("file", parsed.Bindings[^1].Parameter);
    }

    [DataTestMethod]
    [DataRow("unknown-root")]
    [DataRow("version")]
    [DataRow("empty")]
    [DataRow("duplicate-id")]
    [DataRow("duplicate-module")]
    [DataRow("duplicate-id-across-modules")]
    [DataRow("empty-module")]
    [DataRow("empty-module-title")]
    [DataRow("unknown-module")]
    [DataRow("repeated-group")]
    [DataRow("unknown-operation")]
    [DataRow("missing-policy")]
    [DataRow("null-policy")]
    [DataRow("string-policy")]
    [DataRow("absolute-executable")]
    [DataRow("parent-executable")]
    [DataRow("wrong-extension")]
    [DataRow("empty-command")]
    [DataRow("nul-argument")]
    [DataRow("unknown-execution")]
    [DataRow("open-schema")]
    [DataRow("unknown-schema")]
    [DataRow("unknown-parameter-schema")]
    [DataRow("missing-label")]
    [DataRow("unknown-required")]
    [DataRow("duplicate-required")]
    [DataRow("null-required")]
    [DataRow("duplicate-parameter-name")]
    [DataRow("wrong-default-type")]
    [DataRow("null-default")]
    [DataRow("noninteger-bound")]
    [DataRow("wrong-bound-type")]
    [DataRow("unknown-argument")]
    [DataRow("duplicate-argument")]
    [DataRow("missing-argument")]
    [DataRow("wrong-switch-type")]
    [DataRow("missing-flag")]
    [DataRow("positional-flag")]
    [DataRow("attached-switch")]
    [DataRow("unknown-kind")]
    [DataRow("unknown-binding")]
    [DataRow("duplicate-capability")]
    [DataRow("unknown-ui-control")]
    [DataRow("unknown-ui-member")]
    [DataRow("missing-ui-label")]
    [DataRow("empty-ui-label")]
    [DataRow("duplicate-ui-labels")]
    [DataRow("wrong-ui-label-type")]
    [DataRow("null-ui")]
    [DataRow("ui-on-nonboolean")]
    public void CatalogueRejectsAmbiguousUnsupportedOrUnsafeDefinitions(string mutation)
    {
        var root = Catalog();
        var command = Command(root);
        var schema = command["inputSchema"]!.AsObject();
        var properties = schema["properties"]!.AsObject();
        var value = properties["value"]!.AsObject();
        var ui = value["x-ui"]!.AsObject();
        var execute = command["x-execute"]!.AsObject();
        var bindings = execute["arguments"]!.AsArray();
        var binding = bindings[0]!.AsObject();
        switch (mutation)
        {
            case "unknown-root": root["externalCatalogue"] = "other.json"; break;
            case "version": root["schemaVersion"] = 1; break;
            case "empty": root["modules"] = new JsonArray(); break;
            case "duplicate-id": Module(root)["operations"]!.AsArray().Add(command.DeepClone()); break;
            case "duplicate-module":
                var duplicateModule = Module(root).DeepClone().AsObject();
                duplicateModule["id"] = "TEST";
                root["modules"]!.AsArray().Add(duplicateModule);
                break;
            case "duplicate-id-across-modules":
                var otherModule = Module(root).DeepClone().AsObject();
                otherModule["id"] = "other";
                root["modules"]!.AsArray().Add(otherModule);
                break;
            case "empty-module": Module(root)["operations"] = new JsonArray(); break;
            case "empty-module-title": Module(root)["title"] = ""; break;
            case "unknown-module": Module(root)["group"] = "Test"; break;
            case "repeated-group": command["group"] = "Test"; break;
            case "unknown-operation": command["commandOverride"] = "other.exe"; break;
            case "missing-policy": command.Remove("requiresHostProcessVisibility"); break;
            case "null-policy": command["requiresHostDesktop"] = null; break;
            case "string-policy": command["requiresHostDesktop"] = "false"; break;
            case "absolute-executable": execute["command"] = new JsonArray("C:\\other.exe"); break;
            case "parent-executable": execute["command"] = new JsonArray("..\\other.exe"); break;
            case "wrong-extension": execute["command"] = new JsonArray("other.cmd"); break;
            case "empty-command": execute["command"] = new JsonArray(); break;
            case "nul-argument": execute["command"]!.AsArray().Add("bad\0argument"); break;
            case "unknown-execution": execute["stdin"] = "json"; break;
            case "open-schema": schema["additionalProperties"] = true; break;
            case "unknown-schema": schema["$ref"] = "external.json"; break;
            case "unknown-parameter-schema": value["x-control"] = "arbitrary"; break;
            case "missing-label": value.Remove("title"); break;
            case "unknown-required": schema["required"] = new JsonArray("missing"); break;
            case "duplicate-required": schema["required"] = new JsonArray("value", "value"); break;
            case "null-required": schema["required"] = null; break;
            case "duplicate-parameter-name": properties["VALUE"] = value.DeepClone(); break;
            case "wrong-default-type": value["default"] = "false"; break;
            case "null-default": value["default"] = null; break;
            case "noninteger-bound": value["minimum"] = 1.5; break;
            case "wrong-bound-type": value["minimum"] = 1; break;
            case "unknown-argument": binding["parameter"] = "missing"; break;
            case "duplicate-argument": bindings.Add(binding.DeepClone()); break;
            case "missing-argument": bindings.Clear(); break;
            case "wrong-switch-type": value["type"] = "string"; value["default"] = "false"; value.Remove("x-ui"); break;
            case "missing-flag": binding.Remove("flag"); break;
            case "positional-flag": binding["kind"] = "positional"; break;
            case "attached-switch": binding["attachedValue"] = true; break;
            case "unknown-kind": binding["kind"] = "shell"; break;
            case "unknown-binding": binding["command"] = "other.exe"; break;
            case "duplicate-capability": command["requiredCapabilities"] = new JsonArray("fileRead", "fileRead"); break;
            case "unknown-ui-control": ui["control"] = "script"; break;
            case "unknown-ui-member": ui["value"] = "--override"; break;
            case "missing-ui-label": ui.Remove("falseLabel"); break;
            case "empty-ui-label": ui["trueLabel"] = ""; break;
            case "duplicate-ui-labels": ui["trueLabel"] = ui["falseLabel"]!.GetValue<string>(); break;
            case "wrong-ui-label-type": ui["trueLabel"] = true; break;
            case "null-ui": value["x-ui"] = null; break;
            case "ui-on-nonboolean":
                value["type"] = "string";
                value["default"] = "false";
                binding["kind"] = "positional";
                binding.Remove("flag");
                break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(root.ToJsonString()), mutation);
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("{\"schemaVersion\":1,\"commands\":[]}")]
    [DataRow("{\"schemaVersion\":2,\"schemaVersion\":2,\"modules\":[]}")]
    public void InvalidRootStructureIsRejected(string json) =>
        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(json));

    [TestMethod]
    public void NestedDuplicateMembersAndOversizedCataloguesAreRejected()
    {
        var json = Catalog().ToJsonString().Replace("\"kind\":\"switch\"", "\"kind\":\"switch\",\"kind\":\"option\"", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(json));
        json = Catalog().ToJsonString().Replace("\"control\":\"choice\"", "\"control\":\"choice\",\"control\":\"choice\"", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(json));
        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(new string(' ', BuiltinCliCatalog.MaximumCatalogBytes + 1)));
    }

    private static JsonObject Module(JsonObject root) => root["modules"]![0]!.AsObject();

    private static JsonObject Command(JsonObject root) => Module(root)["operations"]![0]!.AsObject();

    private static JsonObject Catalog() => JsonNode.Parse("""
        {
          "schemaVersion":2,
          "modules":[{"id":"test", "title":"Test", "operations":[{
            "id":"test.query", "title":"Test operation", "description":"Inspect a value.",
            "inputSchema": {
              "type":"object", "additionalProperties":false,
              "properties":{"value":{"type":"boolean","title":"Value","default":false,
                "x-ui":{"control":"choice","trueLabel":"Yes","falseLabel":"No"}}}
            },
            "x-execute": {
              "command":["TestCLI.exe","query"],
              "arguments":[{"parameter":"value","kind":"switch","flag":"--value"}]
            },
            "requiredCapabilities":["fileRead"],
            "requiresHostProcessVisibility":false,
            "requiresHostDesktop":false
          }]}]
        }
        """)!.AsObject();
}
