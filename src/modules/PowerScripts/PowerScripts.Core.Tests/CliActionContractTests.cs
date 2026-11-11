// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliActionContractTests
{
    [TestMethod]
    public void CataloguePublishesOnlyTheInitialReadOnlyOperations()
    {
        var catalog = BuiltinCliCatalog.Load();
        CollectionAssert.AreEquivalent(
            new[] { "filelocksmith.query", "fancyzones.get-layouts", "fancyzones.get-monitors" },
            catalog.Commands.Select(command => command.Id).ToArray());
        Assert.IsNull(catalog.Get("image-resizer.resize"));
        Assert.IsNull(catalog.Get("powerscripts.inspect-arguments"));
        Assert.IsTrue(catalog.Get("filelocksmith.query")!.RequiredCapabilities.Contains("fileRead"));
        Assert.IsTrue(catalog.Get("fancyzones.get-layouts")!.RequiredCapabilities.Contains("fileRead"));
        Assert.IsTrue(catalog.Get("fancyzones.get-monitors")!.RequiredCapabilities.Contains("fileRead"));
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{"schemaVersion":"1","commandId":"fancyzones.get-layouts","bindings":{}}""")]
    [DataRow("""{"schemaVersion":1,"schemaVersion":1,"commandId":"fancyzones.get-layouts","bindings":{}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"fancyzones.get-layouts","bindings":{},"extra":true}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"constant":"a","parameter":"path"}}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{}}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"constant":"a"},"path":{"constant":"b"}}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"constant":"a","extra":false}}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":[]}""")]
    [DataRow("""{"schemaVersion":1,"commandId":1,"bindings":{}}""")]
    [DataRow("""{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"CONSTANT":"a"}}}""")]
    public void ActionJsonRejectsAmbiguousOrUnknownStructure(string json)
    {
        Assert.ThrowsException<InvalidDataException>(() => CliActionProvider.Parse(json));
    }

    [TestMethod]
    public void SerializedFalseConstantRemainsAnExplicitConstant()
    {
        var action = Action(
            ("path", Constant("C:\\missing\\literal file.txt")),
            ("json", Constant(false)));
        var json = JsonSerializer.Serialize(action);
        var reparsed = CliActionProvider.Parse(json);

        Assert.AreEqual(JsonValueKind.False, reparsed.Bindings["json"].Constant.ValueKind);
        Assert.IsNull(reparsed.Bindings["json"].Parameter);
        Assert.AreEqual("C:\\missing\\literal file.txt", reparsed.Bindings["path"].Constant.GetString());
    }

    [TestMethod]
    public void FixedFileConfigurationDoesNotRequireTheFileOrExecutableDuringCreation()
    {
        var manifest = FileManifest(exposePath: false);
        var missing = Path.Combine(Path.GetTempPath(), "powerscripts-not-created-" + Guid.NewGuid().ToString("N"), "literal file.txt");
        var action = Action(("path", Constant(missing)), ("json", Constant(false)));

        Assert.AreEqual(0, CliActionProvider.Validate(manifest, action).Count);
    }

    [TestMethod]
    public void RuntimeFileReferenceUsesTheStandardTypedParameterContract()
    {
        var manifest = FileManifest();
        var action = Action(("path", Reference("path")));

        Assert.AreEqual(0, CliActionProvider.Validate(manifest, action).Count);
        Assert.AreEqual(ScriptRuntime.PowerToysOperation, manifest.Runtime);
        Assert.IsTrue(manifest.Parameters.Single().IsFile);
        Assert.IsTrue(manifest.Parameters.Single().IsRequired);
    }

    [TestMethod]
    public void RuntimeBooleanDefaultFalseIsPreservedWithoutBecomingAFixedValue()
    {
        var descriptor = Descriptor(exposePath: true);
        var parameters = descriptor["inputSchema"]!["properties"]!["parameters"]!;
        parameters["properties"]!["returnJson"] = new JsonObject
        {
            ["type"] = "boolean",
            ["default"] = false,
        };
        var manifest = TestTool.Parse(descriptor, "action.json");
        var action = Action(("path", Reference("path")), ("json", Reference("returnJson")));

        Assert.AreEqual(0, CliActionProvider.Validate(manifest, action).Count);
        Assert.AreEqual("false", manifest.Parameters.Single(parameter => parameter.Name == "returnJson").Default);
        Assert.AreEqual(JsonValueKind.Undefined, action.Bindings["json"].Constant.ValueKind);
    }

    [TestMethod]
    public void UnknownCommandAndArgumentNamesCannotSelectAnotherExecutable()
    {
        var manifest = FileManifest(exposePath: false);
        var unknownCommand = new CliAction
        {
            SchemaVersion = 1,
            CommandId = "..\\other.exe",
            Bindings = new(),
        };
        Assert.IsTrue(CliActionProvider.Validate(manifest, unknownCommand).Count > 0);

        var action = Action(("path", Constant("C:\\file.txt")), ("executable", Constant("other.exe")));
        Assert.IsTrue(CliActionProvider.Validate(manifest, action).Count > 0);
    }

    [TestMethod]
    public void RequiredCommandArgumentCannotBeOmitted()
    {
        var manifest = FileManifest(exposePath: false);
        var action = Action(("json", Constant(true)));

        Assert.IsTrue(CliActionProvider.Validate(manifest, action).Count > 0);
    }

    [TestMethod]
    public void ReferencesMustPointToADeclaredParameterOfTheCorrectType()
    {
        var file = FileManifest();
        Assert.IsTrue(CliActionProvider.Validate(file, Action(("path", Reference("missing")))).Count > 0);

        var descriptor = Descriptor(exposePath: true);
        descriptor["inputSchema"]!["properties"]!["parameters"]!["properties"]!["path"]!["format"] = null;
        var plainString = TestTool.Parse(descriptor, "action.json");
        Assert.IsTrue(CliActionProvider.Validate(plainString, Action(("path", Reference("path")))).Count > 0);
    }

    [TestMethod]
    public void BooleanConstantsMustUseJsonBooleans()
    {
        var manifest = FileManifest(exposePath: false);
        foreach (var value in new object[] { "false", 0 })
        {
            var action = Action(("path", Constant("C:\\file.txt")), ("json", Constant(value)));
            Assert.IsTrue(CliActionProvider.Validate(manifest, action).Count > 0);
        }
    }

    [TestMethod]
    public void NullConstantCannotSatisfyARequiredFileArgument()
    {
        var manifest = FileManifest(exposePath: false);
        var action = Action(("path", Constant(null)));

        Assert.IsTrue(CliActionProvider.Validate(manifest, action).Count > 0);
    }

    [TestMethod]
    public void RequiredFileCapabilityCannotBeRemovedFromTheDescriptor()
    {
        var descriptor = Descriptor(exposePath: false);
        descriptor["x-powerscript"]!["capabilities"] = new JsonArray();
        var manifest = TestTool.Parse(descriptor, "action.json");

        Assert.IsTrue(CliActionProvider.Validate(manifest, Action(("path", Constant("C:\\file.txt")))).Count > 0);
    }

    [TestMethod]
    public void ProviderAndArbitraryProcessRecipeAreMutuallyExclusive()
    {
        var manifest = FileManifest();
        manifest.Execute!.Command = new() { "cmd.exe", "/c", "echo other" };

        Assert.IsTrue(ManifestValidator.Validate(manifest, string.Empty).Count > 0);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ProviderDescriptorsRejectDuplicateRecipeOrProviderMembers(bool duplicateProvider)
    {
        var root = Path.Combine(Path.GetTempPath(), "powerscripts-provider-duplicates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var descriptor = Descriptor(exposePath: false).ToJsonString();
            var recipe = "\"x-execute\":{\"provider\":\"powertoys-cli\"}";
            var duplicate = duplicateProvider
                ? "\"x-execute\":{\"provider\":\"powertoys-cli\",\"provider\":\"powertoys-cli\"}"
                : recipe + "," + recipe;
            var ambiguous = descriptor.Replace(recipe, duplicate, StringComparison.Ordinal);
            Assert.AreNotEqual(descriptor, ambiguous);
            var file = Path.Combine(root, "action.json.tool.json");
            File.WriteAllText(file, ambiguous);

            Assert.ThrowsException<InvalidDataException>(() => ToolDescriptorParser.TryParseFile(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UnknownProviderRecipeMembersAreRetainedAndRejected()
    {
        var descriptor = Descriptor(exposePath: false);
        descriptor["x-execute"]!["executableOverride"] = "other.exe";
        var manifest = TestTool.Parse(descriptor, "action.json");

        Assert.IsNotNull(manifest.Execute!.AdditionalProperties);
        Assert.IsTrue(manifest.Execute.AdditionalProperties.ContainsKey("executableOverride"));
        Assert.IsTrue(ManifestValidator.Validate(manifest, string.Empty).Count > 0);
    }

    [TestMethod]
    public void NonProviderExplicitRecipeKeepsItsExistingRuntimeClassification()
    {
        var descriptor = TestTool.Descriptor();
        descriptor["x-execute"] = new JsonObject
        {
            ["command"] = new JsonArray("pwsh", "entry.custom"),
        };
        var manifest = TestTool.Parse(descriptor, "entry.custom");

        Assert.AreEqual(ScriptRuntime.PowerShell, manifest.Runtime);
        Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
    }

    [TestMethod]
    public void JsonEntryIsDiscoveredAndItsBodyAndDescriptorBothAffectTrust()
    {
        var root = Path.Combine(Path.GetTempPath(), "powerscripts-action-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var entry = Path.Combine(root, "action.json");
            var action = Action(("path", Constant("C:\\not-required-to-exist.txt")), ("json", Constant(true)));
            File.WriteAllText(entry, JsonSerializer.Serialize(action));
            var descriptor = Descriptor(exposePath: false);
            File.WriteAllText(entry + ".tool.json", descriptor.ToJsonString());

            var registry = new ScriptRegistry(root);
            registry.Load();
            Assert.AreEqual(0, registry.Errors.Count, string.Join("; ", registry.Errors.Select(error => error.Message)));
            var manifest = registry.Scripts.Single();
            Assert.AreEqual(ScriptRuntime.PowerToysOperation, manifest.Runtime);
            var initial = ScriptIntegrity.ComputeHash(manifest);
            Assert.IsFalse(string.IsNullOrEmpty(initial));

            File.WriteAllText(entry, JsonSerializer.Serialize(Action(("path", Constant("C:\\not-required-to-exist.txt")), ("json", Constant(false)))));
            var changedAction = ScriptIntegrity.ComputeHash(manifest);
            Assert.AreNotEqual(initial, changedAction);

            descriptor["title"] = "Reviewed name";
            File.WriteAllText(entry + ".tool.json", descriptor.ToJsonString());
            Assert.AreNotEqual(changedAction, ScriptIntegrity.ComputeHash(manifest));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PowerScriptManifest FileManifest(bool exposePath = true) =>
        TestTool.Parse(Descriptor(exposePath), "action.json");

    private static JsonObject Descriptor(bool exposePath)
    {
        var descriptor = TestTool.Descriptor(name: "file-usage-action");
        descriptor["x-execute"] = new JsonObject { ["provider"] = CliActionProvider.ProviderName };
        descriptor["x-powerscript"] = new JsonObject { ["capabilities"] = new JsonArray("fileRead") };
        if (exposePath)
        {
            descriptor["inputSchema"]!["properties"]!["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string", ["format"] = "file-path" },
                },
                ["required"] = new JsonArray("path"),
            };
        }

        return descriptor;
    }

    private static CliAction Action(params (string Name, CliActionBinding Binding)[] bindings) => new()
    {
        SchemaVersion = 1,
        CommandId = "filelocksmith.query",
        Bindings = bindings.ToDictionary(binding => binding.Name, binding => binding.Binding),
    };

    private static CliActionBinding Constant(object? value) => new()
    {
        Constant = JsonSerializer.SerializeToElement(value),
    };

    private static CliActionBinding Reference(string name) => new() { Parameter = name };
}
