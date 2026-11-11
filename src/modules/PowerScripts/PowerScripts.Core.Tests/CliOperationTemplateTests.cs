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
using PowerScripts.Core.Storage;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CliOperationTemplateTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize() => _root = Path.Combine(Path.GetTempPath(), "ps-operation-template-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void OperationTemplateMetadataRetainsTypesAndLeavesExistingTemplatesAvailable()
    {
        Assert.AreEqual(3, ScriptTemplates.OperationTemplates.Count);
        Assert.AreEqual(ScriptTemplates.All.Count + 3, ScriptTemplates.Available.Count);
        var metadata = ScriptTemplates.OperationTemplates.Single(item => item.CommandId == "filelocksmith.query");
        Assert.AreEqual("powertoys:filelocksmith.query", metadata.Id);
        Assert.AreEqual("filelocksmith", metadata.ModuleId);
        Assert.AreEqual("File Locksmith", metadata.Group);
        var path = metadata.Parameters!.Single(parameter => parameter.Name == "path");
        Assert.IsTrue(path.Required);
        Assert.AreEqual("string", path.Schema.Type);
        Assert.AreEqual("file-path", path.Schema.Format);
        var json = metadata.Parameters.Single(parameter => parameter.Name == "json");
        Assert.AreEqual("boolean", json.Schema.Type);
        Assert.AreEqual("Output format", json.Schema.Title);
        Assert.IsTrue(json.Schema.Default.GetBoolean());
        var wire = JsonSerializer.SerializeToNode(metadata, ToolDescriptorParser.JsonOptions)!;
        Assert.AreEqual("filelocksmith", wire["moduleId"]!.GetValue<string>());
        var format = wire["parameters"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == "json")!["schema"]!;
        Assert.AreEqual("boolean", format["type"]!.GetValue<string>());
        Assert.IsTrue(format["default"]!.GetValue<bool>());
        Assert.AreEqual("choice", format["x-ui"]!["control"]!.GetValue<string>());
        Assert.AreEqual("JSON", format["x-ui"]!["trueLabel"]!.GetValue<string>());
        Assert.AreEqual("Text", format["x-ui"]!["falseLabel"]!.GetValue<string>());
        var request = new JsonObject
        {
            ["projectName"] = "Presentation roundtrip",
            ["parameters"] = new JsonArray(new JsonObject
            {
                ["name"] = "format",
                ["schema"] = format.DeepClone(),
            }),
        };
        Assert.AreEqual("Text", TemplateConfiguration.Parse(request.ToJsonString()).Parameters!.Single().Schema.Ui!.FalseLabel);
        Assert.IsTrue(ScriptTemplates.OperationTemplates.Where(template => template.ModuleId == "fancyzones")
            .All(template => template.Group == "FancyZones"));
        Assert.IsTrue(ScriptTemplates.All.All(template => template.CommandId is null));
    }

    [TestMethod]
    public void RuntimeBooleanPresentationRemainsAnAnnotationOnTheTypedParameter()
    {
        var created = ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Choose an output format",
            Operation: new PowerToysOperationTemplateOptions("filelocksmith.query", new Dictionary<string, CliActionBinding>
            {
                ["path"] = new() { Constant = JsonSerializer.SerializeToElement("C:\\literal.txt") },
                ["json"] = new() { Parameter = "format" },
            })));
        var action = CliActionProvider.Parse(File.ReadAllText(created.EntryPath));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        var parameter = manifest.Parameters.Single();
        Assert.AreEqual("format", parameter.Name);
        Assert.IsTrue(parameter.IsBool);
        Assert.AreEqual("true", parameter.Default);
        Assert.AreEqual("format", action.Bindings["json"].Parameter);
        Assert.AreEqual(0, CliActionProvider.Validate(manifest, action).Count);
        var descriptor = JsonNode.Parse(File.ReadAllText(created.EntryPath + ".tool.json"))!;
        var schema = descriptor["inputSchema"]!["properties"]!["parameters"]!["properties"]!["format"]!;
        Assert.AreEqual("boolean", schema["type"]!.GetValue<string>());
        Assert.AreEqual("JSON", schema["x-ui"]!["trueLabel"]!.GetValue<string>());
        Assert.AreEqual("Text", schema["x-ui"]!["falseLabel"]!.GetValue<string>());
    }

    [TestMethod]
    public void OperationCreationPublishesAnIndependentDataPackageWithoutSettingsWritesOrApproval()
    {
        var previous = SettingsStore.Current;
        var settings = new RecordingSettingsStore();
        try
        {
            SettingsStore.Current = settings;
            var first = ScriptTemplates.Create("powertoys:filelocksmith.query", _root);
            var second = ScriptTemplates.Create("powertoys:filelocksmith.query", _root);
            Assert.AreNotEqual(first.EntryPath, second.EntryPath);
            Assert.AreEqual("action.json", Path.GetFileName(first.EntryPath));
            CollectionAssert.AreEquivalent(
                new[] { "action.json", "action.json.tool.json", "README.md" },
                Directory.GetFiles(Path.GetDirectoryName(first.EntryPath)!).Select(Path.GetFileName).ToArray());
            StringAssert.Contains(File.ReadAllText(first.GuidePath), "MXC");
            Assert.AreEqual(0, settings.Writes.Count);

            var registry = new ScriptRegistry(_root);
            registry.Load();
            Assert.AreEqual(0, registry.Errors.Count);
            Assert.AreEqual(2, registry.Scripts.Count);
            var manifest = registry.Scripts.Single(item => item.EntryFullPath == first.EntryPath);
            Assert.AreEqual(ScriptRuntime.PowerToysOperation, manifest.Runtime);
            Assert.AreEqual(CliActionProvider.ProviderName, manifest.Execute!.Provider);
            Assert.IsNull(manifest.Execute.Command);
            CollectionAssert.Contains(manifest.Capabilities, "fileRead");
            Assert.AreEqual(1, manifest.Parameters.Count);
            Assert.AreEqual("path", manifest.Parameters[0].Name);
            Assert.IsTrue(manifest.Parameters[0].IsFile);
            Assert.IsTrue(manifest.Parameters[0].IsRequired);
            Assert.IsFalse(new TrustStore(settings).IsTrusted(manifest.Id, ScriptIntegrity.ComputeHash(manifest)));
        }
        finally
        {
            SettingsStore.Current = previous;
        }
    }

    [TestMethod]
    public void DefaultBindingsExposeTheRequiredFileAndKeepTheNativeBooleanDefaultFixed()
    {
        var created = ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Inspect file",
            Operation: new PowerToysOperationTemplateOptions("filelocksmith.query")));
        var action = CliActionProvider.Parse(File.ReadAllText(created.EntryPath));
        Assert.AreEqual("path", action.Bindings["path"].Parameter);
        Assert.AreEqual(JsonValueKind.Undefined, action.Bindings["path"].Constant.ValueKind);
        Assert.IsTrue(action.Bindings["json"].Constant.GetBoolean());
        Assert.IsNull(action.Bindings["json"].Parameter);
        using var entry = JsonDocument.Parse(File.ReadAllText(created.EntryPath));
        Assert.IsFalse(entry.RootElement.GetProperty("bindings").GetProperty("path").TryGetProperty("constant", out _));
        Assert.IsFalse(entry.RootElement.GetProperty("bindings").GetProperty("json").TryGetProperty("parameter", out _));
    }

    [TestMethod]
    public void FixedFalseAndLiteralFileNamesRemainTypedDataAndDoNotCreateRuntimeQuestions()
    {
        const string file = "C:\\資料\\report \"one\".txt";
        var created = ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Fixed file",
            Parameters: Array.Empty<ScriptTemplateParameter>(),
            Operation: new PowerToysOperationTemplateOptions("filelocksmith.query", new Dictionary<string, CliActionBinding>
            {
                ["path"] = new() { Constant = JsonSerializer.SerializeToElement(file) },
                ["json"] = new() { Constant = JsonSerializer.SerializeToElement(false) },
            })));
        var action = CliActionProvider.Parse(File.ReadAllText(created.EntryPath));
        Assert.AreEqual(file, action.Bindings["path"].Constant.GetString());
        Assert.AreEqual(JsonValueKind.False, action.Bindings["json"].Constant.ValueKind);
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        Assert.AreEqual(0, manifest.Parameters.Count);
        Assert.AreEqual(0, CliActionProvider.Validate(manifest, action).Count);
    }

    [TestMethod]
    public void RuntimeParametersRetainTheirOwnNameDefaultAndNarrowFileContract()
    {
        const string defaultFile = "C:\\Documents\\default.pdf";
        var created = ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Ask for a document",
            Parameters: new[]
            {
                new ScriptTemplateParameter("document", new JsonSchemaProperty
                {
                    Type = "string",
                    Format = JsonSchemaProperty.FilePathFormat,
                    Title = "Document",
                    Default = JsonSerializer.SerializeToElement(defaultFile),
                }, Required: true, FileExtensions: ".pdf"),
            },
            Operation: new PowerToysOperationTemplateOptions("filelocksmith.query", new Dictionary<string, CliActionBinding>
            {
                ["path"] = new() { Parameter = "document" },
                ["json"] = new() { Constant = JsonSerializer.SerializeToElement(true) },
            })));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        Assert.AreEqual("document", manifest.Parameters.Single().Name);
        Assert.AreEqual(defaultFile, manifest.Parameters.Single().Default);
        var descriptor = JsonNode.Parse(File.ReadAllText(created.EntryPath + ".tool.json"))!;
        Assert.AreEqual(".pdf", descriptor["inputSchema"]!["properties"]!["parameters"]!["properties"]!["document"]!["x-powerscript"]!["extensions"]![0]!.GetValue<string>());
    }

    [TestMethod]
    public void ChangingFixedValuesChangesTheExistingScriptTrustFingerprint()
    {
        var created = ScriptTemplates.Create("powertoys:filelocksmith.query", _root);
        var registry = new ScriptRegistry(_root);
        registry.Load();
        var manifest = registry.Scripts.Single();
        var initial = ScriptIntegrity.ComputeHash(manifest);
        var action = JsonNode.Parse(File.ReadAllText(created.EntryPath))!;
        action["bindings"]!["json"]!["constant"] = false;
        File.WriteAllText(created.EntryPath, action.ToJsonString());
        Assert.AreNotEqual(initial, ScriptIntegrity.ComputeHash(manifest));
    }

    [TestMethod]
    public void InvalidBindingsAreRejectedBeforeAnyPackageIsPublished()
    {
        Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Missing required file",
            Parameters: Array.Empty<ScriptTemplateParameter>(),
            Operation: new PowerToysOperationTemplateOptions("filelocksmith.query", new Dictionary<string, CliActionBinding>()))));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [DataTestMethod]
    [DataRow("files", "text")]
    [DataRow("none", "files")]
    public void OperationsCannotClaimUnsupportedPrimaryInputOrOutput(string input, string output)
    {
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new ScriptTemplateOptions(
            "Invalid contract", Input: input, Output: output,
            Operation: new PowerToysOperationTemplateOptions("fancyzones.get-layouts"))));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void EducationalAndOperationTemplateConfigurationsCannotBeMixed()
    {
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create("greeting", _root, new ScriptTemplateOptions(
            "Mixed", Operation: new PowerToysOperationTemplateOptions("filelocksmith.query"))));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create("powertoys:filelocksmith.query", _root, new ScriptTemplateOptions(
            "Mismatched", Operation: new PowerToysOperationTemplateOptions("fancyzones.get-layouts"))));
        Assert.IsFalse(Directory.Exists(_root));
    }

    private sealed class RecordingSettingsStore : ISettingsStore
    {
        public Dictionary<string, string> Writes { get; } = new(StringComparer.Ordinal);

        public string? ReadBlob(string key) => Writes.GetValueOrDefault(key);

        public void WriteBlob(string key, string contents) => Writes[key] = contents;
    }
}
