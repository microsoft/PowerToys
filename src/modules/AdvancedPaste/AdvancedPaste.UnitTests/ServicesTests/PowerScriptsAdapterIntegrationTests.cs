// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
[TestCategory("PowerScriptsIntegration")]
public sealed class PowerScriptsAdapterIntegrationTests
{
    private const string TextSchema = """{"type":"object","additionalProperties":false,"properties":{"text":{"type":"string"}},"required":["text"]}""";
    private const string FilesSchema = """{"type":"object","additionalProperties":false,"properties":{"files":{"type":"array","minItems":1,"items":{"type":"string"}}},"required":["files"]}""";
    private readonly List<string> _approved = [];
    private string _root;
    private PowerScriptsClient _client;

    [TestInitialize]
    public void Initialize()
    {
        var host = Environment.GetEnvironmentVariable("ADVANCED_PASTE_POWERSCRIPTS_TEST_HOST");
        if (string.IsNullOrEmpty(host))
        {
            Assert.Inconclusive("Set ADVANCED_PASTE_POWERSCRIPTS_TEST_HOST to a Host using an isolated settings/trust store.");
        }

        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "AdvancedPasteConsumer-" + Guid.NewGuid().ToString("N"))).FullName;
        _client = new(host, _root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var id in _approved)
        {
            Assert.IsTrue(_client.TrustRevoke(id));
        }

        if (_root is not null)
        {
            foreach (var file in Directory.EnumerateFiles(_root))
            {
                File.Delete(file);
            }

            Directory.Delete(_root);
        }
    }

    [TestMethod]
    public async Task TextAndTypedParametersReachTheRealHost()
    {
        const string inputSchema = """{"type":"object","additionalProperties":false,"properties":{"text":{"type":"string"},"parameters":{"type":"object","additionalProperties":false,"properties":{"count":{"type":"integer"},"enabled":{"type":"boolean"}},"required":["count","enabled"]}},"required":["text"]}""";
        var script = await ScriptAsync("param([string]$Text,[int]$count,[bool]$enabled); [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); Write-Output (\"{0}|{1}|{2}\" -f $Text,$count,$enabled)", inputSchema, TextSchema);
        var parameters = PowerScriptsService.CollectParameters(script.Parameters, new Dictionary<string, string> { ["count"] = "0", ["enabled"] = "false" });
        var output = await PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("Unicode \u03a9 and \"quotes\"").GetView(), parameters, CancellationToken.None, _client);
        Assert.AreEqual("Unicode \u03a9 and \"quotes\"|0|False", (await output.GetView().GetTextAsync()).TrimEnd());
    }

    [TestMethod]
    public async Task MultipleReturnedFilesArePasteableAndNeverDeletedByClipboardCleanup()
    {
        var first = Path.Combine(_root, "first file.txt");
        var second = Path.Combine(_root, "second file.txt");
        await File.WriteAllTextAsync(first, "one");
        await File.WriteAllTextAsync(second, "two");
        var input = new DataPackage();
        input.SetStorageItems(new[] { await StorageFile.GetFileFromPathAsync(first), await StorageFile.GetFileFromPathAsync(second) });
        var script = await ScriptAsync("param([string[]]$Files); @{ files = @($Files) } | ConvertTo-Json -Compress", FilesSchema, FilesSchema);
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        Assert.IsTrue(await output.HasUsableDataAsync());
        var items = await output.GetStorageItemsAsync();
        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(first, items[0].Path);
        Assert.AreEqual(second, items[1].Path);
        await output.TryCleanupAfterDelayAsync(TimeSpan.Zero);
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
    }

    [TestMethod]
    public async Task ClipboardBitmapPassesInlineThroughTheHostAndReturnsABitmap()
    {
        var schema = MediaSchema("image", "image/png");
        var script = await ScriptAsync("param($Image); @{ image = $Image } | ConvertTo-Json -Depth 12 -Compress", schema, schema);
        var input = await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png");
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        Assert.IsTrue(output.Contains(StandardDataFormats.Bitmap));
        var bytes = await output.GetImageAsPngBytesAsync();
        Assert.IsTrue(bytes.Length > 0);
    }

    [TestMethod]
    public async Task AudioFilePassesInlineAndReturnsPasteableAudioBytes()
    {
        var inputPath = Path.Combine(_root, "source.wav");
        using (var stream = File.Create(inputPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(38);
            writer.Write("WAVEfmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(8000);
            writer.Write(16000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(2);
            writer.Write((short)0);
        }

        var schema = MediaSchema("audio", "audio/wav");
        var script = await ScriptAsync("param($Audio); @{ audio = $Audio } | ConvertTo-Json -Depth 12 -Compress", schema, schema);
        var input = await DataPackageHelpers.CreateFromFileAsync(inputPath);
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        var items = await output.GetStorageItemsAsync();
        Assert.AreEqual(1, items.Count);
        CollectionAssert.AreEqual(await File.ReadAllBytesAsync(inputPath), await File.ReadAllBytesAsync(items[0].Path));
        File.Delete(items[0].Path);
        Directory.Delete(Path.GetDirectoryName(items[0].Path));
    }

    [TestMethod]
    public async Task NoInputScriptCanProduceTextWithoutClipboardContent()
    {
        const string empty = """{"type":"object","additionalProperties":false,"properties":{}}""";
        var script = await ScriptAsync("Write-Output 'generated content'", empty, TextSchema);
        var output = await PowerScriptsService.TransformAsync(script, null, null, CancellationToken.None, _client);
        Assert.AreEqual("generated content", (await output.GetView().GetTextAsync()).TrimEnd());
    }

    [TestMethod]
    public async Task HostFailureIsReportedInsteadOfReturningEmptySuccess()
    {
        var script = await ScriptAsync("param([string]$Text); throw 'Expected consumer failure'", TextSchema, TextSchema);
        var exception = await Assert.ThrowsExactlyAsync<PasteActionException>(() =>
            PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("input").GetView(), null, CancellationToken.None, _client));
        StringAssert.Contains(exception.AIServiceMessage, "Expected consumer failure");
    }

    [TestMethod]
    public async Task CancellationTerminatesTheHostTransform()
    {
        var script = await ScriptAsync("param([string]$Text); Start-Sleep -Seconds 30; Write-Output $Text", TextSchema, TextSchema);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("input").GetView(), null, cancellation.Token, _client));
    }

    [TestMethod]
    public async Task FileConverterProducesAnActualPasteableJsonFile()
    {
        var source = Path.Combine(_root, "people.csv");
        await File.WriteAllTextAsync(source, "name,age\nAda,36\nGrace,85");
        var script = await ScriptAsync(
            "param([string[]]$Files); $ErrorActionPreference='Stop'; $path=Join-Path $env:TEMP ('ap-converted-'+[Guid]::NewGuid().ToString('N')+'.json'); [IO.File]::WriteAllText($path, (Get-Content -LiteralPath $Files[0] | ConvertFrom-Csv | ConvertTo-Json)); @{files=@($path)} | ConvertTo-Json -Compress",
            FilesSchema,
            FilesSchema);
        var input = await DataPackageHelpers.CreateFromFileAsync(source);
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        Assert.IsTrue(await output.HasUsableDataAsync());
        var files = await output.GetStorageItemsAsync();
        Assert.AreEqual(1, files.Count);
        try
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(files[0].Path));
            Assert.AreEqual("Ada", json.RootElement[0].GetProperty("name").GetString());
            Assert.AreEqual("85", json.RootElement[1].GetProperty("age").GetString());
            await output.TryCleanupAfterDelayAsync(TimeSpan.Zero);
            Assert.IsTrue(File.Exists(files[0].Path));
        }
        finally
        {
            File.Delete(files[0].Path);
        }
    }

    [TestMethod]
    public async Task BitmapIsEncodedAsJpegWhenPngIsNotAccepted()
    {
        var script = await ScriptAsync(
            "param($Image); $bytes=[Convert]::FromBase64String($Image.data); Write-Output ($Image.mimeType+':'+[BitConverter]::ToString($bytes[0..2]))",
            MediaSchema("image", "image/jpeg"),
            TextSchema);
        var input = await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png");
        var output = await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client);
        Assert.AreEqual("image/jpeg:FF-D8-FF", (await output.GetView().GetTextAsync()).TrimEnd());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MediaCollectionsPreserveEveryItemAndArrayShape(bool audio)
    {
        var field = audio ? "audio" : "image";
        var mime = audio ? "audio/wav" : "image/png";
        var extension = audio ? ".wav" : ".png";
        byte[] bytes;
        if (audio)
        {
            bytes = Convert.FromBase64String("UklGRiYAAABXQVZFZm10IBAAAAABAAEAQB8AAIA+AAACABAAZGF0YQIAAAAAAA==");
        }
        else
        {
            bytes = await (await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png")).GetView().GetImageAsPngBytesAsync();
        }

        var first = Path.Combine(_root, "first" + extension);
        var second = Path.Combine(_root, "second" + extension);
        await File.WriteAllBytesAsync(first, bytes);
        await File.WriteAllBytesAsync(second, bytes);
        var schema = JsonNode.Parse(MediaSchema(field, mime));
        var properties = schema["properties"].AsObject();
        properties[field] = new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = 2,
            ["maxItems"] = 2,
            ["items"] = properties[field].DeepClone(),
        };
        var script = await ScriptAsync("param($Image,$Audio); @{" + field + "=@($" + field + ")} | ConvertTo-Json -Depth 12 -Compress", schema.ToJsonString(), schema.ToJsonString());
        var input = new DataPackage();
        input.SetStorageItems(new[] { await StorageFile.GetFileFromPathAsync(first), await StorageFile.GetFileFromPathAsync(second) });
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        Assert.IsTrue(await output.HasUsableDataAsync());
        var files = await output.GetStorageItemsAsync();
        try
        {
            Assert.AreEqual(2, files.Count);
            foreach (var file in files)
            {
                CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(file.Path));
            }
        }
        finally
        {
            foreach (var file in files)
            {
                File.Delete(file.Path);
            }

            Directory.Delete(Path.GetDirectoryName(files[0].Path));
        }
    }

    [TestMethod]
    public async Task ChangedClipboardAndOversizedMediaAreRejectedBeforeExecution()
    {
        var marker = Path.Combine(_root, "executed.txt");
        var script = await ScriptAsync("param($Image); Set-Content -LiteralPath '" + marker.Replace("'", "''") + "' -Value 'unexpected'; Write-Output 'unexpected'", MediaSchema("image", "image/png"), TextSchema);
        await Assert.ThrowsExactlyAsync<PasteActionException>(() =>
            PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("not an image").GetView(), null, CancellationToken.None, _client));
        var path = Path.Combine(_root, "oversized.png");
        using (var stream = File.Create(path))
        {
            stream.SetLength(PowerScriptMedia.MaxItemBytes + 1L);
        }

        var input = await DataPackageHelpers.CreateFromFileAsync(path);
        await Assert.ThrowsExactlyAsync<PasteActionException>(() =>
            PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client));
        Assert.IsFalse(File.Exists(marker));
    }

    [TestMethod]
    public async Task EmptyOutputIsAnExplicitFailure()
    {
        var script = await ScriptAsync("param($Text)", TextSchema, TextSchema);
        await Assert.ThrowsExactlyAsync<PasteActionException>(() =>
            PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("input").GetView(), null, CancellationToken.None, _client));
    }

    [TestMethod]
    public async Task PythonFormatterUsesTypedDefaultsAndPreservesUnicode()
    {
        const string inputSchema = """{"type":"object","additionalProperties":false,"properties":{"text":{"type":"string"},"parameters":{"type":"object","additionalProperties":false,"properties":{"repeat":{"type":"integer","default":2},"enabled":{"type":"boolean","default":false}}}},"required":["text"]}""";
        var script = await ScriptAsync(
            "import json,sys\nr=json.load(sys.stdin)\np=r['parameters']\nassert p['enabled'] is False\nsys.stdout.write(r['text'].upper()*p['repeat'])",
            inputSchema,
            TextSchema,
            python: true);
        var output = await PowerScriptsService.TransformAsync(script, DataPackageHelpers.CreateFromText("Hello \u03a9\n").GetView(), null, CancellationToken.None, _client);
        Assert.AreEqual("HELLO \u03a9\nHELLO \u03a9\n", (await output.GetView().GetTextAsync()).ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public async Task PythonFileConverterProducesAccessibleFilesAcrossRuntimeBoundaries()
    {
        var source = Path.Combine(_root, "input.csv");
        await File.WriteAllTextAsync(source, "name,age\nAda,36\nGrace,85");
        var script = await ScriptAsync(
            "import csv,json,pathlib,sys,uuid\nr=json.load(sys.stdin)\nwith open(r['files'][0],encoding='utf-8-sig') as f: rows=list(csv.DictReader(f))\np=pathlib.Path.cwd()/('ap-python-'+uuid.uuid4().hex+'.json')\np.write_text(json.dumps(rows),encoding='utf-8')\nprint(json.dumps({'files':[str(p)]}))",
            FilesSchema,
            FilesSchema,
            python: true);
        var input = await DataPackageHelpers.CreateFromFileAsync(source);
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        Assert.IsTrue(await output.HasUsableDataAsync());
        var files = await output.GetStorageItemsAsync();
        Assert.AreEqual(1, files.Count);
        try
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(files[0].Path));
            Assert.AreEqual("Ada", json.RootElement[0].GetProperty("name").GetString());
            Assert.AreEqual("85", json.RootElement[1].GetProperty("age").GetString());
        }
        finally
        {
            File.Delete(files[0].Path);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PythonInlineMediaSurvivesRuntimeBoundaries(bool audio)
    {
        var field = audio ? "audio" : "image";
        var schema = MediaSchema(field, audio ? "audio/wav" : "image/png");
        var bytes = audio
            ? Convert.FromBase64String("UklGRiYAAABXQVZFZm10IBAAAAABAAEAQB8AAIA+AAACABAAZGF0YQIAAAAAAA==")
            : await (await ResourceUtils.GetImageAssetAsDataPackageAsync("image_with_text_example.png")).GetView().GetImageAsPngBytesAsync();
        var source = Path.Combine(_root, audio ? "input.wav" : "input.png");
        await File.WriteAllBytesAsync(source, bytes);
        var script = await ScriptAsync("import json,sys\nr=json.load(sys.stdin)\nprint(json.dumps({'" + field + "':r['" + field + "']}))", schema, schema, python: true);
        var input = await DataPackageHelpers.CreateFromFileAsync(source);
        var output = (await PowerScriptsService.TransformAsync(script, input.GetView(), null, CancellationToken.None, _client)).GetView();
        if (audio)
        {
            var files = await output.GetStorageItemsAsync();
            Assert.AreEqual(1, files.Count);
            try
            {
                CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(files[0].Path));
            }
            finally
            {
                File.Delete(files[0].Path);
                Directory.Delete(Path.GetDirectoryName(files[0].Path));
            }
        }
        else
        {
            using var stream = await (await output.GetBitmapAsync()).OpenReadAsync();
            using var data = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(data);
            CollectionAssert.AreEqual(bytes, data.ToArray());
        }
    }

    private async Task<PowerScriptInfo> ScriptAsync(string body, string inputSchema, string outputSchema, bool python = false)
    {
        var entry = Path.Combine(_root, python ? "script.py" : "script.ps1");
        await File.WriteAllTextAsync(entry, body);
        var descriptor = new JsonObject
        {
            ["name"] = "ap-consumer-" + Guid.NewGuid().ToString("N"),
            ["title"] = "Advanced Paste consumer test",
            ["description"] = "Isolated integration fixture.",
            ["inputSchema"] = JsonNode.Parse(inputSchema),
            ["outputSchema"] = JsonNode.Parse(outputSchema),
        };
        await File.WriteAllTextAsync(entry + ".tool.json", descriptor.ToJsonString());
        var script = (await _client.ListAsync()).Single();
        Assert.IsTrue(_client.TrustApprove(script.Id));
        _approved.Add(script.Id);
        return script;
    }

    private static string MediaSchema(string field, string mime) =>
        new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                [field] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["mimeType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(mime) },
                        ["data"] = new JsonObject { ["type"] = "string", ["contentEncoding"] = "base64", ["minLength"] = 1 },
                    },
                    ["required"] = new JsonArray("mimeType", "data"),
                },
            },
            ["required"] = new JsonArray(field),
        }.ToJsonString();
}
