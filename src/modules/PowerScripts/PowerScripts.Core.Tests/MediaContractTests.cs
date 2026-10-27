// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
public class MediaContractTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=";

    [TestMethod]
    [DataRow("powershell")]
    [DataRow("python")]
    public void EveryConfiguredMediaCombinationCreatesAValidPackage(string runtime)
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-media-templates-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var input in new[] { "none", "text", "files", "image", "audio" })
            {
                foreach (var output in new[] { "none", "text", "files", "image", "audio" })
                {
                    if (input is not ("image" or "audio") && output is not ("image" or "audio"))
                    {
                        continue;
                    }

                    var created = ScriptTemplates.Create(root, new ScriptTemplateOptions("Media sample", Runtime: runtime, Input: input, Output: output));
                    var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
                    Assert.AreEqual(input, ScriptIo.ToToken(manifest.InputFormat));
                    Assert.AreEqual(output, ScriptIo.ToToken(manifest.OutputFormat));
                    StringAssert.Contains(File.ReadAllText(created.GuidePath), "video");
                    Assert.IsFalse(manifest.Capabilities.Contains("fileRead") && input != "files");
                    Assert.IsFalse(manifest.Capabilities.Contains("fileWrite") && output != "files");
                }
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
    public void ImageObjectsAndExplicitCollectionsRoundTripWithoutPathInterpretation()
    {
        var descriptor = Descriptor();
        var manifest = TestTool.Parse(descriptor);
        var payload = JsonSerializer.Serialize(new { image = new { mimeType = "image/png", data = Png } });
        var input = PythonTransformInput.Parse(manifest, payload);
        Assert.AreEqual(Png, input.Image!.Value.GetProperty("data").GetString());
        Assert.IsNull(input.FilePaths);
        ScriptSchema.Validate(manifest.OutputSchema, JsonSerializer.SerializeToElement(new { image = input.Image }), "output");

        var item = descriptor["inputSchema"]!["properties"]!["image"]!.DeepClone();
        descriptor["inputSchema"]!["properties"]!["image"] = new JsonObject
        {
            ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 2, ["items"] = item,
        };
        manifest = TestTool.Parse(descriptor);
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, payload));
        var array = JsonSerializer.Serialize(new { image = new[] { new { mimeType = "image/png", data = Png } } });
        input = PythonTransformInput.Parse(manifest, array);
        Assert.AreEqual(JsonValueKind.Array, input.Image!.Value.ValueKind);
        Assert.AreEqual(1, input.CreateRequest(manifest)["image"]!.AsArray().Count);
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, """{"image":[]}"""));
    }

    [TestMethod]
    [DataRow("""{"image":"C:\\pictures\\photo.png"}""")]
    [DataRow("""{"image":{"mimeType":"image/png","data":"not base64"}}""")]
    [DataRow("""{"image":{"mimeType":"image/png","data":""}}""")]
    [DataRow("""{"image":{"mimeType":"image/png","data":"aGVsbG8="}}""")]
    [DataRow("""{"image":{"mimeType":"image/png","data":"aGVs bG8="}}""")]
    [DataRow("""{"image":{"mimeType":"image/gif","data":"R0lGODlh"}}""")]
    [DataRow("""{"image":{"mimeType":"audio/wav","data":"UklGRg=="}}""")]
    [DataRow("""{"image":{"mimeType":"image/png","data":"aGVsbG8=","path":"photo.png"}}""")]
    public void InvalidMediaIsRejectedBeforeExecution(string payload)
    {
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(TestTool.Parse(Descriptor()), payload));
    }

    [TestMethod]
    public void BytesCannotBeRelabeledAsAnotherMediaFormat()
    {
        var manifest = TestTool.Parse(Descriptor());
        var payload = JsonSerializer.Serialize(new { image = new { mimeType = "image/jpeg", data = Png } });
        Assert.ThrowsException<InvalidDataException>(() => PythonTransformInput.Parse(manifest, payload));
        var valid = JsonSerializer.Serialize(new { image = new { mimeType = "image/png", data = Png } });
        var parsed = PythonTransformInput.Parse(manifest, valid);
        parsed.Audio = parsed.Image;
        Assert.ThrowsException<InvalidDataException>(() => parsed.CreateRequest(manifest));
    }

    [TestMethod]
    public void MediaBoundsRejectOversizedItemsAndCollections()
    {
        var exact = Convert.ToBase64String(new byte[16 * 1024 * 1024]);
        Assert.AreEqual(16 * 1024 * 1024, ScriptMedia.Decode(exact, "data").Length);
        var oversized = Convert.ToBase64String(new byte[16 * 1024 * 1024 + 1]);
        Assert.ThrowsException<InvalidDataException>(() => ScriptMedia.Decode(oversized, "data"));

        var wav = new byte[11 * 1024 * 1024];
        "RIFF"u8.CopyTo(wav);
        "WAVE"u8.CopyTo(wav.AsSpan(8));
        var item = new { mimeType = "audio/wav", data = Convert.ToBase64String(wav) };
        var array = JsonSerializer.SerializeToElement(new[] { item, item, item });
        Assert.ThrowsException<InvalidDataException>(() => ScriptMedia.Validate(array, "audio", "input.audio"));
        var empty = JsonSerializer.SerializeToElement(Array.Empty<object>());
        Assert.ThrowsException<InvalidDataException>(() => ScriptMedia.Validate(empty, "image", "input.image"));
    }

    [TestMethod]
    public void MediaDefinitionsRequireExplicitMimeChoicesAndBase64()
    {
        var descriptor = Descriptor();
        descriptor["inputSchema"]!["properties"]!["image"]!["properties"]!["data"]!["contentEncoding"] = "gzip";
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = Descriptor();
        descriptor["inputSchema"]!["properties"]!["image"]!["properties"]!["mimeType"]!["enum"] = new JsonArray("image/svg+xml");
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor = Descriptor();
        descriptor["x-execute"] = JsonNode.Parse("""{"command":["python","script.py"]}""");
        Assert.ThrowsException<InvalidDataException>(() => TestTool.Parse(descriptor));
        descriptor["x-execute"]!["stdin"] = "json";
        Assert.IsNotNull(TestTool.Parse(descriptor));
    }

    [TestMethod]
    public void AnimatedPngAndWebpAreNotSilentlyFlattened()
    {
        var png = Convert.FromBase64String(Png);
        var animated = new byte[png.Length + 20];
        png.AsSpan(0, 33).CopyTo(animated);
        animated[36] = 8;
        "acTL"u8.CopyTo(animated.AsSpan(37));
        png.AsSpan(33).CopyTo(animated.AsSpan(53));
        Assert.ThrowsException<InvalidDataException>(() => ScriptMedia.ValidateFormat(animated, "image/png", "image"));
        var webp = new byte[30];
        "RIFF"u8.CopyTo(webp);
        webp[4] = 22;
        "WEBPVP8X"u8.CopyTo(webp.AsSpan(8));
        webp[16] = 10;
        webp[20] = 2;
        Assert.ThrowsException<InvalidDataException>(() => ScriptMedia.ValidateFormat(webp, "image/webp", "image"));
    }

    [TestMethod]
    public void PowerShellTemplatePassesInlineImagesWithoutTemporaryInputFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-media-run-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ScriptTemplates.Create(root, new ScriptTemplateOptions("Image round trip", Input: "image", Output: "image"));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            manifest.FolderPath = Path.GetDirectoryName(created.EntryPath)!;
            var image = JsonSerializer.SerializeToElement(new { mimeType = "image/png", data = Png });
            var result = new ScriptExecutor(new MxcSettings { Enabled = false, RiskAccepted = true }).Execute(manifest, image: image);
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
            using var output = JsonDocument.Parse(result.StdOut);
            ScriptSchema.Validate(manifest.OutputSchema, output.RootElement, "output");
            Assert.AreEqual(Png, output.RootElement.GetProperty("image").GetProperty("data").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static JsonObject Descriptor()
    {
        var descriptor = TestTool.Descriptor();
        var schema = JsonNode.Parse("""
            {
              "type":"object","additionalProperties":false,"required":["image"],
              "properties":{"image":{"type":"object","additionalProperties":false,"required":["mimeType","data"],
                "properties":{
                  "mimeType":{"type":"string","enum":["image/png","image/jpeg","image/webp"]},
                  "data":{"type":"string","contentEncoding":"base64"}
                }}}
            }
            """)!;
        descriptor["inputSchema"] = schema;
        descriptor["outputSchema"] = schema.DeepClone();
        return descriptor;
    }
}
