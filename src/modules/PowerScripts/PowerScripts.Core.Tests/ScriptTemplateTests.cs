// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            var settings = new ScriptSettingsStore(Path.Combine(root, "settings"), new FileSettingsStore(Path.Combine(root, "module")));
            foreach (var template in ScriptTemplates.All)
            {
                var first = ScriptTemplates.Create(template.Id, root);
                var second = ScriptTemplates.Create(template.Id, root);
                Assert.AreNotEqual(first.EntryPath, second.EntryPath);
                Assert.IsTrue(File.ReadAllText(first.GuidePath).Contains("MXC", StringComparison.Ordinal));
                var snapshot = settings.Get(root, first.EntryPath);
                Assert.AreEqual(0, snapshot.ValidationErrors.Count);
                Assert.IsFalse(snapshot.HasLocalSettings);
                Assert.IsFalse(snapshot.HasLocalMxcSettings);
                Assert.IsTrue(snapshot.Manifest.HasDescriptor);
                Assert.IsTrue(File.ReadAllText(first.EntryPath).Contains("OutputEncoding", StringComparison.Ordinal));
            }

            var registry = new ScriptRegistry(root, settings);
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
            CollectionAssert.AreEqual(new[] { ".txt", ".md", ".png" }, descriptor["x-powerscript"]!["extensions"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray());
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
}
