// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        "input": { "extensions": [".md"] }
      },
      {
        "id": "py_upper",
        "name": "Uppercase",
        "description": "Upper the clipboard.",
        "runtime": "Python",
        "io": { "input": "text", "output": "text" },
        "capabilities": [],
        "trusted": false,
        "transform": { "function": "powerscript_from_text_to_text", "inputFormat": "Text", "outputFormat": "Text" }
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

        var transform = scripts.Single(s => s.Id == "py_upper");
        Assert.IsNotNull(transform.Transform);
        Assert.AreEqual("powerscript_from_text_to_text", transform.Transform!.Function);
        Assert.AreEqual("Text", transform.Transform.InputFormat);
    }

    [TestMethod]
    public void ParseList_Tolerates_EmptyOrGarbage()
    {
        Assert.AreEqual(0, PowerScriptInfo.ParseList(string.Empty).Count);
        Assert.AreEqual(0, PowerScriptInfo.ParseList("   ").Count);
        Assert.AreEqual(0, PowerScriptInfo.ParseList("{}").Count);
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
