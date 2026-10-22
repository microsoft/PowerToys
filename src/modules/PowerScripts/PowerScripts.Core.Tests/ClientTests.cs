// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ClientTests
{
    private const string SampleJson = """
    [
      {
        "id": "whats-my-ip",
        "name": "What's My IP",
        "description": "Prints the public IP.",
        "runtime": "PowerShell",
        "publisher": "PowerToys samples",
        "iconPath": "C:\\scripts\\ip\\icon.ico",
        "io": { "input": "none", "output": "text" },
        "capabilities": ["network"],
        "trusted": true,
        "parameters": [
          {
            "name": "format",
            "type": "choice",
            "isRequired": true,
            "label": "Output format",
            "description": "Select an output format.",
            "default": "short",
            "options": ["short", "long"],
            "min": null,
            "max": null
          }
        ],
        "input": null
      },
      {
        "id": "convert_md_to_txt",
        "name": "Convert Markdown to Text",
        "description": "MD to TXT.",
        "runtime": "PowerShell",
        "io": { "input": "files", "output": "files" },
        "capabilities": ["fileRead", "fileWrite"],
        "trusted": false,
        "input": { "extensions": [".md"], "minFiles": 2, "maxFiles": 3 }
      },
      {
        "id": "py_upper",
        "name": "Uppercase",
        "description": "Upper the clipboard.",
        "runtime": "Python",
        "io": { "input": "text", "output": "text" },
        "capabilities": [],
        "trusted": false,
        "transform": { "function": "uppercase", "inputFormat": "Text", "outputFormat": "Text" }
      },
      { "name": "no-id-skipped" }
    ]
    """;

    [TestMethod]
    public void ConsumerTargetingIsOptionalCaseInsensitiveAndIndependentOfIo()
    {
        var scripts = PowerScriptInfo.ParseList("""
            [
              {"id":"all","io":{"input":"text","output":"text"}},
              {"id":"color","io":{"input":"text","output":"text"},"consumers":["color-picker"]}
            ]
            """);
        Assert.IsTrue(scripts[0].IsDiscoverableBy("keyboard-manager"));
        Assert.IsTrue(scripts[1].IsDiscoverableBy("COLOR-PICKER"));
        Assert.IsFalse(scripts[1].IsDiscoverableBy("advanced-paste"));
        Assert.IsTrue(scripts[1].Accepts(PowerScriptIO.Text));
        var client = new PowerScriptsClient(hostPath: @"C:\does-not-exist\host.exe");
        Assert.ThrowsException<ArgumentException>(() => client.List("bad module"));
    }

    [TestMethod]
    public void ParseList_MapsFields_AndSkipsInvalid()
    {
        var scripts = PowerScriptInfo.ParseList(SampleJson);

        // The entry with no id is skipped.
        Assert.AreEqual(3, scripts.Count);

        var ip = scripts[0];
        Assert.AreEqual("whats-my-ip", ip.Id);
        Assert.AreEqual("What's My IP", ip.Name);
        Assert.AreEqual("PowerShell", ip.Runtime);
        Assert.AreEqual("PowerToys samples", ip.Publisher);
        Assert.AreEqual(@"C:\scripts\ip\icon.ico", ip.IconPath);
        Assert.IsNull(ip.IconWarning);
        Assert.IsNull(scripts[1].IconPath, "Older Hosts and scripts without icons must remain compatible.");
        Assert.IsTrue(ip.Trusted);
        CollectionAssert.AreEquivalent(new[] { "network" }, ip.Capabilities.ToArray());
        Assert.AreEqual(1, ip.Parameters.Count);
        Assert.AreEqual("format", ip.Parameters[0].Name);
        Assert.IsTrue(ip.Parameters[0].IsRequired);
        CollectionAssert.AreEqual(new[] { "short", "long" }, ip.Parameters[0].Options.ToArray());
        Assert.IsNull(ip.Transform);
    }

    [TestMethod]
    public void ParseList_ReadsIO_AndFiltersByShape()
    {
        var scripts = PowerScriptInfo.ParseList(SampleJson);

        var ip = scripts.Single(s => s.Id == "whats-my-ip");
        Assert.AreEqual(PowerScriptIO.None, ip.Input);
        Assert.AreEqual(PowerScriptIO.Text, ip.Output);
        Assert.IsTrue(ip.IsAction);

        var upper = scripts.Single(s => s.Id == "py_upper");
        Assert.AreEqual(PowerScriptIO.Text, upper.Input);
        Assert.IsTrue(upper.Accepts(PowerScriptIO.Text));
        Assert.IsFalse(upper.IsAction);

        var file = scripts.Single(s => s.Id == "convert_md_to_txt");
        Assert.AreEqual(PowerScriptIO.Files, file.Input);

        // A module discovers scripts by I/O: actions for a hotkey, text-consumers for a paste.
        Assert.AreEqual(1, scripts.Count(s => s.IsAction));
        Assert.AreEqual(1, scripts.Count(s => s.Accepts(PowerScriptIO.Text)));
    }

    [TestMethod]
    public void ParseList_ReadsInputExtensions_AndTransform()
    {
        var scripts = PowerScriptInfo.ParseList(SampleJson);

        var file = scripts.Single(s => s.Id == "convert_md_to_txt");
        CollectionAssert.AreEquivalent(new[] { ".md" }, file.InputExtensions.ToArray());
        Assert.AreEqual(2, file.InputMinFiles);
        Assert.AreEqual(3, file.InputMaxFiles);

        var transform = scripts.Single(s => s.Id == "py_upper");
        Assert.IsNotNull(transform.Transform);
        Assert.AreEqual("uppercase", transform.Transform!.Function);
        Assert.AreEqual("Text", transform.Transform.InputFormat);
    }

    [TestMethod]
    public void ParseList_Tolerates_EmptyOrGarbage()
    {
        Assert.AreEqual(0, PowerScriptInfo.ParseList(string.Empty).Count);
        Assert.AreEqual(0, PowerScriptInfo.ParseList("   ").Count);
        Assert.AreEqual(0, PowerScriptInfo.ParseList("{}").Count);
    }

    [DataTestMethod]
    [DataRow("html")]
    [DataRow("image")]
    [DataRow("audio")]
    [DataRow("video")]
    public void ParseList_RejectsRemovedOutputTypesInsteadOfTreatingThemAsNone(string format)
    {
        Assert.ThrowsException<JsonException>(() => PowerScriptInfo.ParseList(
            $$$"""[{"id":"legacy","io":{"input":"none","output":"{{{format}}}"}}]"""));
    }

    [TestMethod]
    public void FileDiscovery_IsOriginIndependentAndRespectsExtensionsAndCount()
    {
        var script = new PowerScriptInfo
        {
            Input = PowerScriptIO.Files,
            InputExtensions = new[] { "PNG", ".jpg" },
            InputMinFiles = 1,
            InputMaxFiles = 1,
        };

        Assert.IsTrue(script.AcceptsFiles(new[] { @"C:\Clipboard\capture.png" }));
        Assert.IsTrue(script.AcceptsFiles(new[] { @"C:\Pictures\photo.PNG" }));
        Assert.IsTrue(script.AcceptsFiles(new[] { @"C:\Pictures\photo.jpg" }));
        Assert.IsTrue(script.AcceptsFileExtension(" PNG "));
        Assert.IsFalse(script.AcceptsFiles(new[] { @"C:\Pictures\photo.gif" }));
        Assert.IsFalse(script.AcceptsFiles(new[] { "one.png", "two.png" }));
        Assert.IsFalse(script.AcceptsFiles(Array.Empty<string>()));

        var batch = PowerScriptInfo.ParseList(SampleJson).Single(s => s.Id == "convert_md_to_txt");
        Assert.IsFalse(batch.AcceptsFiles(new[] { "one.md" }));
        Assert.IsTrue(batch.AcceptsFiles(new[] { "one.md", "two.md" }));
        Assert.IsFalse(batch.AcceptsFiles(new[] { "one.md", "two.png" }));
        Assert.IsFalse(batch.AcceptsFiles(new[] { "one.md", "two.md", "three.md", "four.md" }));
    }

    [TestMethod]
    public void Client_FileDiscoveryAndTransform_UseTheSamePngContractAsExplorer()
    {
        var hostPath = PowerScriptsClient.ResolveHostPath();
        Assert.IsFalse(string.IsNullOrEmpty(hostPath), "The referenced Host project must be built.");
        var root = Path.Combine(Path.GetTempPath(), "ps-png-client-" + Guid.NewGuid().ToString("N"));
        var id = "png-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(root);
        var client = new PowerScriptsClient(hostPath, scriptsRoot: root);
        string? produced = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "inspect.ps1"), """
                param([string[]]$Files)
                [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
                if ($env:POWERSCRIPTS_FILES) { $Files = $env:POWERSCRIPTS_FILES -split "`n" }
                $output = Join-Path $env:POWERSCRIPTS_MXC_WORKSPACE ('result ' + [char]0x6D4B + [char]0x8BD5 + '.png')
                [IO.File]::WriteAllBytes($output, [IO.File]::ReadAllBytes($Files[0]))
                @{ file_paths = @($output) } | ConvertTo-Json -Compress
                """);
            File.WriteAllText(Path.Combine(root, "inspect.ps1.tool.json"), $$"""
                {
                  "name": "{{id}}",
                  "x-powerscript": {
                    "input": "files", "output": "files", "extensions": [".png"],
                    "minFiles": 1, "maxFiles": 1
                  }
                }
                """);
            var png = Path.Combine(root, "clipboard.png");
            File.WriteAllBytes(png, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));

            Assert.AreEqual(id, client.ListForFiles(new[] { png }).Single().Id);
            Assert.AreEqual(0, client.ListForFiles(new[] { png, png }).Count);
            Assert.AreEqual(0, client.ListForFiles(new[] { Path.ChangeExtension(png, ".jpg") }).Count);
            Assert.AreEqual(0, client.ListForFiles(Array.Empty<string>()).Count);

            foreach (var selection in new[] { new[] { png }, new[] { png, png }, new[] { Path.ChangeExtension(png, ".jpg") }, Array.Empty<string>() })
            {
                var startInfo = new ProcessStartInfo(hostPath!)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var argument in new[] { "shell-menu", "--root", root, "--files" }.Concat(selection))
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    process.Kill(entireProcessTree: true);
                    Assert.Fail("Explorer discovery timed out.");
                }
                Assert.AreEqual(0, process.ExitCode, error.GetAwaiter().GetResult());
                var shellIds = output.GetAwaiter().GetResult()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('\t')[0]).ToArray();
                CollectionAssert.AreEquivalent(client.ListForFiles(selection).Select(script => script.Id).ToArray(), shellIds);
            }

            Assert.IsTrue(client.TrustApprove(id));
            var result = client.TransformAsync(
                id,
                new PowerScriptTransformInput { FilePaths = new[] { png } },
                noConsent: true).GetAwaiter().GetResult();
            Assert.IsTrue(result.Succeeded, result.StdErr);
            Assert.IsNull(result.Text);
            var outputPath = result.FilePaths!.Single();
            produced = Path.Combine(Path.GetDirectoryName(outputPath)!, "result \u6D4B\u8BD5.png");
            Assert.AreEqual("result \u6D4B\u8BD5.png", Path.GetFileName(outputPath));
            CollectionAssert.AreEqual(File.ReadAllBytes(png), File.ReadAllBytes(outputPath));
        }
        finally
        {
            client.TrustRevoke(id);
            if (produced is not null)
            {
                File.Delete(produced);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Client_WithoutHost_IsUnavailable_AndReturnsEmpty()
    {
        // A client pointed at a non-existent host degrades gracefully rather than throwing.
        var client = new PowerScriptsClient(hostPath: Path.Combine(Path.GetTempPath(), "does-not-exist.exe"));
        Assert.IsFalse(client.IsAvailable);
        Assert.AreEqual(0, client.List().Count);
        Assert.IsFalse(client.TrustApprove("x"));
        Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, client.Run("x").ExitCode);
    }

    [TestMethod]
    public void Client_EndToEnd_List_And_Trust_AgainstRealHost()
    {
        var hostPath = PowerScriptsClient.ResolveHostPath();
        if (string.IsNullOrEmpty(hostPath))
        {
            Assert.Inconclusive("PowerScripts.Host.exe not found in this environment; skipping integration test.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "ps-client-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "it_hello.ps1"), "Write-Host hi");
            File.WriteAllText(
                Path.Combine(root, "it_hello.ps1.tool.json"),
                "{ \"name\":\"it_hello\", \"description\":\"it\", \"inputSchema\":{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},\"required\":[\"name\"]} }");

            var client = new PowerScriptsClient(hostPath, scriptsRoot: root);
            Assert.IsTrue(client.IsAvailable);

            var scripts = client.List();
            var hello = scripts.SingleOrDefault(s => s.Id == "it_hello");
            Assert.IsNotNull(hello, "expected the host to list the chosen-folder script");
            Assert.IsFalse(hello!.Trusted);

            Assert.IsTrue(client.TrustApprove("it_hello"));
            Assert.IsTrue(client.List().Single(s => s.Id == "it_hello").Trusted);
            var missingParameter = client.TransformAsync("it_hello", new PowerScriptTransformInput()).GetAwaiter().GetResult();
            Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, missingParameter.ExitCode);
            StringAssert.Contains(missingParameter.StdErr, "Missing required parameter");
            Assert.IsTrue(client.TrustRevoke("it_hello"));
        }
        finally
        {
            new PowerScriptsClient(hostPath, scriptsRoot: root).TrustRevoke("it_hello");
            Directory.Delete(root, recursive: true);
        }
    }
}
