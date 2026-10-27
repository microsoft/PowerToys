// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Storage;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ScriptTemplateTests
{
    [TestMethod]
    public void TemplatesCreateValidIndependentPackagesWithoutLocalPolicyOrApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-templates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var template in ScriptTemplates.All)
            {
                var first = ScriptTemplates.Create(template.Id, root);
                var second = ScriptTemplates.Create(template.Id, root);
                Assert.AreNotEqual(first.EntryPath, second.EntryPath);
                Assert.IsTrue(File.ReadAllText(first.GuidePath).Contains("MXC", StringComparison.Ordinal));
                var manifest = ToolDescriptorParser.TryParseFile(first.EntryPath + ".tool.json")!;
                Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
                Assert.IsTrue(File.ReadAllText(first.EntryPath).Contains("OutputEncoding", StringComparison.Ordinal));
            }

            var registry = new ScriptRegistry(root);
            registry.Load();
            Assert.AreEqual(ScriptTemplates.All.Count * 2, registry.Scripts.Count);
            Assert.AreEqual(0, registry.Errors.Count);
            Assert.AreEqual(registry.Scripts.Count, registry.Scripts.Select(script => script.Id).Distinct().Count());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow("../greeting")]
    [DataRow("missing")]
    public void UnknownTemplateDoesNotCreateAnyFiles(string id)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-rejected-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(id, root));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void CustomizationWritesDescriptorAndNormalizesFileFiltersWithoutChangingScriptCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-custom-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new ScriptTemplateOptions("  My File Inspector  ", "Literal \"quoted\" text\nand a second line.", "TXT; *.Md, .txt png");
            var first = ScriptTemplates.Create("file-info", root, options);
            var second = ScriptTemplates.Create("file-info", root, options);
            var descriptor = JsonNode.Parse(File.ReadAllText(first.EntryPath + ".tool.json"))!;
            Assert.AreEqual("My File Inspector", descriptor["title"]!.GetValue<string>());
            Assert.AreEqual(options.Description, descriptor["description"]!.GetValue<string>());
            CollectionAssert.AreEqual(new[] { ".txt", ".md", ".png" }, descriptor["inputSchema"]!["properties"]!["files"]!["items"]!["x-powerscript"]!["extensions"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());
            StringAssert.StartsWith(Path.GetFileName(Path.GetDirectoryName(first.EntryPath))!, "my-file-inspector-");
            Assert.AreNotEqual(first.EntryPath, second.EntryPath);
            Assert.AreEqual(File.ReadAllText(first.EntryPath), File.ReadAllText(second.EntryPath));
            Assert.IsFalse(File.ReadAllText(first.EntryPath).Contains("My File Inspector", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [DataTestMethod]
    [DataRow("greeting", "", null)]
    [DataRow("greeting", "   ", null)]
    [DataRow("greeting", "Name\nwith controls", null)]
    [DataRow("greeting", "My script", ".txt")]
    [DataRow("file-info", "My script", "../txt")]
    [DataRow("file-info", "My script", "*.tar.gz")]
    [DataRow("file-info", "My script", "*.txt, *")]
    [DataRow("file-info", "My script", ";;;")]
    public void InvalidCustomizationCreatesNoFiles(string id, string name, string? extensions)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-invalid-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(id, root, new(name, FileExtensions: extensions)));
        Assert.IsFalse(Directory.Exists(root));
    }

    [DataTestMethod]
    [DataRow("powershell", "none", "none")]
    [DataRow("powershell", "none", "text")]
    [DataRow("powershell", "none", "files")]
    [DataRow("powershell", "text", "none")]
    [DataRow("powershell", "text", "text")]
    [DataRow("powershell", "text", "files")]
    [DataRow("powershell", "files", "none")]
    [DataRow("powershell", "files", "text")]
    [DataRow("powershell", "files", "files")]
    [DataRow("python", "none", "none")]
    [DataRow("python", "none", "text")]
    [DataRow("python", "none", "files")]
    [DataRow("python", "text", "none")]
    [DataRow("python", "text", "text")]
    [DataRow("python", "text", "files")]
    [DataRow("python", "files", "none")]
    [DataRow("python", "files", "text")]
    [DataRow("python", "files", "files")]
    public void ConfiguredCreationMatchesSelectedContractWithoutPolicyChanges(string runtime, string input, string output)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-configured-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new ScriptTemplateOptions("  My script  ", "Quoted \"description\"",
                input == "files" ? "TXT; *.md" : null, runtime, input, output,
                MinFiles: input == "files" ? 2 : 1, MaxFiles: input == "files" ? 5 : 0);
            var created = ScriptTemplates.Create(root, options);
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
            Assert.AreEqual("My script", manifest.Name);
            Assert.AreEqual(options.Description, manifest.Description);
            Assert.AreEqual(runtime == "python" ? ScriptRuntime.Python : ScriptRuntime.PowerShell, manifest.Runtime);
            Assert.AreEqual(input, ScriptIo.ToToken(manifest.InputFormat));
            Assert.AreEqual(output, ScriptIo.ToToken(manifest.OutputFormat));
            Assert.AreEqual(runtime == "python" ? ".py" : ".ps1", Path.GetExtension(created.EntryPath));
            Assert.IsFalse(File.ReadAllText(created.EntryPath).Contains("def main(", StringComparison.Ordinal));
            if (input == "files")
            {
                CollectionAssert.AreEqual(new[] { ".txt", ".md" }, manifest.Input!.Extensions);
                Assert.AreEqual(2, manifest.Input.MinFiles);
                Assert.AreEqual(5, manifest.Input.MaxFiles);
                CollectionAssert.Contains(manifest.Capabilities, "fileRead");
            }
            else
            {
                Assert.IsNull(manifest.Input);
                CollectionAssert.DoesNotContain(manifest.Capabilities, "fileRead");
            }

            Assert.AreEqual(output == "files", manifest.Capabilities.Contains("fileWrite"));
            StringAssert.Contains(File.ReadAllText(created.GuidePath), $"{runtime}, {input} -> {output}");
            var registry = new ScriptRegistry(root);
            registry.Load();
            Assert.AreEqual(1, registry.Scripts.Count);
            Assert.AreEqual(0, registry.Errors.Count);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [DataTestMethod]
    [DataRow("missing", "none", "text", null, 1, 0)]
    [DataRow("powershell", "html", "text", null, 1, 0)]
    [DataRow("python", "none", "video", null, 1, 0)]
    [DataRow("powershell", "none", "text", ".txt", 1, 0)]
    [DataRow("python", "text", "text", null, 2, 0)]
    [DataRow("powershell", "files", "text", "../txt", 1, 0)]
    [DataRow("python", "files", "text", "*;.txt", 1, 0)]
    [DataRow("powershell", "files", "text", null, 0, 0)]
    [DataRow("python", "files", "text", null, 2, 1)]
    [DataRow("powershell", "files", "text", null, 1, -1)]
    public void InvalidConfiguredContractCreatesNothing(string runtime, string input, string output, string? extensions, int minimum, int maximum)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-configured-invalid-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(root,
            new ScriptTemplateOptions("My script", FileExtensions: extensions, Runtime: runtime, Input: input, Output: output, MinFiles: minimum, MaxFiles: maximum)));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void ConfiguredDisplayTextDoesNotChangeExecutableCodeOrOverwriteExistingPackages()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-configured-text-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = ScriptTemplates.Create(root, new ScriptTemplateOptions("Script ' $(throw 'unexpected')", "Literal quotes and \"text\"."));
            var second = ScriptTemplates.Create(root, new ScriptTemplateOptions("Different name", "Different description"));
            Assert.AreNotEqual(first.EntryPath, second.EntryPath);
            Assert.AreEqual(File.ReadAllText(first.EntryPath), File.ReadAllText(second.EntryPath));
            Assert.AreNotEqual(
                JsonNode.Parse(File.ReadAllText(first.EntryPath + ".tool.json"))!["name"]!.GetValue<string>(),
                JsonNode.Parse(File.ReadAllText(second.EntryPath + ".tool.json"))!["name"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
