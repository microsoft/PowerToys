// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Security;
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
    [DynamicData(nameof(ConfiguredCombinations), DynamicDataSourceType.Method)]
    public void ConfiguredCreationMatchesSelectedContractWithoutPolicyChanges(string runtime, string input, string output, bool withParameters)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-configured-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new ScriptTemplateOptions("  My script  ", "Quoted \"description\"",
                input == "files" ? "TXT; *.md" : null, runtime, input, output,
                MinFiles: input == "files" ? 2 : 1, MaxFiles: input == "files" ? 5 : 0,
                Parameters: withParameters ? ExampleParameters() : null);
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

    public static IEnumerable<object[]> ConfiguredCombinations()
    {
        var shapes = new[] { "none", "text", "files", "image", "audio" };
        foreach (var runtime in new[] { "powershell", "python" })
        {
            foreach (var input in shapes)
            {
                foreach (var output in shapes)
                {
                    foreach (var withParameters in new[] { false, true })
                    {
                        yield return new object[] { runtime, input, output, withParameters };
                    }
                }
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(ConfiguredCombinations), DynamicDataSourceType.Method)]
    public void GeneratedExamplesExecuteWithUsefulResults(string runtime, string input, string output, bool withParameters)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-execution-" + Guid.NewGuid().ToString("N"));
        List<string>? outputPaths = null;
        try
        {
            var created = ScriptTemplates.Create(root, new("Example", Runtime: runtime, Input: input, Output: output,
                Parameters: withParameters ? ExampleParameters() : null));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            manifest.FolderPath = Path.GetDirectoryName(created.EntryPath)!;
            var selected = Path.Combine(root, "\u6587\u4ef6 one.txt");
            File.WriteAllText(selected, "hello", new UTF8Encoding(false));
            const string text = "Hello \u4e16\u754c\nsecond line";
            var media = input == "image"
                ? JsonSerializer.SerializeToElement(new { mimeType = "image/png", data = Convert.ToBase64String(ExampleImage()) })
                : JsonSerializer.SerializeToElement(new { mimeType = "audio/wav", data = Convert.ToBase64String(ExampleAudio()) });
            var request = new PythonTransformInput
            {
                Text = input == "text" ? text : null,
                FilePaths = input == "files" ? new() { selected } : null,
                Image = input == "image" ? media : null,
                Audio = input == "audio" ? media : null,
            };
            var result = ExecuteExample(manifest, request);
            outputPaths = result.FilePaths;
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
            if (withParameters)
            {
                var summary = result.StdErr.Split('\n').First(line => line.StartsWith("Parameters: ", StringComparison.Ordinal));
                using var answers = JsonDocument.Parse(summary["Parameters: ".Length..]);
                Assert.AreEqual(0, answers.RootElement.GetProperty("count").GetInt32());
                Assert.IsFalse(answers.RootElement.GetProperty("enabled").GetBoolean());
                Assert.AreEqual("short", answers.RootElement.GetProperty("mode").GetString());
                Assert.AreEqual(string.Empty, answers.RootElement.GetProperty("empty").GetString());
                Assert.AreEqual("$(throw 'must remain data') \u4e16\u754c", answers.RootElement.GetProperty("label").GetString());
                Assert.IsFalse(answers.RootElement.TryGetProperty("omitted", out _));
            }

            if (output == "none")
            {
                Assert.IsNull(result.Text);
                Assert.IsNull(result.FilePaths);
                Assert.IsNull(result.Image);
                Assert.IsNull(result.Audio);
                StringAssert.Contains(result.StdErr, "Completed input inspection.");
            }
            else if (output == "text" || output == "files")
            {
                string content;
                if (output == "files")
                {
                    Assert.AreEqual(1, result.FilePaths!.Count);
                    var path = result.FilePaths[0];
                    Assert.IsTrue(Path.IsPathFullyQualified(path));
                    Assert.IsFalse(path.StartsWith(manifest.FolderPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                    if (input is "image" or "audio")
                    {
                        Assert.AreEqual(input == "image" ? ".png" : ".wav", Path.GetExtension(path));
                        CollectionAssert.AreEqual(Convert.FromBase64String(media.GetProperty("data").GetString()!), File.ReadAllBytes(path));
                        return;
                    }

                    var bytes = File.ReadAllBytes(path);
                    Assert.IsFalse(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
                    content = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    content = result.Text!.Replace("\r\n", "\n", StringComparison.Ordinal);
                }

                if (input == "text")
                {
                    StringAssert.StartsWith(content, output == "text" ? text.ToUpperInvariant() : text);
                    if (!withParameters)
                    {
                        Assert.AreEqual(output == "text" ? text.ToUpperInvariant() : text, content.TrimEnd('\r', '\n'));
                    }
                }
                else if (input == "files")
                {
                    StringAssert.Contains(content, "\u6587\u4ef6 one.txt (5 bytes)");
                }
                else if (input is "image" or "audio")
                {
                    var bytes = Convert.FromBase64String(media.GetProperty("data").GetString()!);
                    StringAssert.Contains(content, media.GetProperty("mimeType").GetString()!);
                    StringAssert.Contains(content, $"({bytes.Length} bytes)");
                    StringAssert.Contains(content, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
                }
                else
                {
                    StringAssert.StartsWith(content, "Hello from your new PowerScript.");
                }

                Assert.AreEqual(withParameters, content.Contains("Parameters: ", StringComparison.Ordinal));
            }
            else
            {
                var returned = output == "image" ? result.Image!.Value : result.Audio!.Value;
                var bytes = Convert.FromBase64String(returned.GetProperty("data").GetString()!);
                if (input == output)
                {
                    Assert.AreEqual(media.GetProperty("mimeType").GetString(), returned.GetProperty("mimeType").GetString());
                    CollectionAssert.AreEqual(Convert.FromBase64String(media.GetProperty("data").GetString()!), bytes);
                }
                else if (output == "image")
                {
                    Assert.AreEqual("image/png", returned.GetProperty("mimeType").GetString());
                    AssertFingerprintPng(bytes);
                }
                else
                {
                    Assert.AreEqual("audio/wav", returned.GetProperty("mimeType").GetString());
                    AssertToneWav(bytes);
                }
            }
        }
        finally
        {
            foreach (var path in outputPaths ?? new())
            {
                File.Delete(path);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [DataTestMethod]
    [DataRow("powershell", "image")]
    [DataRow("powershell", "audio")]
    [DataRow("python", "image")]
    [DataRow("python", "audio")]
    public void GeneratedMediaDependsOnInputAndTypedAnswers(string runtime, string output)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-influence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ScriptTemplates.Create(root, new("Example", Runtime: runtime, Input: "text", Output: output,
                Parameters: new[] { new ScriptTemplateParameter("label", new() { Type = "string" }) }));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            manifest.FolderPath = Path.GetDirectoryName(created.EntryPath)!;
            string Run(string text, string label)
            {
                var result = ExecuteExample(manifest, new() { Text = text, Params = new() { ["label"] = label } });
                Assert.AreEqual(0, result.ExitCode, result.StdErr);
                return (output == "image" ? result.Image!.Value : result.Audio!.Value).GetProperty("data").GetString()!;
            }

            var first = Run("alpha", "first");
            Assert.AreEqual(first, Run("alpha", "first"));
            Assert.AreNotEqual(first, Run("beta", "first"));
            Assert.AreNotEqual(first, Run("alpha", "second"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ScriptTemplateParameter[] ExampleParameters() => new[]
    {
        new ScriptTemplateParameter("count", new() { Type = "integer", Default = JsonSerializer.SerializeToElement(0) }, Required: true),
        new ScriptTemplateParameter("enabled", new() { Type = "boolean", Default = JsonSerializer.SerializeToElement(false) }),
        new ScriptTemplateParameter("label", new() { Type = "string", Default = JsonSerializer.SerializeToElement("$(throw 'must remain data') \u4e16\u754c") }),
        new ScriptTemplateParameter("mode", new() { Type = "string", Enum = new() { "short", "full" }, Default = JsonSerializer.SerializeToElement("short") }),
        new ScriptTemplateParameter("empty", new() { Type = "string", Default = JsonSerializer.SerializeToElement(string.Empty) }),
        new ScriptTemplateParameter("omitted", new() { Type = "string" }),
    };

    private static PythonTransformResult ExecuteExample(PowerScriptManifest manifest, PythonTransformInput input)
    {
        var isolation = new MxcSettings { Enabled = false, RiskAccepted = true };
        if (manifest.Runtime == ScriptRuntime.Python)
        {
            var settings = new PythonSettings { Mode = PythonRuntimeMode.Windows };
            settings.Windows.InterpreterPath = Environment.GetEnvironmentVariable("POWERSCRIPTS_TEST_PYTHON") ?? string.Empty;
            var result = new PythonRuntime(settings, isolation).Run(manifest, input);
            if (result.ExitCode == 2 && result.StdErr.StartsWith("No Python interpreter found.", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Python is optional. Install Python on PATH or set POWERSCRIPTS_TEST_PYTHON to run this execution case.");
            }

            return result;
        }

        var execution = new ScriptExecutor(isolation).Execute(manifest, files: input.FilePaths, text: input.Text,
            parameters: input.Params?.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
            image: input.Image, audio: input.Audio);
        Assert.AreEqual(0, execution.ExitCode, execution.StdErr);
        var parsed = manifest.OutputFormat switch
        {
            PowerScriptDataFormat.None => new PythonTransformResult(),
            PowerScriptDataFormat.Text => new PythonTransformResult { Text = execution.StdOut },
            _ => PythonTransformResult.Parse(execution.StdOut, manifest.OutputFormat, manifest.OutputSchema),
        };
        if (manifest.OutputFormat == PowerScriptDataFormat.None)
        {
            Assert.AreEqual(string.Empty, execution.StdOut);
        }

        parsed.ExitCode = execution.ExitCode;
        parsed.StdErr = execution.StdErr;
        return parsed;
    }

    private static byte[] ExampleImage() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");

    private static byte[] ExampleAudio()
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(44);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(8);
        writer.Write(new byte[] { 0, 0, 1, 0, 255, 255, 0, 0 });
        return buffer.ToArray();
    }

    private static void AssertFingerprintPng(byte[] bytes)
    {
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.AreEqual(64, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)));
        Assert.AreEqual(64, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
        Assert.AreEqual(8, bytes[24]);
        Assert.IsTrue(bytes[25] is 2 or 6);
        using var compressed = new MemoryStream();
        for (var offset = 8; offset < bytes.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset));
            if (Encoding.ASCII.GetString(bytes, offset + 4, 4) == "IDAT")
            {
                compressed.Write(bytes, offset + 8, length);
            }

            offset += 12 + length;
        }

        compressed.Position = 0;
        using var decompressor = new ZLibStream(compressed, CompressionMode.Decompress);
        using var pixels = new MemoryStream();
        decompressor.CopyTo(pixels);
        Assert.AreEqual(64 * (1 + 64 * (bytes[25] == 6 ? 4 : 3)), pixels.Length);
    }

    private static void AssertToneWav(byte[] bytes)
    {
        Assert.AreEqual(8044, bytes.Length);
        Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.AreEqual(8036, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.AreEqual("WAVEfmt ", Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.AreEqual(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(20)));
        Assert.AreEqual(1, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22)));
        Assert.AreEqual(16000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)));
        Assert.AreEqual(16, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(34)));
        Assert.AreEqual("data", Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.AreEqual(8000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)));
        var samples = Enumerable.Range(0, 4000).Select(i => (int)BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44 + 2 * i))).ToArray();
        Assert.IsTrue(samples.Any(sample => sample != 0), "Generated audio must not be a silent placeholder.");
        Assert.IsTrue(samples.All(sample => Math.Abs(sample) <= 3276), "Keep demonstration audio at a low amplitude.");
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

    [TestMethod]
    [DataRow("powershell")]
    [DataRow("python")]
    public void ConfiguredParametersPreserveTypesChoicesDefaultsAndRequiredFlags(string runtime)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-parameters-" + Guid.NewGuid().ToString("N"));
        try
        {
            var parameters = new[]
            {
                new ScriptTemplateParameter("count", new() { Type = "integer", Minimum = 0, Maximum = 4, Default = JsonSerializer.SerializeToElement(0) }, Required: true),
                new ScriptTemplateParameter("enabled", new() { Type = "boolean", Default = JsonSerializer.SerializeToElement(false) }),
                new ScriptTemplateParameter("mode", new() { Type = "string", Title = "Mode", Description = "Output style", Enum = new() { "short", "full" }, Default = JsonSerializer.SerializeToElement("short") }),
                new ScriptTemplateParameter("label", new() { Type = "string", Default = JsonSerializer.SerializeToElement(string.Empty) }),
                new ScriptTemplateParameter("source", new() { Type = "string", Format = "file-path" }, FileExtensions: "TXT;*.json"),
            };
            var created = ScriptTemplates.Create(root, new("Typed options", Runtime: runtime, Parameters: parameters));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            var input = PythonTransformInput.Parse(manifest, "{}").CreateRequest(manifest);
            Assert.AreEqual(0, input["parameters"]!["count"]!.GetValue<int>());
            Assert.IsFalse(input["parameters"]!["enabled"]!.GetValue<bool>());
            Assert.AreEqual(string.Empty, input["parameters"]!["label"]!.GetValue<string>());
            Assert.AreEqual("short", input["parameters"]!["mode"]!.GetValue<string>());
            Assert.AreEqual(5, manifest.Parameters.Count);
            Assert.IsTrue(manifest.Parameters.Single(p => p.Name == "count").IsRequired);
            Assert.AreEqual("Mode", manifest.Parameters.Single(p => p.Name == "mode").Label);
            CollectionAssert.Contains(manifest.Capabilities, "fileRead");
            var extensions = manifest.InputSchema.Properties!["parameters"].Properties!["source"].PowerScript!.Extensions;
            CollectionAssert.AreEqual(new[] { ".txt", ".json" }, extensions!);
            if (runtime == "powershell")
            {
                var code = File.ReadAllText(created.EntryPath);
                StringAssert.Contains(code, "[int]$count");
                StringAssert.Contains(code, "[bool]$enabled");
                StringAssert.Contains(code, "[string]$mode");
            }
            else
            {
                StringAssert.Contains(File.ReadAllText(created.EntryPath), "parameters = request['parameters']");
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("bad-name")]
    [DataRow("x);throw 'injected';#")]
    [DataRow("PID")]
    [DataRow("ErrorActionPreference")]
    public void UnsafeParameterNamesNeverPublishPackages(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-parameter-invalid-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(root,
            new("Invalid", Parameters: new[] { new ScriptTemplateParameter(name, new() { Type = "string" }) })));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("primary")]
    [DataRow("enum-default")]
    [DataRow("typed-default")]
    [DataRow("bounds")]
    public void InvalidParameterContractsNeverPublishPackages(string failure)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-contract-invalid-" + Guid.NewGuid().ToString("N"));
        var schema = new JsonSchemaProperty { Type = "string" };
        var parameters = new List<ScriptTemplateParameter> { new(failure == "primary" ? "Text" : "mode", schema) };
        if (failure == "duplicate")
        {
            parameters.Add(new("MODE", new() { Type = "string" }));
            Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(root, new("Invalid", Parameters: parameters)));
        }
        else
        {
            if (failure == "enum-default")
            {
                schema.Enum = new() { "short" };
                schema.Default = JsonSerializer.SerializeToElement("unknown");
            }
            else if (failure == "typed-default")
            {
                schema.Type = "boolean";
                schema.Default = JsonSerializer.SerializeToElement("false");
            }
            else if (failure == "bounds")
            {
                schema.Type = "integer";
                schema.Minimum = 2;
                schema.Maximum = 1;
            }

            Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(root, new("Invalid", Input: "text", Parameters: parameters)));
        }

        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void GeneratedPowerShellBindsParametersWithoutEmbeddingDefaultValuesAsCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-template-bindings-" + Guid.NewGuid().ToString("N"));
        const string literal = "$(throw 'must remain data')";
        try
        {
            var created = ScriptTemplates.Create(root, new("Bindings", Parameters: new[]
            {
                new ScriptTemplateParameter("content", new() { Type = "string", Default = JsonSerializer.SerializeToElement(literal) }),
                new ScriptTemplateParameter("count", new() { Type = "integer", Default = JsonSerializer.SerializeToElement(0) }),
                new ScriptTemplateParameter("enabled", new() { Type = "boolean", Default = JsonSerializer.SerializeToElement(false) }),
            }));
            var code = File.ReadAllText(created.EntryPath);
            Assert.IsFalse(code.Contains(literal, StringComparison.Ordinal));
            File.WriteAllText(created.EntryPath, code.Replace("Write-Output $__powerscriptContent", "Write-Output ($content + '|' + $count.GetType().Name + '|' + $enabled.GetType().Name)", StringComparison.Ordinal));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            manifest.FolderPath = Path.GetDirectoryName(created.EntryPath)!;
            var result = new ScriptExecutor(new MxcSettings { Enabled = false, RiskAccepted = true }).Execute(manifest);
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
            Assert.AreEqual(literal + "|Int32|Boolean", result.StdOut.Trim());
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
