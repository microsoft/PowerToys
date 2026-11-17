// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class HostCliBrokerTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize() => _root = Path.Combine(Path.GetTempPath(), "ps-host-cli-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void EveryRecipeEnablesItsScopedBrokerByDefaultWithoutAnMxcOverride()
    {
        foreach (var definition in PowerShellCliTemplates.All)
        {
            var manifest = CreateManifest(definition.Id);
            Assert.IsNotNull(manifest.HostCli, definition.Id);
            Assert.AreEqual(definition.Id, manifest.HostCli.Id);
            Assert.AreEqual(0, HostCliBroker.Validate(manifest).Count, definition.Id);
            CollectionAssert.IsSubsetOf(PowerShellCliTemplates.HostCapabilities(definition.Id).ToArray(), manifest.Capabilities);
            var parameters = definition.Parameters.ToDictionary(parameter => parameter.Name,
                parameter => (string?)parameter.Schema.Default.ToString());
            Assert.IsNotNull(HostCliBroker.CreateInvocation(manifest,
                definition.Input == "files" ? [Path.Combine(_root, "input.txt")] : [],
                parameters, definition.Input == "text" ? "Input text" : null), definition.Id);
            var descriptor = JsonNode.Parse(File.ReadAllText(manifest.EntryFullPath + ".tool.json"))!;
            Assert.IsNull(descriptor["x-powerscript"]!["mxc"], definition.Id);
        }
    }

    [TestMethod]
    public void MissingCapabilitiesUnknownRecipesAndCustomLaunchersCannotGrantHostAccess()
    {
        var manifest = CreateManifest("fancyzones");
        manifest.Capabilities.Remove(HostCliBroker.Capability);
        Assert.IsTrue(HostCliBroker.Validate(manifest).Count > 0);
        manifest = CreateManifest("advanced-paste-transform");
        manifest.Capabilities.Remove("network");
        Assert.IsTrue(HostCliBroker.Validate(manifest).Count > 0);
        manifest = CreateManifest("fancyzones");
        manifest.HostCli!.Id = @"..\cmd.exe";
        Assert.IsTrue(HostCliBroker.Validate(manifest).Count > 0);
        manifest = CreateManifest("fancyzones");
        manifest.Execute = new ScriptExecute { Command = ["cmd.exe"] };
        Assert.IsTrue(HostCliBroker.Validate(manifest).Count > 0);
        manifest = CreateManifest("fancyzones");
        manifest.HostCli = null;
        Assert.IsTrue(HostCliBroker.Validate(manifest).Count > 0);
    }

    [DataTestMethod]
    [DataRow("id", "settings-module-state")]
    [DataRow("token", "another-execution")]
    [DataRow("executable", "cmd.exe")]
    [DataRow("commandLine", "powershell.exe -Command whoami")]
    public void UnlistedCommandsAndCrossExecutionRequestsAreDeniedBeforeLaunch(string name, string value)
    {
        var (invocation, request) = CreateInvocation();
        request[name] = value;
        var launches = 0;
        var result = invocation.Run(request.ToJsonString(), _root, 1000, (_, _, _) =>
        {
            launches++;
            return new ProcessRunResult(0, "unexpected", string.Empty);
        });
        Assert.AreEqual(2, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StdOut);
        Assert.AreEqual(0, launches);
        StringAssert.Contains(result.StdErr, "refused the host CLI request");
    }

    [TestMethod]
    public void KnownButUnapprovedActionsAndValuesCannotBeSubstituted()
    {
        var (invocation, request) = CreateInvocation();
        request["values"]!["Command"] = "open-editor";
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(request.ToJsonString()));
        (invocation, request) = CreateInvocation();
        request["values"]!["Monitor"] = "1";
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(request.ToJsonString()));
        (invocation, request) = CreateInvocation();
        request["values"]!["Unknown"] = true;
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(request.ToJsonString()));
    }

    [TestMethod]
    public void TextInputCanBindAScalarWhenTheRecipeFileIsFixed()
    {
        var definition = PowerShellCliTemplates.Get("advanced-paste-transform");
        var file = Path.Combine(_root, "fixed.txt");
        var bindings = definition.Parameters.ToDictionary(parameter => parameter.Name,
            parameter => new PowerShellCliBinding("fixed", parameter.Schema.Default));
        bindings["Action"] = new PowerShellCliBinding("input");
        bindings["SourceFile"] = new PowerShellCliBinding("fixed", JsonSerializer.SerializeToElement(file));
        var created = ScriptTemplates.Create(_root, new(
            "Scalar primary input", Input: "text", CliContent: definition.Id, CliBindings: bindings));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        var invocation = HostCliBroker.CreateInvocation(manifest, [], new Dictionary<string, string?>(), "markdown")!;
        var values = definition.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Schema.Default);
        values["Action"] = JsonSerializer.SerializeToElement("markdown");
        values["SourceFile"] = JsonSerializer.SerializeToElement(file);
        invocation.ValidateRequest(JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            id = definition.Id,
            token = invocation.Token,
            values,
            files = new[] { file },
        }));
    }

    [TestMethod]
    public void SelectedFileIdentityIsBoundToTheOriginalConsumerInput()
    {
        var manifest = CreateManifest("file-locksmith");
        var source = Path.Combine(_root, "original.txt");
        var invocation = HostCliBroker.CreateInvocation(manifest, [source], new Dictionary<string, string?>(), null)!;
        var request = JsonSerializer.SerializeToNode(new
        {
            protocolVersion = 1,
            id = "file-locksmith",
            token = invocation.Token,
            values = new { SourceFile = source, Json = true },
            files = new[] { source },
        })!;
        invocation.ValidateRequest(request.ToJsonString());
        request["files"]![0] = Path.Combine(_root, "other.txt");
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(request.ToJsonString()));
    }

    [TestMethod]
    public void RuntimeValuesAreValidatedAgainstTheCatalogueNotOnlyTheScriptSchema()
    {
        var definition = PowerShellCliTemplates.Get("fancyzones");
        var created = ScriptTemplates.Create(_root, new("Runtime host request", CliContent: definition.Id));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        var values = definition.Parameters.ToDictionary(parameter => parameter.Name,
            parameter => (string?)parameter.Schema.Default.ToString());
        values["Command"] = "not-a-command";
        Assert.ThrowsException<InvalidDataException>(() => HostCliBroker.CreateInvocation(manifest, [], values, null));
    }

    [TestMethod]
    public void CanonicalHostCodeDoesNotContainUserValuesAndOnlyLaunchesOnce()
    {
        var interpreter = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (!File.Exists(interpreter))
        {
            Assert.Inconclusive("The canonical-host invocation check requires the installed PowerShell 7 runtime.");
        }

        var (invocation, request) = CreateInvocation("value'; Write-Error injected; '");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "FancyZonesCLI.exe"), []);
        var launches = 0;
        var result = invocation.Run(request.ToJsonString(), _root, 1234, (start, input, timeout) =>
        {
            launches++;
            Assert.AreEqual(interpreter, start.FileName);
            Assert.AreEqual(1234, timeout);
            var code = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList[^1]));
            Assert.IsFalse(code.Contains("Write-Error injected", StringComparison.Ordinal));
            Assert.IsFalse(code.Contains(HostCliBroker.EnvironmentVariable, StringComparison.Ordinal));
            StringAssert.Contains(code, "FancyZonesCLI.exe");
            StringAssert.Contains(input!, "Write-Error injected");
            Assert.AreEqual(_root, start.Environment[MxcProcessRunner.WorkspaceEnvironmentVariable]);
            return new ProcessRunResult(17, string.Empty, "native failure");
        });
        Assert.AreEqual(17, result.ExitCode);
        Assert.AreEqual("native failure", result.StdErr);
        var replay = invocation.Run(request.ToJsonString(), _root, 1234, (_, _, _) =>
        {
            launches++;
            return new ProcessRunResult(0, "unexpected", string.Empty);
        });
        Assert.AreEqual(1, launches);
        Assert.AreEqual(2, replay.ExitCode);
        StringAssert.Contains(replay.StdErr, "already been consumed");
    }

    [TestMethod]
    public void BrokerPermissionChangesInvalidateExistingScriptTrust()
    {
        var manifest = CreateManifest("fancyzones");
        var hash = ScriptIntegrity.ComputeHash(manifest);
        Assert.IsFalse(string.IsNullOrEmpty(hash));
        var descriptorPath = manifest.EntryFullPath + ".tool.json";
        var descriptor = JsonNode.Parse(File.ReadAllText(descriptorPath))!;
        descriptor["x-powerscript"]!["hostCli"]!["bindings"]!["Command"]!["value"] = "open-editor";
        File.WriteAllText(descriptorPath, descriptor.ToJsonString());
        Assert.AreNotEqual(hash, ScriptIntegrity.ComputeHash(manifest));
    }

    [TestMethod]
    public void MalformedDuplicateOversizedAndMutatedRequestsNeverReachExecution()
    {
        var (invocation, request) = CreateInvocation();
        var valid = request.ToJsonString();
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(valid.Replace(
            "\"protocolVersion\":1", "\"protocolVersion\":1,\"protocolVersion\":1", StringComparison.Ordinal)));
        Assert.ThrowsException<InvalidDataException>(() => invocation.ValidateRequest(new string(' ', HostCliBroker.MaximumRequestBytes + 1)));
        var random = new Random(0x7B10);
        for (var i = 0; i < 1024; i++)
        {
            var mutated = valid.ToCharArray();
            mutated[random.Next(mutated.Length)] = (char)random.Next(0, 256);
            CheckRejectedOrAuthorized(new string(mutated));
            CheckRejectedOrAuthorized(valid[..random.Next(valid.Length)]);
        }

        void CheckRejectedOrAuthorized(string candidate)
        {
            try
            {
                invocation.ValidateRequest(candidate);
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                // Rejections are expected; unexpected parser failures must fail this fuzz test.
            }
        }
    }

    private PowerScriptManifest CreateManifest(string id)
    {
        var definition = PowerShellCliTemplates.Get(id);
        var created = ScriptTemplates.Create(_root, new(
            "Broker " + id, Input: definition.Input, Output: definition.Output, CliContent: id,
            CliBindings: definition.Parameters.ToDictionary(parameter => parameter.Name,
                parameter => new PowerShellCliBinding("fixed", parameter.Schema.Default))));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        manifest.FolderPath = Path.GetDirectoryName(created.EntryPath)!;
        return manifest;
    }

    private (HostCliInvocation Invocation, JsonObject Request) CreateInvocation(string layout = "")
    {
        var definition = PowerShellCliTemplates.Get("fancyzones");
        var values = definition.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Schema.Default);
        values["Layout"] = JsonSerializer.SerializeToElement(layout);
        var invocation = new HostCliInvocation(definition, values, [], _root);
        var request = JsonSerializer.SerializeToNode(new
        {
            protocolVersion = 1,
            id = definition.Id,
            token = invocation.Token,
            values,
            files = Array.Empty<string>(),
        })!.AsObject();
        return (invocation, request);
    }
}
