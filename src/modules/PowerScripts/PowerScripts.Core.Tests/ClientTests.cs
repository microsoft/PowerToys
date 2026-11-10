// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ClientTests
{
    [TestMethod]
    public async Task AsyncDiscoverySurfacesMissingHostInsteadOfAnEmptyCatalogue()
    {
        var client = new PowerScriptsClient("test-harness", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "host.exe"));
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => client.ListAsync());
    }

    [TestMethod]
    public async Task CancelledDiscoveryDoesNotStartTheHost()
    {
        var client = new PowerScriptsClient("test-harness", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "host.exe"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => client.ListAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task ConsumerStateSurfacesMissingHostInsteadOfAnEnabledOrEmptyIntegration()
    {
        var client = new PowerScriptsClient(PowerScriptConsumer.CommandPalette, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "host.exe"));
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => client.GetConsumerStateAsync());
    }

    [TestMethod]
    public async Task CancelledConsumerStateDoesNotStartTheHost()
    {
        var client = new PowerScriptsClient(PowerScriptConsumer.CommandPalette, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "host.exe"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => client.GetConsumerStateAsync(cancellation.Token));
    }

    private const string ConsumerSnapshotJson = """
        {
          "isPowerScriptsEnabled": true,
          "isPowerScriptsPolicyControlled": false,
          "consumers": [
            { "id": "advanced-paste", "userEnabled": false, "isEnabled": false, "canChange": true, "isPolicyControlled": false, "disabledReason": null },
            { "id": "keyboard-manager", "userEnabled": true, "isEnabled": true, "canChange": true, "isPolicyControlled": false, "disabledReason": null },
            { "id": "command-palette", "userEnabled": true, "isEnabled": true, "canChange": true, "isPolicyControlled": false, "disabledReason": null }
          ]
        }
        """;

    [TestMethod]
    public void ParseConsumerStateSelectsTheCallerAndPreservesPreferenceAndPolicyFields()
    {
        var state = PowerScriptConsumerState.ParseSnapshot(ConsumerSnapshotJson, PowerScriptConsumer.CommandPalette);

        Assert.AreEqual("command-palette", state.Id);
        Assert.IsTrue(state.IsPowerScriptsEnabled);
        Assert.IsFalse(state.IsPowerScriptsPolicyControlled);
        Assert.IsTrue(state.UserEnabled);
        Assert.IsTrue(state.IsEnabled);
        Assert.IsTrue(state.CanChange);
        Assert.IsFalse(state.IsPolicyControlled);
        Assert.IsNull(state.DisabledReason);
        Assert.IsFalse(PowerScriptConsumerState.ParseSnapshot(ConsumerSnapshotJson, PowerScriptConsumer.AdvancedPaste).IsEnabled);
    }

    [DataTestMethod]
    [DataRow(true, true, true, false)]
    [DataRow(false, false, true, false)]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, false, true)]
    public void ParseConsumerStateReadsActualHostSnapshotWhenNullReasonsAreOmitted(
        bool userEnabled,
        bool enabled,
        bool canChange,
        bool policyControlled)
    {
        var snapshot = new ConsumerSettingsSnapshot(
            true,
            false,
            [new ConsumerState(PowerScriptConsumer.CommandPalette, userEnabled, enabled, canChange, policyControlled, null)]);
        var json = JsonSerializer.Serialize(snapshot, ToolDescriptorParser.JsonOptions);
        using var document = JsonDocument.Parse(json);
        Assert.IsFalse(
            document.RootElement.GetProperty("consumers").EnumerateArray().Single().TryGetProperty("disabledReason", out _),
            "The Host omits null fields; the Client must accept the actual serialized contract.");

        var state = PowerScriptConsumerState.ParseSnapshot(json, PowerScriptConsumer.CommandPalette);

        Assert.AreEqual(PowerScriptConsumer.CommandPalette, state.Id);
        Assert.IsTrue(state.IsPowerScriptsEnabled);
        Assert.IsFalse(state.IsPowerScriptsPolicyControlled);
        Assert.AreEqual(userEnabled, state.UserEnabled);
        Assert.AreEqual(enabled, state.IsEnabled);
        Assert.AreEqual(canChange, state.CanChange);
        Assert.AreEqual(policyControlled, state.IsPolicyControlled);
        Assert.IsNull(state.DisabledReason);
    }

    [DataTestMethod]
    [DataRow(false, true, true, false, "module-disabled")]
    [DataRow(true, false, true, true, "parent-policy-disabled")]
    [DataRow(true, false, false, true, null)]
    [DataRow(true, false, false, false, null)]
    public void ParseConsumerStatePreservesDisabledReasons(
        bool moduleEnabled,
        bool modulePolicyControlled,
        bool userEnabled,
        bool consumerPolicyControlled,
        string? reason)
    {
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!;
        snapshot["isPowerScriptsEnabled"] = moduleEnabled;
        snapshot["isPowerScriptsPolicyControlled"] = modulePolicyControlled;
        var consumer = snapshot["consumers"]![2]!;
        consumer["userEnabled"] = userEnabled;
        consumer["isEnabled"] = false;
        var canChange = moduleEnabled && reason is null && !consumerPolicyControlled;
        consumer["canChange"] = canChange;
        consumer["isPolicyControlled"] = consumerPolicyControlled;
        consumer["disabledReason"] = reason;
        var state = PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette);

        Assert.AreEqual(moduleEnabled, state.IsPowerScriptsEnabled);
        Assert.AreEqual(modulePolicyControlled, state.IsPowerScriptsPolicyControlled);
        Assert.AreEqual(userEnabled, state.UserEnabled);
        Assert.IsFalse(state.IsEnabled);
        Assert.AreEqual(canChange, state.CanChange);
        Assert.AreEqual(consumerPolicyControlled, state.IsPolicyControlled);
        Assert.AreEqual(reason, state.DisabledReason);
    }

    [DataTestMethod]
    [DataRow("id")]
    [DataRow("userEnabled")]
    [DataRow("isEnabled")]
    [DataRow("canChange")]
    [DataRow("isPolicyControlled")]
    public void ParseConsumerStateRejectsMissingConsumerFields(string name)
    {
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!;
        ((JsonObject)snapshot["consumers"]![2]!).Remove(name);
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette));
    }

    [DataTestMethod]
    [DataRow("isPowerScriptsEnabled")]
    [DataRow("isPowerScriptsPolicyControlled")]
    [DataRow("consumers")]
    public void ParseConsumerStateRejectsMissingSnapshotFields(string name)
    {
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!.AsObject();
        snapshot.Remove(name);
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette));
    }

    [DataTestMethod]
    [DataRow("userEnabled")]
    [DataRow("isEnabled")]
    [DataRow("canChange")]
    [DataRow("isPolicyControlled")]
    [DataRow("disabledReason")]
    public void ParseConsumerStateRejectsInvalidConsumerFieldTypes(string name)
    {
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!;
        snapshot["consumers"]![2]![name] = 1;
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette));
    }

    [TestMethod]
    public void ParseConsumerStateRejectsUnknownAndDuplicateConsumersInsteadOfAssumingEnabled()
    {
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot("[]", PowerScriptConsumer.CommandPalette));
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot(ConsumerSnapshotJson, "future-module"));
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!;
        var consumers = snapshot["consumers"]!.AsArray();
        consumers.Add(consumers[2]!.DeepClone());
        Assert.ThrowsException<JsonException>(() => PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette));
    }

    [TestMethod]
    public void ParseConsumerStateToleratesAddedSnapshotFields()
    {
        var snapshot = JsonNode.Parse(ConsumerSnapshotJson)!;
        snapshot["futureField"] = true;
        snapshot["consumers"]![2]!["futurePreference"] = "keep-compatible";
        Assert.IsTrue(PowerScriptConsumerState.ParseSnapshot(snapshot.ToJsonString(), PowerScriptConsumer.CommandPalette).IsEnabled);
    }

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
        "trusted": false
      },
      { "name": "no-id-skipped" }
    ]
    """;

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
    public void ParseListReadsInputExtensionsAndPrimaryIo()
    {
        var scripts = PowerScriptInfo.ParseList(SampleJson);

        var file = scripts.Single(s => s.Id == "convert_md_to_txt");
        CollectionAssert.AreEquivalent(new[] { ".md" }, file.InputExtensions.ToArray());
        Assert.AreEqual(2, file.InputMinFiles);
        Assert.AreEqual(3, file.InputMaxFiles);

        var transform = scripts.Single(s => s.Id == "py_upper");
        Assert.AreEqual(PowerScriptIO.Text, transform.Input);
        Assert.AreEqual(PowerScriptIO.Text, transform.Output);
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
    [DataRow("video")]
    public void ParseList_RejectsRemovedOutputTypesInsteadOfTreatingThemAsNone(string format)
    {
        Assert.ThrowsException<JsonException>(() => PowerScriptInfo.ParseList(
            $$$"""[{"id":"legacy","io":{"input":"none","output":"{{{format}}}"}}]"""));
    }

    [TestMethod]
    public void ParseListPreservesMediaTypesAndSchemasForConsumerAdaptation()
    {
        var scripts = PowerScriptInfo.ParseList("""
            [{"id":"media","io":{"input":"image","output":"audio"},
              "inputSchema":{"type":"object","properties":{"image":{"type":"object"}}},
              "outputSchema":{"type":"object","properties":{"audio":{"type":"array"}}}}]
            """);
        Assert.AreEqual(PowerScriptIO.Image, scripts[0].Input);
        Assert.AreEqual(PowerScriptIO.Audio, scripts[0].Output);
        Assert.AreEqual("object", scripts[0].InputSchema!.Value.GetProperty("properties").GetProperty("image").GetProperty("type").GetString());
        Assert.AreEqual("array", scripts[0].OutputSchema!.Value.GetProperty("properties").GetProperty("audio").GetProperty("type").GetString());
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
    public void Client_FileDiscoveryAndTransform_UseTheSharedPngContract()
    {
        var hostPath = PowerScriptsClient.ResolveHostPath();
        Assert.IsFalse(string.IsNullOrEmpty(hostPath), "The referenced Host project must be built.");
        var root = Path.Combine(Path.GetTempPath(), "ps-png-client-" + Guid.NewGuid().ToString("N"));
        var id = "png-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(root);
        var client = new PowerScriptsClient("test-harness", hostPath, scriptsRoot: root);
        string? produced = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "inspect.ps1"), """
                param([string[]]$Files)
                [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
                if ($env:POWERSCRIPTS_FILES) { $Files = $env:POWERSCRIPTS_FILES -split "`n" }
                $output = Join-Path $env:POWERSCRIPTS_MXC_WORKSPACE ('result ' + [char]0x6D4B + [char]0x8BD5 + '.png')
                [IO.File]::WriteAllBytes($output, [IO.File]::ReadAllBytes($Files[0]))
                @{ files = @($output) } | ConvertTo-Json -Compress
                """);
            File.WriteAllText(Path.Combine(root, "inspect.ps1.tool.json"), $$"""
                {
                  "name": "{{id}}",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "files": {
                        "type": "array",
                        "items": {
                          "type": "string",
                          "x-powerscript": {
                            "extensions": [
                              ".png"
                            ]
                          }
                        },
                        "minItems": 1,
                        "maxItems": 1
                      }
                    },
                    "required": [
                      "files"
                    ],
                    "additionalProperties": false
                  },
                  "outputSchema": {
                    "type": "object",
                    "properties": {
                      "files": {
                        "type": "array",
                        "items": {
                          "type": "string"
                        }
                      }
                    },
                    "required": [
                      "files"
                    ],
                    "additionalProperties": false
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
        var client = new PowerScriptsClient("test-harness", hostPath: Path.Combine(Path.GetTempPath(), "does-not-exist.exe"));
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
                """
                {"name":"it_hello",
                 "inputSchema":{"type":"object","additionalProperties":false,"properties":{
                   "parameters":{"type":"object","additionalProperties":false,
                     "properties":{"name":{"type":"string"}},"required":["name"]}
                 }},
                 "outputSchema":{"type":"object","additionalProperties":false,
                   "properties":{"text":{"type":"string"}},"required":["text"]}}
                """);

            var client = new PowerScriptsClient("test-harness", hostPath, scriptsRoot: root);
            Assert.IsTrue(client.IsAvailable);

            var scripts = client.List();
            var hello = scripts.SingleOrDefault(s => s.Id == "it_hello");
            Assert.IsNotNull(hello, "expected the host to list the chosen-folder script");
            Assert.IsFalse(hello!.Trusted);

            Assert.IsTrue(client.TrustApprove("it_hello"));
            Assert.IsTrue(client.List().Single(s => s.Id == "it_hello").Trusted);
            var missingParameter = client.TransformAsync("it_hello", new PowerScriptTransformInput()).GetAwaiter().GetResult();
            Assert.AreEqual(PowerScriptsProtocol.ExitCode.Usage, missingParameter.ExitCode);
            StringAssert.Contains(missingParameter.StdErr, "Missing required property");
            Assert.IsTrue(client.TrustRevoke("it_hello"));
        }
        finally
        {
            new PowerScriptsClient("test-harness", hostPath, scriptsRoot: root).TrustRevoke("it_hello");
            Directory.Delete(root, recursive: true);
        }
    }
}
