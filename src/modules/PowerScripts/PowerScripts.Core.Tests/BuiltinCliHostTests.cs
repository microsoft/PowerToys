// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.Core;
using PowerScripts.Core.Cli;

namespace PowerScripts.Core.Tests;

/// <summary>
/// Exercises discovery and refusal paths through the real Host without executing any native CLI or
/// editing the user's PowerToys configuration. The existing HostCliTests continue to guard scripts.
/// </summary>
[TestClass]
public class BuiltinCliHostTests
{
    private static string _hostPath = string.Empty;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _hostPath = PowerScriptsClient.ResolveHostPath() ?? string.Empty;
        Assert.IsFalse(string.IsNullOrEmpty(_hostPath), "Build the referenced PowerScripts.Host project before running Host contract tests.");
    }

    [TestMethod]
    public void Catalog_ProjectsUiMetadataWithoutExecutableRecipes()
    {
        var (exitCode, stdout, stderr) = RunHost("cli", "list", "--json");
        Assert.AreEqual(0, exitCode, stderr);
        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        var commands = root.GetProperty("commands");
        Assert.IsTrue(commands.GetArrayLength() > 0, "The embedded catalogue should advertise its built-in commands even when a CLI is not deployed.");
        foreach (var command in commands.EnumerateArray())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(command.GetProperty("id").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(command.GetProperty("name").GetString()));
            Assert.AreEqual(JsonValueKind.String, command.GetProperty("group").ValueKind);
            Assert.AreEqual(JsonValueKind.String, command.GetProperty("description").ValueKind);
            Assert.IsTrue(command.GetProperty("isAvailable").ValueKind is JsonValueKind.True or JsonValueKind.False);
            Assert.AreEqual(JsonValueKind.Array, command.GetProperty("parameters").ValueKind);
            Assert.IsFalse(command.TryGetProperty("executable", out _));
            Assert.IsFalse(command.TryGetProperty("executablePath", out _));
            Assert.IsFalse(command.TryGetProperty("x-execute", out _));
            Assert.IsFalse(command.TryGetProperty("entryFullPath", out _));
        }
    }

    [TestMethod]
    public void Catalog_RejectsExternalCatalogueOrScriptRoot()
    {
        var (exitCode, stdout, _) = RunHost("cli", "list", "--json", "--root", @"Z:\untrusted-cli-catalogue");
        Assert.AreNotEqual(0, exitCode);
        using var document = JsonDocument.Parse(stdout);
        Assert.IsFalse(document.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual(0, document.RootElement.GetProperty("commands").GetArrayLength());
        StringAssert.Contains(document.RootElement.GetProperty("error").GetString(), "cannot be overridden");
    }

    [TestMethod]
    public void Run_UnknownIdReturnsStructuredFailure()
    {
        var response = RunFailure("cli", "run", "test.undefined-cli-command", "--json", "--no-consent");
        Assert.AreEqual("unknownCommand", response.GetProperty("status").GetString());
        StringAssert.Contains(response.GetProperty("error").GetString(), "not defined");
    }

    [TestMethod]
    public void Run_UnknownParameterIsRefusedBeforeNativeExecution()
    {
        string id = BuiltinCliCatalog.Load().Commands[0].Id;
        var response = RunFailure("cli", "run", id, "--set", "undefined_test_parameter=value", "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
        StringAssert.Contains(response.GetProperty("error").GetString(), "Unknown parameter");
    }

    [TestMethod]
    public void Run_RejectsDuplicateParametersAcrossBothTransports()
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("TARGET\0second"));
        var response = RunFailure("cli", "run", "unused", "--set", "target=first", "--set-base64", encoded, "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
        StringAssert.Contains(response.GetProperty("error").GetString(), "more than once");
    }

    [TestMethod]
    public void Run_RejectsNulInsideAnEncodedValue()
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("target\0first\0second"));
        var response = RunFailure("cli", "run", "unused", "--set-base64", encoded, "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
        StringAssert.Contains(response.GetProperty("error").GetString(), "NUL");
    }

    [DataTestMethod]
    [DataRow("not-base64")]
    [DataRow("/w==")]
    [DataRow("bm8tc2VwYXJhdG9y")]
    public void Run_RejectsInvalidEncodedParameter(string encoded)
    {
        var response = RunFailure("cli", "run", "unused", "--set-base64", encoded, "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("120001")]
    [DataRow("unbounded")]
    public void Run_RejectsUnboundedOrInvalidTimeout(string timeout)
    {
        var response = RunFailure("cli", "run", "unused", "--timeout-ms", timeout, "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
    }

    [DataTestMethod]
    [DataRow("--root")]
    [DataRow("--executable")]
    [DataRow("--catalog")]
    public void Run_RejectsExecutableAndCatalogueOverrides(string option)
    {
        var response = RunFailure("cli", "run", "unused", option, "untrusted", "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
        StringAssert.Contains(response.GetProperty("error").GetString(), "cannot be overridden");
    }

    [TestMethod]
    public void Run_RejectsAdditionalPositionals()
    {
        var response = RunFailure("cli", "run", "unused", "another-command", "--json");
        Assert.AreEqual("invalidParameters", response.GetProperty("status").GetString());
    }

    private static JsonElement RunFailure(params string[] arguments)
    {
        var (exitCode, stdout, _) = RunHost(arguments);
        Assert.AreNotEqual(0, exitCode);
        using var document = JsonDocument.Parse(stdout);
        var response = document.RootElement;
        Assert.IsFalse(response.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual(exitCode, response.GetProperty("exitCode").GetInt32());
        Assert.AreEqual(string.Empty, response.GetProperty("stdOut").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(response.GetProperty("error").GetString()));
        return response.Clone();
    }

    private static (int ExitCode, string StdOut, string StdErr) RunHost(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(_hostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[ModuleState.IgnoreEnabledEnvironmentVariable] = "1";
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("The Host did not complete the non-executing CLI contract request within 15 seconds.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
