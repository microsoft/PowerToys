// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliCatalogTests
{
    private const string ValidCatalogJson = """
        {
          "schemaVersion": 1,
          "commands": [
            {
              "id": "test.command",
              "title": "Test command",
              "group": "Tests",
              "description": "Inspect the selected file.",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "path": {
                    "type": "string",
                    "format": "file-path",
                    "title": "Selected file",
                    "description": "The file to inspect."
                  },
                  "json": { "type": "boolean", "default": true }
                },
                "required": ["path"]
              },
              "x-execute": {
                "command": ["FileLocksmithCLI.exe"],
                "arguments": [
                  { "parameter": "json", "kind": "switch", "flag": "--json" },
                  { "parameter": "path", "kind": "positional" }
                ]
              }
            }
          ]
        }
        """;

    [TestMethod]
    public void Load_ContainsTheEmbeddedNativeCommands()
    {
        var catalog = BuiltinCliCatalog.Load();
        var expectedIds = new[]
        {
            "powerscripts.inspect-arguments",
            "filelocksmith.query",
            "fancyzones.get-layouts",
            "fancyzones.get-monitors",
            "image-resizer.resize",
        };

        foreach (var id in expectedIds)
        {
            var command = catalog.Get(id);
            Assert.IsNotNull(command, $"Missing embedded command '{id}'.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(command.Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(command.Group));
            Assert.IsFalse(string.IsNullOrWhiteSpace(command.Description));
        }
    }

    [TestMethod]
    public void Load_ArgumentInspectionDemoPreservesTheFormAndBindingContract()
    {
        var command = BuiltinCliCatalog.Load().Get("powerscripts.inspect-arguments");
        Assert.IsNotNull(command);
        Assert.AreEqual("PowerScripts/PowerScripts.Host.exe", command.Executable);
        CollectionAssert.AreEqual(new[] { "demo", "echo" }, command.FixedArguments.ToArray());

        var target = command.Parameters.Single(parameter => parameter.Name == "target");
        Assert.AreEqual(ScriptParameter.ParameterTypeString, target.Type);
        Assert.IsTrue(target.IsRequired);
        Assert.AreEqual("demo", target.Default);

        var mode = command.Parameters.Single(parameter => parameter.Name == "mode");
        Assert.AreEqual(ScriptParameter.ParameterTypeChoice, mode.Type);
        CollectionAssert.AreEqual(new[] { "fast", "full" }, mode.Options);
        Assert.AreEqual("fast", mode.Default);

        var count = command.Parameters.Single(parameter => parameter.Name == "count");
        Assert.AreEqual(ScriptParameter.ParameterTypeInt, count.Type);
        Assert.AreEqual("1", count.Default);
        Assert.AreEqual(1, count.Min);
        Assert.AreEqual(100, count.Max);

        var verbose = command.Parameters.Single(parameter => parameter.Name == "verbose");
        Assert.AreEqual(ScriptParameter.ParameterTypeBool, verbose.Type);
        Assert.AreEqual("false", verbose.Default);
        var verboseBinding = command.Bindings.Single(binding => binding.Parameter == "verbose");
        Assert.AreEqual(CliArgumentKind.Switch, verboseBinding.Kind);
        Assert.AreEqual("--verbose", verboseBinding.Name);
        foreach (var name in new[] { "target", "mode", "count" })
        {
            var binding = command.Bindings.Single(candidate => candidate.Parameter == name);
            Assert.AreEqual(CliArgumentKind.Option, binding.Kind);
            Assert.AreEqual("--" + name, binding.Name);
        }
    }

    [TestMethod]
    public void Load_FileLocksmithQueryUsesARequiredFileAndAnOptionalJsonSwitch()
    {
        var command = BuiltinCliCatalog.Load().Get("filelocksmith.query");
        Assert.IsNotNull(command);
        Assert.AreEqual("FileLocksmithCLI.exe", command.Executable);

        var path = command.Parameters.Single(parameter => parameter.Name == "path");
        Assert.AreEqual(ScriptParameter.ParameterTypeFile, path.Type);
        Assert.IsTrue(path.IsRequired);
        Assert.AreEqual(CliArgumentKind.Positional, command.Bindings.Single(binding => binding.Parameter == "path").Kind);

        var json = command.Parameters.Single(parameter => parameter.Name == "json");
        Assert.AreEqual(ScriptParameter.ParameterTypeBool, json.Type);
        Assert.AreEqual("true", json.Default);
        Assert.IsFalse(json.IsRequired);
        var jsonBinding = command.Bindings.Single(binding => binding.Parameter == "json");
        Assert.AreEqual(CliArgumentKind.Switch, jsonBinding.Kind);
        Assert.AreEqual("--json", jsonBinding.Name);
        CollectionAssert.DoesNotContain(command.FixedArguments.ToArray(), "--json");
    }

    [TestMethod]
    public void Load_FancyZonesGetLayoutsHasOnlyItsFixedSubcommand()
    {
        var command = BuiltinCliCatalog.Load().Get("fancyzones.get-layouts");
        Assert.IsNotNull(command);

        CollectionAssert.AreEqual(new[] { "get-layouts" }, command.FixedArguments.ToArray());
        Assert.AreEqual(0, command.Parameters.Count);
        Assert.AreEqual(0, command.Bindings.Count);
    }

    [TestMethod]
    public void Get_ReturnsNullForAnUnknownCommand()
    {
        Assert.IsNull(BuiltinCliCatalog.Load().Get("missing.command"));
    }

    [TestMethod]
    public void Parse_ReadsTheInputSchemaAndExplicitArgumentBindings()
    {
        var catalog = BuiltinCliCatalog.Parse(ValidCatalogJson);
        var command = catalog.Get("test.command");
        Assert.IsNotNull(command);
        Assert.AreEqual("Test command", command.Name);
        Assert.AreEqual("Tests", command.Group);
        Assert.AreEqual("Inspect the selected file.", command.Description);
        Assert.AreEqual("FileLocksmithCLI.exe", command.Executable);
        Assert.AreEqual(0, command.FixedArguments.Count);

        var path = command.Parameters.Single(parameter => parameter.Name == "path");
        Assert.AreEqual("Selected file", path.Label);
        Assert.AreEqual("The file to inspect.", path.Description);
        Assert.AreEqual(ScriptParameter.ParameterTypeFile, path.Type);
        Assert.IsTrue(path.IsRequired);
        Assert.AreEqual(CliArgumentKind.Positional, command.Bindings.Single(binding => binding.Parameter == "path").Kind);

        var json = command.Parameters.Single(parameter => parameter.Name == "json");
        Assert.AreEqual(ScriptParameter.ParameterTypeBool, json.Type);
        Assert.AreEqual("true", json.Default);
        var binding = command.Bindings.Single(candidate => candidate.Parameter == "json");
        Assert.AreEqual(CliArgumentKind.Switch, binding.Kind);
        Assert.AreEqual("--json", binding.Name);
    }

    [DataTestMethod]
    [DataRow("test.command")]
    [DataRow("TEST.COMMAND")]
    public void Parse_RejectsDuplicateCommandIds(string duplicateId)
    {
        var document = CreateDocument();
        var duplicate = GetCommand(document).DeepClone().AsObject();
        duplicate["id"] = duplicateId;
        document["commands"]!.AsArray().Add(duplicate);

        AssertInvalid(document);
    }

    [DataTestMethod]
    [DataRow("number")]
    [DataRow("object")]
    [DataRow("array")]
    [DataRow("custom")]
    public void Parse_RejectsUnsupportedParameterTypes(string type)
    {
        var document = CreateDocument();
        var command = GetCommand(document);
        var parameter = command["inputSchema"]!["properties"]!["json"]!.AsObject();
        parameter["type"] = type;
        parameter.Remove("default");
        command["x-execute"]!["arguments"]![0]!["kind"] = "option";

        AssertInvalid(document);
    }

    [TestMethod]
    public void Parse_RejectsBindingsToUndeclaredParameters()
    {
        var document = CreateDocument();
        GetCommand(document)["x-execute"]!["arguments"]!.AsArray().Add(new JsonObject
        {
            ["parameter"] = "undeclared",
            ["kind"] = "option",
            ["flag"] = "--extra",
        });

        AssertInvalid(document);
    }

    [TestMethod]
    public void Parse_RejectsNullRequiredParameterNamesAsInvalidData()
    {
        var document = CreateDocument();
        GetCommand(document)["inputSchema"]!["required"]!.AsArray().Add((JsonNode?)null);

        AssertInvalid(document);
    }

    [TestMethod]
    public void Parse_RejectsSwitchBindingsForNonBooleanParameters()
    {
        var document = CreateDocument();
        var parameter = GetCommand(document)["inputSchema"]!["properties"]!["json"]!;
        parameter["type"] = "string";
        parameter["default"] = "true";

        AssertInvalid(document);
    }

    [TestMethod]
    public void Parse_RejectsSwitchBindingsWithoutAFlag()
    {
        var document = CreateDocument();
        GetCommand(document)["x-execute"]!["arguments"]![0]!.AsObject().Remove("flag");

        AssertInvalid(document);
    }

    [DataTestMethod]
    [DataRow(@"C:\Windows\cmd.exe")]
    [DataRow(@"\Windows\cmd.exe")]
    [DataRow(@"\\server\share\tool.exe")]
    [DataRow("/usr/bin/tool.exe")]
    public void Parse_RejectsAbsoluteExecutablePaths(string executable)
    {
        var document = CreateDocument();
        GetCommand(document)["x-execute"]!["command"]![0] = executable;

        AssertInvalid(document);
    }

    [DataTestMethod]
    [DataRow(@"..\tool.exe")]
    [DataRow(@"native\..\tool.exe")]
    [DataRow("../tool.exe")]
    [DataRow("native/../tool.exe")]
    public void Parse_RejectsExecutablePathsContainingParentTraversal(string executable)
    {
        var document = CreateDocument();
        GetCommand(document)["x-execute"]!["command"]![0] = executable;

        AssertInvalid(document);
    }

    private static JsonObject CreateDocument() => JsonNode.Parse(ValidCatalogJson)!.AsObject();

    private static JsonObject GetCommand(JsonObject document) => document["commands"]![0]!.AsObject();

    private static void AssertInvalid(JsonObject document) =>
        Assert.ThrowsException<InvalidDataException>(() => BuiltinCliCatalog.Parse(document.ToJsonString()));
}
