// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliExecutorTests
{
    private string _temporaryRoot = string.Empty;
    private string _installation = string.Empty;
    private string _input = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryRoot = Path.Combine(Path.GetTempPath(), "PowerScripts-CliTests-" + Guid.NewGuid().ToString("N"));
        _installation = Path.Combine(_temporaryRoot, "install");
        Directory.CreateDirectory(_installation);
        // This path fixture is never launched; every execution test supplies an in-memory runner.
        File.WriteAllBytes(Path.Combine(_installation, "tool.exe"), [0x4d, 0x5a]);
        _input = Path.Combine(_temporaryRoot, "selected file.txt");
        File.WriteAllText(_input, "input");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Execute_PreservesArgumentTokensAndUsesTheNamespacedSecurityContext()
    {
        ProcessStartInfo? captured = null;
        ProcessExecutionContext? security = null;
        int capturedTimeout = 0;
        var executor = new CliExecutor(_installation, (start, context, timeout) =>
        {
            captured = start;
            security = context;
            capturedTimeout = timeout;
            return new ProcessRunResult(0, "output", "diagnostic");
        });
        const string text = "quoted \"value\" & $(still one argument) 中文";

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?>
        {
            ["text"] = text,
            ["verbose"] = "false",
            ["path"] = _input,
        }, timeoutMs: 1234);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("output", result.StdOut);
        Assert.AreEqual("diagnostic", result.StdErr);
        Assert.IsNotNull(captured);
        Assert.AreEqual(Path.Combine(_installation, "tool.exe"), captured.FileName);
        Assert.IsFalse(captured.UseShellExecute);
        CollectionAssert.AreEqual(new[] { "inspect", text, "--count", "2", _input }, captured.ArgumentList.ToArray());
        Assert.IsNotNull(security);
        Assert.AreEqual("builtin-cli:test.command", security.ScriptId);
        Assert.AreEqual(_installation, security.ScriptsRoot);
        CollectionAssert.AreEqual(new[] { _input }, security.InputPaths.ToArray());
        CollectionAssert.AreEqual(MxcPolicies.All.ToArray(), security.RecommendedPolicies.ToArray());
        Assert.AreEqual(1234, capturedTimeout);
    }

    [TestMethod]
    public void Execute_EmitsOnlyTheFlagForAnEnabledSwitch()
    {
        ProcessStartInfo? captured = null;
        var executor = new CliExecutor(_installation, (start, _, _) =>
        {
            captured = start;
            return new ProcessRunResult(0, string.Empty, string.Empty);
        });

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?>
        {
            ["text"] = "value",
            ["verbose"] = "TRUE",
            ["path"] = _input,
        });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(captured);
        CollectionAssert.AreEqual(new[] { "inspect", "value", "--count", "2", "--verbose", _input }, captured.ArgumentList.ToArray());
    }

    [TestMethod]
    public void Execute_InvalidParametersNeverReachTheProcessRunner()
    {
        var calls = 0;
        var executor = new CliExecutor(_installation, (_, _, _) =>
        {
            calls++;
            return new ProcessRunResult(0, string.Empty, string.Empty);
        });

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?> { ["unexpected"] = "value" });

        Assert.AreEqual("invalidParameters", result.Status);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(120001)]
    public void Execute_RejectsUnboundedOrExcessiveTimeouts(int timeout)
    {
        var calls = 0;
        var executor = new CliExecutor(_installation, (_, _, _) =>
        {
            calls++;
            return new ProcessRunResult(0, string.Empty, string.Empty);
        });

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?>(), timeout);

        Assert.AreEqual("invalidParameters", result.Status);
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow(1, "failed")]
    [DataRow(124, "timeout")]
    [DataRow(125, "restricted")]
    [DataRow(126, "restricted")]
    [DataRow(127, "failed")]
    public void Execute_PreservesFailuresWithoutRetryingOutsideTheSecurityRunner(int exitCode, string status)
    {
        var calls = 0;
        var executor = new CliExecutor(_installation, (_, _, _) =>
        {
            calls++;
            return new ProcessRunResult(exitCode, "partial output", "refused or failed");
        });

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?> { ["text"] = "value", ["path"] = _input });

        Assert.AreEqual(status, result.Status);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual("partial output", result.StdOut);
        Assert.AreEqual("refused or failed", result.StdErr);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void Execute_MissingExecutableReturnsUnavailableWithoutAProcess()
    {
        File.Delete(Path.Combine(_installation, "tool.exe"));
        var calls = 0;
        var executor = new CliExecutor(_installation, (_, _, _) =>
        {
            calls++;
            return new ProcessRunResult(0, string.Empty, string.Empty);
        });

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?> { ["text"] = "value", ["path"] = _input });

        Assert.AreEqual("unavailable", result.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StdErr));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Execute_StartFailureIsReportedAsFailure()
    {
        var executor = new CliExecutor(_installation, (_, _, _) => throw new Win32Exception("Executable became unavailable."));

        var result = executor.Execute(CreateDefinition(), new Dictionary<string, string?> { ["text"] = "value", ["path"] = _input });

        Assert.AreEqual("failed", result.Status);
        StringAssert.Contains(result.StdErr, "Executable became unavailable.");
    }

    private static BuiltinCliDefinition CreateDefinition() => new()
    {
        Id = "test.command",
        Name = "Test native command",
        Group = "Tests",
        Executable = "tool.exe",
        FixedArguments = ["inspect"],
        Parameters =
        [
            new ScriptParameter { Name = "text", IsRequired = true },
            new ScriptParameter { Name = "count", Type = "int", Default = "2", Min = 1, Max = 3 },
            new ScriptParameter { Name = "verbose", Type = "bool", Default = "false" },
            new ScriptParameter { Name = "path", Type = "file", IsRequired = true },
        ],
        Bindings =
        [
            new CliArgumentBinding { Parameter = "text", Kind = CliArgumentKind.Positional },
            new CliArgumentBinding { Parameter = "count", Kind = CliArgumentKind.Option, Name = "--count" },
            new CliArgumentBinding { Parameter = "verbose", Kind = CliArgumentKind.Switch, Name = "--verbose" },
            new CliArgumentBinding { Parameter = "path", Kind = CliArgumentKind.Positional },
        ],
    };
}
